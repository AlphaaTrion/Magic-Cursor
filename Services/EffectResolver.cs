using CursorMagic.Models;

namespace CursorMagic.Services;

public static class EffectResolver
{
    public static ClickEffect Resolve(CursorTheme theme, AppSettings settings)
    {
        if (theme.Id.Equals("lightsaber", StringComparison.OrdinalIgnoreCase))
        {
            var bladeColor = settings.ThemeColorOverrides.TryGetValue(theme.Id, out var savedBladeColor)
                ? savedBladeColor
                : theme.Effect.PrimaryColor;
            return new ClickEffect
            {
                Type = "Saber Blade",
                PrimaryColor = bladeColor,
                SecondaryColor = "#FFFFFF",
                ParticleCount = 3,
                Radius = 0,
                DurationMs = 430
            };
        }

        if (theme.Id.Equals("omnitrix", StringComparison.OrdinalIgnoreCase))
        {
            return new ClickEffect
            {
                Type = "Omnitrix Core",
                PrimaryColor = "#FF2020",
                SecondaryColor = "#FF6262",
                ParticleCount = 3,
                Radius = 0,
                DurationMs = 420
            };
        }

        var effect = settings.ThemeEffectOverrides.TryGetValue(theme.Id, out var effectName)
            ? Create(effectName)
            : Clone(theme.Effect);

        if (settings.ThemeColorOverrides.TryGetValue(theme.Id, out var color))
        {
            effect.PrimaryColor = color;
        }

        return effect;
    }

    public static ClickEffect Clone(ClickEffect effect)
    {
        return new ClickEffect
        {
            Type = effect.Type,
            PrimaryColor = effect.PrimaryColor,
            SecondaryColor = effect.SecondaryColor,
            ParticleCount = effect.ParticleCount,
            Radius = effect.Radius,
            DurationMs = effect.DurationMs
        };
    }

    public static ClickEffect Create(string name) => name switch
    {
        "Star Wand" => new ClickEffect { Type = "Star Wand", PrimaryColor = "#FFD957", SecondaryColor = "#F25AAE", ParticleCount = 12, Radius = 32, DurationMs = 580 },
        "Hearts" => new ClickEffect { Type = "Hearts", PrimaryColor = "#F25AAE", SecondaryColor = "#FFD6EC", ParticleCount = 8, Radius = 26, DurationMs = 560 },
        "Rings" => new ClickEffect { Type = "Rings", PrimaryColor = "#55F7FF", SecondaryColor = "#FFFFFF", ParticleCount = 5, Radius = 30, DurationMs = 520 },
        "Fireflies" => new ClickEffect { Type = "Fireflies", PrimaryColor = "#C9FF7A", SecondaryColor = "#FFEA7A", ParticleCount = 10, Radius = 32, DurationMs = 620 },
        "Time Vortex" => new ClickEffect { Type = "Time Vortex", PrimaryColor = "#4A67FF", SecondaryColor = "#FFFFFF", ParticleCount = 5, Radius = 0, DurationMs = 640 },
        "Scanner Sweep" => new ClickEffect { Type = "Scanner Sweep", PrimaryColor = "#F2D36B", SecondaryColor = "#5FE2EF", ParticleCount = 4, Radius = 0, DurationMs = 520 },
        "Warp Trail" => new ClickEffect { Type = "Warp Trail", PrimaryColor = "#5FE2EF", SecondaryColor = "#FFFFFF", ParticleCount = 10, Radius = 42, DurationMs = 360 },
        "Energon Sparks" => new ClickEffect { Type = "Energon Sparks", PrimaryColor = "#F04B4B", SecondaryColor = "#D8DEE8", ParticleCount = 9, Radius = 30, DurationMs = 500 },
        "Blade Slash" => new ClickEffect { Type = "Blade Slash", PrimaryColor = "#D9DEE5", SecondaryColor = "#5FE2EF", ParticleCount = 6, Radius = 22, DurationMs = 350 },
        "Daylight Glint" => new ClickEffect { Type = "Daylight Glint", PrimaryColor = "#DCE7F2", SecondaryColor = "#5FE2EF", ParticleCount = 7, Radius = 24, DurationMs = 390 },
        "Eclipse Runes" => new ClickEffect { Type = "Eclipse Runes", PrimaryColor = "#FF123A", SecondaryColor = "#7E0A18", ParticleCount = 8, Radius = 24, DurationMs = 430 },
        "Protection Burst" => new ClickEffect { Type = "Protection Burst", PrimaryColor = "#BDEFFF", SecondaryColor = "#F2C453", ParticleCount = 11, Radius = 34, DurationMs = 620 },
        "Rune Motes" => new ClickEffect { Type = "Rune Motes", PrimaryColor = "#C58C58", SecondaryColor = "#F2C453", ParticleCount = 8, Radius = 30, DurationMs = 620 },
        "Rune Burst" => new ClickEffect { Type = "Rune Burst", PrimaryColor = "#A6192E", SecondaryColor = "#D9DEE5", ParticleCount = 7, Radius = 24, DurationMs = 520 },
        "Dark Curse" => new ClickEffect { Type = "Dark Curse", PrimaryColor = "#A6192E", SecondaryColor = "#D9DEE5", ParticleCount = 7, Radius = 22, DurationMs = 470 },
        "Crystal Shards" => new ClickEffect { Type = "Crystal Shards", PrimaryColor = "#BDEFFF", SecondaryColor = "#FFFFFF", ParticleCount = 9, Radius = 30, DurationMs = 560 },
        "Bubble Burst" => new ClickEffect { Type = "Bubble Burst", PrimaryColor = "#9AF2FF", SecondaryColor = "#F69BD8", ParticleCount = 8, Radius = 32, DurationMs = 560 },
        "Pixel Bits" => new ClickEffect { Type = "Pixel Bits", PrimaryColor = "#E8ECFF", SecondaryColor = "#F6C84D", ParticleCount = 10, Radius = 28, DurationMs = 440 },
        "Moon Dust" => new ClickEffect { Type = "Moon Dust", PrimaryColor = "#FFE78A", SecondaryColor = "#DDE8FF", ParticleCount = 8, Radius = 28, DurationMs = 560 },
        "Candy Burst" => new ClickEffect { Type = "Candy Burst", PrimaryColor = "#FFE46A", SecondaryColor = "#F25AAE", ParticleCount = 10, Radius = 30, DurationMs = 530 },
        "Ink Flick" => new ClickEffect { Type = "Ink Flick", PrimaryColor = "#202124", SecondaryColor = "#F0624D", ParticleCount = 7, Radius = 24, DurationMs = 460 },
        _ => new ClickEffect { Type = "Sparkles", PrimaryColor = "#FFD957", SecondaryColor = "#F25AAE", ParticleCount = 12, Radius = 32, DurationMs = 560 }
    };
}
