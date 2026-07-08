using System.Buffers.Binary;

namespace Rimdex.Embedding;

internal static class EmbeddingVector {
    public static byte[] NormalizeToBlob(float[] vector) {
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

        var bytes = new byte[vector.Length * sizeof(float)];
        for (var i = 0; i < vector.Length; i++) {
            BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(i * sizeof(float), sizeof(float)),
                (float)(vector[i] / norm));
        }

        return bytes;
    }
}