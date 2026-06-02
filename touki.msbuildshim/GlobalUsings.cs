// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

#pragma warning disable IDE0005 // Using directive is unnecessary.
global using System;
global using System.Collections.Generic;
global using System.Linq;

#if NETFRAMEWORK
global using Microsoft.IO;
#else
global using System.IO;
#endif

global using Touki.Io;
#pragma warning restore IDE0005
