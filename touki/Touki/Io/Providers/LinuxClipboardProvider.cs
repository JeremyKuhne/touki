// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

#if NET

using System.Runtime.Versioning;
using System.Text;

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
internal sealed partial class LinuxClipboardProvider : IClipboardProvider
{
    /// <summary>
    ///  Shared instance.
    /// </summary>
    public static LinuxClipboardProvider Instance { get; } = new();

    private static readonly Transport s_transport = DetectTransport();

    // POSIX access(2) mode flag for "is executable" from <unistd.h>.
    private const int X_OK = 1;

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
        // Materialize once. Process redirection cannot consume a span directly.
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
    public bool TryClear() => s_transport switch
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
            string stdout = process.StandardOutput.ReadToEnd();
            process.StandardError.ReadToEnd();

            if (!process.WaitForExit(5000))
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

            if (process.ExitCode != 0)
            {
                return false;
            }

            output = stdout;
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
            process.StandardInput.Write(input);
            process.StandardInput.Close();

            // xclip / wl-copy fork themselves into the background after consuming stdin and
            // closing it, so the parent observes a quick exit while the daemon continues to
            // hold the selection. A short bounded wait is sufficient.
            if (!process.WaitForExit(5000))
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
            if (!process.WaitForExit(5000))
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

            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}

#endif
