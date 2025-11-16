// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

#pragma warning disable IDE0005 // Using directive is unnecessary.

// As we have implicit usings disabled (so we can redirect System.IO), we need to bring in other
// namespaces explicitly here.

global using System;
global using System.Collections.Generic;
global using System.Diagnostics;
global using System.Diagnostics.CodeAnalysis;
global using System.Numerics;
global using System.Threading;

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

// Pull in the Touki namespaces to light up extension based functionality.

global using Touki;
global using Touki.Collections;
global using Touki.Exceptions;
global using Touki.Interop;
global using Touki.Io;
global using Touki.Text;
#if NETFRAMEWORK
global using Framework.Touki;
#endif

#pragma warning restore IDE0005 // Using directive is unnecessary.
