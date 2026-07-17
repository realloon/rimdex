namespace Rimdex.Sync;

internal readonly record struct SyncOptions(int? Limit, bool Full) {
    public void Validate() {
        if (Limit is <= 0) {
            throw new ArgumentOutOfRangeException(nameof(Limit), "limit must be positive");
        }
    }
}