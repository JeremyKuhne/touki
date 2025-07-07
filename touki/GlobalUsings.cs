// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

#pragma warning disable IDE0005 // Using directive is unnecessary.
global using System;
global using System.Collections.Generic;
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

global using Stream = System.IO.Stream;
global using FileAttributes = System.IO.FileAttributes;
global using IOException = System.IO.IOException;
global using FileNotFoundException = System.IO.FileNotFoundException;
global using DirectoryNotFoundException = System.IO.DirectoryNotFoundException;
global using PathTooLongException = System.IO.PathTooLongException;
global using DriveNotFoundException = System.IO.DriveNotFoundException;

global using Marshal = System.Runtime.InteropServices.Marshal;

#if NETFRAMEWORK || NET6_0
global using ArgumentNull = Touki.Exceptions.ArgumentNullAdapter;
global using ArgumentOutOfRange = Touki.Exceptions.ArgumentOutOfRangeAdapter;
global using ObjectDisposed = Touki.Exceptions.ObjectDisposedAdapter;
global using Overflow = Touki.Exceptions.OverflowAdapter;
#else
global using ArgumentNull = System.ArgumentNullException;
global using ArgumentOutOfRange = System.ArgumentOutOfRangeException;
global using ObjectDisposed = System.ObjectDisposedException;
#endif

global using NotSupported = Touki.NotSupportedAdapter;

#pragma warning restore IDE0005 // Using directive is unnecessary.
