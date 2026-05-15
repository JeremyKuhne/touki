// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

#if NET

using System.Runtime.Versioning;
using System.Text;
using System.Threading.Tasks;

namespace Touki.Io.Providers;

/// <summary>
///  Linux clipboard transport for <see cref="Clipboard"/>.
/// </summary>
/// <remarks>
///  <para>
///   The X11 and Wayland clipboards are owned by a client process, not the operating system.
///   This provider delegates to a small set of well-known clipboard helpers
///   (<c>wl-copy</c>/<c>wl-paste</c> for Wayland, <c>xclip</c> and <c>xsel</c> for X11) which
///   fork themselves into the background and hold the selection so the text survives
///   after the calling process exits. The text may still be lost if no clipboard manager
///   (Klipper, GPaste, …) is running and another client claims the selection while the
///   helper is still the owner, or if the helper itself is terminated.
///  </para>
///  <para>
///   If none of the helpers can be located on <c>PATH</c>, all operations return
///   <see langword="false"/>. Contributors on Debian / Ubuntu can satisfy the dependency with
///   <c>apt install wl-clipboard xclip</c>.
///  </para>
/// </remarks>
[SupportedOSPlatform("linux")]
// Coverage is collected only by the Windows CI job; the Linux helper branches
// always show as uncovered there. Functional coverage comes from the dedicated
// Linux CI job, which runs the ClipboardTests under xvfb-run + xclip.
[ExcludeFromCodeCoverage]
internal sealed partial class LinuxClipboardProvider : IClipboardProvider
{
    /// <summary>
    ///  Shared instance.
    /// </summary>
    public static LinuxClipboardProvider Instance { get; } = new();

    private static readonly Transport s_transport = DetectTransport();
    private static readonly Transport s_clearTransport = DetectClearTransport();

    // POSIX access(2) mode flag for "is executable" from <unistd.h>.
    private const int X_OK = 1;

    // Sub-process timeout. Clipboard helpers either fork into the background within
    // a few milliseconds (set / clear) or perform a single X / Wayland round-trip
    // (get). Anything longer than this means the display server or helper is wedged
    // and we'd rather report failure than hang the caller.
    private const int ProcessTimeoutMs = 5000;

    // The kernel-level "can the current process execute this file" check. Honors mode
    // bits, POSIX.1e ACLs, capabilities, and the `noexec` mount flag - the same probe
    // shells use to implement `command -v`. Returns 0 on success, -1 with errno set
    // otherwise. We only care about success / failure so errno is not inspected.
    [LibraryImport("libc", EntryPoint = "access", StringMarshalling = StringMarshalling.Utf8)]
    private static partial int Access(string pathname, int mode);

    private LinuxClipboardProvider()
    {
    }

    /// <inheritdoc/>
    /// <remarks>
    ///  <para>
    ///   Returns <see langword="true"/> when a supported helper is on <c>PATH</c> and the
    ///   corresponding display-server environment variable (<c>WAYLAND_DISPLAY</c> for
    ///   Wayland, <c>DISPLAY</c> for X11) is set. This is a static probe of the host
    ///   environment - it does not connect to the display server. A misconfigured
    ///   <c>DISPLAY</c> pointing at an unreachable server will still report
    ///   <see langword="true"/>; the subsequent <see cref="TryGetText"/> /
    ///   <see cref="TrySetText"/> call will return <see langword="false"/> instead.
    ///  </para>
    /// </remarks>
    public bool IsAvailable => s_transport != Transport.None;

    /// <inheritdoc/>
    public bool HasText => s_transport != Transport.None && TryGetText(out _);

    /// <inheritdoc/>
    public bool TryGetText([NotNullWhen(true)] out string? text)
    {
        text = null;
        return s_transport switch
        {
            Transport.WaylandWlCopy => TryRunForOutput("wl-paste", "--no-newline", out text),
            Transport.X11Xclip => TryRunForOutput("xclip", "-selection clipboard -o", out text),
            Transport.X11Xsel => TryRunForOutput("xsel", "--clipboard --output", out text),
            _ => false,
        };
    }

    /// <inheritdoc/>
    public bool TrySetText(ReadOnlySpan<char> text)
    {
        // Bail out on headless hosts before materialising the payload; the helper
        // process redirection cannot consume a span directly, so we'd otherwise
        // allocate a string proportional to the input only to discard it.
        if (s_transport == Transport.None)
        {
            return false;
        }

        string payload = text.ToString();
        return s_transport switch
        {
            Transport.WaylandWlCopy => TryRunWithInput("wl-copy", string.Empty, payload),
            Transport.X11Xclip => TryRunWithInput("xclip", "-selection clipboard -i", payload),
            Transport.X11Xsel => TryRunWithInput("xsel", "--clipboard --input", payload),
            _ => false,
        };
    }

    /// <inheritdoc/>
    /// <remarks>
    ///  <para>
    ///   <c>xclip</c> has no native "release the selection" verb - asking it to set the
    ///   clipboard to an empty string would leave xclip running as the owner serving an
    ///   empty payload, which differs from the Windows / macOS / Wayland / xsel semantic
    ///   of "no clipboard content". When <c>xsel</c> is also installed it is used here in
    ///   preference to fall through to its <c>--clear</c> verb, which does release the
    ///   selection. When only <c>xclip</c> is available the best we can do is take
    ///   ownership with an empty payload.
    ///  </para>
    /// </remarks>
    public bool TryClear() => s_clearTransport switch
    {
        Transport.WaylandWlCopy => TryRunDetached("wl-copy", "--clear"),
        Transport.X11Xclip => TryRunWithInput("xclip", "-selection clipboard -i", string.Empty),
        Transport.X11Xsel => TryRunDetached("xsel", "--clipboard --clear"),
        _ => false,
    };

    private enum Transport
    {
        None,
        WaylandWlCopy,
        X11Xclip,
        X11Xsel,
    }

    private static Transport DetectTransport()
    {
        // Wayland is preferred when present so the data is offered through the user's
        // current display server. Both Wayland and X11 can be live at once under XWayland.
        bool wayland = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY"));
        bool x11 = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DISPLAY"));

        if (wayland && IsExecutableOnPath("wl-copy") && IsExecutableOnPath("wl-paste"))
        {
            return Transport.WaylandWlCopy;
        }

        if (x11 && IsExecutableOnPath("xclip"))
        {
            return Transport.X11Xclip;
        }

        if (x11 && IsExecutableOnPath("xsel"))
        {
            return Transport.X11Xsel;
        }

        return Transport.None;
    }

    private static Transport DetectClearTransport()
    {
        // xclip is the active transport on most X11 distributions because it is more
        // commonly installed by default, but it lacks a "release the selection" verb.
        // Prefer xsel for TryClear when both are present so the observable post-clear
        // state matches the other platforms (TryGetText returns false rather than
        // returning true with an empty string).
        if (s_transport == Transport.X11Xclip && IsExecutableOnPath("xsel"))
        {
            return Transport.X11Xsel;
        }

        return s_transport;
    }

    private static bool IsExecutableOnPath(string fileName)
    {
        string? path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(path))
        {
            return false;
        }

        foreach (string directory in path.Split(Path.PathSeparator))
        {
            if (string.IsNullOrEmpty(directory))
            {
                continue;
            }

            // Path.Join (unlike Path.Combine) never interprets a rooted second argument
            // as discarding the first, and it does not validate input - bad PATH entries
            // are handled by access(2) returning -1, not by a thrown exception.
            string candidate = Path.Join(directory, fileName);

            if (Access(candidate, X_OK) == 0)
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryRunForOutput(string fileName, string arguments, [NotNullWhen(true)] out string? output)
    {
        output = null;
        try
        {
            ProcessStartInfo info = new()
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using Process process = Process.Start(info)!;

            // Read stdout / stderr asynchronously so a child that fills its pipe buffer
            // does not deadlock against a synchronous ReadToEnd here; the kernel pipe is
            // typically only 64 KB and a misbehaving helper could exceed that. The async
            // reads complete after the child closes its end of the pipe (which it does on
            // exit) so the bounded wait below is enough to publish the final output.
            Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync();
            Task<string> stderrTask = process.StandardError.ReadToEndAsync();

            if (!process.WaitForExit(ProcessTimeoutMs))
            {
                try
                {
                    process.Kill();
                }
                catch
                {
                }

                return false;
            }

            // Child has exited; the kernel will EOF both pipes. Give the reader tasks a
            // short window to drain - in practice they complete within microseconds, but
            // a bounded wait avoids hanging if the framework is delayed publishing EOF.
            if (!Task.WaitAll([stdoutTask, stderrTask], ProcessTimeoutMs))
            {
                return false;
            }

            if (process.ExitCode != 0)
            {
                return false;
            }

            output = stdoutTask.Result;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryRunWithInput(string fileName, string arguments, string input)
    {
        try
        {
            ProcessStartInfo info = new()
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardInput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardInputEncoding = Encoding.UTF8,
            };

            using Process process = Process.Start(info)!;

            // Drain stderr asynchronously so a chatty helper can't fill the stderr pipe
            // and block its own exit while we're waiting.
            Task<string> stderrTask = process.StandardError.ReadToEndAsync();

            // Write the payload off-thread so a full stdin pipe (rare, but possible with
            // very large text on a slow helper) can be bounded by ProcessTimeoutMs rather
            // than blocking the calling thread indefinitely. We close stdin in a finally
            // so the helper observes EOF even if the write itself throws.
            Task writeTask = Task.Run(() =>
            {
                try
                {
                    process.StandardInput.Write(input);
                }
                finally
                {
                    process.StandardInput.Close();
                }
            });

            if (!writeTask.Wait(ProcessTimeoutMs))
            {
                try
                {
                    process.Kill();
                }
                catch
                {
                }

                return false;
            }

            // Surface a write-side exception (e.g. broken pipe if the helper exited early).
            if (writeTask.IsFaulted)
            {
                return false;
            }

            // xclip / wl-copy fork themselves into the background after consuming stdin and
            // closing it, so the parent observes a quick exit while the daemon continues to
            // hold the selection. A short bounded wait is sufficient.
            if (!process.WaitForExit(ProcessTimeoutMs))
            {
                try
                {
                    process.Kill();
                }
                catch
                {
                }

                return false;
            }

            stderrTask.Wait(ProcessTimeoutMs);
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryRunDetached(string fileName, string arguments)
    {
        try
        {
            ProcessStartInfo info = new()
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using Process process = Process.Start(info)!;

            // Drain stderr asynchronously so a chatty helper can't fill the stderr pipe
            // and block its own exit while we're waiting for it.
            Task<string> stderrTask = process.StandardError.ReadToEndAsync();

            if (!process.WaitForExit(ProcessTimeoutMs))
            {
                try
                {
                    process.Kill();
                }
                catch
                {
                }

                return false;
            }

            stderrTask.Wait(ProcessTimeoutMs);
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}

#endif
