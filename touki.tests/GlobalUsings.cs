// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

#pragma warning disable IDE0005 // Using directive is unnecessary.
global using System;
global using System.Collections.Generic;
global using System.Diagnostics;
global using System.Diagnostics.CodeAnalysis;
global using System.Linq;
global using System.Runtime.CompilerServices;
global using System.Runtime.InteropServices;
global using System.Threading;
global using System.Threading.Tasks;

global using MemoryStream = System.IO.MemoryStream;
global using StreamReader = System.IO.StreamReader;
global using FileAttributes = System.IO.FileAttributes;

#if NETFRAMEWORK
global using Microsoft.IO;
global using Microsoft.IO.Enumeration;
#else
global using System.IO;
global using System.IO.Enumeration;
#endif

global using Touki.Exceptions;

#pragma warning restore IDE0005

global using FluentAssertions;
