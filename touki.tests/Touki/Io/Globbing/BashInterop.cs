// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

#if NET

namespace Touki.Io.Globbing;

/// <summary>
///  Locates a usable <c>bash</c> binary and evaluates a pattern via
///  <c>[[ "$INPUT" == $PATTERN ]]</c>, for use as an oracle by the per-dialect oracle test
///  classes.
/// </summary>
/// <remarks>
///  <para>
///   On Linux/macOS, walks <c>PATH</c> looking for a <c>bash</c> file. On Windows, walks
///   <c>PATH</c> for <c>bash.exe</c> and also probes the standard Git for Windows install
///   locations (<c>%ProgramFiles%\Git\bin\bash.exe</c>, <c>%ProgramFiles(x86)%\Git\bin\bash.exe</c>,
///   <c>%LocalAppData%\Programs\Git\bin\bash.exe</c>). Git Bash ships GNU bash 5.x with
///   <c>extglob</c> and <c>globstar</c>, so the <c>[[ == ]]</c> pattern surface is
///   identical to a native Linux bash for our purposes.
///  </para>
///  <para>
///   Pattern and input are passed via environment variables, never interpolated into the
///   command string, so shell quoting cannot corrupt special characters.
///  </para>
/// </remarks>
internal static class BashInterop
{
    private static string? s_bashPath;
    private static bool s_bashPathResolved;

    public static string? ResolveBashPath()
    {
        if (s_bashPathResolved)
        {
            return s_bashPath;
        }

        // On Windows, prefer the Git for Windows install over whatever `bash.exe`
        // happens to be on PATH first. The WSL launcher stub at
        // `%LocalAppData%\Microsoft\WindowsApps\bash.exe` is installed automatically
        // when WSL is enabled and would otherwise win the PATH walk &mdash; but it's
        // a wrapper around `wsl.exe`, doesn't forward environment variables, and
        // doesn't handle bash's `[[ ... ]]` conditional syntax the same way. Git
        // Bash ships a real GNU bash 5.x and is what our oracle assumes.
        if (OperatingSystem.IsWindows())
        {
            string[] preferredCandidates =
            [
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Git", "bin", "bash.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Git", "bin", "bash.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Git", "bin", "bash.exe"),
            ];
            foreach (string candidate in preferredCandidates)
            {
                if (File.Exists(candidate))
                {
                    s_bashPath = candidate;
                    s_bashPathResolved = true;
                    return s_bashPath;
                }
            }
        }

        string exeName = OperatingSystem.IsWindows() ? "bash.exe" : "bash";
        string? pathVar = Environment.GetEnvironmentVariable("PATH");
        if (pathVar is not null)
        {
            foreach (string dir in pathVar.Split(Path.PathSeparator))
            {
                if (string.IsNullOrWhiteSpace(dir))
                {
                    continue;
                }

                try
                {
                    string candidate = Path.Combine(dir, exeName);
                    if (File.Exists(candidate))
                    {
                        // Skip the WSL launcher stub even if encountered through PATH
                        // (see preferred-candidate comment above).
                        if (OperatingSystem.IsWindows()
                            && candidate.Contains(@"\WindowsApps\", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        s_bashPath = candidate;
                        s_bashPathResolved = true;
                        return s_bashPath;
                    }
                }
                catch
                {
                    // Ignore unreadable PATH entries (bad chars, network shares offline, etc.).
                }
            }
        }

        s_bashPathResolved = true;
        return null;
    }

    /// <summary>
    ///  Returns <see langword="true"/> when bash's <c>[[ "$INPUT" == $PATTERN ]]</c>
    ///  matches, evaluated with <c>-O extglob -O globstar</c>.
    /// </summary>
    public static bool Matches(string bashPath, string pattern, string input)
    {
        ProcessStartInfo psi = new()
        {
            FileName = bashPath,
            ArgumentList =
            {
                "-O", "extglob",
                "-O", "globstar",
                "-c",
                "[[ \"$INPUT\" == $PATTERN ]]",
            },
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        psi.Environment["PATTERN"] = pattern;
        psi.Environment["INPUT"] = input;

        using Process p = Process.Start(psi)!;
        p.WaitForExit();
        return p.ExitCode == 0;
    }
}

#endif
