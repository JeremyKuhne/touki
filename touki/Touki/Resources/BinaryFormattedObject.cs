// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

// Adapted from dotnet/runtime at 7aa830a03599a8255c2c4abf2947afc5b346cc6f (MIT licensed):
// src/libraries/System.Resources.Extensions/src/System/Resources/Extensions/BinaryFormat/

using System.Formats.Nrbf;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Runtime.Serialization;
using System.Threading;
using Touki.Resources.BinaryFormat;

namespace Touki.Resources;

#pragma warning disable SYSLIB0050 // Type or member is obsolete.

/// <summary>
///  Represents an object graph encoded in the .NET Remoting Binary Format (NRBF).
/// </summary>
/// <remarks>
///  <para>
///   Constructing this type parses the payload into serialization records without instantiating payload-defined
///   types. Call <see cref="Deserialize"/> to instantiate the graph.
///  </para>
///  <para>
///   Deserialization can invoke serialization constructors, <see cref="ISerializable"/> implementations,
///   <see cref="IDeserializationCallback"/>, and methods marked with serialization callback attributes. Register only
///   trusted types and deserialize only trusted payloads.
///  </para>
/// </remarks>
public sealed class BinaryFormattedObject
{
    private static readonly PayloadOptions s_payloadOptions = new()
    {
        UndoTruncatedTypeNames = true
    };

    private static readonly StreamingContext s_streamingContext = new(StreamingContextStates.All);
    private readonly ITypeResolver _typeResolver;
    private int _deserializationStarted;

    internal static FormatterConverter DefaultConverter { get; } = new();

    /// <summary>
    ///  Creates an object model by parsing <paramref name="stream"/>.
    /// </summary>
    /// <param name="stream">The readable stream containing an NRBF payload.</param>
    /// <param name="typeResolver">
    ///  The resolver containing every type that may be instantiated. A resolver containing the default framework types
    ///  is created when this value is <see langword="null"/>.
    /// </param>
    /// <remarks>The supplied stream remains open.</remarks>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is <see langword="null"/>.</exception>
    /// <exception cref="SerializationException">The payload is malformed.</exception>
    /// <exception cref="NotSupportedException">
    ///  The payload uses an unsupported NRBF feature, such as an array with non-zero lower bounds.
    /// </exception>
    public BinaryFormattedObject(Stream stream, ITypeResolver? typeResolver = null)
    {
        ArgumentNullException.ThrowIfNull(stream);

        _typeResolver = typeResolver ?? new RegisteredTypeResolver();

        try
        {
            RootRecord = NrbfDecoder.Decode(
                stream,
                out IReadOnlyDictionary<SerializationRecordId, SerializationRecord> recordMap,
                options: s_payloadOptions,
                leaveOpen: true);

            RecordMap = recordMap;
        }
        catch (Exception exception)
            when (exception is ArgumentException
                or InvalidCastException
                or ArithmeticException
                or IOException
                or KeyNotFoundException)
        {
            throw exception.ConvertToSerializationException();
        }
        catch (TargetInvocationException exception)
        {
            throw ExceptionDispatchInfo.Capture(exception.InnerException!).SourceException
                .ConvertToSerializationException();
        }
    }

    /// <summary>
    ///  Deserializes the parsed object graph.
    /// </summary>
    /// <returns>The root object.</returns>
    /// <exception cref="SerializationException">
    ///  The graph is malformed, a required type is not registered, or a registered type cannot be deserialized.
    /// </exception>
    /// <exception cref="InvalidOperationException">This method has already been called.</exception>
    /// <remarks>
    ///  This is a one-shot operation, including when deserialization fails. Registered serialization constructors and
    ///  callbacks can throw additional exceptions.
    /// </remarks>
    public object Deserialize()
    {
        if (Interlocked.Exchange(ref _deserializationStarted, 1) != 0)
        {
            throw new InvalidOperationException("The parsed object graph has already been deserialized.");
        }

        try
        {
            return BinaryFormatDeserializer.Deserialize(
                RootRecord.Id,
                RecordMap,
                _typeResolver,
                s_streamingContext);
        }
        catch (Exception exception)
            when (exception is ArgumentException
                or InvalidCastException
                or ArithmeticException
                or IOException
                or KeyNotFoundException)
        {
            throw exception.ConvertToSerializationException();
        }
        catch (TargetInvocationException exception)
        {
            throw ExceptionDispatchInfo.Capture(exception.InnerException!).SourceException
                .ConvertToSerializationException();
        }
    }

    /// <summary>
    ///  Gets the root serialization record.
    /// </summary>
    public SerializationRecord RootRecord { get; }

    /// <summary>
    ///  Gets the serialization records indexed by identifier.
    /// </summary>
    public IReadOnlyDictionary<SerializationRecordId, SerializationRecord> RecordMap { get; }

    /// <summary>
    ///  Gets a serialization record by identifier.
    /// </summary>
    /// <param name="id">The record identifier.</param>
    public SerializationRecord this[SerializationRecordId id] => RecordMap[id];
}

#pragma warning restore SYSLIB0050 // Type or member is obsolete.