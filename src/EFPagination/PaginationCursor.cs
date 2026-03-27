using System.Buffers;
using System.Buffers.Text;
using System.Collections.Concurrent;
using System.Globalization;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace EFPagination;

/// <summary>
/// Encodes and decodes opaque cursor tokens containing typed pagination boundary values.
/// </summary>
public static class PaginationCursor
{
    private const int StackAllocThreshold = 256;
    private const int DateOnlyFormatLength = 10;
    private const int MaxTimeOnlyFormatLength = 16;
    private const int MaxTimeSpanFormatLength = 26;

    private const byte CursorValueKindNull = 0;
    private const byte CursorValueKindString = 1;
    private const byte CursorValueKindBoolean = 2;
    private const byte CursorValueKindChar = 3;
    private const byte CursorValueKindInt32 = 4;
    private const byte CursorValueKindInt64 = 5;
    private const byte CursorValueKindUInt32 = 6;
    private const byte CursorValueKindUInt64 = 7;
    private const byte CursorValueKindInt16 = 8;
    private const byte CursorValueKindUInt16 = 9;
    private const byte CursorValueKindByte = 10;
    private const byte CursorValueKindSByte = 11;
    private const byte CursorValueKindSingle = 12;
    private const byte CursorValueKindDouble = 13;
    private const byte CursorValueKindDecimal = 14;
    private const byte CursorValueKindGuid = 15;
    private const byte CursorValueKindDateTime = 16;
    private const byte CursorValueKindDateTimeOffset = 17;
    private const byte CursorValueKindDateOnly = 18;
    private const byte CursorValueKindTimeOnly = 19;
    private const byte CursorValueKindTimeSpan = 20;
    private const byte CursorValueKindEnum = 21;

    private static readonly JsonWriterOptions s_writerOptions = new() { SkipValidation = true };
    private static readonly object s_boxedTrue = true;
    private static readonly object s_boxedFalse = false;
    private static readonly ConcurrentDictionary<string, Type> s_enumTypeCache = new();
    private static readonly ConcurrentDictionary<Type, EnumMetadata> s_enumMetadataCache = new();

    [ThreadStatic]
    private static PooledBufferWriter? s_bufferWriter;

    [ThreadStatic]
    private static Utf8JsonWriter? s_jsonWriter;

    /// <summary>
    /// Encodes pagination values and optional metadata into an opaque cursor token.
    /// </summary>
    /// <param name="values">The values to encode in definition order.</param>
    /// <param name="options">Optional cursor metadata to encode.</param>
    /// <returns>A base64url cursor token containing the encoded values and metadata.</returns>
    /// <exception cref="NotSupportedException">One of the supplied values has a type that the cursor format does not support.</exception>
    /// <exception cref="InvalidOperationException">A supported value cannot be formatted into the cursor payload.</exception>
    public static string Encode(ReadOnlySpan<ColumnValue> values, PaginationCursorOptions? options = null)
    {
        var opts = options.GetValueOrDefault();

        var bufferWriter = s_bufferWriter ??= new PooledBufferWriter(StackAllocThreshold);
        bufferWriter.Reset();

        var writer = s_jsonWriter;
        if (writer is null)
        {
            writer = new Utf8JsonWriter(bufferWriter, s_writerOptions);
            s_jsonWriter = writer;
        }
        else
        {
            writer.Reset(bufferWriter);
        }

        writer.WriteStartObject();

        if (opts.SortBy is not null)
            writer.WriteString("s"u8, opts.SortBy);

        if (opts.TotalCount.HasValue)
            writer.WriteNumber("c"u8, opts.TotalCount.GetValueOrDefault());

        if (values.Length > 0)
        {
            writer.WritePropertyName("v"u8);
            writer.WriteStartArray();
            for (var i = 0; i < values.Length; i++)
                WriteTypedValue(writer, values[i].Value);
            writer.WriteEndArray();
        }

        writer.WriteEndObject();
        writer.Flush();

        var encodedLength = Base64Url.GetEncodedLength(bufferWriter.WrittenCount);

        return string.Create(encodedLength, bufferWriter, static (span, bw) =>
        {
            Base64Url.TryEncodeToChars(bw.WrittenSpan, span, out _);
        });
    }

    private static void WriteTypedValue(Utf8JsonWriter writer, object? value)
    {
        writer.WriteStartArray();

        switch (value)
        {
            case null:
                writer.WriteNumberValue(CursorValueKindNull);
                writer.WriteNullValue();
                break;
            case string s:
                writer.WriteNumberValue(CursorValueKindString);
                writer.WriteStringValue(s);
                break;
            case bool b:
                writer.WriteNumberValue(CursorValueKindBoolean);
                writer.WriteBooleanValue(b);
                break;
            case char c:
                writer.WriteNumberValue(CursorValueKindChar);
                writer.WriteStringValue([c]);
                break;
            case byte b8:
                writer.WriteNumberValue(CursorValueKindByte);
                writer.WriteNumberValue(b8);
                break;
            case sbyte sb:
                writer.WriteNumberValue(CursorValueKindSByte);
                writer.WriteNumberValue(sb);
                break;
            case short s16:
                writer.WriteNumberValue(CursorValueKindInt16);
                writer.WriteNumberValue(s16);
                break;
            case ushort u16:
                writer.WriteNumberValue(CursorValueKindUInt16);
                writer.WriteNumberValue(u16);
                break;
            case int i:
                writer.WriteNumberValue(CursorValueKindInt32);
                writer.WriteNumberValue(i);
                break;
            case long l:
                writer.WriteNumberValue(CursorValueKindInt64);
                writer.WriteNumberValue(l);
                break;
            case uint u32:
                writer.WriteNumberValue(CursorValueKindUInt32);
                writer.WriteNumberValue(u32);
                break;
            case ulong u64:
                writer.WriteNumberValue(CursorValueKindUInt64);
                writer.WriteNumberValue(u64);
                break;
            case float f:
                writer.WriteNumberValue(CursorValueKindSingle);
                writer.WriteNumberValue(f);
                break;
            case double d:
                writer.WriteNumberValue(CursorValueKindDouble);
                writer.WriteNumberValue(d);
                break;
            case decimal m:
                writer.WriteNumberValue(CursorValueKindDecimal);
                writer.WriteNumberValue(m);
                break;
            case Guid g:
                writer.WriteNumberValue(CursorValueKindGuid);
                writer.WriteStringValue(g);
                break;
            case DateTime dt:
                writer.WriteNumberValue(CursorValueKindDateTime);
                writer.WriteStringValue(dt);
                break;
            case DateTimeOffset dto:
                writer.WriteNumberValue(CursorValueKindDateTimeOffset);
                writer.WriteStringValue(dto);
                break;
            case DateOnly dateOnly:
                writer.WriteNumberValue(CursorValueKindDateOnly);
                Span<char> dateBuf = stackalloc char[DateOnlyFormatLength];
                dateOnly.TryFormat(dateBuf, out var dateWritten, "O", CultureInfo.InvariantCulture);
                writer.WriteStringValue(dateBuf[..dateWritten]);
                break;
            case TimeOnly timeOnly:
                writer.WriteNumberValue(CursorValueKindTimeOnly);
                Span<byte> timeBuf = stackalloc byte[MaxTimeOnlyFormatLength];
                if (!Utf8Formatter.TryFormat(timeOnly.ToTimeSpan(), timeBuf, out var timeWritten, 'c'))
                    throw new InvalidOperationException("Failed to format TimeOnly value.");
                writer.WriteStringValue(timeBuf[..timeWritten]);
                break;
            case TimeSpan timeSpan:
                writer.WriteNumberValue(CursorValueKindTimeSpan);
                Span<byte> timeSpanBuf = stackalloc byte[MaxTimeSpanFormatLength];
                if (!Utf8Formatter.TryFormat(timeSpan, timeSpanBuf, out var timeSpanWritten, 'c'))
                    throw new InvalidOperationException("Failed to format TimeSpan value.");
                writer.WriteStringValue(timeSpanBuf[..timeSpanWritten]);
                break;
            case Enum e:
                WriteEnumValue(writer, e);
                break;
            default:
                throw new NotSupportedException($"Cursor value type '{value.GetType()}' is not supported.");
        }

        writer.WriteEndArray();
    }

    /// <summary>
    /// Decodes a cursor token into a caller-supplied <see cref="ColumnValue"/> buffer.
    /// </summary>
    /// <param name="encoded">The encoded cursor token.</param>
    /// <param name="values">The destination buffer. Each entry should already contain the expected column name.</param>
    /// <param name="written">When this method returns <see langword="true"/>, contains the number of values decoded into <paramref name="values"/>.</param>
    /// <returns><see langword="true"/> if the cursor was decoded successfully; otherwise <see langword="false"/>.</returns>
    public static bool TryDecode(ReadOnlySpan<char> encoded, Span<ColumnValue> values, out int written)
    {
        return TryDecode(encoded, values, out written, out _, out _);
    }

    /// <summary>
    /// Decodes a cursor token into definition-bound ordered pagination values.
    /// </summary>
    /// <typeparam name="T">The entity type associated with the pagination definition.</typeparam>
    /// <param name="encoded">The encoded cursor token.</param>
    /// <param name="definition">The pagination definition that determines the expected value order.</param>
    /// <param name="values">When this method returns <see langword="true"/>, contains the decoded ordered values.</param>
    /// <param name="written">When this method returns <see langword="true"/>, contains the number of decoded values.</param>
    /// <returns><see langword="true"/> if the cursor was decoded successfully; otherwise <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="definition"/> is <see langword="null"/>.</exception>
    public static bool TryDecode<T>(
        ReadOnlySpan<char> encoded,
        PaginationQueryDefinition<T> definition,
        out PaginationValues<T> values,
        out int written)
    {
        return TryDecode(encoded, definition, out values, out written, out _, out _);
    }

    /// <summary>
    /// Decodes a cursor token into definition-bound ordered pagination values and optional metadata.
    /// </summary>
    /// <typeparam name="T">The entity type associated with the pagination definition.</typeparam>
    /// <param name="encoded">The encoded cursor token.</param>
    /// <param name="definition">The pagination definition that determines the expected value order.</param>
    /// <param name="values">When this method returns <see langword="true"/>, contains the decoded ordered values.</param>
    /// <param name="written">When this method returns <see langword="true"/>, contains the number of decoded values.</param>
    /// <param name="sortBy">When this method returns <see langword="true"/>, contains the decoded logical sort key, if present.</param>
    /// <param name="totalCount">When this method returns <see langword="true"/>, contains the decoded total count, if present.</param>
    /// <returns><see langword="true"/> if the cursor was decoded successfully; otherwise <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="definition"/> is <see langword="null"/>.</exception>
    public static bool TryDecode<T>(
        ReadOnlySpan<char> encoded,
        PaginationQueryDefinition<T> definition,
        out PaginationValues<T> values,
        out int written,
        out string? sortBy,
        out int? totalCount)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var orderedValues = new object?[definition.ColumnCount];
        if (TryDecodeOrdered(encoded, orderedValues, out written, out sortBy, out totalCount))
        {
            values = new PaginationValues<T>(orderedValues);
            return true;
        }

        values = PaginationValues<T>.Empty;
        return false;
    }

    /// <summary>
    /// Decodes a cursor token into a caller-supplied <see cref="ColumnValue"/> buffer and optional metadata.
    /// </summary>
    /// <param name="encoded">The encoded cursor token.</param>
    /// <param name="values">The destination buffer. Each entry should already contain the expected column name.</param>
    /// <param name="written">When this method returns <see langword="true"/>, contains the number of values decoded into <paramref name="values"/>.</param>
    /// <param name="sortBy">When this method returns <see langword="true"/>, contains the decoded logical sort key, if present.</param>
    /// <param name="totalCount">When this method returns <see langword="true"/>, contains the decoded total count, if present.</param>
    /// <returns><see langword="true"/> if the cursor was decoded successfully; otherwise <see langword="false"/>.</returns>
    public static bool TryDecode(ReadOnlySpan<char> encoded, Span<ColumnValue> values, out int written, out string? sortBy, out int? totalCount)
    {
        return TryDecodeCore(encoded, values, null, out written, out sortBy, out totalCount);
    }

    private static bool TryDecodeOrdered(ReadOnlySpan<char> encoded, object?[] values, out int written, out string? sortBy, out int? totalCount)
    {
        return TryDecodeCore(encoded, default, values, out written, out sortBy, out totalCount);
    }

    private static bool TryDecodeCore(
        ReadOnlySpan<char> encoded,
        Span<ColumnValue> columnValues,
        object?[]? orderedValues,
        out int written,
        out string? sortBy,
        out int? totalCount)
    {
        written = 0;
        sortBy = null;
        totalCount = null;

        if (encoded.IsEmpty)
            return false;

        var maxDecodedLen = Base64Url.GetMaxDecodedLength(encoded.Length);
        byte[]? rentedBuffer = null;
        Span<byte> buffer = maxDecodedLen <= StackAllocThreshold
            ? stackalloc byte[maxDecodedLen]
            : (rentedBuffer = ArrayPool<byte>.Shared.Rent(maxDecodedLen));

        try
        {
            if (!Base64Url.TryDecodeFromChars(encoded, buffer, out var bytesWritten))
                return false;

            var reader = new Utf8JsonReader(buffer[..bytesWritten]);
            if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
                return false;

            var sawSortBy = false;
            var sawTotalCount = false;
            var sawValues = false;
            var decodedValueCount = 0;

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                {
                    if (reader.Read())
                        return false;

                    written = decodedValueCount;
                    return true;
                }

                if (reader.TokenType != JsonTokenType.PropertyName)
                    return false;

                if (reader.ValueTextEquals("s"u8))
                {
                    if (sawSortBy || !reader.Read())
                        return false;

                    if (reader.TokenType is not JsonTokenType.String and not JsonTokenType.Null)
                        return false;

                    sortBy = reader.TokenType == JsonTokenType.Null ? null : reader.GetString();
                    sawSortBy = true;
                }
                else if (reader.ValueTextEquals("c"u8))
                {
                    if (sawTotalCount || !reader.Read() || reader.TokenType != JsonTokenType.Number)
                        return false;

                    if (!reader.TryGetInt32(out var count))
                        return false;

                    totalCount = count;
                    sawTotalCount = true;
                }
                else if (reader.ValueTextEquals("v"u8))
                {
                    if (sawValues || !reader.Read() || reader.TokenType != JsonTokenType.StartArray)
                        return false;

                    if (!(orderedValues is not null
                        ? TryReadOrderedValues(ref reader, orderedValues, out decodedValueCount)
                        : TryReadColumnValues(ref reader, columnValues, out decodedValueCount)))
                    {
                        return false;
                    }

                    sawValues = true;
                }
                else
                {
                    if (!reader.Read())
                        return false;

                    if (reader.TokenType is JsonTokenType.StartObject or JsonTokenType.StartArray)
                        reader.Skip();
                }
            }

            return false;
        }
        catch (FormatException)
        {
            written = 0;
            sortBy = null;
            totalCount = null;
            return false;
        }
        catch (InvalidOperationException)
        {
            written = 0;
            sortBy = null;
            totalCount = null;
            return false;
        }
        catch (ArgumentException)
        {
            written = 0;
            sortBy = null;
            totalCount = null;
            return false;
        }
        catch (JsonException)
        {
            written = 0;
            sortBy = null;
            totalCount = null;
            return false;
        }
        finally
        {
            if (rentedBuffer is not null)
                ArrayPool<byte>.Shared.Return(rentedBuffer);
        }
    }

    private static bool TryReadColumnValues(ref Utf8JsonReader reader, Span<ColumnValue> values, out int written)
    {
        written = 0;

        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            if (!TryReadTypedValue(ref reader, out var value))
                return false;

            if ((uint)written >= (uint)values.Length)
                return false;

            values[written] = new ColumnValue(values[written].Name, value);
            written++;
        }

        return reader.TokenType == JsonTokenType.EndArray;
    }

    private static bool TryReadOrderedValues(ref Utf8JsonReader reader, object?[] values, out int written)
    {
        written = 0;

        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            if (!TryReadTypedValue(ref reader, out var value))
                return false;

            if ((uint)written >= (uint)values.Length)
                return false;

            values[written] = value;
            written++;
        }

        return reader.TokenType == JsonTokenType.EndArray;
    }

    private static void WriteEnumValue(Utf8JsonWriter writer, Enum value)
    {
        var enumType = value.GetType();
        var metadata = s_enumMetadataCache.GetOrAdd(enumType, static t =>
        {
            var fullName = t.FullName ?? ThrowEnumNoFullName(t);
            var assemblyName = t.Assembly.GetName().Name ?? ThrowEnumNoAssemblyName(t);
            return new EnumMetadata(
                string.Concat(fullName, ", ", assemblyName),
                Type.GetTypeCode(Enum.GetUnderlyingType(t)));
        });

        writer.WriteNumberValue(CursorValueKindEnum);
        writer.WriteStringValue(metadata.StableName);

        switch (metadata.UnderlyingTypeCode)
        {
            case TypeCode.Byte:
                writer.WriteNumberValue((byte)(object)value);
                break;
            case TypeCode.SByte:
                writer.WriteNumberValue((sbyte)(object)value);
                break;
            case TypeCode.Int16:
                writer.WriteNumberValue((short)(object)value);
                break;
            case TypeCode.UInt16:
                writer.WriteNumberValue((ushort)(object)value);
                break;
            case TypeCode.Int32:
                writer.WriteNumberValue((int)(object)value);
                break;
            case TypeCode.UInt32:
                writer.WriteNumberValue((uint)(object)value);
                break;
            case TypeCode.Int64:
                writer.WriteNumberValue((long)(object)value);
                break;
            case TypeCode.UInt64:
                writer.WriteNumberValue((ulong)(object)value);
                break;
            default:
                ThrowEnumUnsupportedUnderlyingType(enumType);
                break;
        }
    }

    private static bool TryReadTypedValue(ref Utf8JsonReader reader, out object? value)
    {
        value = null;

        if (reader.TokenType != JsonTokenType.StartArray)
            return false;

        if (!reader.Read() || reader.TokenType != JsonTokenType.Number || !reader.TryGetByte(out var kind))
            return false;

        switch (kind)
        {
            case CursorValueKindNull:
                if (!reader.Read() || reader.TokenType != JsonTokenType.Null)
                    return false;
                break;
            case CursorValueKindString:
                if (!reader.Read() || reader.TokenType != JsonTokenType.String)
                    return false;
                value = reader.GetString();
                break;
            case CursorValueKindBoolean:
                if (!reader.Read() || reader.TokenType is not JsonTokenType.True and not JsonTokenType.False)
                    return false;
                value = reader.TokenType == JsonTokenType.True ? s_boxedTrue : s_boxedFalse;
                break;
            case CursorValueKindChar:
                if (!reader.Read() || !TryReadChar(ref reader, out var charValue))
                    return false;
                value = charValue;
                break;
            case CursorValueKindInt32:
                if (!reader.Read() || reader.TokenType != JsonTokenType.Number || !reader.TryGetInt32(out var int32Value))
                    return false;
                value = int32Value;
                break;
            case CursorValueKindInt64:
                if (!reader.Read() || reader.TokenType != JsonTokenType.Number || !reader.TryGetInt64(out var int64Value))
                    return false;
                value = int64Value;
                break;
            case CursorValueKindUInt32:
                if (!reader.Read() || reader.TokenType != JsonTokenType.Number || !reader.TryGetUInt32(out var uint32Value))
                    return false;
                value = uint32Value;
                break;
            case CursorValueKindUInt64:
                if (!reader.Read() || reader.TokenType != JsonTokenType.Number || !reader.TryGetUInt64(out var uint64Value))
                    return false;
                value = uint64Value;
                break;
            case CursorValueKindInt16:
                if (!reader.Read() || reader.TokenType != JsonTokenType.Number || !reader.TryGetInt16(out var int16Value))
                    return false;
                value = int16Value;
                break;
            case CursorValueKindUInt16:
                if (!reader.Read() || reader.TokenType != JsonTokenType.Number || !reader.TryGetUInt16(out var uint16Value))
                    return false;
                value = uint16Value;
                break;
            case CursorValueKindByte:
                if (!reader.Read() || reader.TokenType != JsonTokenType.Number || !reader.TryGetByte(out var byteValue))
                    return false;
                value = byteValue;
                break;
            case CursorValueKindSByte:
                if (!reader.Read() || reader.TokenType != JsonTokenType.Number || !reader.TryGetSByte(out var sbyteValue))
                    return false;
                value = sbyteValue;
                break;
            case CursorValueKindSingle:
                if (!reader.Read() || reader.TokenType != JsonTokenType.Number || !reader.TryGetSingle(out var singleValue))
                    return false;
                value = singleValue;
                break;
            case CursorValueKindDouble:
                if (!reader.Read() || reader.TokenType != JsonTokenType.Number || !reader.TryGetDouble(out var doubleValue))
                    return false;
                value = doubleValue;
                break;
            case CursorValueKindDecimal:
                if (!reader.Read() || reader.TokenType != JsonTokenType.Number || !reader.TryGetDecimal(out var decimalValue))
                    return false;
                value = decimalValue;
                break;
            case CursorValueKindGuid:
                if (!reader.Read() || reader.TokenType != JsonTokenType.String || !reader.TryGetGuid(out var guidValue))
                    return false;
                value = guidValue;
                break;
            case CursorValueKindDateTime:
                if (!reader.Read() || reader.TokenType != JsonTokenType.String || !reader.TryGetDateTime(out var dateTimeValue))
                    return false;
                value = dateTimeValue;
                break;
            case CursorValueKindDateTimeOffset:
                if (!reader.Read() || reader.TokenType != JsonTokenType.String || !reader.TryGetDateTimeOffset(out var dateTimeOffsetValue))
                    return false;
                value = dateTimeOffsetValue;
                break;
            case CursorValueKindDateOnly:
                if (!reader.Read() || !TryReadDateOnly(ref reader, out var dateOnlyValue))
                    return false;
                value = dateOnlyValue;
                break;
            case CursorValueKindTimeOnly:
                if (!reader.Read() || !TryReadTimeOnly(ref reader, out var timeOnlyValue))
                    return false;
                value = timeOnlyValue;
                break;
            case CursorValueKindTimeSpan:
                if (!reader.Read() || !TryReadTimeSpan(ref reader, out var timeSpanValue))
                    return false;
                value = timeSpanValue;
                break;
            case CursorValueKindEnum:
                if (!TryReadEnum(ref reader, out value))
                    return false;
                break;
            default:
                return false;
        }

        if (!reader.Read() || reader.TokenType != JsonTokenType.EndArray)
            return false;

        return true;
    }

    private static bool TryReadChar(ref Utf8JsonReader reader, out char value)
    {
        value = default;
        if (reader.TokenType != JsonTokenType.String)
            return false;

        Span<char> buffer = stackalloc char[1];
        if (reader.CopyString(buffer) != 1)
            return false;

        value = buffer[0];
        return true;
    }

    private static bool TryReadDateOnly(ref Utf8JsonReader reader, out DateOnly value)
    {
        value = default;
        if (reader.TokenType != JsonTokenType.String)
            return false;

        Span<char> buffer = stackalloc char[DateOnlyFormatLength];
        if (reader.CopyString(buffer) != DateOnlyFormatLength)
            return false;

        return DateOnly.TryParseExact(buffer, "O", CultureInfo.InvariantCulture, DateTimeStyles.None, out value);
    }

    private static bool TryReadTimeOnly(ref Utf8JsonReader reader, out TimeOnly value)
    {
        value = default;
        if (!TryReadTimeSpan(ref reader, out var timeSpan))
            return false;

        if (timeSpan < TimeOnly.MinValue.ToTimeSpan() || timeSpan > TimeOnly.MaxValue.ToTimeSpan())
            return false;

        value = TimeOnly.FromTimeSpan(timeSpan);
        return true;
    }

    private static bool TryReadTimeSpan(ref Utf8JsonReader reader, out TimeSpan value)
    {
        value = default;
        if (reader.TokenType != JsonTokenType.String)
            return false;

        Span<byte> buffer = stackalloc byte[MaxTimeSpanFormatLength];
        var bytesWritten = reader.CopyString(buffer);
        return Utf8Parser.TryParse(buffer[..bytesWritten], out value, out var bytesConsumed, 'c') && bytesWritten == bytesConsumed;
    }

    private static bool TryReadEnum(ref Utf8JsonReader reader, out object? value)
    {
        value = null;

        if (!reader.Read() || reader.TokenType != JsonTokenType.String)
            return false;

        var typeName = reader.GetString();
        if (typeName is null)
            return false;

        if (!TryResolveEnumType(typeName, out var enumType))
            return false;

        if (!reader.Read() || reader.TokenType != JsonTokenType.Number)
            return false;

        if (reader.TryGetInt64(out var signedValue))
            value = Enum.ToObject(enumType, signedValue);
        else if (reader.TryGetUInt64(out var unsignedValue))
            value = Enum.ToObject(enumType, unsignedValue);
        else
            return false;

        return true;
    }

    private static bool TryResolveEnumType(string typeName, out Type enumType)
    {
        if (s_enumTypeCache.TryGetValue(typeName, out var cachedType))
        {
            enumType = cachedType;
            return true;
        }

        enumType = Type.GetType(typeName, throwOnError: false)!;
        if (enumType is null || !enumType.IsEnum)
            return false;

        s_enumTypeCache.TryAdd(typeName, enumType);
        return true;
    }

    private readonly record struct EnumMetadata(string StableName, TypeCode UnderlyingTypeCode);

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static string ThrowEnumNoFullName(Type t) =>
        throw new NotSupportedException($"Enum type '{t}' does not have a full name.");

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static string ThrowEnumNoAssemblyName(Type t) =>
        throw new NotSupportedException($"Enum type '{t}' assembly does not have a name.");

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowEnumUnsupportedUnderlyingType(Type enumType) =>
        throw new NotSupportedException($"Enum underlying type '{Enum.GetUnderlyingType(enumType)}' is not supported.");

    private sealed class PooledBufferWriter(int initialCapacity) : IBufferWriter<byte>
    {
        private byte[] _buffer = ArrayPool<byte>.Shared.Rent(initialCapacity);

        public int WrittenCount
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private set;
        }

        public ReadOnlySpan<byte> WrittenSpan
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _buffer.AsSpan(0, WrittenCount);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Reset() => WrittenCount = 0;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Advance(int count) => WrittenCount += count;

        public Memory<byte> GetMemory(int sizeHint = 0)
        {
            if (sizeHint <= 0) sizeHint = 1;
            if (WrittenCount + sizeHint > _buffer.Length)
                Grow(sizeHint);
            return _buffer.AsMemory(WrittenCount);
        }

        public Span<byte> GetSpan(int sizeHint = 0)
        {
            if (sizeHint <= 0) sizeHint = 1;
            if (WrittenCount + sizeHint > _buffer.Length)
                Grow(sizeHint);
            return _buffer.AsSpan(WrittenCount);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void Grow(int sizeHint)
        {
            var newSize = Math.Max(_buffer.Length * 2, WrittenCount + sizeHint);
            var newBuffer = ArrayPool<byte>.Shared.Rent(newSize);
            _buffer.AsSpan(0, WrittenCount).CopyTo(newBuffer);
            ArrayPool<byte>.Shared.Return(_buffer);
            _buffer = newBuffer;
        }
    }
}
