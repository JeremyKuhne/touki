// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Runtime.InteropServices;
using System.ComponentModel;
using Windows.Win32;
#if NET
using System.Runtime.Versioning;
#endif

namespace touki.perf;

#if NET
[SupportedOSPlatform("windows")]
#endif
internal static unsafe partial class LargeFileCacheControl
{
#if NET
    [SupportedOSPlatform("windows10.0")]
#endif
    internal static void PurgeSystemCacheAndStandby()
    {
        const uint SeIncreaseQuotaPrivilege = 5;
        byte previous;
        int status = RtlAdjustPrivilege(SeIncreaseQuotaPrivilege, enable: 1, currentThread: 0, &previous);
        ThrowOnFailure(status, "Enable SeIncreaseQuotaPrivilege");
        try
        {
            nuint flush = unchecked((nuint)(nint)(-1));
            if (!PInvoke.SetSystemFileCacheSize(flush, flush, Flags: 0))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
        }
        finally
        {
            byte ignored;
            status = RtlAdjustPrivilege(SeIncreaseQuotaPrivilege, previous, currentThread: 0, &ignored);
            ThrowOnFailure(status, "Restore SeIncreaseQuotaPrivilege");
        }

        PurgeStandbyList();
    }

    // These ntdll exports are absent from the CsWin32 metadata. Purge only clean
    // standby pages, never other processes' working sets or the modified-page list.
    internal static void PurgeStandbyList()
    {
        const uint SeProfileSingleProcessPrivilege = 13;
        const int SystemMemoryListInformation = 80;
        const int MemoryPurgeStandbyList = 4;
        byte previous;
        int status = RtlAdjustPrivilege(SeProfileSingleProcessPrivilege, enable: 1, currentThread: 0, &previous);
        ThrowOnFailure(status, "Enable SeProfileSingleProcessPrivilege");
        try
        {
            int command = MemoryPurgeStandbyList;
            status = NtSetSystemInformation(SystemMemoryListInformation, &command, sizeof(int));
            ThrowOnFailure(status, "Purge standby list");
        }
        finally
        {
            byte ignored;
            status = RtlAdjustPrivilege(SeProfileSingleProcessPrivilege, previous, currentThread: 0, &ignored);
            ThrowOnFailure(status, "Restore SeProfileSingleProcessPrivilege");
        }
    }

    private static void ThrowOnFailure(int status, string operation)
    {
        if (status < 0)
        {
            throw new InvalidOperationException($"{operation} failed with NTSTATUS 0x{status:X8}. Run elevated.");
        }
    }

#if NET
    [LibraryImport("ntdll.dll")]
    private static partial int NtSetSystemInformation(int informationClass, void* information, uint length);

    [LibraryImport("ntdll.dll")]
    private static partial int RtlAdjustPrivilege(uint privilege, byte enable, byte currentThread, byte* previous);
#else
    [DllImport("ntdll.dll", ExactSpelling = true)]
    private static extern int NtSetSystemInformation(int informationClass, void* information, uint length);

    [DllImport("ntdll.dll", ExactSpelling = true)]
    private static extern int RtlAdjustPrivilege(uint privilege, byte enable, byte currentThread, byte* previous);
#endif
}
