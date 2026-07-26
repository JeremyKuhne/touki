// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Touki.Analyzers;

/// <summary>
///  Reports a <c>new StringBuilder(...)</c> that only builds a string inside the method that creates it, where
///  <c>Touki.Text.ValueStringBuilder</c> seeded from a stack buffer does the same work without the allocation.
/// </summary>
/// <remarks>
///  <para>
///   <b>Why the rule is narrow.</b> <c>ValueStringBuilder</c> is a <see langword="ref"/> <see langword="struct"/>,
///   so it is only a legal replacement where the builder never leaves the method: it cannot be stored in a field,
///   returned, passed to an API that wants a <c>StringBuilder</c>, captured by a lambda, or held across an
///   <see langword="await"/> or <see langword="yield"/>. A builder that escapes has no fix available, so it is not
///   reported - a warning the user cannot act on only teaches them to disable the rule.
///  </para>
///  <para>
///   <b>Constraints and limitations.</b>
///   <list type="bullet">
///    <item>
///     <description>
///      A creation is reported when it initializes or is assigned to a local that never escapes, or when it is
///      the start of a fluent chain that ends in something other than a builder, such as
///      <c>new StringBuilder().Append(x).ToString()</c>. A chain that still yields a builder is classified by
///      where that builder lands, so <c>StringBuilder b = new StringBuilder().Append(x)</c> is judged by whether
///      <c>b</c> escapes. Every other shape is left alone.
///     </description>
///    </item>
///    <item>
///     <description>
///      A local escapes on any use other than calling one of the builder's members, reading one of its
///      properties, or writing a new value into the local - so returning it, passing it anywhere, aliasing it
///      into another local, storing it in a field or an array element, casting it, or referencing it inside a
///      lambda or local function all count. Whole blocks are skipped for <see langword="async"/> methods and
///      for iterators, the latter recognized by a <see langword="yield"/> in the member's own body rather than
///      in a nested local function.
///     </description>
///    </item>
///    <item>
///     <description>
///      Escape is a presence-based approximation over the bound <see cref="IOperation"/> tree, not a control-flow
///      analysis: a single escaping use anywhere in the member silences the diagnostic for that local, even on
///      paths that could not reach it. That direction is deliberate - a missed allocation is preferable to a
///      warning with no valid fix.
///     </description>
///    </item>
///    <item>
///     <description>
///      Only <c>new StringBuilder(...)</c> is considered. A <c>StringBuilder</c> obtained from elsewhere - a
///      parameter, a pool, <c>StringWriter.GetStringBuilder()</c> - is not the creating code's to change.
///     </description>
///    </item>
///   </list>
///  </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class PreferValueStringBuilderAnalyzer : DiagnosticAnalyzer
{
    /// <summary>
    ///  The diagnostic identifier reported by this analyzer.
    /// </summary>
    public const string DiagnosticId = "TOUKI0030";

    /// <summary>
    ///  The CLR metadata name of the type this analyzer steers callers away from. Resolved once per compilation
    ///  and compared against candidate creations by identity.
    /// </summary>
    public const string StringBuilderMetadataName = "System.Text.StringBuilder";

    private static readonly DiagnosticDescriptor s_rule = new(
        id: DiagnosticId,
        title: "Use ValueStringBuilder to build strings",
        messageFormat: "Build the string with 'Touki.Text.ValueStringBuilder' seeded from a stack buffer instead of allocating a 'StringBuilder'",
        category: "Performance",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "A 'StringBuilder' that only builds a string inside the method that creates it allocates the builder and its chunks on the heap. 'Touki.Text.ValueStringBuilder' seeded with a stack buffer does the same work without allocating, renting from the shared array pool only when the content outgrows the buffer.",
        helpLinkUri: "https://github.com/JeremyKuhne/touki");

    // Cache the supported-diagnostics array so the property does not allocate a new array on every access.
    private static readonly ImmutableArray<DiagnosticDescriptor> s_supportedDiagnostics = [s_rule];

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => s_supportedDiagnostics;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start =>
        {
            // Every diagnostic needs StringBuilder, so a compilation that cannot see it registers no actions and
            // costs ~nothing. Capturing the symbol in the closure (not a static field) avoids rooting the
            // Compilation across edits.
            if (start.Compilation.GetTypeByMetadataName(StringBuilderMetadataName) is not { } stringBuilder)
            {
                return;
            }

            // Whether a builder escapes is a member-global property, so a whole operation block is analyzed at
            // once rather than a single operation kind.
            start.RegisterOperationBlockAction(c => AnalyzeOperationBlock(c, stringBuilder));
        });
    }

    private static void AnalyzeOperationBlock(OperationBlockAnalysisContext context, INamedTypeSymbol stringBuilder)
    {
        // A ref struct local cannot live across an 'await', so no creation in an async method has a valid fix.
        if (context.OwningSymbol is IMethodSymbol { IsAsync: true })
        {
            return;
        }

        // Creations bound to a local, paired with that local so later uses can rule them out.
        List<(ISymbol Local, IOperation Creation)> candidates = [];

        // Locals whose builder leaves the method, where a ref struct cannot be substituted. Only builder locals
        // are tracked, since those are the only ones ever looked up.
        HashSet<ISymbol> escaped = new(SymbolEqualityComparer.Default);

        // Creations left behind by an expression that yields something other than the builder.
        List<IOperation> temporaries = [];

        // A ref struct local cannot live across a 'yield' either, but there is no symbol flag for an iterator,
        // so the block is recognized by the yield operations in the member's own body. A local function can be
        // an iterator of its own without making its container one, so those are not counted.
        bool isIterator = false;

        foreach (IOperation root in context.OperationBlocks)
        {
            foreach (IOperation operation in Descend(root))
            {
                switch (operation)
                {
                    case IReturnOperation { Kind: OperationKind.YieldReturn or OperationKind.YieldBreak }
                        when !IsInsideNestedFunction(operation):
                        isIterator = true;
                        break;
                    case IObjectCreationOperation creation
                        when SymbolEqualityComparer.Default.Equals(creation.Type, stringBuilder):
                        Classify(creation, stringBuilder, candidates, temporaries);
                        break;
                    case ILocalReferenceOperation reference
                        when SymbolEqualityComparer.Default.Equals(reference.Local.Type, stringBuilder)
                            && !IsSafeUse(reference):
                        escaped.Add(reference.Local);
                        break;
                    case IAnonymousFunctionOperation or ILocalFunctionOperation:
                        // A ref struct cannot be captured, so every builder local a lambda or local function
                        // touches is out of reach, including the uses that would otherwise look safe.
                        foreach (IOperation nested in Descend(operation))
                        {
                            if (nested is ILocalReferenceOperation captured
                                && SymbolEqualityComparer.Default.Equals(captured.Local.Type, stringBuilder))
                            {
                                escaped.Add(captured.Local);
                            }
                        }

                        break;
                }
            }
        }

        if (isIterator)
        {
            return;
        }

        foreach (IOperation temporary in temporaries)
        {
            context.ReportDiagnostic(Diagnostic.Create(s_rule, temporary.Syntax.GetLocation()));
        }

        foreach ((ISymbol local, IOperation creation) in candidates)
        {
            if (!escaped.Contains(local))
            {
                context.ReportDiagnostic(Diagnostic.Create(s_rule, creation.Syntax.GetLocation()));
            }
        }
    }

    /// <summary>
    ///  Records <paramref name="creation"/> as a reportable candidate when the expression containing it leaves
    ///  the builder behind or binds it to a local. Any other position hands the instance somewhere a
    ///  <see langword="ref"/> <see langword="struct"/> cannot go, so it is deliberately dropped rather than
    ///  guessed at.
    /// </summary>
    private static void Classify(
        IObjectCreationOperation creation,
        INamedTypeSymbol stringBuilder,
        List<(ISymbol Local, IOperation Creation)> candidates,
        List<IOperation> temporaries)
    {
        // Follow a fluent chain for as long as it keeps producing a builder, so the classification is driven by
        // what the chain finally yields rather than by the first call in it. 'new StringBuilder().Append(x)' is
        // itself a builder that may still be stored, returned, or passed on; only a chain ending in something
        // else - a 'ToString()' or a 'Length' - leaves the builder behind for good.
        IOperation value = creation;

        while (true)
        {
            IOperation? consumer = GetEffectiveParent(value);
            if (!IsFluentReceiver(consumer, value))
            {
                break;
            }

            if (!SymbolEqualityComparer.Default.Equals(consumer!.Type, stringBuilder))
            {
                temporaries.Add(creation);
                return;
            }

            value = consumer;
        }

        switch (GetEffectiveParent(value))
        {
            // 'StringBuilder builder = new(...)'. A ref local aliases another location rather than owning a
            // value, and a local of a wider type (an 'object' or an interface) is not something a ref struct
            // can stand in for, so neither is a candidate.
            case IVariableInitializerOperation { Parent: IVariableDeclaratorOperation declarator }
                when declarator.Symbol.RefKind == RefKind.None
                    && SymbolEqualityComparer.Default.Equals(declarator.Symbol.Type, stringBuilder):
                candidates.Add((declarator.Symbol, creation));
                break;

            // 'builder = new(...)' onto an existing local.
            case ISimpleAssignmentOperation { Target: ILocalReferenceOperation target }
                when SymbolEqualityComparer.Default.Equals(target.Local.Type, stringBuilder):
                candidates.Add((target.Local, creation));
                break;
        }
    }

    /// <summary>
    ///  Returns <see langword="true"/> if <paramref name="operation"/> sits inside a lambda or a local function
    ///  rather than directly in the body of the member being analyzed.
    /// </summary>
    private static bool IsInsideNestedFunction(IOperation operation)
    {
        for (IOperation? parent = operation.Parent; parent is not null; parent = parent.Parent)
        {
            if (parent is IAnonymousFunctionOperation or ILocalFunctionOperation)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///  Returns <see langword="true"/> if <paramref name="value"/> is the receiver that <paramref name="consumer"/>
    ///  calls a member on, which is the link a fluent chain is built from.
    /// </summary>
    private static bool IsFluentReceiver(IOperation? consumer, IOperation value) => consumer switch
    {
        IInvocationOperation invocation => ReferenceEquals(invocation.Instance, value),
        IPropertyReferenceOperation property => ReferenceEquals(property.Instance, value),
        _ => false
    };

    /// <summary>
    ///  Gets the operation that consumes <paramref name="operation"/>, looking through the parentheses and the
    ///  implicit conversion node that a target-typed <c>new()</c> introduces between the creation and the
    ///  declarator that gives it a type.
    /// </summary>
    private static IOperation? GetEffectiveParent(IOperation operation)
    {
        IOperation? parent = operation.Parent;
        while (parent is IConversionOperation { OperatorMethod: null } or IParenthesizedOperation)
        {
            parent = parent.Parent;
        }

        return parent;
    }

    /// <summary>
    ///  Returns <see langword="true"/> if <paramref name="reference"/> is a use that keeps the builder where it
    ///  is: calling one of its members, reading one of its properties, or writing a new value into the local
    ///  itself. Every other use is treated as an escape.
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   Safe uses are whitelisted rather than escapes blacklisted, so a shape this pass has not been taught -
    ///   an array element, a collection expression, a cast, a tuple, an interpolation - counts as an escape and
    ///   silences the diagnostic. That is the direction that cannot produce a warning with no valid fix.
    ///  </para>
    /// </remarks>
    private static bool IsSafeUse(ILocalReferenceOperation reference) => reference.Parent switch
    {
        // 'builder.Append(x)' - the builder is the receiver. An extension method puts the builder in an
        // argument instead, which deliberately falls through to the escape case.
        IInvocationOperation invocation => ReferenceEquals(invocation.Instance, reference),

        // 'builder.Length' or 'builder[i]' - the builder is the receiver.
        IPropertyReferenceOperation property => ReferenceEquals(property.Instance, reference),

        // 'builder = ...' - writing into the local, not handing the instance anywhere.
        ISimpleAssignmentOperation assignment => ReferenceEquals(assignment.Target, reference),

        _ => false
    };

    private static IEnumerable<IOperation> Descend(IOperation root)
    {
        Stack<IOperation> stack = new();
        stack.Push(root);
        while (stack.Count > 0)
        {
            IOperation operation = stack.Pop();
            yield return operation;
            foreach (IOperation child in operation.ChildOperations)
            {
                stack.Push(child);
            }
        }
    }
}
