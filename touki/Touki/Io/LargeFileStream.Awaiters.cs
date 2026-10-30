// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

#if NET
using System.Threading.Tasks;

namespace Touki.Io;

public sealed partial class LargeFileStream
{
#if NET11_0
    // The .NET 11 preview builder boxes ConfiguredValueTaskAwaiter at each suspended pooled await.
    // These private adapters avoid that internal-interface cast while retaining ConfigureAwait(false).
    // Native I/O awaits remain unwrapped so their state-machine-aware completion path stays intact.
    private static PooledReadAwaiter ConfigureReadAwait(ValueTask pending) => new(pending);

    private static PooledReadAwaiter<T> ConfigureReadAwait<T>(ValueTask<T> pending) => new(pending);

#else
    private static ConfiguredValueTaskAwaitable ConfigureReadAwait(ValueTask pending) =>
        pending.ConfigureAwait(continueOnCapturedContext: false);

    private static ConfiguredValueTaskAwaitable<T> ConfigureReadAwait<T>(ValueTask<T> pending) =>
        pending.ConfigureAwait(continueOnCapturedContext: false);
#endif
}
#endif
