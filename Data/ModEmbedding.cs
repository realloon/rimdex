namespace Rimdex.Data;

internal sealed record ModEmbedding(int ModId, string SearchTextHash, byte[] Embedding, int Dimension);