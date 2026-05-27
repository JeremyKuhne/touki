// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

global using System;
global using System.Collections.Generic;

global using BenchmarkDotNet.Attributes;
global using BenchmarkDotNet.Jobs;
global using Touki;

#if NETFRAMEWORK
global using Microsoft.IO;
#else
global using System.IO;
#endif

global using FileAttributes = System.IO.FileAttributes;
