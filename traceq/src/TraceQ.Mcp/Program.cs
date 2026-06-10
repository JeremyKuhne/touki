// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using TraceQ.Mcp;
using TraceQ.Server;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

// stdout carries the MCP JSON-RPC stream; every diagnostic must go to stderr or it
// corrupts the protocol. Route all logging - down to the most verbose level - to stderr.
builder.Logging.AddConsole(static options => options.LogToStandardErrorThreshold = LogLevel.Trace);

// One cache of parsed traces is shared across every tool call for the server's lifetime.
builder.Services.AddSingleton<TraceStore>();

builder.Services
    .AddMcpServer(static options =>
    {
        options.ServerInfo = new Implementation
        {
            Name = "traceq",
            Version = typeof(TraceTools).Assembly.GetName().Version?.ToString() ?? "0.0.0"
        };

        // The workflow summary the client surfaces to the model at initialize time.
        options.ServerInstructions = TraceServerInstructions.Text;
    })
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

await builder.Build().RunAsync().ConfigureAwait(false);
return 0;
