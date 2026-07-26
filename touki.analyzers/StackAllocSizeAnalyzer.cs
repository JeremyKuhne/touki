// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Collections.Immutable;
using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Touki.Analyzers;

/// <summary>
///  Reports <see langword="stackalloc"/> expressions whose total size exceeds a configurable
///  byte threshold, defaulting to <see cref="DefaultMaxBytes"/> bytes.
/// </summary>
/// <remarks>
///  <para>
///   Stack space is a fixed, per-thread budget. Overrunning it raises
///   <c>StackOverflowException</c>, which cannot be caught and terminates the process. Large
///   scratch buffers should be rented from <c>ArrayPool&lt;T&gt;</c> or acquired through
///   <c>BufferScope&lt;T&gt;</c>, which falls back to the pool when the request outgrows its
///   stack seed.
///  </para>
///  <para>
///   Only allocations with a compile-time constant length and a primitive, enum, pointer, or
///   native-integer element type are evaluated. A run-time length or a custom struct element
///   is not reported, because the total size is not knowable from source.
///  </para>
///  <para>
///   The threshold is configured per path in <c>.editorconfig</c>:
///   <code>dotnet_code_quality.TOUKI0011.max_stackalloc_bytes = 512</code>
///  </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class StackAllocSizeAnalyzer : DiagnosticAnalyzer
{
    /// <summary>
    ///  The diagnostic identifier reported by this analyzer.
    /// </summary>
    public const string DiagnosticId = "TOUKI0011";

    /// <summary>
    ///  The <c>.editorconfig</c> key that overrides the maximum allowed size, in bytes.
    /// </summary>
    public const string MaxBytesOption = "dotnet_code_quality.TOUKI0011.max_stackalloc_bytes";

    /// <summary>
    ///  The maximum allowed size, in bytes, when <see cref="MaxBytesOption"/> is not configured.
    /// </summary>
    public const int DefaultMaxBytes = 1024;

    // A native integer or pointer is 4 bytes in a 32-bit process and 8 in a 64-bit one, and which of those
    // runs is not knowable when the analyzer sees the code. Assume the larger so the rule reports a size at
    // least as large as the allocation will really be rather than under-reporting it.
    private const long NativeIntegerSize = 8;

    private static readonly DiagnosticDescriptor s_rule = new(
        id: DiagnosticId,
        title: "Avoid large stackalloc allocations",
        messageFormat: "'stackalloc' of {0} bytes exceeds the maximum of {1} bytes; rent from ArrayPool<T> or use BufferScope<T>",
        category: "Reliability",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Large 'stackalloc' allocations risk an uncatchable StackOverflowException. Use a pooled buffer instead.",
        helpLinkUri: HelpLinks.ForRule(DiagnosticId));

    // Cache the supported-diagnostics array so the property does not allocate a new array on every access.
    private static readonly ImmutableArray<DiagnosticDescriptor> s_supportedDiagnostics = ImmutableArray.Create(s_rule);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => s_supportedDiagnostics;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(
            AnalyzeStackAlloc,
            SyntaxKind.StackAllocArrayCreationExpression,
            SyntaxKind.ImplicitStackAllocArrayCreationExpression);
    }

    private static void AnalyzeStackAlloc(SyntaxNodeAnalysisContext context)
    {
        if (!TryGetElementTypeAndCount(context, out ITypeSymbol? elementType, out long elementCount)
            || !TryGetElementSize(elementType, out long elementSize))
        {
            return;
        }

        // elementCount is bounded by int.MaxValue and elementSize by 16, so this cannot overflow.
        long totalBytes = elementSize * elementCount;

        // Read the configured threshold last; the size calculation above is the cheaper filter.
        int maxBytes = GetMaxBytes(context);

        if (totalBytes <= maxBytes)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(s_rule, context.Node.GetLocation(), totalBytes, maxBytes));
    }

    private static bool TryGetElementTypeAndCount(
        SyntaxNodeAnalysisContext context,
        out ITypeSymbol? elementType,
        out long elementCount)
    {
        elementType = null;
        elementCount = 0;

        switch (context.Node)
        {
            case StackAllocArrayCreationExpressionSyntax { Type: ArrayTypeSyntax arrayType } stackAlloc:
                elementType = context.SemanticModel.GetTypeInfo(arrayType.ElementType, context.CancellationToken).Type;
                return TryGetExplicitCount(context, arrayType, stackAlloc.Initializer, out elementCount);

            case ImplicitStackAllocArrayCreationExpressionSyntax implicitStackAlloc:
                elementType = GetImplicitElementType(context);
                elementCount = implicitStackAlloc.Initializer.Expressions.Count;
                return true;

            default:
                return false;
        }
    }

    private static bool TryGetExplicitCount(
        SyntaxNodeAnalysisContext context,
        ArrayTypeSyntax arrayType,
        InitializerExpressionSyntax? initializer,
        out long elementCount)
    {
        elementCount = 0;

        if (arrayType.RankSpecifiers.Count != 1 || arrayType.RankSpecifiers[0].Sizes.Count != 1)
        {
            return false;
        }

        ExpressionSyntax size = arrayType.RankSpecifiers[0].Sizes[0];

        if (size is OmittedArraySizeExpressionSyntax)
        {
            // 'stackalloc int[] { 1, 2, 3 }' takes its length from the initializer.
            if (initializer is null)
            {
                return false;
            }

            elementCount = initializer.Expressions.Count;
            return true;
        }

        Optional<object?> constant = context.SemanticModel.GetConstantValue(size, context.CancellationToken);

        if (!constant.HasValue)
        {
            // A run-time length cannot be evaluated here.
            return false;
        }

        // These are the integral types a length expression is allowed to have.
        long count = constant.Value switch
        {
            int value => value,
            uint value => value,
            long value => value,
            ulong value when value <= long.MaxValue => (long)value,
            _ => 0
        };

        if (count <= 0)
        {
            return false;
        }

        elementCount = count;
        return true;
    }

    private static ITypeSymbol? GetImplicitElementType(SyntaxNodeAnalysisContext context)
    {
        // 'stackalloc[] { ... }' is typed as 'T*' or as 'Span<T>' depending on the target.
        ITypeSymbol? type = context.SemanticModel.GetTypeInfo(context.Node, context.CancellationToken).Type;

        return type switch
        {
            IPointerTypeSymbol pointer => pointer.PointedAtType,
            INamedTypeSymbol { TypeArguments.Length: 1 } span => span.TypeArguments[0],
            _ => null
        };
    }

    private static bool TryGetElementSize(ITypeSymbol? elementType, out long elementSize)
    {
        elementSize = 0;

        if (elementType is null)
        {
            return false;
        }

        // An enum occupies the same space as the primitive it is built on.
        if (elementType is INamedTypeSymbol { EnumUnderlyingType: { } underlyingType })
        {
            elementType = underlyingType;
        }

        if (elementType.TypeKind == TypeKind.Pointer
            || elementType.SpecialType is SpecialType.System_IntPtr or SpecialType.System_UIntPtr)
        {
            elementSize = NativeIntegerSize;
            return true;
        }

        elementSize = elementType.SpecialType switch
        {
            SpecialType.System_Boolean or SpecialType.System_Byte or SpecialType.System_SByte => 1,
            SpecialType.System_Char or SpecialType.System_Int16 or SpecialType.System_UInt16 => 2,
            SpecialType.System_Int32 or SpecialType.System_UInt32 or SpecialType.System_Single => 4,
            SpecialType.System_Int64 or SpecialType.System_UInt64 or SpecialType.System_Double => 8,
            SpecialType.System_Decimal => 16,

            // A custom struct has no source-visible size; leave it alone rather than guess.
            _ => 0
        };

        return elementSize != 0;
    }

    private static int GetMaxBytes(SyntaxNodeAnalysisContext context)
    {
        AnalyzerConfigOptions options = context.Options.AnalyzerConfigOptionsProvider.GetOptions(context.Node.SyntaxTree);

        return options.TryGetValue(MaxBytesOption, out string? value)
            && int.TryParse(value.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out int maxBytes)
            && maxBytes > 0
                ? maxBytes
                : DefaultMaxBytes;
    }
}
