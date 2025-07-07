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

using System.Buffers;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Threading;
using Touki.Framework.Resources;

namespace Touki.Exceptions;

[StackTraceHidden]
internal static class ThrowHelper
{
    [DoesNotReturn]
    internal static void ThrowArithmeticException(string message) => throw new ArithmeticException(message);

    [DoesNotReturn]
    internal static void ThrowAccessViolationException() => throw new AccessViolationException();

    [DoesNotReturn]
    internal static void ThrowArrayTypeMismatchException() => throw new ArrayTypeMismatchException();

    [DoesNotReturn]
    internal static void ThrowInvalidTypeWithPointersNotSupported(Type targetType) =>
        throw new ArgumentException(Strings.Format(SRF.Argument_InvalidTypeWithPointersNotSupported, targetType.ToString()));

    [DoesNotReturn]
    internal static void ThrowIndexOutOfRangeException() => throw new IndexOutOfRangeException();

    [DoesNotReturn]
    internal static void ThrowArgumentOutOfRangeException() => throw new ArgumentOutOfRangeException();

    [DoesNotReturn]
    internal static void ThrowArgumentException_DestinationTooShort() =>
        throw new ArgumentException(SRF.Argument_DestinationTooShort, paramName: "destination");

    [DoesNotReturn]
    internal static void ThrowArgumentException_InvalidTimeSpanStyles() =>
        throw new ArgumentException(SRF.Argument_InvalidTimeSpanStyles, paramName: "styles");

    [DoesNotReturn]
    internal static void ThrowArgumentException_InvalidEnumValue<TEnum>(
        TEnum value,
        [CallerArgumentExpression(nameof(value))] string argumentName = "") =>
        throw new ArgumentException(
            Strings.Format(SRF.Argument_InvalidEnumValue, Value.Create(value), typeof(TEnum).Name),
            argumentName);

    [DoesNotReturn]
    internal static void ThrowArgumentException_OverlapAlignmentMismatch() =>
        throw new ArgumentException(SRF.Argument_OverlapAlignmentMismatch);

    [DoesNotReturn]
    internal static void ThrowArgumentException_ArgumentNull_TypedRefType() =>
        throw new ArgumentNullException("value", SRF.ArgumentNull_TypedRefType);

    [DoesNotReturn]
    internal static void ThrowArgumentException_CannotExtractScalar(ExceptionArgument argument) =>
        throw GetArgumentException(ExceptionResource.Argument_CannotExtractScalar, argument);

    [DoesNotReturn]
    internal static void ThrowArgumentException_TupleIncorrectType(object obj) =>
        throw new ArgumentException(Strings.Format(SRF.ArgumentException_ValueTupleIncorrectType, obj.GetType().ToString()), "other");

    [DoesNotReturn]
    internal static void ThrowArgumentOutOfRange_IndexMustBeLessException() => throw GetArgumentOutOfRangeException(
        ExceptionArgument.index,
        ExceptionResource.ArgumentOutOfRange_IndexMustBeLess);

    [DoesNotReturn]
    internal static void ThrowArgumentOutOfRange_IndexMustBeLessOrEqualException() =>
        throw GetArgumentOutOfRangeException(
            ExceptionArgument.index,
            ExceptionResource.ArgumentOutOfRange_IndexMustBeLessOrEqual);

    [DoesNotReturn]
    internal static void ThrowArgumentException_BadComparer(object? comparer) =>
        throw new ArgumentException(Strings.Format(SRF.Arg_BogusIComparer, comparer?.ToString() ?? "null"));

    [DoesNotReturn]
    internal static void ThrowIndexArgumentOutOfRange_NeedNonNegNumException() =>
        throw GetArgumentOutOfRangeException(
            ExceptionArgument.index,
            ExceptionResource.ArgumentOutOfRange_NeedNonNegNum);

    [DoesNotReturn]
    internal static void ThrowValueArgumentOutOfRange_NeedNonNegNumException() =>
        throw GetArgumentOutOfRangeException(
            ExceptionArgument.value,
            ExceptionResource.ArgumentOutOfRange_NeedNonNegNum);

    [DoesNotReturn]
    internal static void ThrowLengthArgumentOutOfRange_ArgumentOutOfRange_NeedNonNegNum() =>
        throw GetArgumentOutOfRangeException(
            ExceptionArgument.length,
            ExceptionResource.ArgumentOutOfRange_NeedNonNegNum);

    [DoesNotReturn]
    internal static void ThrowStartIndexArgumentOutOfRange_ArgumentOutOfRange_IndexMustBeLessOrEqual() =>
        throw GetArgumentOutOfRangeException(
            ExceptionArgument.startIndex,
            ExceptionResource.ArgumentOutOfRange_IndexMustBeLessOrEqual);

    [DoesNotReturn]
    internal static void ThrowStartIndexArgumentOutOfRange_ArgumentOutOfRange_IndexMustBeLess() =>
        throw GetArgumentOutOfRangeException(ExceptionArgument.startIndex, ExceptionResource.ArgumentOutOfRange_IndexMustBeLess);

    [DoesNotReturn]
    internal static void ThrowCountArgumentOutOfRange_ArgumentOutOfRange_Count() =>
        throw GetArgumentOutOfRangeException(ExceptionArgument.count, ExceptionResource.ArgumentOutOfRange_Count);

    [DoesNotReturn]
    internal static void ThrowArgumentOutOfRange_Year() =>
        throw GetArgumentOutOfRangeException(ExceptionArgument.year, ExceptionResource.ArgumentOutOfRange_Year);

    [DoesNotReturn]
    internal static void ThrowArgumentOutOfRange_Month(int month) =>
        throw new ArgumentOutOfRangeException(nameof(month), month, SRF.ArgumentOutOfRange_Month);

    [DoesNotReturn]
    internal static void ThrowArgumentOutOfRange_DayNumber(int dayNumber) =>
        throw new ArgumentOutOfRangeException(nameof(dayNumber), dayNumber, SRF.ArgumentOutOfRange_DayNumber);

    [DoesNotReturn]
    internal static void ThrowArgumentOutOfRange_BadYearMonthDay() =>
        throw new ArgumentOutOfRangeException(null, SRF.ArgumentOutOfRange_BadYearMonthDay);

    [DoesNotReturn]
    internal static void ThrowArgumentOutOfRange_BadHourMinuteSecond() =>
        throw new ArgumentOutOfRangeException(null, SRF.ArgumentOutOfRange_BadHourMinuteSecond);

    [DoesNotReturn]
    internal static void ThrowArgumentOutOfRange_TimeSpanTooLong() =>
        throw new ArgumentOutOfRangeException(null, SRF.Overflow_TimeSpanTooLong);

    [DoesNotReturn]
    internal static void ThrowArgumentOutOfRange_RoundingDigits(string name) =>
        throw new ArgumentOutOfRangeException(name, SRF.ArgumentOutOfRange_RoundingDigits);

    [DoesNotReturn]
    internal static void ThrowArgumentOutOfRange_RoundingDigits_MathF(string name) =>
        throw new ArgumentOutOfRangeException(name, SRF.ArgumentOutOfRange_RoundingDigits_MathF);

    [DoesNotReturn]
    internal static void ThrowArgumentOutOfRange_Range<T>(string parameterName, T value, T minInclusive, T maxInclusive) =>
        throw new ArgumentOutOfRangeException(
            parameterName,
            value,
            Strings.Format(SRF.ArgumentOutOfRange_Range, Value.Create(minInclusive), Value.Create(maxInclusive)));

    [DoesNotReturn]
    internal static void ThrowOverflowException() => throw new OverflowException();

    [DoesNotReturn]
    internal static void ThrowOverflowException_NegateTwosCompNum() => throw new OverflowException(SRF.Overflow_NegateTwosCompNum);

    [DoesNotReturn]
    internal static void ThrowOverflowException_TimeSpanTooLong() => throw new OverflowException(SRF.Overflow_TimeSpanTooLong);

    [DoesNotReturn]
    internal static void ThrowOverflowException_TimeSpanDuration() => throw new OverflowException(SRF.Overflow_Duration);

    [DoesNotReturn]
    internal static void ThrowArgumentException_Arg_CannotBeNaN() => throw new ArgumentException(SRF.Arg_CannotBeNaN);

    [DoesNotReturn]
    internal static void ThrowArgumentException_Arg_CannotBeNaN(ExceptionArgument argument) =>
        throw new ArgumentException(SRF.Arg_CannotBeNaN, paramName: GetArgumentName(argument));

    [DoesNotReturn]
    internal static void ThrowWrongKeyTypeArgumentException<T>(T key, Type targetType) =>
        // Generic key to move the boxing to the right hand side of throw
        throw GetWrongKeyTypeArgumentException(key, targetType);

    [DoesNotReturn]
    internal static void ThrowWrongValueTypeArgumentException<T>(T value, Type targetType) =>
        // Generic key to move the boxing to the right hand side of throw
        throw GetWrongValueTypeArgumentException(value, targetType);

    private static ArgumentException GetAddingDuplicateWithKeyArgumentException(object? key) =>
        new ArgumentException(Strings.Format(SRF.Argument_AddingDuplicateWithKey, key?.ToString() ?? "null"));

    [DoesNotReturn]
    internal static void ThrowAddingDuplicateWithKeyArgumentException<T>(T key) =>
        // Generic key to move the boxing to the right hand side of throw
        throw GetAddingDuplicateWithKeyArgumentException(key);

    [DoesNotReturn]
    internal static void ThrowKeyNotFoundException<T>(T key) =>
        // Generic key to move the boxing to the right hand side of throw
        throw GetKeyNotFoundException(key);

    [DoesNotReturn]
    internal static void ThrowArgumentException(ExceptionResource resource) => throw GetArgumentException(resource);

    [DoesNotReturn]
    internal static void ThrowArgumentException(ExceptionResource resource, ExceptionArgument argument) =>
        throw GetArgumentException(resource, argument);

    [DoesNotReturn]
    internal static void ThrowArgumentException_HandleNotSync(string paramName) =>
        throw new ArgumentException(SRF.Arg_HandleNotSync, paramName: paramName);

    [DoesNotReturn]
    internal static void ThrowArgumentException_HandleNotAsync(string paramName) =>
        throw new ArgumentException(SRF.Arg_HandleNotAsync, paramName: paramName);

    [DoesNotReturn]
    internal static void ThrowArgumentNullException(ExceptionArgument argument) =>
        throw new ArgumentNullException(GetArgumentName(argument));

    [DoesNotReturn]
    internal static void ThrowArgumentNullException(ExceptionResource resource) =>
        throw new ArgumentNullException(GetResourceString(resource));

    [DoesNotReturn]
    internal static void ThrowArgumentNullException(ExceptionArgument argument, ExceptionResource resource) =>
        throw new ArgumentNullException(GetArgumentName(argument), GetResourceString(resource));

    [DoesNotReturn]
    internal static void ThrowArgumentOutOfRangeException(ExceptionArgument argument) =>
        throw new ArgumentOutOfRangeException(GetArgumentName(argument));

    [DoesNotReturn]
    internal static void ThrowArgumentOutOfRangeException(ExceptionArgument argument, ExceptionResource resource) =>
        throw GetArgumentOutOfRangeException(argument, resource);

    [DoesNotReturn]
    internal static void ThrowArgumentOutOfRangeException(
        ExceptionArgument argument,
        int paramNumber,
        ExceptionResource resource) =>
        throw GetArgumentOutOfRangeException(argument, paramNumber, resource);

    [DoesNotReturn]
    internal static void ThrowEndOfFileException() => throw CreateEndOfFileException();

    internal static Exception CreateEndOfFileException() =>
        new EndOfStreamException(SRF.IO_EOF_ReadBeyondEOF);

    [DoesNotReturn]
    internal static void ThrowInvalidOperationException() => throw new InvalidOperationException();

    [DoesNotReturn]
    internal static void ThrowInvalidOperationException(ExceptionResource resource) =>
        throw GetInvalidOperationException(resource);

    [DoesNotReturn]
    internal static void ThrowInvalidOperationException(ExceptionResource resource, Exception e) =>
        throw new InvalidOperationException(GetResourceString(resource), e);

    [DoesNotReturn]
    internal static void ThrowNullReferenceException() => throw new NullReferenceException(SRF.Arg_NullArgumentNullRef);

    [DoesNotReturn]
    internal static void ThrowSerializationException(ExceptionResource resource) =>
        throw new SerializationException(GetResourceString(resource));

    [DoesNotReturn]
    internal static void ThrowRankException(ExceptionResource resource) =>
        throw new RankException(GetResourceString(resource));

    [DoesNotReturn]
    internal static void ThrowNotSupportedException(ExceptionResource resource) =>
        throw new NotSupportedException(GetResourceString(resource));

    [DoesNotReturn]
    internal static void ThrowNotSupportedException_UnseekableStream() =>
        throw new NotSupportedException(SRF.NotSupported_UnseekableStream);

    [DoesNotReturn]
    internal static void ThrowNotSupportedException_UnreadableStream() =>
        throw new NotSupportedException(SRF.NotSupported_UnreadableStream);

    [DoesNotReturn]
    internal static void ThrowNotSupportedException_UnwritableStream() =>
        throw new NotSupportedException(SRF.NotSupported_UnwritableStream);

    [DoesNotReturn]
    internal static void ThrowObjectDisposedException(object? instance) =>
        throw new ObjectDisposedException(instance?.GetType().FullName);

    [DoesNotReturn]
    internal static void ThrowObjectDisposedException(Type? type) => throw new ObjectDisposedException(type?.FullName);

    [DoesNotReturn]
    internal static void ThrowObjectDisposedException_StreamClosed(string? objectName) =>
        throw new ObjectDisposedException(objectName, SRF.ObjectDisposed_StreamClosed);

    [DoesNotReturn]
    internal static void ThrowObjectDisposedException_FileClosed() =>
        throw new ObjectDisposedException(null, SRF.ObjectDisposed_FileClosed);

    [DoesNotReturn]
    internal static void ThrowObjectDisposedException(ExceptionResource resource) =>
        throw new ObjectDisposedException(null, GetResourceString(resource));

    [DoesNotReturn]
    internal static void ThrowNotSupportedException() => throw new NotSupportedException();

    [DoesNotReturn]
    internal static void ThrowAggregateException(List<Exception> exceptions) => throw new AggregateException(exceptions);

    [DoesNotReturn]
    internal static void ThrowOutOfMemoryException() => throw new OutOfMemoryException();

    [DoesNotReturn]
    internal static void ThrowDivideByZeroException() => throw new DivideByZeroException();

    [DoesNotReturn]
    internal static void ThrowOutOfMemoryException_StringTooLong() => throw new OutOfMemoryException(SRF.OutOfMemory_StringTooLong);

    [DoesNotReturn]
    internal static void ThrowOutOfMemoryException_LockEnter_WaiterCountOverflow() =>
        throw new OutOfMemoryException(SRF.Lock_Enter_WaiterCountOverflow_OutOfMemoryException);

    [DoesNotReturn]
    internal static void ThrowArgumentException_Argument_IncompatibleArrayType() =>
        throw new ArgumentException(SRF.Argument_IncompatibleArrayType);

    [DoesNotReturn]
    internal static void ThrowArgumentException_InvalidHandle(string? paramName) =>
        throw new ArgumentException(SRF.Arg_InvalidHandle, paramName: paramName);

    [DoesNotReturn]
    internal static void ThrowUnexpectedStateForKnownCallback(object? state) =>
        throw new ArgumentOutOfRangeException(nameof(state), state, SRF.Argument_UnexpectedStateForKnownCallback);

    [DoesNotReturn]
    internal static void ThrowInvalidOperationException_InvalidOperation_EnumNotStarted() =>
        throw new InvalidOperationException(SRF.InvalidOperation_EnumNotStarted);

    [DoesNotReturn]
    internal static void ThrowInvalidOperationException_InvalidOperation_EnumEnded() =>
        throw new InvalidOperationException(SRF.InvalidOperation_EnumEnded);

    [DoesNotReturn]
    internal static void ThrowInvalidOperationException_EnumCurrent(int index) =>
        throw GetInvalidOperationException_EnumCurrent(index);

    [DoesNotReturn]
    internal static void ThrowInvalidOperationException_InvalidOperation_EnumFailedVersion() =>
        throw new InvalidOperationException(SRF.InvalidOperation_EnumFailedVersion);

    [DoesNotReturn]
    internal static void ThrowInvalidOperationException_InvalidOperation_EnumOpCantHappen() =>
        throw new InvalidOperationException(SRF.InvalidOperation_EnumOpCantHappen);

    [DoesNotReturn]
    internal static void ThrowInvalidOperationException_InvalidOperation_NoValue() =>
        throw new InvalidOperationException(SRF.InvalidOperation_NoValue);

    [DoesNotReturn]
    internal static void ThrowInvalidOperationException_ConcurrentOperationsNotSupported() =>
        throw new InvalidOperationException(SRF.InvalidOperation_ConcurrentOperationsNotSupported);

    [DoesNotReturn]
    internal static void ThrowInvalidOperationException_HandleIsNotInitialized() =>
        throw new InvalidOperationException(SRF.InvalidOperation_HandleIsNotInitialized);

    [DoesNotReturn]
    internal static void ThrowInvalidOperationException_HandleIsNotPinned() =>
        throw new InvalidOperationException(SRF.InvalidOperation_HandleIsNotPinned);

    [DoesNotReturn]
    internal static void ThrowArraySegmentCtorValidationFailedExceptions(Array? array, int offset, int count) =>
        throw GetArraySegmentCtorValidationFailedException(array, offset, count);

    [DoesNotReturn]
    internal static void ThrowInvalidOperationException_InvalidUtf8() => throw new InvalidOperationException(SRF.InvalidOperation_InvalidUtf8);

    [DoesNotReturn]
    internal static void ThrowFormatException_BadFormatSpecifier() => throw new FormatException(SRF.Argument_BadFormatSpecifier);

    [DoesNotReturn]
    internal static void ThrowFormatException_NeedSingleChar() => throw new FormatException(SRF.Format_NeedSingleChar);

    [DoesNotReturn]
    internal static void ThrowFormatException_BadBoolean(ReadOnlySpan<char> value) =>
        throw new FormatException(Strings.Format(SRF.Format_BadBoolean, value.ToString()));

    [DoesNotReturn]
    internal static void ThrowArgumentOutOfRangeException_PrecisionTooLarge() =>
        throw new ArgumentOutOfRangeException("precision", Strings.Format(SRF.Argument_PrecisionTooLarge, StandardFormat.MaxPrecision));

    [DoesNotReturn]
    internal static void ThrowArgumentOutOfRangeException_SymbolDoesNotFit() =>
        throw new ArgumentOutOfRangeException("symbol", SRF.Argument_BadFormatSpecifier);

    [DoesNotReturn]
    internal static void ThrowArgumentOutOfRangeException_NeedNonNegNum(string paramName) =>
        throw new ArgumentOutOfRangeException(paramName, SRF.ArgumentOutOfRange_NeedNonNegNum);

    [DoesNotReturn]
    internal static void ArgumentOutOfRangeException_Enum_Value() =>
        throw new ArgumentOutOfRangeException("value", SRF.ArgumentOutOfRange_Enum);

    [DoesNotReturn]
    internal static void ThrowFormatInvalidString() => throw new FormatException(SRF.Format_InvalidString);

    [DoesNotReturn]
    internal static void ThrowFormatInvalidString(int offset, ExceptionResource resource) =>
        throw new FormatException(Strings.Format(SRF.Format_InvalidStringWithOffsetAndReason, offset, GetResourceString(resource)));

    [DoesNotReturn]
    internal static void ThrowFormatIndexOutOfRange() => throw new FormatException(SRF.Format_IndexOutOfRange);

    [DoesNotReturn]
    internal static void ThrowSynchronizationLockException_LockExit() =>
        throw new SynchronizationLockException(SRF.Lock_Exit_SynchronizationLockException);

    internal static AmbiguousMatchException GetAmbiguousMatchException(MemberInfo memberInfo)
    {
        Type? declaringType = memberInfo.DeclaringType;
        return new AmbiguousMatchException(
            Strings.Format(SRF.Arg_AmbiguousMatchException_MemberInfo, declaringType.ToString(), memberInfo.ToString()));
    }

    internal static AmbiguousMatchException GetAmbiguousMatchException(Attribute attribute) =>
        new AmbiguousMatchException(Strings.Format(SRF.Arg_AmbiguousMatchException_Attribute, attribute.ToString()));

    internal static AmbiguousMatchException GetAmbiguousMatchException(CustomAttributeData customAttributeData) =>
        new AmbiguousMatchException(Strings.Format(SRF.Arg_AmbiguousMatchException_CustomAttributeData, customAttributeData.ToString()));

    private static Exception GetArraySegmentCtorValidationFailedException(Array? array, int offset, int count)
    {
        if (array == null)
            return new ArgumentNullException(nameof(array));
        if (offset < 0)
            return new ArgumentOutOfRangeException(nameof(offset), SRF.ArgumentOutOfRange_NeedNonNegNum);
        if (count < 0)
            return new ArgumentOutOfRangeException(nameof(count), SRF.ArgumentOutOfRange_NeedNonNegNum);

        Debug.Assert(array.Length - offset < count);
        return new ArgumentException(SRF.Argument_InvalidOffLen);
    }

    private static ArgumentException GetArgumentException(ExceptionResource resource) =>
        new ArgumentException(GetResourceString(resource));

    private static InvalidOperationException GetInvalidOperationException(ExceptionResource resource) =>
        new InvalidOperationException(GetResourceString(resource));

    private static ArgumentException GetWrongKeyTypeArgumentException(object? key, Type targetType) =>
        new ArgumentException(
            Strings.Format(SRF.Arg_WrongType, key?.ToString() ?? "null", targetType.ToString()),
            nameof(key));

    private static ArgumentException GetWrongValueTypeArgumentException(object? value, Type targetType) =>
        new ArgumentException(
            Strings.Format(SRF.Arg_WrongType, value?.ToString() ?? "null", targetType.ToString()),
            nameof(value));

    private static KeyNotFoundException GetKeyNotFoundException(object? key) =>
        new KeyNotFoundException(Strings.Format(SRF.Arg_KeyNotFoundWithKey, key?.ToString() ?? "null"));

    private static ArgumentOutOfRangeException GetArgumentOutOfRangeException(
        ExceptionArgument argument,
        ExceptionResource resource) =>
        new ArgumentOutOfRangeException(GetArgumentName(argument), GetResourceString(resource));

    private static ArgumentException GetArgumentException(ExceptionResource resource, ExceptionArgument argument) =>
        new ArgumentException(GetResourceString(resource), GetArgumentName(argument));

    private static ArgumentOutOfRangeException GetArgumentOutOfRangeException(
        ExceptionArgument argument,
        int paramNumber,
        ExceptionResource resource) =>
        new ArgumentOutOfRangeException($"{GetArgumentName(argument)}[{paramNumber}]", GetResourceString(resource));

    private static InvalidOperationException GetInvalidOperationException_EnumCurrent(int index) =>
        new InvalidOperationException(index < 0
            ? SRF.InvalidOperation_EnumNotStarted
            : SRF.InvalidOperation_EnumEnded);

    // Allow nulls for reference types and Nullable<U>, but not for value types.
    // Aggressively inline so the jit evaluates the if in place and either drops the call altogether
    // Or just leaves null test and call to the Non-returning ThrowHelper.ThrowArgumentNullException
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void IfNullAndNullsAreIllegalThenThrow<T>(object? value, ExceptionArgument argName)
    {
        // Note that default(T) is not equal to null for value types except when T is Nullable<U>.
        if (!(default(T) == null) && value == null)
            ThrowArgumentNullException(argName);
    }

    // This function will convert an ExceptionArgument enum value to the argument name string.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static string GetArgumentName(ExceptionArgument argument)
    {
        Debug.Assert(Enum.IsDefined(typeof(ExceptionArgument), argument),
            "The enum value is not defined, please check the ExceptionArgument Enum.");

        return argument.ToString();
    }

    // This function will convert an ExceptionResource enum value to the resource string.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static string GetResourceString(ExceptionResource resource)
    {
        Debug.Assert(Enum.IsDefined(typeof(ExceptionArgument), resource),
            "The enum value is not defined, please check the ExceptionResource Enum.");

        return SRF.GetResourceString(resource.ToString())!;
    }
}
