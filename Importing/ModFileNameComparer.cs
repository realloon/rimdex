namespace Rimdex.Importing;

internal sealed class ModFileNameComparer : IComparer<string> {
    public static readonly ModFileNameComparer Instance = new();

    private ModFileNameComparer() { }

    public int Compare(string? left, string? right) {
        if (ReferenceEquals(left, right)) {
            return 0;
        }

        if (left is null) {
            return -1;
        }

        if (right is null) {
            return 1;
        }

        var leftId = Path.GetFileNameWithoutExtension(left);
        var rightId = Path.GetFileNameWithoutExtension(right);

        return leftId.Length == rightId.Length
            ? string.CompareOrdinal(leftId, rightId)
            : leftId.Length.CompareTo(rightId.Length);
    }
}