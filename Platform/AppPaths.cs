namespace Rimdex.Platform;

internal static class AppPaths {
    public static string DatabasePath {
        get {
            var dataDirectory = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return string.IsNullOrWhiteSpace(dataDirectory)
                ? throw new InvalidOperationException("Could not resolve the local application data directory.")
                : Path.Combine(dataDirectory, "rimdex", "rimdex.sqlite");
        }
    }
}