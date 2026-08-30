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
global using System.IO;
global using System.Runtime.CompilerServices;
global using System.Runtime.InteropServices;

#if NETFRAMEWORK
global using Directory = Microsoft.IO.Directory;
global using DirectoryInfo = Microsoft.IO.DirectoryInfo;
global using EnumerationOptions = Microsoft.IO.EnumerationOptions;
global using File = Microsoft.IO.File;
global using FileInfo = Microsoft.IO.FileInfo;
global using FileSystemEntry = Microsoft.IO.Enumeration.FileSystemEntry;
global using FileSystemInfo = Microsoft.IO.FileSystemInfo;
global using FileSystemName = Microsoft.IO.Enumeration.FileSystemName;
global using MatchCasing = Microsoft.IO.MatchCasing;
global using MatchType = Microsoft.IO.MatchType;
global using Path = Microsoft.IO.Path;
global using SearchOption = Microsoft.IO.SearchOption;

// Open generic types cannot be mapped with using aliases.
global using Microsoft.IO.Enumeration;
#else
global using System.IO.Enumeration;
#endif

global using Marshal = System.Runtime.InteropServices.Marshal;

// For some reason including all of System.Text causes XML doc generation to fail on .NET Framework builds.
global using StringBuilder = System.Text.StringBuilder;

global using Touki.Exceptions;
global using Touki.Text;
global using Touki.Buffers;
global using Touki.Io;

#pragma warning restore IDE0005 // Using directive is unnecessary.
