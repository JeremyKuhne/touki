// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace touki.perf;

public enum LargeFileReadReference
{
    TunedFileStream,
    NaiveFileStream,
    Raw,
#if NET
    LargeFileStream,
    RawAsyncHandle,
    TunedAsyncHandle,
    LargeFileStreamSyncTransport,
    LargeFileStreamDualTransport,
    LargeFileStreamDualTimedTransport
#endif
}
