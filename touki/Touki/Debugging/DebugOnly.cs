// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki;

/// <summary>
///  Checks whether the immediate caller belongs to the Touki assembly in Debug builds and throws in Release builds.
/// </summary>
[ExcludeFromCodeCoverage]
internal static class DebugOnly
{
    internal static bool CallerIsInToukiAssembly()
#if !DEBUG
    {
        // Shouldn't be using this in release builds
        throw new InvalidOperationException();
    }
#else
    {
        StackTrace stackTrace = new(skipFrames: 2, fNeedFileInfo: false);

        return stackTrace.GetFrame(0) is { } frame
#if NET
            && DiagnosticMethodInfo.Create(frame)?.DeclaringAssemblyName is { } assemblyName
            && assemblyName == typeof(DebugOnly).Assembly.FullName;
#else
            && frame.GetMethod()?.DeclaringType is { } declaringType
            && declaringType.Assembly == typeof(DebugOnly).Assembly;
#endif
    }
#endif
}
