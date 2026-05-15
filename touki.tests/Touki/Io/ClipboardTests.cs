// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io;

/// <summary>
///  Cross-platform contract tests for <see cref="Clipboard"/>.
/// </summary>
/// <remarks>
///  <para>
///   Clipboard access is global process state and races other tests that touch the
///   clipboard, so the entire class is serialized via <see cref="SequentialCollection"/>.
///   Tests are gated on <see cref="Clipboard.IsAvailable"/>; on a headless Linux host
///   with no clipboard helper installed they skip rather than fail.
///  </para>
///  <para>
///   The clipboard is a system-wide resource. Other applications running on the developer
///   workstation or CI agent (browsers, password managers, IME helpers, ...) can briefly
///   own it and cause a single <see cref="Clipboard.TrySetText(ReadOnlySpan{char})"/> or
///   <see cref="Clipboard.TryGetText(out string?)"/> call to fail even when the provider
///   itself is healthy. Tests are decorated with <see cref="RetryFactAttribute"/> so that
///   transient contention is absorbed by re-executing the test rather than baking retry
///   logic into the production clipboard surface.
///  </para>
/// </remarks>
[Collection("SequentialCollection")]
public class ClipboardTests
{
    [RetryFact(
        MaxRetries = 5,
        Skip = "Clipboard is not available on this host.",
        SkipUnless = nameof(Clipboard.IsAvailable),
        SkipType = typeof(Clipboard))]
    public void TrySetText_ThenTryGetText_RoundTrips()
    {
        string original = SnapshotText();
        try
        {
            string payload = "touki-clipboard-roundtrip-" + Guid.NewGuid().ToString("N");
            Clipboard.TrySetText(payload).Should().BeTrue();
            Clipboard.TryGetText(out string? roundTripped).Should().BeTrue();
            roundTripped.Should().Be(payload);
        }
        finally
        {
            RestoreText(original);
        }
    }

    [RetryFact(
        MaxRetries = 5,
        Skip = "Clipboard is not available on this host.",
        SkipUnless = nameof(Clipboard.IsAvailable),
        SkipType = typeof(Clipboard))]
    public void TrySetText_Empty_RoundTripsAsEmpty()
    {
        string original = SnapshotText();
        try
        {
            Clipboard.TrySetText(string.Empty).Should().BeTrue();
            Clipboard.TryGetText(out string? roundTripped).Should().BeTrue();
            roundTripped.Should().Be(string.Empty);
        }
        finally
        {
            RestoreText(original);
        }
    }

    [RetryFact(
        MaxRetries = 5,
        Skip = "Clipboard is not available on this host.",
        SkipUnless = nameof(Clipboard.IsAvailable),
        SkipType = typeof(Clipboard))]
    public void TrySetText_TextWithUnicode_RoundTrips()
    {
        string original = SnapshotText();
        try
        {
            // Mix BMP and supplementary plane characters to exercise surrogate pairs.
            string payload = "héllo \U0001F600 wörld";
            Clipboard.TrySetText(payload).Should().BeTrue();
            Clipboard.TryGetText(out string? roundTripped).Should().BeTrue();
            roundTripped.Should().Be(payload);
        }
        finally
        {
            RestoreText(original);
        }
    }

    [RetryFact(
        MaxRetries = 5,
        Skip = "Clipboard is not available on this host.",
        SkipUnless = nameof(Clipboard.IsAvailable),
        SkipType = typeof(Clipboard))]
    public void HasText_AfterTrySetText_IsTrue()
    {
        string original = SnapshotText();
        try
        {
            Clipboard.TrySetText("touki-has-text-probe").Should().BeTrue();
            Clipboard.HasText.Should().BeTrue();
        }
        finally
        {
            RestoreText(original);
        }
    }

    [RetryFact(
        MaxRetries = 5,
        Skip = "Clipboard is not available on this host.",
        SkipUnless = nameof(Clipboard.IsAvailable),
        SkipType = typeof(Clipboard))]
    public void TryClear_AfterTrySetText_ClearsClipboard()
    {
        string original = SnapshotText();
        try
        {
            // Seed a known payload so we can prove TryClear changes the observable state.
            string payload = "touki-clipboard-clear-" + Guid.NewGuid().ToString("N");
            Clipboard.TrySetText(payload).Should().BeTrue();
            Clipboard.HasText.Should().BeTrue();

            Clipboard.TryClear().Should().BeTrue();

            // After a clear, the clipboard must not still report the prior payload. The
            // exact post-clear state varies across platforms (Windows / macOS / Wayland /
            // xsel release the selection so TryGetText returns false; the xclip-only path
            // on Linux can only take ownership with an empty payload, in which case
            // TryGetText returns true with an empty string) - both are valid.
            bool hasText = Clipboard.TryGetText(out string? roundTripped);
            if (hasText)
            {
                roundTripped.Should().NotBe(payload);
                roundTripped.Should().BeEmpty();
            }
        }
        finally
        {
            RestoreText(original);
        }
    }

    /// <summary>
    ///  Best-effort capture of the current clipboard text so the test can restore it
    ///  in its <c>finally</c> block. Returns <see cref="string.Empty"/> when the clipboard
    ///  is empty or transiently unreadable.
    /// </summary>
    private static string SnapshotText()
    {
        if (Clipboard.TryGetText(out string? captured))
        {
            return captured;
        }

        return string.Empty;
    }

    /// <summary>
    ///  Best-effort restore of the previous clipboard contents. Failures here do not
    ///  fail the test; the only cost is that the user's clipboard ends up holding the
    ///  test payload.
    /// </summary>
    private static void RestoreText(string text)
    {
        if (text.Length == 0)
        {
            Clipboard.TryClear();
        }
        else
        {
            Clipboard.TrySetText(text);
        }
    }
}
