using System.Text.Json.Serialization;

namespace CursorMagic.Models;

public sealed class CursorTheme
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Category { get; set; } = "";
    public string PreviewPath { get; set; } = "";
    public bool IsUserTheme { get; set; }
    public bool IsActive { get; set; }
    public List<CursorVariant> Variants { get; set; } = [];
    public ClickEffect Effect { get; set; } = new();
    public string GlowCursorPath { get; set; } = "";
}

public sealed class CursorVariant
{
    public string Role { get; set; } = "Arrow";
    public string AssetPath { get; set; } = "";
    public int HotspotX { get; set; }
    public int HotspotY { get; set; }
    public int Size { get; set; } = 48;
}

public sealed class ClickEffect
{
    public string Type { get; set; } = "Sparkles";
    public string PrimaryColor { get; set; } = "#FFD957";
    public string SecondaryColor { get; set; } = "#F25AAE";
    public int ParticleCount { get; set; } = 12;
    public double Radius { get; set; } = 34;
    public int DurationMs { get; set; } = 560;
}

public sealed class AppSettings
{
    public bool EffectsPaused { get; set; }
    public bool StartWithWindows { get; set; }
    public bool RestoreOnExit { get; set; }
    public string ActiveThemeId { get; set; } = "";
    public double AnimationScale { get; set; } = 1.0;
    public double AnimationBrightness { get; set; } = 1.0;
    public Dictionary<string, string> ThemeEffectOverrides { get; set; } = [];
    public Dictionary<string, string> ThemeColorOverrides { get; set; } = [];
    public List<string> BlockedProcessNames { get; set; } = [];
}

public sealed class CursorRegistryBackup
{
    public Dictionary<string, string> Values { get; set; } = [];
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(CursorTheme))]
[JsonSerializable(typeof(List<CursorTheme>))]
[JsonSerializable(typeof(AppSettings))]
[JsonSerializable(typeof(CursorRegistryBackup))]
internal partial class CursorJsonContext : JsonSerializerContext
{
}
