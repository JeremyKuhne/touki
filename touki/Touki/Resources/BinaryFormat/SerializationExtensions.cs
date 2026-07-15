// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

// Adapted from dotnet/runtime at 7aa830a03599a8255c2c4abf2947afc5b346cc6f (MIT licensed):
// src/libraries/System.Resources.Extensions/src/System/Resources/Extensions/BinaryFormat/

using System.Runtime.Serialization;

namespace Touki.Resources.BinaryFormat;

internal static class SerializationExtensions
{
    internal static SerializationException ConvertToSerializationException(this Exception exception)
        => exception is SerializationException serializationException
            ? serializationException
#if NET
            : (SerializationException)System.Runtime.ExceptionServices.ExceptionDispatchInfo.SetRemoteStackTrace(
                new SerializationException(exception.Message, exception),
                exception.StackTrace ?? string.Empty);
#else
            : new SerializationException(exception.Message, exception);
#endif
}