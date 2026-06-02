// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io;

/// <summary>
///  Skips the decorated test unless <see cref="Clipboard.IsAvailable"/> reports that the
///  platform clipboard is usable on the current host.
/// </summary>
/// <remarks>
///  <para>
///   Replaces the xUnit <c>SkipUnless</c> / <c>SkipType</c> gating that the retired
///   custom retry attribute provided.
///  </para>
/// </remarks>
internal sealed class SkipUnlessClipboardAvailableAttribute()
    : SkipAttribute("Clipboard is not available on this host.")
{
    public override Task<bool> ShouldSkip(TestRegisteredContext context) =>
        Task.FromResult(!Clipboard.IsAvailable);
}
