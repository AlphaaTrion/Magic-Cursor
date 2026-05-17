using CursorMagic.Models;

namespace CursorMagic.Services;

public static class EffectResolver
{
    public static ClickEffect Resolve(CursorTheme theme, AppSettings settings)
    {
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
        _ => new ClickEffect { Type = "Sparkles", PrimaryColor = "#FFD957", SecondaryColor = "#F25AAE", ParticleCount = 12, Radius = 32, DurationMs = 560 }
    };
}
