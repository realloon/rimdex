namespace Rimdex.Platform;

internal static class AppPaths {
    private static readonly string Root = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "rimdex");

    public static readonly string ConfigPath = Path.Combine(Root, "config.json");

    public static readonly string DatabasePath = Path.Combine(Root, "rimdex.sqlite");
}