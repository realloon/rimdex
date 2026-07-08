using System.Buffers.Binary;
using System.Runtime.InteropServices;

namespace Rimdex.Embedding;

internal static class EmbeddingVector {
    public static byte[] NormalizeToBlob(float[] vector) {
        var normalized = Normalize(vector);
        var bytes = new byte[normalized.Length * sizeof(float)];
        for (var i = 0; i < normalized.Length; i++) {
            BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(i * sizeof(float), sizeof(float)), normalized[i]);
        }

        return bytes;
    }

    public static float[] Normalize(float[] vector) {
        if (vector.Length == 0) {
            throw new InvalidDataException("Embedding API returned an empty vector");
        }

        double sum = 0;
        foreach (var value in vector) {
            if (!float.IsFinite(value)) {
                throw new InvalidDataException("Embedding API returned a non-finite vector value");
            }

            sum += (double)value * value;
        }

        var norm = Math.Sqrt(sum);
        if (norm == 0 || !double.IsFinite(norm)) {
            throw new InvalidDataException("Embedding API returned an invalid vector norm");
        }

        var normalized = new float[vector.Length];
        for (var i = 0; i < vector.Length; i++) {
            normalized[i] = (float)(vector[i] / norm);
        }

        return normalized;
    }

    public static float CosineDistance(byte[] embedding, int dimension, float[] query) {
        if (!BitConverter.IsLittleEndian) {
            throw new NotSupportedException("Stored embeddings require a little-endian CPU");
        }

        if (dimension != query.Length) {
            throw new InvalidDataException(
                $"Stored embedding dimension {dimension} does not match query dimension {query.Length}");
        }

        if (embedding.Length != dimension * sizeof(float)) {
            throw new InvalidDataException("Stored embedding blob length does not match its dimension");
        }

        var dot = 0f;
        var values = MemoryMarshal.Cast<byte, float>(embedding);
        for (var i = 0; i < dimension; i++) {
            dot += values[i] * query[i];
        }

        return 1 - dot;
    }
}