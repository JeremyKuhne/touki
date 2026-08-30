// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

#pragma warning disable IDE0005 // Using directive is unnecessary.

// As we have implicit usings disabled, we need to bring in namespaces explicitly here.

global using System;
global using System.Collections.Generic;
global using System.Diagnostics;
global using System.Diagnostics.CodeAnalysis;
global using System.IO;
global using System.Numerics;
global using System.Threading;

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
#else
global using System.IO.Enumeration;
#endif

// Pull in the Touki namespaces to light up extension based functionality.

global using Touki;
global using Touki.Collections;
global using Touki.Exceptions;
global using Touki.Interop;
global using Touki.Io;
global using Touki.Text;

#pragma warning restore IDE0005 // Using directive is unnecessary.
