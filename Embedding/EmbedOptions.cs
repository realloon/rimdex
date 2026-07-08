namespace Rimdex.Embedding;

internal readonly record struct EmbedOptions(int Limit, int BatchSize) {
    public void Validate() {
        if (Limit <= 0) {
            throw new ArgumentOutOfRangeException(nameof(Limit), "limit must be positive");
        }

        if (BatchSize <= 0) {
            throw new ArgumentOutOfRangeException(nameof(BatchSize), "batch-size must be positive");
        }
    }
}