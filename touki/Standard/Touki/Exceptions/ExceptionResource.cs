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
// The convention for this enum is using the resource name as the enum name
//
internal enum ExceptionResource
{
    ArgumentOutOfRange_IndexMustBeLessOrEqual,
    ArgumentOutOfRange_IndexMustBeLess,
    ArgumentOutOfRange_IndexCount,
    ArgumentOutOfRange_IndexCountBuffer,
    ArgumentOutOfRange_Count,
    ArgumentOutOfRange_Year,
    Arg_ArrayPlusOffTooSmall,
    Arg_ByteArrayTooSmallForValue,
    NotSupported_ReadOnlyCollection,
    Arg_RankMultiDimNotSupported,
    Arg_NonZeroLowerBound,
    ArgumentOutOfRange_GetCharCountOverflow,
    ArgumentOutOfRange_ListInsert,
    ArgumentOutOfRange_NeedNonNegNum,
    ArgumentOutOfRange_NotGreaterThanBufferLength,
    ArgumentOutOfRange_SmallCapacity,
    Argument_InvalidOffLen,
    Argument_CannotExtractScalar,
    ArgumentOutOfRange_BiggerThanCollection,
    Serialization_MissingKeys,
    Serialization_NullKey,
    NotSupported_KeyCollectionSet,
    NotSupported_ValueCollectionSet,
    InvalidOperation_NullArray,
    TaskT_TransitionToFinal_AlreadyCompleted,
    TaskCompletionSourceT_TrySetException_NullException,
    TaskCompletionSourceT_TrySetException_NoExceptions,
    NotSupported_StringComparison,
    ConcurrentCollection_SyncRoot_NotSupported,
    Task_MultiTaskContinuation_NullTask,
    InvalidOperation_WrongAsyncResultOrEndCalledMultiple,
    Task_MultiTaskContinuation_EmptyTaskList,
    Task_Start_TaskCompleted,
    Task_Start_Promise,
    Task_Start_ContinuationTask,
    Task_Start_AlreadyStarted,
    Task_RunSynchronously_Continuation,
    Task_RunSynchronously_Promise,
    Task_RunSynchronously_TaskCompleted,
    Task_RunSynchronously_AlreadyStarted,
    AsyncMethodBuilder_InstanceNotInitialized,
    Task_ContinueWith_ESandLR,
    Task_ContinueWith_NotOnAnything,
    Task_InvalidTimerTimeSpan,
    Task_Delay_InvalidMillisecondsDelay,
    Task_Dispose_NotCompleted,
    Task_ThrowIfDisposed,
    Task_WaitMulti_NullTask,
    ArgumentException_OtherNotArrayOfCorrectLength,
    ArgumentNull_Array,
    ArgumentNull_SafeHandle,
    ArgumentOutOfRange_EndIndexStartIndex,
    ArgumentOutOfRange_Enum,
    ArgumentOutOfRange_HugeArrayNotSupported,
    Argument_AddingDuplicate,
    Argument_InvalidArgumentForComparison,
    Arg_LowerBoundsMustMatch,
    Arg_MustBeType,
    Arg_Need1DArray,
    Arg_Need2DArray,
    Arg_Need3DArray,
    Arg_NeedAtLeast1Rank,
    Arg_RankIndices,
    Arg_RanksAndBounds,
    InvalidOperation_IComparerFailed,
    NotSupported_FixedSizeCollection,
    Rank_MultiDimNotSupported,
    Arg_TypeNotSupported,
    Argument_SpansMustHaveSameLength,
    Argument_InvalidFlag,
    CancellationTokenSource_Disposed,
    Argument_AlignmentMustBePow2,
    InvalidOperation_SpanOverlappedOperation,
    InvalidOperation_TimeProviderNullLocalTimeZone,
    InvalidOperation_TimeProviderInvalidTimestampFrequency,
    Format_UnexpectedClosingBrace,
    Format_UnclosedFormatItem,
    Format_ExpectedAsciiDigit,
    Argument_HasToBeArrayClass,
    InvalidOperation_IncompatibleComparer,
}

