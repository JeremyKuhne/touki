// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using ConsoleAppFramework;

namespace TraceQ.Cli;

/// <summary>
///  The CLI host: registers the verb surface with ConsoleAppFramework and runs it.
/// </summary>
/// <remarks>
///  <para>
///   ConsoleAppFramework is a source generator with no runtime dependency; the
///   <c>ConsoleApp</c> builder and its parsing are generated into this assembly from
///   the <see cref="TraceCommands"/> methods. The app sets
///   <see cref="Environment.ExitCode"/> from each verb's <see cref="int"/> return, so
///   this host surfaces that as its own return value.
///  </para>
/// </remarks>
internal static class CliApp
{
    /// <summary>
    ///  Runs the CLI for the given process arguments.
    /// </summary>
    /// <param name="args">The process arguments.</param>
    /// <returns>The process exit code.</returns>
    public static int Run(string[] args)
    {
        // ConsoleAppFramework's Log (help) defaults to stdout, which is correct, but
        // LogError (parse and validation failures) also defaults to stdout. Route it
        // to stderr so framework errors share the stream the verbs write their own
        // errors to, keeping stdout clean for results.
        ConsoleApp.LogError = static message => Console.Error.WriteLine(message);

        ConsoleApp.ConsoleAppBuilder app = ConsoleApp.Create();
        app.Add<TraceCommands>();
        app.Run(args);
        return Environment.ExitCode;
    }
}
