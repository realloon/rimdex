namespace Rimdex.Platform;

internal static class AppPaths {
    private static string RimdexDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".rimdex");

    public static readonly string ConfigPath = Path.Combine(RimdexDirectory, "config.json");

    public static readonly string DatabasePath = Path.Combine(RimdexDirectory, "rimdex.sqlite");
}