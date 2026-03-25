using System.Buffers.Binary;
using System.Text;

namespace DotPilot.Core.Providers;

internal static class GgufMetadataReader
{
    private static readonly byte[] MagicBytes = "GGUF"u8.ToArray();
    private const string ArchitectureKey = "general.architecture";

    public static async ValueTask<(bool IsSuccess, string? Architecture, string? ErrorMessage)> TryReadArchitectureAsync(
        string modelPath,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = File.OpenRead(modelPath);

            if (!await HasMagicHeaderAsync(stream, cancellationToken).ConfigureAwait(false))
            {
                return (false, null, "The selected file is not a readable GGUF model.");
            }

            _ = await ReadUInt32Async(stream, cancellationToken).ConfigureAwait(false);
            var tensorCount = await ReadUInt64Async(stream, cancellationToken).ConfigureAwait(false);
            _ = tensorCount;
            var metadataCount = await ReadUInt64Async(stream, cancellationToken).ConfigureAwait(false);
            for (ulong index = 0; index < metadataCount; index++)
            {
                var key = await ReadStringAsync(stream, cancellationToken).ConfigureAwait(false);
                var valueType = (GgufMetadataValueType)await ReadUInt32Async(stream, cancellationToken).ConfigureAwait(false);
                if (string.Equals(key, ArchitectureKey, StringComparison.Ordinal))
                {
                    if (valueType != GgufMetadataValueType.String)
                    {
                        return (false, null, "The GGUF file does not contain a readable general.architecture value.");
                    }

                    var architecture = await ReadStringAsync(stream, cancellationToken).ConfigureAwait(false);
                    return !string.IsNullOrWhiteSpace(architecture)
                        ? (true, architecture, null)
                        : CreateMissingArchitectureResult();
                }

                await SkipValueAsync(stream, valueType, cancellationToken).ConfigureAwait(false);
            }

            return CreateMissingArchitectureResult();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception) when (File.Exists(modelPath))
        {
            return (false, null, "The selected file is not a readable GGUF model.");
        }
    }

    private static (bool IsSuccess, string? Architecture, string? ErrorMessage) CreateMissingArchitectureResult()
    {
        return (false, null, "The GGUF file is missing general.architecture metadata.");
    }

    private static async ValueTask<bool> HasMagicHeaderAsync(Stream stream, CancellationToken cancellationToken)
    {
        var magic = new byte[MagicBytes.Length];
        await stream.ReadExactlyAsync(magic, cancellationToken).ConfigureAwait(false);
        return magic.Length == MagicBytes.Length && magic.AsSpan().SequenceEqual(MagicBytes);
    }

    private static async ValueTask<string> ReadStringAsync(Stream stream, CancellationToken cancellationToken)
    {
        var length = await ReadUInt64Async(stream, cancellationToken).ConfigureAwait(false);
        EnsureReadable(stream, length);
        var buffer = new byte[checked((int)length)];
        await stream.ReadExactlyAsync(buffer, cancellationToken).ConfigureAwait(false);
        return Encoding.UTF8.GetString(buffer);
    }

    private static async ValueTask SkipValueAsync(
        Stream stream,
        GgufMetadataValueType valueType,
        CancellationToken cancellationToken)
    {
        switch (valueType)
        {
            case GgufMetadataValueType.UInt8:
            case GgufMetadataValueType.Int8:
            case GgufMetadataValueType.Bool:
                SkipBytes(stream, 1);
                return;
            case GgufMetadataValueType.UInt16:
            case GgufMetadataValueType.Int16:
                SkipBytes(stream, 2);
                return;
            case GgufMetadataValueType.UInt32:
            case GgufMetadataValueType.Int32:
            case GgufMetadataValueType.Float32:
                SkipBytes(stream, 4);
                return;
            case GgufMetadataValueType.UInt64:
            case GgufMetadataValueType.Int64:
            case GgufMetadataValueType.Float64:
                SkipBytes(stream, 8);
                return;
            case GgufMetadataValueType.String:
                _ = await ReadStringAsync(stream, cancellationToken).ConfigureAwait(false);
                return;
            case GgufMetadataValueType.Array:
                await SkipArrayAsync(stream, cancellationToken).ConfigureAwait(false);
                return;
            default:
                throw new InvalidDataException($"Unsupported GGUF metadata type '{valueType}'.");
        }
    }

    private static async ValueTask SkipArrayAsync(Stream stream, CancellationToken cancellationToken)
    {
        var elementType = (GgufMetadataValueType)await ReadUInt32Async(stream, cancellationToken).ConfigureAwait(false);
        var count = await ReadUInt64Async(stream, cancellationToken).ConfigureAwait(false);
        for (ulong index = 0; index < count; index++)
        {
            await SkipValueAsync(stream, elementType, cancellationToken).ConfigureAwait(false);
        }
    }

    private static void SkipBytes(Stream stream, ulong byteCount)
    {
        EnsureReadable(stream, byteCount);
        _ = stream.Seek(checked((long)byteCount), SeekOrigin.Current);
    }

    private static void EnsureReadable(Stream stream, ulong byteCount)
    {
        var remaining = checked((ulong)Math.Max(0, stream.Length - stream.Position));
        if (remaining < byteCount || byteCount > int.MaxValue)
        {
            throw new InvalidDataException("The GGUF file is truncated.");
        }
    }

    private static async ValueTask<uint> ReadUInt32Async(Stream stream, CancellationToken cancellationToken)
    {
        var buffer = new byte[sizeof(uint)];
        await stream.ReadExactlyAsync(buffer, cancellationToken).ConfigureAwait(false);
        return BinaryPrimitives.ReadUInt32LittleEndian(buffer);
    }

    private static async ValueTask<ulong> ReadUInt64Async(Stream stream, CancellationToken cancellationToken)
    {
        var buffer = new byte[sizeof(ulong)];
        await stream.ReadExactlyAsync(buffer, cancellationToken).ConfigureAwait(false);
        return BinaryPrimitives.ReadUInt64LittleEndian(buffer);
    }

    private enum GgufMetadataValueType : uint
    {
        UInt8 = 0,
        Int8 = 1,
        UInt16 = 2,
        Int16 = 3,
        UInt32 = 4,
        Int32 = 5,
        Float32 = 6,
        Bool = 7,
        String = 8,
        Array = 9,
        UInt64 = 10,
        Int64 = 11,
        Float64 = 12,
    }
}
