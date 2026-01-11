// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


// This file defines an internal static class used to throw exceptions in BCL code.
// The main purpose is to reduce code size.
//
// The old way to throw an exception generates quite a lot IL code and assembly code.
// Following is an example:
//     C# source
//          throw new ArgumentNullException(nameof(key), SRF.ArgumentNull_Key);
//     IL code:
//          IL_0003:  ldstr      "key"
//          IL_0008:  ldstr      "ArgumentNull_Key"
//          IL_000d:  call       string System.Environment::GetResourceString(string)
//          IL_0012:  newobj     instance void System.ArgumentNullException::.ctor(string,string)
//          IL_0017:  throw
//    which is 21bytes in IL.
//
// So we want to get rid of the ldstr and call to Environment.GetResource in IL.
// In order to do that, I created two enums: ExceptionResource, ExceptionArgument to represent the
// argument name and resource name in a small integer. The source code will be changed to
//    ThrowHelper.ThrowArgumentNullException(ExceptionArgument.key, ExceptionResource.ArgumentNull_Key);
//
// The IL code will be 7 bytes.
//    IL_0008:  ldc.i4.4
//    IL_0009:  ldc.i4.4
//    IL_000a:  call       void System.ThrowHelper::ThrowArgumentNullException(valuetype System.ExceptionArgument)
//    IL_000f:  ldarg.0
//
// This will also reduce the Jitted code size a lot.
//
// It is very important we do this for generic classes because we can easily generate the same code
// multiple times for different instantiation.
//

namespace Touki.Exceptions;

//
// The convention for this enum is using the argument name as the enum name
//
internal enum ExceptionArgument
{
    obj,
    dictionary,
    array,
    info,
    key,
    text,
    values,
    value,
    startIndex,
    task,
    bytes,
    byteIndex,
    byteCount,
    ch,
    chars,
    charIndex,
    charCount,
    s,
    input,
    ownedMemory,
    list,
    index,
    capacity,
    collection,
    item,
    converter,
    match,
    count,
    action,
    comparison,
    exceptions,
    exception,
    pointer,
    start,
    format,
    formats,
    culture,
    comparer,
    comparable,
    source,
    length,
    comparisonType,
    manager,
    sourceBytesToCopy,
    callBack,
    creationOptions,
    function,
    scheduler,
    continuation,
    continuationAction,
    continuationFunction,
    tasks,
    asyncResult,
    beginMethod,
    endMethod,
    endFunction,
    cancellationToken,
    continuationOptions,
    delay,
    millisecondsDelay,
    millisecondsTimeout,
    stateMachine,
    timeout,
    type,
    sourceIndex,
    sourceArray,
    destinationIndex,
    destinationArray,
    pHandle,
    handle,
    other,
    newSize,
    lengths,
    len,
    keys,
    indices,
    index1,
    index2,
    index3,
    endIndex,
    elementType,
    arrayIndex,
    year,
    codePoint,
    str,
    options,
    prefix,
    suffix,
    buffer,
    buffers,
    offset,
    stream,
    anyOf,
    overlapped,
    minimumBytes,
    arrayType,
    divisor,
    factor,
    set,
    alignment
}

