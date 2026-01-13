// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

#pragma warning disable IDE0005 // Using directive is unnecessary.
global using System;
global using System.Buffers;
global using System.Collections.Generic;
global using System.ComponentModel;
global using System.Diagnostics;
global using System.Diagnostics.CodeAnalysis;
global using System.Runtime.CompilerServices;
global using System.Runtime.InteropServices;

// Try to direct as much as possible to Microsoft.IO on .NET Framework. Follow with explicit usings for types
// that are not defined in Microsoft.IO (exchange types).

#if NETFRAMEWORK
global using Microsoft.IO;
global using Microsoft.IO.Enumeration;
#else
global using System.IO;
global using System.IO.Enumeration;
#endif

global using DirectoryNotFoundException = System.IO.DirectoryNotFoundException;
global using DriveNotFoundException = System.IO.DriveNotFoundException;
global using FileAttributes = System.IO.FileAttributes;
global using FileNotFoundException = System.IO.FileNotFoundException;
global using IOException = System.IO.IOException;
global using PathTooLongException = System.IO.PathTooLongException;
global using Stream = System.IO.Stream;
global using StreamReader = System.IO.StreamReader;
global using StreamWriter = System.IO.StreamWriter;
global using StringWriter = System.IO.StringWriter;
global using TextReader = System.IO.TextReader;
global using TextWriter = System.IO.TextWriter;

global using Marshal = System.Runtime.InteropServices.Marshal;

// For some reason including all of System.Text causes XML doc generation to fail on .NET Framework builds.
global using StringBuilder = System.Text.StringBuilder;

global using Touki.Exceptions;
global using Touki.Text;
global using Touki.Io;

#pragma warning restore IDE0005 // Using directive is unnecessary.
