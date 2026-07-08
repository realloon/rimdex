namespace Rimdex.Platform;

internal static class AppPaths {
    public static string ConfigPath {
        get {
            var configDirectory = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return string.IsNullOrWhiteSpace(configDirectory)
                ? throw new InvalidOperationException("Could not resolve the application config directory.")
                : Path.Combine(configDirectory, "rimdex", "config.json");
        }
    }

    public static string DatabasePath {
        get {
            var dataDirectory = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return string.IsNullOrWhiteSpace(dataDirectory)
                ? throw new InvalidOperationException("Could not resolve the local application data directory.")
                : Path.Combine(dataDirectory, "rimdex", "rimdex.sqlite");
        }
    }
}