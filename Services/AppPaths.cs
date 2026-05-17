using System.IO;

namespace CursorMagic.Services;

public static class AppPaths
{
    public static string Root { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "CursorMagic");

    public static string BuiltInThemes { get; } = Path.Combine(Root, "BuiltInThemes");
    public static string UserThemes { get; } = Path.Combine(Root, "UserThemes");
    public static string SettingsPath { get; } = Path.Combine(Root, "settings.json");
    public static string BackupPath { get; } = Path.Combine(Root, "cursor-backup.json");

    public static void Ensure()
    {
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(BuiltInThemes);
        Directory.CreateDirectory(UserThemes);
    }
}
