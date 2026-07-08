namespace Rimdex.Data;

internal sealed record SearchVectorRow(
    int ModId,
    int Dimension,
    byte[] Embedding);