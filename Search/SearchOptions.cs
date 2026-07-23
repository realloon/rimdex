namespace Rimdex.Search;

internal readonly record struct SearchOptions(string Query, int Limit, bool Keyword) {
    public void Validate() {
        if (string.IsNullOrWhiteSpace(Query)) {
            throw new ArgumentException("query must not be empty", nameof(Query));
        }

        if (Limit <= 0) {
            throw new ArgumentOutOfRangeException(nameof(Limit), "limit must be positive");
        }
    }
}