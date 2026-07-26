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
///      A creation is reported when it initializes or is assigned to a local that never escapes, or when it is the
///      receiver of a fluent chain such as <c>new StringBuilder().Append(x).ToString()</c>. Every other shape is
///      left alone.
///     </description>
///    </item>
///    <item>
///     <description>
///      A local escapes if it is returned, passed as an argument, assigned to a field, property, parameter, or
///      another local, or referenced inside a lambda or local function. Whole blocks are skipped for
///      <see langword="async"/> methods and iterators.
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

        // Locals whose builder leaves the method, where a ref struct cannot be substituted.
        HashSet<ISymbol> escaped = new(SymbolEqualityComparer.Default);

        // Creations that never bind to a local - the receiver of a fluent chain.
        List<IOperation> temporaries = [];

        // A ref struct local cannot live across a 'yield' either, but there is no symbol flag for an iterator,
        // so the block is recognized by the yield operations it contains.
        bool isIterator = false;

        foreach (IOperation root in context.OperationBlocks)
        {
            foreach (IOperation operation in Descend(root))
            {
                switch (operation)
                {
                    case IReturnOperation { Kind: OperationKind.YieldReturn or OperationKind.YieldBreak }:
                        isIterator = true;
                        break;
                    case IObjectCreationOperation creation
                        when SymbolEqualityComparer.Default.Equals(creation.Type, stringBuilder):
                        Classify(creation, stringBuilder, candidates, temporaries);
                        break;
                    case IReturnOperation { ReturnedValue: { } returned }:
                        MarkEscape(returned, escaped);
                        break;
                    case IArgumentOperation argument:
                        MarkEscape(argument.Value, escaped);
                        break;
                    case ISimpleAssignmentOperation assignment:
                        // Storing into a field, property, or parameter escapes the method; aliasing into another
                        // local is treated the same way, since this pass does not follow the alias.
                        if (assignment.Target is IFieldReferenceOperation or IPropertyReferenceOperation
                            or IParameterReferenceOperation or ILocalReferenceOperation)
                        {
                            MarkEscape(assignment.Value, escaped);
                        }

                        break;
                    case IAnonymousFunctionOperation or ILocalFunctionOperation:
                        // A ref struct cannot be captured, so every local a lambda or local function touches is
                        // out of reach.
                        foreach (IOperation nested in Descend(operation))
                        {
                            if (nested is ILocalReferenceOperation captured)
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
    ///  Records <paramref name="creation"/> as a reportable candidate when it binds to a local or is a fluent
    ///  receiver. Any other position hands the instance somewhere a <see langword="ref"/> <see langword="struct"/>
    ///  cannot go, so it is deliberately dropped rather than guessed at.
    /// </summary>
    private static void Classify(
        IObjectCreationOperation creation,
        INamedTypeSymbol stringBuilder,
        List<(ISymbol Local, IOperation Creation)> candidates,
        List<IOperation> temporaries)
    {
        switch (GetEffectiveParent(creation))
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

            // Receiver of a fluent chain: 'new StringBuilder().Append(x).ToString()'. The instance is an unnamed
            // temporary that never leaves the expression.
            case IInvocationOperation or IPropertyReferenceOperation:
                temporaries.Add(creation);
                break;
        }
    }

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
    ///  Records the local referenced by <paramref name="value"/> as escaping, looking through the implicit
    ///  conversions and parentheses that hand off the same instance.
    /// </summary>
    private static void MarkEscape(IOperation? value, HashSet<ISymbol> escaped)
    {
        while (true)
        {
            switch (value)
            {
                case IConversionOperation { IsImplicit: true, OperatorMethod: null } conversion:
                    value = conversion.Operand;
                    break;
                case IParenthesizedOperation parenthesized:
                    value = parenthesized.Operand;
                    break;
                case ILocalReferenceOperation local:
                    escaped.Add(local.Local);
                    return;
                default:
                    return;
            }
        }
    }

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
