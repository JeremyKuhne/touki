// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki;

#if DEBUG
internal static class DebugOnly
{
    public static bool CallerIsInToukiAssembly()
    {
        StackTrace stackTrace = new(skipFrames: 2, fNeedFileInfo: false);
        if (stackTrace.GetFrame(0) is not { } frame
            || frame.GetMethod()?.DeclaringType is not { } declaringType)
        {
            return false;
        }

        return declaringType.Assembly == typeof(DebugOnly).Assembly;
    }
}
#endif
