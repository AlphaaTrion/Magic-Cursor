using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CursorMagic.Models;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using Pen = System.Windows.Media.Pen;
using Point = System.Windows.Point;

namespace CursorMagic.Services;

public sealed class CursorAssetGenerator
{
    private const int LogicalSize = 48;
    private const int CursorPixelSize = 128;
    private const int PreviewPixelSize = 512;
    private const string AssetRevision = "q9";

    private static readonly string[] ThemeRoles =
    [
        "Arrow",
        "Help",
        "AppStarting",
        "Wait",
        "Crosshair",
        "IBeam",
        "NWPen",
        "No",
        "SizeNS",
        "SizeWE",
        "SizeNWSE",
        "SizeNESW",
        "SizeAll",
        "UpArrow",
        "Hand",
        "Pin",
        "Person"
    ];

    public List<CursorTheme> EnsureBuiltInThemes()
    {
        AppPaths.Ensure();
        var themes = BuiltInSpecs().Select(EnsureBuiltInTheme).ToList();
        File.WriteAllText(
            Path.Combine(AppPaths.BuiltInThemes, "themes.json"),
            JsonSerializer.Serialize(themes, CursorJsonContext.Default.ListCursorTheme));
        return themes;
    }

    public CursorTheme? RebuildBuiltInTheme(string id, string accentColor)
    {
        var spec = BuiltInSpecs().FirstOrDefault(item => item.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        if (spec is null)
        {
            return null;
        }

        return EnsureBuiltInTheme(spec with
        {
            AccentColor = accentColor,
            Effect = EffectWithAccent(spec.Effect, accentColor)
        });
    }

    public CursorTheme CreateUserTheme(
        string name,
        string imagePath,
        int hotspotX,
        int hotspotY,
        string clickEffect,
        string decoration)
    {
        AppPaths.Ensure();
        var id = Slug(name);
        var themeFolder = Path.Combine(AppPaths.UserThemes, id);
        var suffix = 1;
        while (Directory.Exists(themeFolder))
        {
            id = $"{Slug(name)}-{suffix++}";
            themeFolder = Path.Combine(AppPaths.UserThemes, id);
        }

        Directory.CreateDirectory(themeFolder);

        var bitmap = LoadBitmap(imagePath);
        var cursorArt = RenderUserCursor(bitmap, decoration, CursorPixelSize);
        var previewArt = RenderUserCursor(bitmap, decoration, PreviewPixelSize);
        var previewPath = Path.Combine(themeFolder, "preview.png");
        var cursorPath = Path.Combine(themeFolder, "arrow.cur");
        SavePng(previewArt, previewPath);
        SaveCursor(cursorArt, cursorPath, hotspotX, hotspotY);

        var effect = EffectFor(clickEffect);
        var theme = new CursorTheme
        {
            Id = id,
            Name = name,
            Category = "Custom",
            Description = "Created in Cursor Magic.",
            PreviewPath = previewPath,
            IsUserTheme = true,
            Effect = effect,
            Variants = VariantsFor(cursorPath, hotspotX, hotspotY)
        };

        File.WriteAllText(
            Path.Combine(themeFolder, "theme.json"),
            JsonSerializer.Serialize(theme, CursorJsonContext.Default.CursorTheme));
        return theme;
    }

    public static ImageSource LoadPreview(string path)
    {
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.UriSource = new Uri(path, UriKind.Absolute);
        image.EndInit();
        image.Freeze();
        return image;
    }

    private CursorTheme EnsureBuiltInTheme(BuiltInThemeSpec spec)
    {
        var folder = Path.Combine(AppPaths.BuiltInThemes, spec.Id);
        Directory.CreateDirectory(folder);
        var previewPath = Path.Combine(folder, $"preview-{AssetRevision}.png");
        var cursorPath = Path.Combine(folder, $"arrow-{AssetRevision}.cur");

        var cursorArt = RenderBuiltIn(spec, CursorPixelSize);
        var previewArt = RenderBuiltIn(spec, PreviewPixelSize);
        SavePng(previewArt, previewPath);
        SaveCursor(cursorArt, cursorPath, spec.HotspotX, spec.HotspotY);

        return new CursorTheme
        {
            Id = spec.Id,
            Name = spec.Name,
            Description = spec.Description,
            Category = spec.Category,
            PreviewPath = previewPath,
            Effect = spec.Effect,
            Variants = VariantsFor(cursorPath, spec.HotspotX, spec.HotspotY)
        };
    }

    private static List<CursorVariant> VariantsFor(string cursorPath, int hotspotX, int hotspotY)
    {
        return ThemeRoles.Select(role => new CursorVariant
        {
            Role = role,
            AssetPath = cursorPath,
            HotspotX = hotspotX,
            HotspotY = hotspotY,
            Size = CursorPixelSize
        }).ToList();
    }

    private static RenderTargetBitmap RenderBuiltIn(BuiltInThemeSpec spec, int outputSize)
    {
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.PushTransform(new ScaleTransform(outputSize / (double)LogicalSize, outputSize / (double)LogicalSize));
            switch (spec.Id)
            {
                case "star-wand":
                    DrawStarWand(dc);
                    break;
                case "neon-ring":
                    DrawArrow(dc, "#05070B", "#55F7FF", "#FF4FD8");
                    dc.DrawEllipse(null, Pen("#55F7FF", 2), new Point(31, 18), 7, 7);
                    break;
                case "crystal-arrow":
                    DrawCrystal(dc);
                    break;
                case "pixel-sword":
                    DrawPixelSword(dc);
                    break;
                case "moonbeam":
                    DrawCrescent(dc);
                    break;
                case "bubble-pop":
                    DrawBubble(dc);
                    break;
                case "heart-charm":
                    DrawHeartCursor(dc);
                    break;
                case "glass-shard":
                    DrawShard(dc);
                    break;
                case "firefly-trail":
                    DrawArrow(dc, "#17251A", "#C9FF7A", "#FFEA7A");
                    dc.DrawEllipse(Brush("#F8FF8A"), null, new Point(34, 11), 2.5, 2.5);
                    dc.DrawEllipse(Brush("#C9FF7A"), null, new Point(38, 18), 1.8, 1.8);
                    break;
                case "sketch-pen":
                    DrawSketchPen(dc);
                    break;
                case "candy-bolt":
                    DrawCandyBolt(dc);
                    break;
                case "minimal-crosshair":
                    DrawCrosshair(dc);
                    break;
                case "lightsaber":
                    DrawLightsaber(dc, spec.AccentColor);
                    break;
                case "hero-sword":
                    DrawHeroSword(dc);
                    break;
                case "eclipse-sword":
                    DrawEclipseSword(dc);
                    break;
                case "omnitrix":
                    DrawOmnitrix(dc);
                    break;
                case "tardis":
                    DrawTardis(dc);
                    break;
                case "autobot-crest":
                    DrawAutobotCrest(dc);
                    break;
                case "decepticon-crest":
                    DrawDecepticonCrest(dc);
                    break;
                case "shera-sword":
                    DrawSheraSword(dc);
                    break;
                case "ancient-staff":
                    DrawAncientStaff(dc);
                    break;
                case "starfleet-delta":
                    DrawStarfleetDelta(dc);
                    break;
                case "starship":
                    DrawStarship(dc);
                    break;
                case "dark-one-dagger":
                    DrawDarkOneDagger(dc);
                    break;
            }
            dc.Pop();
        }

        return RenderVisual(visual, outputSize);
    }

    private static RenderTargetBitmap RenderUserCursor(BitmapSource source, string decoration, int outputSize)
    {
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.PushTransform(new ScaleTransform(outputSize / (double)LogicalSize, outputSize / (double)LogicalSize));
            var scale = Math.Min(36 / source.Width, 36 / source.Height);
            var width = source.Width * scale;
            var height = source.Height * scale;
            dc.DrawImage(source, new Rect((LogicalSize - width) / 2, (LogicalSize - height) / 2, width, height));

            if (decoration == "Star")
            {
                DrawStar(dc, new Point(36, 12), 8, 4, Brush("#FFE36A"), Pen("#743E9E", 1.4));
            }
            else if (decoration == "Heart")
            {
                DrawHeart(dc, new Point(36, 13), 0.55, Brush("#F25AAE"), Pen("#6E2B5C", 1.2));
            }
            else if (decoration == "Ring")
            {
                dc.DrawEllipse(null, Pen("#55F7FF", 2.2), new Point(35, 14), 8, 8);
            }

            dc.DrawEllipse(Brush("#FFFFFFFF"), Pen("#272B35", 1), new Point(4, 4), 3, 3);
            dc.Pop();
        }
        return RenderVisual(visual, outputSize);
    }

    private static RenderTargetBitmap RenderVisual(DrawingVisual visual, int outputSize)
    {
        var bitmap = new RenderTargetBitmap(outputSize, outputSize, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }

    private static BitmapSource LoadBitmap(string path)
    {
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.UriSource = new Uri(path, UriKind.Absolute);
        image.EndInit();
        image.Freeze();
        return image;
    }

    private static void SavePng(BitmapSource bitmap, string path)
    {
        using var stream = File.Create(path);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        encoder.Save(stream);
    }

    private static void SaveCursor(BitmapSource bitmap, string path, int hotspotX, int hotspotY)
    {
        using var pngStream = new MemoryStream();
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        encoder.Save(pngStream);
        var png = pngStream.ToArray();

        using var writer = new BinaryWriter(File.Create(path));
        var width = bitmap.PixelWidth;
        var height = bitmap.PixelHeight;
        var scaledHotspotX = (int)Math.Round(hotspotX * (width / (double)LogicalSize));
        var scaledHotspotY = (int)Math.Round(hotspotY * (height / (double)LogicalSize));
        writer.Write((ushort)0);
        writer.Write((ushort)2);
        writer.Write((ushort)1);
        writer.Write((byte)(width >= 256 ? 0 : width));
        writer.Write((byte)(height >= 256 ? 0 : height));
        writer.Write((byte)0);
        writer.Write((byte)0);
        writer.Write((ushort)Math.Clamp(scaledHotspotX, 0, width - 1));
        writer.Write((ushort)Math.Clamp(scaledHotspotY, 0, height - 1));
        writer.Write(png.Length);
        writer.Write(22);
        writer.Write(png);
    }

    private static void DrawStarWand(DrawingContext dc)
    {
        var ink = Pen("#15151C", 2.1);

        DrawWandWing(dc, true);
        DrawWandWing(dc, false);
        DrawWandHorn(dc, true);
        DrawWandHorn(dc, false);

        var handle = Geometry.Parse("M 20.8,27.5 L 27.2,27.5 L 27.2,42.8 L 20.8,42.8 Z");
        dc.DrawGeometry(Brush("#7157B7"), ink, handle);
        dc.PushClip(handle);
        dc.DrawGeometry(Brush("#B89AF3"), null, Geometry.Parse("M 20.9,28 L 27.2,34.2 L 27.2,40.2 L 20.9,34 Z"));
        dc.DrawGeometry(Brush("#4D3C8C"), null, Geometry.Parse("M 20.9,37.3 L 27.2,43.1 L 20.9,43.1 Z"));
        dc.Pop();

        dc.DrawEllipse(Brush("#F6D456"), ink, new Point(24, 42.2), 5.8, 3.0);
        var gem = Geometry.Parse("M 24,42.9 L 28.8,45.8 L 24,48 L 19.2,45.8 Z");
        dc.DrawGeometry(Brush("#EF8CFF"), Pen("#FFFFFF", 1.2), gem);
        dc.DrawLine(Pen("#B754D5", 1.1), new Point(24, 42.8), new Point(24, 47.6));
        dc.DrawLine(Pen("#B754D5", 1.1), new Point(19.2,45.7), new Point(28.8,45.7));

        DrawHeart(dc, new Point(24, 28.2), 0.38, Brush("#F04DA5"), Pen("#591B4B", 1.05));

        dc.DrawEllipse(Brush("#B58CE7"), ink, new Point(24, 15.3), 15.1, 15.1);
        dc.DrawEllipse(Brush("#BFE8F6"), Pen("#15151C", 1.7), new Point(24, 15.3), 11.6, 11.6);

        dc.DrawGeometry(Brush("#EAFBFF"), null, Geometry.Parse("M 14.2,12.7 L 20.5,6.1 L 23.1,12.6 Z"));
        dc.DrawGeometry(Brush("#D8F3FC"), null, Geometry.Parse("M 25.5,6.3 L 29.1,12.8 L 34,11.7 L 31.2,6.4 Z"));
        dc.DrawGeometry(Brush("#A8D9EA"), null, Geometry.Parse("M 13.4,18.2 L 20.5,17.7 L 15.7,23.1 Z"));
        dc.DrawGeometry(Brush("#9ED4E6"), null, Geometry.Parse("M 28.1,18 L 34.5,16.9 L 32.6,23.3 Z"));

        DrawStar(dc, new Point(24, 15.3), 10.2, 4.1, Brush("#FFD45E"), Pen("#FFFFFF", 1.05));
        DrawStar(dc, new Point(24, 15.3), 4.1, 1.7, Brush("#F47A22"), null);

        foreach (var p in new[] { new Point(24, 3.9), new Point(34.7,15.2), new Point(24,26.6), new Point(13.3,15.2), new Point(18.5,8.3), new Point(29.5,8.4) })
        {
            dc.DrawEllipse(Brush("#D43A9D"), null, p, 2.0, 2.0);
        }
    }

    private static void DrawWandWing(DrawingContext dc, bool left)
    {
        var s = left ? -1 : 1;
        var outer = new StreamGeometry();
        using (var ctx = outer.Open())
        {
            ctx.BeginFigure(new Point(24 + s * 12.3, 13), true, true);
            ctx.BezierTo(new Point(24 + s * 17.7, 1.2), new Point(24 + s * 25.7, 2.1), new Point(24 + s * 26.2, 14.8), true, false);
            ctx.BezierTo(new Point(24 + s * 26.4, 22.2), new Point(24 + s * 20.5, 24.6), new Point(24 + s * 17.5, 23), true, false);
            ctx.BezierTo(new Point(24 + s * 18.8, 28.5), new Point(24 + s * 13.1, 27.9), new Point(24 + s * 12.7, 21.6), true, false);
        }

        outer.Freeze();
        dc.DrawGeometry(Brush("#FFFFFF"), Pen("#15151C", 2.1), outer);

        var inner = new StreamGeometry();
        using (var ctx = inner.Open())
        {
            ctx.BeginFigure(new Point(24 + s * 18.2, 9), true, true);
            ctx.BezierTo(new Point(24 + s * 22.6, 7), new Point(24 + s * 22.9, 16.2), new Point(24 + s * 18, 21.5), true, false);
            ctx.BezierTo(new Point(24 + s * 20.6, 18), new Point(24 + s * 18.9, 12.5), new Point(24 + s * 18.2, 9), true, false);
        }

        inner.Freeze();
        dc.DrawGeometry(Brush("#B4B0F0"), null, inner);
    }

    private static void DrawWandHorn(DrawingContext dc, bool left)
    {
        var s = left ? -1 : 1;
        var horn = new StreamGeometry();
        using (var ctx = horn.Open())
        {
            ctx.BeginFigure(new Point(24 + s * 7.9, 5.3), true, true);
            ctx.BezierTo(new Point(24 + s * 9.7, 0.6), new Point(24 + s * 12.9, 0.4), new Point(24 + s * 11.3, 8.6), true, false);
            ctx.LineTo(new Point(24 + s * 6.9, 9.3), true, false);
        }

        horn.Freeze();
        dc.DrawGeometry(Brush("#D84CA9"), Pen("#15151C", 1.35), horn);
    }

    private static void DrawArrow(DrawingContext dc, string fill, string stroke, string accent)
    {
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(new Point(4, 3), true, true);
            ctx.LineTo(new Point(33, 27), true, false);
            ctx.LineTo(new Point(21, 29), true, false);
            ctx.LineTo(new Point(28, 43), true, false);
            ctx.LineTo(new Point(20, 46), true, false);
            ctx.LineTo(new Point(13, 31), true, false);
            ctx.LineTo(new Point(5, 40), true, false);
        }

        geometry.Freeze();
        dc.DrawGeometry(Brush(fill), Pen(stroke, 2.4), geometry);
        dc.DrawLine(Pen(accent, 1.5), new Point(10, 10), new Point(25, 25));
    }

    private static void DrawCrystal(DrawingContext dc)
    {
        var geometry = Geometry.Parse("M 5,3 L 35,22 L 23,26 L 30,43 L 21,45 L 14,29 L 6,39 Z");
        dc.DrawGeometry(Brush("#D7FAFF"), Pen("#35495F", 2.2), geometry);
        dc.DrawLine(Pen("#78D7FF", 2), new Point(12, 9), new Point(22, 26));
        dc.DrawLine(Pen("#FFFFFF", 1.4), new Point(8, 7), new Point(32, 22));
    }

    private static void DrawPixelSword(DrawingContext dc)
    {
        dc.DrawRectangle(Brush("#23212E"), null, new Rect(9, 6, 6, 6));
        dc.DrawRectangle(Brush("#E8ECFF"), null, new Rect(15, 12, 6, 6));
        dc.DrawRectangle(Brush("#B8C2FF"), null, new Rect(21, 18, 6, 6));
        dc.DrawRectangle(Brush("#7A88D7"), null, new Rect(27, 24, 6, 6));
        dc.DrawRectangle(Brush("#322844"), null, new Rect(18, 30, 21, 5));
        dc.DrawRectangle(Brush("#F6C84D"), null, new Rect(30, 34, 5, 10));
        dc.DrawRectangle(null, Pen("#1D1826", 2), new Rect(9, 6, 30, 38));
    }

    private static void DrawCrescent(DrawingContext dc)
    {
        DrawArrow(dc, "#1E2433", "#DDE8FF", "#FFE78A");
        dc.DrawEllipse(Brush("#FFE78A"), null, new Point(33, 12), 8, 8);
        dc.DrawEllipse(Brush("#1E2433"), null, new Point(36, 9), 8, 8);
    }

    private static void DrawBubble(DrawingContext dc)
    {
        DrawArrow(dc, "#E7FCFF", "#56B6D6", "#F69BD8");
        dc.DrawEllipse(null, Pen("#9AF2FF", 2), new Point(34, 13), 6, 6);
        dc.DrawEllipse(null, Pen("#F69BD8", 1.6), new Point(38, 23), 4, 4);
    }

    private static void DrawHeartCursor(DrawingContext dc)
    {
        DrawArrow(dc, "#FFF1FA", "#82335F", "#F25AAE");
        DrawHeart(dc, new Point(34, 15), 0.58, Brush("#F25AAE"), Pen("#82335F", 1.2));
    }

    private static void DrawShard(DrawingContext dc)
    {
        var geometry = Geometry.Parse("M 5,3 L 38,18 L 20,25 L 31,43 L 21,45 L 14,29 L 6,39 Z");
        dc.DrawGeometry(Brush("#EAF6FF"), Pen("#334155", 2), geometry);
        dc.DrawLine(Pen("#94A3B8", 1.5), new Point(13, 8), new Point(20, 25));
        dc.DrawLine(Pen("#FFFFFF", 1.5), new Point(8, 6), new Point(35, 18));
    }

    private static void DrawSketchPen(DrawingContext dc)
    {
        var body = Geometry.Parse("M 7,4 L 35,28 L 25,38 L 4,7 Z");
        dc.DrawGeometry(Brush("#FFF8E8"), Pen("#202124", 2.2), body);
        dc.DrawLine(Pen("#202124", 2), new Point(9, 5), new Point(35, 28));
        dc.DrawLine(Pen("#F0624D", 3), new Point(20, 18), new Point(29, 27));
    }

    private static void DrawCandyBolt(DrawingContext dc)
    {
        var bolt = Geometry.Parse("M 21,3 L 8,25 L 20,24 L 14,45 L 39,17 L 25,19 Z");
        dc.DrawGeometry(Brush("#FFE46A"), Pen("#3A2439", 2.4), bolt);
        dc.DrawLine(Pen("#F25AAE", 3), new Point(17, 13), new Point(26, 19));
        dc.DrawLine(Pen("#6FE7FF", 3), new Point(15, 27), new Point(25, 25));
    }

    private static void DrawCrosshair(DrawingContext dc)
    {
        dc.DrawEllipse(null, Pen("#202124", 2.3), new Point(24, 24), 13, 13);
        dc.DrawLine(Pen("#202124", 2), new Point(24, 5), new Point(24, 17));
        dc.DrawLine(Pen("#202124", 2), new Point(24, 31), new Point(24, 43));
        dc.DrawLine(Pen("#202124", 2), new Point(5, 24), new Point(17, 24));
        dc.DrawLine(Pen("#202124", 2), new Point(31, 24), new Point(43, 24));
        dc.DrawEllipse(Brush("#55F7FF"), null, new Point(24, 24), 2.5, 2.5);
    }

    private static void DrawLightsaber(DrawingContext dc, string bladeColor)
    {
        dc.DrawLine(Pen(bladeColor, 13.5, 0.18), new Point(5.4, 5.2), new Point(31.2, 31.0));
        dc.DrawLine(Pen(bladeColor, 8.2, 0.38), new Point(5.4, 5.2), new Point(31.2, 31.0));
        dc.DrawLine(Pen("#FFFFFF", 4.8), new Point(5.6, 5.4), new Point(30.7, 30.5));
        dc.DrawLine(Pen(bladeColor, 2.1, 0.82), new Point(6.2, 6.1), new Point(30.2, 30.1));
        dc.DrawLine(Pen("#FFFFFF", 1.0, 0.86), new Point(8.5, 6.6), new Point(25.8, 24.1));

        DrawStar(dc, new Point(8.1, 7.2), 2.9, 1.1, Brush("#FFFFFF", 0.88), null);
        dc.DrawEllipse(Brush(bladeColor, 0.72), null, new Point(18.1, 17.9), 1.3, 1.3);

        var emitter = Geometry.Parse("M 26.4,32.5 L 32.5,26.4 L 36.4,30.3 L 30.3,36.4 Z");
        dc.DrawGeometry(Linear("#E5EBF2", "#6E7886", 0, 0, 1, 1), Pen("#080C12", 1.5), emitter);

        var hilt = Geometry.Parse("M 30.2,35.4 L 35.4,30.2 L 46.4,41.2 C 47.6,42.4 47.5,44.5 46.2,45.9 C 44.8,47.3 42.5,47.2 41.2,45.8 Z");
        dc.DrawGeometry(Linear("#303B49", "#090D13", 0, 0, 1, 1), Pen("#020407", 1.7), hilt);
        dc.DrawLine(Pen("#B7C0CC", 1.25), new Point(34.5, 34.8), new Point(43.4, 43.7));
        dc.DrawLine(Pen("#667386", 1.7), new Point(36.9, 32.9), new Point(34.0, 35.8));
        dc.DrawLine(Pen("#667386", 1.7), new Point(41.0, 37.0), new Point(38.1, 39.9));
        dc.DrawLine(Pen(bladeColor, 1.8), new Point(43.7, 41.3), new Point(41.2, 43.8));
        dc.DrawEllipse(Brush(bladeColor), Pen("#071014", 1.1), new Point(31.4, 31.3), 3.1, 3.1);
        dc.DrawEllipse(Brush("#FFFFFF", 0.85), null, new Point(30.4, 30.3), 0.9, 0.9);
    }

    private static void DrawHeroSword(DrawingContext dc)
    {
        var blade = Geometry.Parse("M 3.0,6.2 L 8.2,3.1 L 39.2,20.6 L 43.4,18.9 L 39.9,24.4 L 45.0,25.1 L 32.6,35.0 Z");
        dc.DrawGeometry(Brush("#000000", 0.2), null, Geometry.Parse("M 5.8,9.0 L 10.0,6.2 L 42.4,22.3 L 46.2,22.0 L 35.2,37.0 Z"));
        dc.DrawGeometry(Linear("#F7FBFF", "#697789", 0, 0, 1, 1), Pen("#0C121A", 1.75), blade);
        dc.DrawGeometry(Linear("#FFFFFF", "#BFCAD6", 0, 0, 1, 1), null, Geometry.Parse("M 7.3,5.8 L 34.5,21.5 L 30.2,26.4 L 9.1,8.8 Z"));
        dc.DrawGeometry(Brush("#8794A4", 0.7), null, Geometry.Parse("M 34.5,21.5 L 41.8,22.6 L 33.0,33.0 L 30.2,26.4 Z"));
        dc.DrawLine(Pen("#52606E", 0.95), new Point(14.2, 9.6), new Point(33.2, 21.2));
        dc.DrawLine(Pen("#52606E", 0.8), new Point(16.1, 13.2), new Point(31.0, 22.9));
        dc.DrawLine(Pen("#B8C5D2", 0.8), new Point(18.0, 15.8), new Point(36.4, 24.2));
        dc.DrawLine(Pen("#FFFFFF", 0.9, 0.88), new Point(8.0, 6.2), new Point(27.5, 17.6));

        var guard = Geometry.Parse("M 26.2,32.0 L 32.0,25.3 L 40.4,31.9 L 34.2,39.2 Z");
        dc.DrawGeometry(Linear("#303948", "#101620", 0, 0, 1, 1), Pen("#05080D", 1.35), guard);
        dc.DrawLine(Pen("#101620", 4.0), new Point(34.1, 37.5), new Point(45.2, 45.4));
        dc.DrawLine(Pen("#253142", 5.6), new Point(35.9, 34.9), new Point(43.6, 40.6));
        dc.DrawLine(Pen("#0B0F16", 1.1), new Point(36.4, 35.2), new Point(43.2, 40.2));
        dc.DrawLine(Pen("#E6F3F7", 1.1), new Point(35.9, 34.4), new Point(38.1, 36.0));
        dc.DrawEllipse(Brush("#55E5F2", 0.25), null, new Point(31.2, 33.4), 5.4, 5.4);
        dc.DrawEllipse(Linear("#9CF6FF", "#218BA4", 0, 0, 1, 1), Pen("#102733", 0.95), new Point(31.2, 33.4), 2.8, 2.8);
        dc.DrawEllipse(Brush("#FFFFFF", 0.92), null, new Point(30.2, 32.4), 0.8, 0.8);
    }

    private static void DrawEclipseSword(DrawingContext dc)
    {
        var glowBlade = Geometry.Parse("M 3.0,7.0 L 8.4,4.0 L 42.5,24.0 L 44.8,22.5 L 42.4,27.2 L 46.2,27.8 L 33.0,36.1 Z");
        dc.DrawGeometry(Brush("#FF1838", 0.18), null, glowBlade);
        var blade = Geometry.Parse("M 3.6,7.2 L 8.7,4.5 L 39.7,22.5 L 43.2,21.3 L 40.2,26.0 L 44.9,26.5 L 32.9,35.0 Z");
        dc.DrawGeometry(Linear("#4E5968", "#121824", 0, 0, 1, 1), Pen("#070A10", 1.7), blade);
        dc.DrawGeometry(Linear("#243040", "#0B1018", 0, 0, 1, 1), null, Geometry.Parse("M 8.2,6.7 L 31.6,22.2 L 29.6,27.2 L 10.8,10.1 Z"));
        dc.DrawGeometry(Brush("#FF123A", 0.82), Pen("#FF6A7F", 0.55), Geometry.Parse("M 13.6,10.8 L 35.0,23.0 L 33.0,25.0 L 16.1,14.3 Z"));
        dc.DrawLine(Pen("#FF123A", 1.25), new Point(17.0, 13.0), new Point(30.5, 21.4));
        dc.DrawLine(Pen("#FF123A", 0.95), new Point(23.0, 18.8), new Point(32.0, 24.4));
        dc.DrawLine(Pen("#FF6A7F", 0.75, 0.9), new Point(10.7, 8.0), new Point(25.4, 16.8));

        var guard = Geometry.Parse("M 26.4,32.3 L 32.8,25.5 L 39.8,31.9 L 33.2,39.1 Z");
        dc.DrawGeometry(Linear("#3A1B24", "#0D080B", 0, 0, 1, 1), Pen("#070305", 1.3), guard);
        dc.DrawLine(Pen("#FF123A", 1.7), new Point(30.0, 31.4), new Point(34.3, 27.4));
        dc.DrawEllipse(Brush("#FF123A", 0.3), null, new Point(32.4, 32.5), 4.6, 4.6);
        dc.DrawEllipse(Linear("#FF4D66", "#B20621", 0, 0, 1, 1), Pen("#31040A", 0.8), new Point(32.4, 32.5), 2.5, 2.5);

        dc.DrawLine(Pen("#1C0E13", 5.7), new Point(34.8, 37.4), new Point(45.3, 45.1));
        dc.DrawLine(Pen("#3F1822", 2.0), new Point(35.5, 37.7), new Point(44.5, 44.2));
        dc.DrawLine(Pen("#FF123A", 1.0), new Point(39.0, 40.0), new Point(42.0, 42.2));
    }

    private static void DrawOmnitrix(DrawingContext dc)
    {
        dc.DrawEllipse(Brush("#000000", 0.2), null, new Point(25, 25), 20.0, 20.0);
        dc.DrawEllipse(Brush("#4C4C4E"), Pen("#050505", 1.9), new Point(24, 24), 19.2, 19.2);
        dc.DrawEllipse(Brush("#111211"), Pen("#090A09", 0.9), new Point(24, 24), 16.0, 16.0);
        dc.DrawEllipse(Brush("#777779"), Pen("#C2C3C5", 0.7), new Point(24, 24), 13.0, 13.0);

        dc.DrawGeometry(Brush("#656567"), Pen("#080808", 0.7), Geometry.Parse("M 11.8,17.0 C 10.6,21.0 10.6,27.0 11.8,31.0 L 20.0,25.0 L 20.0,23.0 Z"));
        dc.DrawGeometry(Brush("#656567"), Pen("#080808", 0.7), Geometry.Parse("M 36.2,17.0 C 37.4,21.0 37.4,27.0 36.2,31.0 L 28.0,25.0 L 28.0,23.0 Z"));

        dc.DrawGeometry(Brush("#B7F728"), Pen("#050605", 1.05), Geometry.Parse("M 17.4,10.6 L 30.6,10.6 L 24,23.0 Z"));
        dc.DrawGeometry(Brush("#B7F728"), Pen("#050605", 1.05), Geometry.Parse("M 17.4,37.4 L 30.6,37.4 L 24,25.0 Z"));
        dc.DrawLine(Pen("#050605", 4.0), new Point(17.8, 11.0), new Point(23.1, 22.5));
        dc.DrawLine(Pen("#050605", 4.0), new Point(30.2, 37.0), new Point(24.9, 25.5));
        dc.DrawLine(Pen("#050605", 1.55), new Point(17.8, 37.0), new Point(30.2, 11.0));
        dc.DrawLine(Pen("#E4FF83", 0.9, 0.82), new Point(19.4, 11.7), new Point(22.7, 20.6));

        dc.DrawEllipse(null, Pen("#0A0B0A", 2.5), new Point(24, 24), 19.2, 19.2);
        foreach (var p in new[] { new Point(24, 4.8), new Point(43.2, 24), new Point(24, 43.2), new Point(4.8, 24) })
        {
            dc.DrawEllipse(Brush("#151716"), Pen("#050505", 0.8), p, 4.35, 4.35);
            dc.DrawEllipse(Brush("#B7F728"), null, p, 3.05, 3.05);
            dc.DrawEllipse(Brush("#ECFFA2", 0.8), null, new Point(p.X - 0.8, p.Y - 0.8), 0.75, 0.75);
        }
    }

    private static void DrawTardis(DrawingContext dc)
    {
        dc.DrawRectangle(Brush("#000000", 0.18), null, new Rect(11, 7.2, 30, 39));
        dc.DrawRectangle(Linear("#3149A8", "#172768", 0, 0, 1, 1), Pen("#030610", 1.55), new Rect(8.5, 9.0, 31.0, 35.8));
        dc.DrawRectangle(Linear("#3A51B2", "#1E2F7C", 0, 0, 1, 1), Pen("#030610", 1.2), new Rect(7.2, 6.2, 33.6, 3.9));
        dc.DrawRectangle(Linear("#293F9C", "#182768", 0, 0, 1, 1), Pen("#030610", 1.1), new Rect(10.0, 3.8, 28.0, 3.0));
        dc.DrawRectangle(Brush("#F5F7FA"), Pen("#030610", 0.65), new Rect(20.2, 0.9, 7.6, 3.2));
        dc.DrawRectangle(Brush("#0A0A0A"), null, new Rect(10.9, 12.0, 26.2, 4.8));
        DrawFittedText(dc, "POLICE", new Rect(13.2, 12.55, 7.6, 3.1), "#FFFFFF", 3.2, FontWeights.Bold);
        DrawFittedText(dc, "PUBLIC", new Rect(21.0, 12.15, 6.3, 1.9), "#FFFFFF", 2.1, FontWeights.Bold);
        DrawFittedText(dc, "CALL", new Rect(21.8, 14.35, 4.7, 1.8), "#FFFFFF", 2.0, FontWeights.Bold);
        DrawFittedText(dc, "BOX", new Rect(28.5, 12.55, 6.4, 3.1), "#FFFFFF", 3.2, FontWeights.Bold);
        dc.DrawLine(Pen("#566DD0", 1.0), new Point(12.3, 9.5), new Point(12.3, 43.6));
        dc.DrawLine(Pen("#0A1447", 1.1), new Point(36.1, 9.5), new Point(36.1, 43.6));
        dc.DrawLine(Pen("#030610", 1.2), new Point(24, 17.2), new Point(24, 44.5));
        foreach (var rect in new[] { new Rect(14.2, 20.2, 7.2, 6.8), new Rect(26.6, 20.2, 7.2, 6.8) })
        {
            dc.DrawRectangle(Linear("#EFF4F8", "#94A4B1", 0, 0, 1, 1), Pen("#05070B", 0.9), rect);
            dc.DrawLine(Pen("#05070B", 0.7), new Point(rect.Left + rect.Width / 2, rect.Top), new Point(rect.Left + rect.Width / 2, rect.Bottom));
            dc.DrawLine(Pen("#05070B", 0.7), new Point(rect.Left, rect.Top + rect.Height / 2), new Point(rect.Right, rect.Top + rect.Height / 2));
            dc.DrawLine(Pen("#FFFFFF", 0.5, 0.75), new Point(rect.Left + 0.8, rect.Top + 0.7), new Point(rect.Right - 0.8, rect.Top + 0.7));
        }
        foreach (var rect in new[] { new Rect(14.3, 30.0, 7.4, 10.7), new Rect(26.3, 30.0, 7.4, 10.7) })
        {
            dc.DrawRectangle(null, Pen("#05070B", 1.15), rect);
            dc.DrawRectangle(Brush("#213487", 0.55), null, new Rect(rect.Left + 1.0, rect.Top + 1.0, rect.Width - 2.0, rect.Height - 2.0));
        }
        dc.DrawRectangle(Linear("#344BAD", "#172467", 0, 0, 1, 1), Pen("#030610", 1.1), new Rect(6.4, 43.5, 35.2, 3.6));
    }

    private static void DrawAutobotCrest(DrawingContext dc)
    {
        var outer = Geometry.Parse("M 24,3.0 L 40.4,10.2 L 37.4,29.0 L 32.0,38.4 L 24,45.1 L 16.0,38.4 L 10.6,29.0 L 7.6,10.2 Z");
        dc.DrawGeometry(Brush("#000000", 0.20), null, Geometry.Parse("M 9.5,12.4 L 24,5.0 L 38.5,12.4 L 35.8,30.4 L 24,46.7 L 12.2,30.4 Z"));
        dc.DrawGeometry(Linear("#FCFCFD", "#7F8998", 0, 0, 1, 1), Pen("#111721", 1.65), outer);
        dc.DrawGeometry(Linear("#FF5C5C", "#8E1119", 0, 0, 1, 1), Pen("#180B10", 0.8), Geometry.Parse("M 12.1,12.6 L 22.7,7.4 L 21.2,24.0 L 15.2,22.2 Z M 35.9,12.6 L 25.3,7.4 L 26.8,24.0 L 32.8,22.2 Z M 16.0,25.3 L 22.6,25.3 L 21.4,34.1 L 17.5,36.9 Z M 32.0,25.3 L 25.4,25.3 L 26.6,34.1 L 30.5,36.9 Z M 18.8,37.6 L 29.2,37.6 L 24.0,43.0 Z"));
        dc.DrawGeometry(Brush("#10151C"), null, Geometry.Parse("M 14.5,16.0 L 20.4,17.4 L 19.1,21.3 L 14.1,20.2 Z M 33.5,16.0 L 27.6,17.4 L 28.9,21.3 L 33.9,20.2 Z M 21.0,24.8 L 27.0,24.8 L 24.0,28.0 Z"));
        dc.DrawLine(Pen("#FFF3F3", 0.75, 0.62), new Point(13.7, 12.7), new Point(22.0, 8.8));
        dc.DrawLine(Pen("#5E0C11", 1.0), new Point(24, 7.8), new Point(24, 41.8));
    }

    private static void DrawDecepticonCrest(DrawingContext dc)
    {
        var crest = Geometry.Parse("M 24,2.8 L 41.1,15.0 L 36.2,38.2 L 24,45.7 L 11.8,38.2 L 6.9,15.0 Z");
        dc.DrawGeometry(Brush("#000000", 0.22), null, Geometry.Parse("M 9.4,17.0 L 24,5.2 L 38.6,17.0 L 34.1,39.5 L 24,47 L 13.9,39.5 Z"));
        dc.DrawGeometry(Linear("#D8BCFF", "#5D368F", 0, 0, 1, 1), Pen("#140C1F", 1.7), crest);
        dc.DrawGeometry(Linear("#332047", "#130B1C", 0, 0, 1, 1), Pen("#060309", 0.8), Geometry.Parse("M 12.0,16.7 L 22.0,10.5 L 20.1,27.0 L 11.4,25.0 Z M 36.0,16.7 L 26.0,10.5 L 27.9,27.0 L 36.6,25.0 Z M 15.1,28.1 L 22.7,27.0 L 21.6,36.9 L 17.2,39.0 Z M 32.9,28.1 L 25.3,27.0 L 26.4,36.9 L 30.8,39.0 Z M 18.3,39.5 L 29.7,39.5 L 24,44.3 Z"));
        dc.DrawGeometry(Brush("#EADFFF", 0.95), null, Geometry.Parse("M 14.1,18.3 L 20.0,19.3 L 19.0,22.4 L 13.2,21.6 Z M 33.9,18.3 L 28.0,19.3 L 29.0,22.4 L 34.8,21.6 Z"));
        dc.DrawGeometry(Brush("#B187F5", 0.65), null, Geometry.Parse("M 21.8,11.0 L 24,7.0 L 26.2,11.0 L 24,26.5 Z"));
        dc.DrawLine(Pen("#170E23", 1.0), new Point(24, 7.8), new Point(24, 42.0));
        dc.DrawLine(Pen("#F0E4FF", 0.75, 0.58), new Point(12.0, 16.0), new Point(21.3, 10.7));
    }

    private static void DrawSheraSword(DrawingContext dc)
    {
        var blade = Geometry.Parse("M 24,0.8 L 30.3,9.2 L 30.0,24.0 L 24,36.3 L 18.0,24.0 L 17.7,9.2 Z");
        dc.DrawGeometry(Brush("#9CF4FF", 0.2), null, Geometry.Parse("M 24,0 L 32.0,8.4 L 31.5,24.2 L 24,39.0 L 16.5,24.2 L 16.0,8.4 Z"));
        dc.DrawGeometry(Linear("#FFFFFF", "#85EDFF", 0, 0, 1, 1), Pen("#237383", 1.35), blade);
        dc.DrawGeometry(Brush("#D7FAFF", 0.74), null, Geometry.Parse("M 24,3.0 L 28.1,10.2 L 27.8,23.2 L 24.0,33.0 Z"));
        dc.DrawGeometry(Brush("#8EDFEF", 0.55), null, Geometry.Parse("M 24,3.0 L 19.9,10.2 L 20.2,23.2 L 24.0,33.0 Z"));
        dc.DrawLine(Pen("#FFFFFF", 1.0, 0.95), new Point(24, 3.1), new Point(24, 35.0));
        dc.DrawLine(Pen("#3DAABD", 0.75), new Point(19.6, 22.5), new Point(28.4, 22.5));
        dc.DrawLine(Pen("#BDF8FF", 0.85), new Point(21.7, 10.4), new Point(26.2, 5.9));

        var guard = Geometry.Parse("M 8.8,29.4 C 13.6,20.6 18.9,22.5 24,27.7 C 29.1,22.5 34.4,20.6 39.2,29.4 C 32.4,33.3 28.4,34.2 24,32.0 C 19.6,34.2 15.6,33.3 8.8,29.4 Z");
        dc.DrawGeometry(Linear("#FFE89A", "#BD7D18", 0, 0, 1, 1), Pen("#68450C", 1.35), guard);
        dc.DrawGeometry(Brush("#FFF7C8", 0.56), null, Geometry.Parse("M 12.5,28.2 C 17.4,24.2 20.7,26.4 24,29.1 C 27.3,26.4 30.6,24.2 35.5,28.2 C 30.2,30.6 27.4,31.3 24,30.1 C 20.6,31.3 17.8,30.6 12.5,28.2 Z"));
        dc.DrawEllipse(Linear("#B6FCFF", "#19AFC7", 0, 0, 1, 1), Pen("#6F4D11", 0.95), new Point(24, 28.2), 2.7, 4.2);
        dc.DrawEllipse(Brush("#FFFFFF", 0.85), null, new Point(23.2, 26.8), 0.8, 1.0);
        dc.DrawLine(Pen("#D09A29", 4.2), new Point(24, 31.2), new Point(24, 44.5));
        dc.DrawLine(Pen("#FFE8A0", 1.0, 0.8), new Point(23.1, 32.4), new Point(23.1, 43.4));
        dc.DrawEllipse(Linear("#F7D66C", "#B66C12", 0, 0, 1, 1), Pen("#68450C", 1.1), new Point(24, 45.0), 3.7, 2.6);
    }

    private static void DrawAncientStaff(DrawingContext dc)
    {
        dc.DrawLine(Pen("#140C08", 5.2), new Point(11.4, 45.0), new Point(31.0, 8.0));
        dc.DrawLine(Pen("#4A2B1D", 4.2), new Point(11.4, 45.0), new Point(31.0, 8.0));
        dc.DrawLine(Pen("#9A6948", 1.0, 0.82), new Point(13.3, 40.7), new Point(29.7, 9.8));
        dc.DrawLine(Pen("#1D100B", 0.9, 0.75), new Point(18.4, 31.8), new Point(21.1, 29.2));
        dc.DrawLine(Pen("#1D100B", 0.9, 0.75), new Point(23.1, 22.8), new Point(25.8, 20.2));
        var head = Geometry.Parse("M 25.3,4.2 C 28.2,-0.4 35.2,0.8 38.1,5.2 C 37.5,11.8 31.3,14.0 25.8,10.4 C 23.7,8.3 23.6,6.2 25.3,4.2 Z");
        dc.DrawGeometry(Linear("#BD8A62", "#5B3424", 0, 0, 1, 1), Pen("#1D100B", 1.5), head);
        dc.DrawGeometry(Brush("#6E4430", 0.9), null, Geometry.Parse("M 27.0,4.8 L 31.5,1.9 L 36.2,5.8 L 34.2,10.1 L 28.1,9.7 Z"));
        dc.DrawLine(Pen("#24130C", 0.95), new Point(28.0, 7.0), new Point(34.0, 4.5));
        dc.DrawLine(Pen("#24130C", 0.95), new Point(29.2, 10.0), new Point(35.0, 6.2));
        dc.DrawLine(Pen("#D09B72", 0.9, 0.65), new Point(27.0, 4.2), new Point(33.0, 2.6));
        dc.DrawEllipse(Brush("#2A1912"), null, new Point(30.2, 6.0), 1.25, 1.55);
        dc.DrawEllipse(Brush("#2A1912"), null, new Point(34.1, 5.6), 1.1, 1.4);
    }

    private static void DrawStarfleetDelta(DrawingContext dc)
    {
        dc.DrawGeometry(Brush("#000000", 0.16), null, Geometry.Parse("M 25.4,4.8 L 39.8,45.0 L 24.4,36.6 L 9.2,45.4 Z"));
        var delta = Geometry.Parse("M 24,2.6 L 38.7,44.1 L 24,35.6 L 9.3,44.1 Z");
        dc.DrawGeometry(Linear("#FFF0A8", "#A86F16", 0, 0, 1, 1), Pen("#121821", 1.65), delta);
        dc.DrawGeometry(Brush("#FFFFFF", 0.35), null, Geometry.Parse("M 24,6.0 L 28.8,31.8 L 24,29.4 Z"));
        dc.DrawGeometry(Linear("#19202A", "#06090D", 0, 0, 1, 1), Pen("#5E3E0B", 0.7), Geometry.Parse("M 24,14.2 L 29.4,34.0 L 24,30.7 L 18.6,34.0 Z"));
        dc.DrawLine(Pen("#FFEAA1", 0.9, 0.75), new Point(14.0, 39.1), new Point(23.3, 5.2));
        dc.DrawEllipse(Brush("#5FE2EF", 0.28), null, new Point(24, 39.0), 4.0, 4.0);
        dc.DrawEllipse(Brush("#5FE2EF"), Pen("#102A32", 0.75), new Point(24, 39.0), 2.1, 2.1);
    }

    private static void DrawStarship(DrawingContext dc)
    {
        dc.DrawEllipse(Brush("#79F2FF", 0.08), null, new Point(15.7, 13.5), 14.8, 7.8);
        var saucer = Geometry.Parse("M 2.8,13.2 C 5.2,4.8 24.8,3.5 33.0,10.6 C 34.4,16.5 27.0,20.8 15.5,21.2 C 7.1,21.5 1.5,18.2 2.8,13.2 Z");
        dc.DrawGeometry(Linear("#FAFCFF", "#8C98A8", 0, 0, 1, 1), Pen("#1B2633", 1.15), saucer);
        dc.DrawEllipse(Brush("#E8EFF6", 0.92), Pen("#9AA7B4", 0.75), new Point(15.8, 12.6), 9.9, 3.9);
        dc.DrawEllipse(Brush("#D8E1EA"), Pen("#7A8796", 0.65), new Point(15.9, 11.9), 5.4, 2.0);
        dc.DrawEllipse(Brush("#F8FBFF"), null, new Point(15.9, 11.2), 2.8, 0.8);
        dc.DrawEllipse(Brush("#5FE2EF"), null, new Point(8.8, 12.5), 0.9, 0.9);
        DrawFittedText(dc, "NCC-1701", new Rect(10.8, 15.2, 9.2, 1.8), "#263344", 1.55, FontWeights.Bold);
        dc.DrawLine(Pen("#FFFFFF", 0.6, 0.68), new Point(5.8, 12.3), new Point(20.5, 7.3));

        dc.DrawGeometry(Linear("#EAF0F6", "#8794A4", 0, 0, 1, 1), Pen("#243140", 0.75), Geometry.Parse("M 22.9,17.8 L 27.3,20.2 L 25.1,30.5 L 20.7,28.0 Z"));
        dc.DrawGeometry(Linear("#F3F7FB", "#7B8798", 0, 0, 1, 1), Pen("#243140", 0.85), Geometry.Parse("M 25.0,29.2 C 27.1,25.2 32.8,25.0 36.9,28.5 C 38.2,32.4 34.7,35.8 30.0,36.2 C 27.6,34.7 25.8,32.5 25.0,29.2 Z"));
        dc.DrawEllipse(Brush("#41CFE4"), Pen("#1F4751", 0.48), new Point(35.4, 29.9), 1.7, 1.05);

        dc.DrawLine(Pen("#CBD5DF", 1.85), new Point(28.0, 28.8), new Point(35.2, 23.5));
        dc.DrawLine(Pen("#243140", 0.58), new Point(28.0, 28.8), new Point(35.2, 23.5));
        dc.DrawLine(Pen("#CBD5DF", 1.85), new Point(27.5, 33.1), new Point(35.4, 38.3));
        dc.DrawLine(Pen("#243140", 0.58), new Point(27.5, 33.1), new Point(35.4, 38.3));

        DrawNacelle(dc, new Point(36.7, 22.6), -17);
        DrawNacelle(dc, new Point(36.7, 39.0), 17);
    }

    private static void DrawNacelle(DrawingContext dc, Point center, double angle)
    {
        dc.PushTransform(new RotateTransform(angle, center.X, center.Y));
        var body = new Rect(center.X - 9.0, center.Y - 2.55, 18.0, 5.1);
        dc.DrawRoundedRectangle(Brush("#000000", 0.14), null, new Rect(body.X + 0.7, body.Y + 0.7, body.Width, body.Height), 2.7, 2.7);
        dc.DrawRoundedRectangle(Linear("#F4F7FA", "#7B8798", 0, 0, 1, 1), Pen("#23303F", 0.85), body, 2.55, 2.55);
        dc.DrawRectangle(Linear("#6B83FF", "#334BE0", 0, 0, 1, 1), null, new Rect(center.X - 7.0, center.Y - 2.05, 4.5, 4.1));
        dc.DrawRectangle(Brush("#D7E0EA", 0.78), null, new Rect(center.X - 2.5, center.Y - 2.0, 8.8, 4.0));
        dc.DrawLine(Pen("#FFFFFF", 0.52, 0.85), new Point(center.X - 4.0, center.Y - 1.55), new Point(center.X + 6.2, center.Y - 1.55));
        dc.DrawLine(Pen("#657284", 0.45, 0.65), new Point(center.X - 1.0, center.Y + 1.75), new Point(center.X + 5.2, center.Y + 1.75));
        dc.DrawEllipse(Brush("#F25AAE", 0.28), null, new Point(center.X + 8.3, center.Y), 3.2, 3.2);
        dc.DrawEllipse(Linear("#FF79B8", "#D43B86", 0, 0, 1, 1), Pen("#7A1F4B", 0.65), new Point(center.X + 8.0, center.Y), 2.25, 2.25);
        dc.DrawEllipse(Brush("#FFFFFF", 0.55), null, new Point(center.X + 7.3, center.Y - 0.7), 0.55, 0.55);
        dc.Pop();
    }

    private static void DrawDarkOneDagger(DrawingContext dc)
    {
        dc.DrawGeometry(Brush("#000000", 0.16), null, Geometry.Parse("M 20.2,2.0 L 27.9,2.0 L 28.9,15.0 C 34.3,15.3 37.2,17.2 37.0,19.2 C 36.7,22.0 31.0,22.9 28.5,22.1 C 30.1,29.1 27.8,39.9 23.8,47.5 C 19.9,39.8 17.7,29.4 19.5,22.1 C 16.9,22.9 11.3,22.0 11.0,19.2 C 10.8,17.2 13.7,15.3 19.1,15.0 Z"));

        dc.DrawLine(Pen("#050506", 6.2), new Point(24, 4.0), new Point(24, 16.3));
        dc.DrawLine(Pen("#1A1A1E", 4.4), new Point(24, 4.2), new Point(24, 16.0));
        for (var y = 5.8; y < 15.6; y += 2.0)
        {
            dc.DrawLine(Pen("#44444A", 0.9, 0.8), new Point(21.8, y), new Point(26.1, y + 1.3));
        }

        dc.DrawEllipse(Linear("#45474D", "#111114", 0, 0, 1, 1), Pen("#050506", 1.1), new Point(24, 3.4), 4.4, 2.7);
        dc.DrawEllipse(Linear("#E44558", "#8E1020", 0, 0, 1, 1), Pen("#050506", 0.85), new Point(24, 2.6), 3.0, 2.0);
        dc.DrawEllipse(Brush("#FFA0AB", 0.75), null, new Point(23.2, 1.9), 0.75, 0.45);

        dc.DrawEllipse(Linear("#F3F5F7", "#8C929A", 0, 0, 1, 1), Pen("#080809", 1.2), new Point(24, 18.2), 11.0, 3.0);
        dc.DrawEllipse(Brush("#070708"), null, new Point(24, 17.6), 3.5, 1.35);
        dc.DrawGeometry(Linear("#D8DCE2", "#5E636B", 0, 0, 1, 1), Pen("#080809", 0.9), Geometry.Parse("M 20.2,19.0 C 20.8,15.6 27.2,15.6 27.8,19.0 C 27.0,21.8 21.0,21.8 20.2,19.0 Z"));
        dc.DrawEllipse(Brush("#050506"), null, new Point(22.3, 18.6), 0.9, 0.8);
        dc.DrawEllipse(Brush("#050506"), null, new Point(25.7, 18.6), 0.9, 0.8);
        dc.DrawGeometry(Brush("#050506"), null, Geometry.Parse("M 23.3,20.0 L 24.7,20.0 L 24.0,21.0 Z"));
        dc.DrawGeometry(Brush("#050506"), null, Geometry.Parse("M 17.2,19.8 L 20.3,22.6 L 16.0,24.0 Z M 30.8,19.8 L 27.7,22.6 L 32.0,24.0 Z"));

        var blade = Geometry.Parse("M 20.3,21.3 C 18.5,26.8 20.0,31.8 18.9,36.5 C 18.2,39.5 21.4,43.0 24,47.0 C 26.6,43.0 29.8,39.5 29.1,36.5 C 28.0,31.8 29.5,26.8 27.7,21.3 C 25.5,22.4 22.5,22.4 20.3,21.3 Z");
        dc.DrawGeometry(Linear("#FFFFFF", "#838A94", 0, 0, 1, 1), Pen("#101012", 1.45), blade);
        dc.DrawGeometry(Brush("#E4E7EB", 0.9), null, Geometry.Parse("M 24,22.1 C 22.7,28.4 22.6,37.4 24,44.7 C 25.4,37.4 25.3,28.4 24,22.1 Z"));
        dc.DrawGeometry(Brush("#B8BEC6", 0.68), null, Geometry.Parse("M 26.6,22.4 C 27.5,28.1 26.9,34.0 27.9,38.1 C 27.3,40.0 26.1,42.0 24.7,44.2 C 25.4,36.7 25.2,28.5 24.5,22.1 Z"));
        dc.DrawLine(Pen("#202124", 0.78), new Point(24, 21.8), new Point(24, 45.0));

        dc.PushTransform(new RotateTransform(70, 23.0, 31.0));
        DrawFittedText(dc, "RUMPLE", new Rect(20.0, 29.6, 9.0, 2.0), "#1A1A1D", 2.0, FontWeights.Bold);
        dc.Pop();
        dc.DrawLine(Pen("#171719", 0.65), new Point(20.9, 27.1), new Point(26.0, 30.2));
        dc.DrawLine(Pen("#171719", 0.65), new Point(21.2, 33.2), new Point(26.6, 36.2));
        dc.DrawGeometry(null, Pen("#171719", 0.62), Geometry.Parse("M 20.8,38.5 C 22.6,37.5 25.4,39.2 27.2,38.1"));
    }

    private static void DrawHeart(DrawingContext dc, Point center, double scale, Brush fill, Pen? pen)
    {
        var geometry = Geometry.Parse("M 0,-8 C -8,-17 -23,-6 -18,8 C -15,17 -5,23 0,28 C 5,23 15,17 18,8 C 23,-6 8,-17 0,-8 Z").Clone();
        geometry.Transform = new TransformGroup
        {
            Children =
            {
                new ScaleTransform(scale, scale),
                new TranslateTransform(center.X, center.Y)
            }
        };
        dc.DrawGeometry(fill, pen, geometry);
    }

    private static void DrawStar(DrawingContext dc, Point center, double outer, double inner, Brush fill, Pen? pen)
    {
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            for (var i = 0; i < 10; i++)
            {
                var angle = -Math.PI / 2 + i * Math.PI / 5;
                var radius = i % 2 == 0 ? outer : inner;
                var point = new Point(center.X + Math.Cos(angle) * radius, center.Y + Math.Sin(angle) * radius);
                if (i == 0)
                {
                    ctx.BeginFigure(point, true, true);
                }
                else
                {
                    ctx.LineTo(point, true, false);
                }
            }
        }

        geometry.Freeze();
        dc.DrawGeometry(fill, pen, geometry);
    }

    private static void DrawTinyText(DrawingContext dc, string text, double size, Point origin, string color)
    {
        var formatted = new FormattedText(
            text,
            CultureInfo.InvariantCulture,
            System.Windows.FlowDirection.LeftToRight,
            new Typeface(new System.Windows.Media.FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
            size,
            Brush(color),
            1.0);
        dc.DrawText(formatted, origin);
    }

    private static void DrawFittedText(DrawingContext dc, string text, Rect bounds, string color, double maxSize, FontWeight weight)
    {
        var formatted = new FormattedText(
            text,
            CultureInfo.InvariantCulture,
            System.Windows.FlowDirection.LeftToRight,
            new Typeface(new System.Windows.Media.FontFamily("Segoe UI"), FontStyles.Normal, weight, FontStretches.Normal),
            maxSize,
            Brush(color),
            1.0);
        var width = Math.Max(0.01, formatted.WidthIncludingTrailingWhitespace);
        var height = Math.Max(0.01, formatted.Height);
        var scale = Math.Min(bounds.Width / width, bounds.Height / height);

        dc.PushTransform(new TranslateTransform(bounds.Left + (bounds.Width - width * scale) / 2, bounds.Top + (bounds.Height - height * scale) / 2));
        dc.PushTransform(new ScaleTransform(scale, scale));
        dc.DrawText(formatted, new Point(0, 0));
        dc.Pop();
        dc.Pop();
    }

    private static LinearGradientBrush Linear(string startColor, string endColor, double startX, double startY, double endX, double endY)
    {
        var brush = new LinearGradientBrush
        {
            StartPoint = new Point(startX, startY),
            EndPoint = new Point(endX, endY)
        };
        brush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString(startColor), 0));
        brush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString(endColor), 1));
        brush.Freeze();
        return brush;
    }

    private static SolidColorBrush Glow(string color, double opacity) => Brush(color, opacity);

    private static Pen Pen(string color, double thickness) => new(Brush(color), thickness)
    {
        StartLineCap = PenLineCap.Round,
        EndLineCap = PenLineCap.Round,
        LineJoin = PenLineJoin.Round
    };

    private static Pen Pen(string color, double thickness, double opacity) => new(Brush(color, opacity), thickness)
    {
        StartLineCap = PenLineCap.Round,
        EndLineCap = PenLineCap.Round,
        LineJoin = PenLineJoin.Round
    };

    private static SolidColorBrush Brush(string color)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        brush.Freeze();
        return brush;
    }

    private static SolidColorBrush Brush(string color, double opacity)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color))
        {
            Opacity = opacity
        };
        brush.Freeze();
        return brush;
    }

    private static string Slug(string value)
    {
        var chars = value.ToLowerInvariant()
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '-')
            .ToArray();
        var slug = string.Join("-", new string(chars).Split('-', StringSplitOptions.RemoveEmptyEntries));
        return string.IsNullOrWhiteSpace(slug) ? $"custom-{DateTime.Now:yyyyMMddHHmmss}" : slug;
    }

    private static ClickEffect EffectFor(string name) => name switch
    {
        "Hearts" => new ClickEffect { Type = "Hearts", PrimaryColor = "#F25AAE", SecondaryColor = "#FFD6EC", ParticleCount = 8, Radius = 26, DurationMs = 560 },
        "Rings" => new ClickEffect { Type = "Rings", PrimaryColor = "#55F7FF", SecondaryColor = "#FFFFFF", ParticleCount = 5, Radius = 30, DurationMs = 520 },
        "Fireflies" => new ClickEffect { Type = "Fireflies", PrimaryColor = "#C9FF7A", SecondaryColor = "#FFEA7A", ParticleCount = 10, Radius = 32, DurationMs = 620 },
        _ => new ClickEffect { Type = "Sparkles", PrimaryColor = "#FFD957", SecondaryColor = "#F25AAE", ParticleCount = 12, Radius = 32, DurationMs = 560 }
    };

    private static ClickEffect EffectWithAccent(ClickEffect effect, string accentColor)
    {
        return new ClickEffect
        {
            Type = effect.Type,
            PrimaryColor = accentColor,
            SecondaryColor = effect.SecondaryColor,
            ParticleCount = effect.ParticleCount,
            Radius = effect.Radius,
            DurationMs = effect.DurationMs
        };
    }

    private static List<BuiltInThemeSpec> BuiltInSpecs() =>
    [
        new("star-wand", "Star Wand", "A cursor-sized recreation of Star Butterfly's wand with tiny hearts and sparkles.", "Magic", 24, 14, new ClickEffect { Type = "Star Wand", PrimaryColor = "#FFD957", SecondaryColor = "#F25AAE", ParticleCount = 12, Radius = 32, DurationMs = 580 }),
        new("neon-ring", "Neon Ring", "Black pointer with cyan and pink glow accents.", "Neon", 4, 3, new ClickEffect { Type = "Rings", PrimaryColor = "#55F7FF", SecondaryColor = "#FF4FD8", ParticleCount = 6, Radius = 30, DurationMs = 520 }),
        new("crystal-arrow", "Crystal Arrow", "Faceted pale-blue pointer with glassy edges.", "Gem", 5, 3, new ClickEffect { Type = "Sparkles", PrimaryColor = "#8BE7FF", SecondaryColor = "#FFFFFF", ParticleCount = 9, Radius = 30, DurationMs = 540 }),
        new("pixel-sword", "Pixel Sword", "A compact pixel-art blade for retro desktops.", "Pixel", 9, 6, new ClickEffect { Type = "Sparkles", PrimaryColor = "#E8ECFF", SecondaryColor = "#F6C84D", ParticleCount = 8, Radius = 28, DurationMs = 500 }),
        new("moonbeam", "Moonbeam", "Dark pointer with a tiny crescent moon accent.", "Celestial", 4, 3, new ClickEffect { Type = "Rings", PrimaryColor = "#FFE78A", SecondaryColor = "#DDE8FF", ParticleCount = 6, Radius = 28, DurationMs = 560 }),
        new("bubble-pop", "Bubble Pop", "Soft aqua pointer with bubble highlights.", "Playful", 4, 3, new ClickEffect { Type = "Rings", PrimaryColor = "#9AF2FF", SecondaryColor = "#F69BD8", ParticleCount = 7, Radius = 30, DurationMs = 540 }),
        new("heart-charm", "Heart Charm", "Light pointer with a pink heart charm.", "Magic", 4, 3, new ClickEffect { Type = "Hearts", PrimaryColor = "#F25AAE", SecondaryColor = "#FFD6EC", ParticleCount = 8, Radius = 26, DurationMs = 560 }),
        new("glass-shard", "Glass Shard", "Sharp translucent pointer with slate lines.", "Minimal", 5, 3, new ClickEffect { Type = "Sparkles", PrimaryColor = "#EAF6FF", SecondaryColor = "#94A3B8", ParticleCount = 7, Radius = 28, DurationMs = 480 }),
        new("firefly-trail", "Firefly Trail", "Deep green pointer with tiny light motes.", "Nature", 4, 3, new ClickEffect { Type = "Fireflies", PrimaryColor = "#C9FF7A", SecondaryColor = "#FFEA7A", ParticleCount = 10, Radius = 34, DurationMs = 620 }),
        new("sketch-pen", "Sketch Pen", "Hand-drawn pen nib cursor with a red mark.", "Creative", 7, 4, new ClickEffect { Type = "Sparkles", PrimaryColor = "#202124", SecondaryColor = "#F0624D", ParticleCount = 6, Radius = 24, DurationMs = 480 }),
        new("candy-bolt", "Candy Bolt", "Bright lightning cursor with candy stripes.", "Playful", 21, 3, new ClickEffect { Type = "Sparkles", PrimaryColor = "#FFE46A", SecondaryColor = "#F25AAE", ParticleCount = 9, Radius = 30, DurationMs = 530 }),
        new("minimal-crosshair", "Minimal Crosshair", "Precision ring cursor for focused work.", "Utility", 24, 24, new ClickEffect { Type = "Rings", PrimaryColor = "#202124", SecondaryColor = "#55F7FF", ParticleCount = 4, Radius = 24, DurationMs = 450 }),
        new("lightsaber", "Lightsaber", "A glowing saber pointer with configurable blade color.", "Sci-Fi", 5, 5, new ClickEffect { Type = "Saber Sparks", PrimaryColor = "#55F7FF", SecondaryColor = "#FFFFFF", ParticleCount = 10, Radius = 34, DurationMs = 430 }, "#55F7FF"),
        new("hero-sword", "Sword of Daylight", "A broad silver daylight sword cursor with cyan jewel details.", "Fantasy", 7, 5, new ClickEffect { Type = "Blade Glints", PrimaryColor = "#DCE7F2", SecondaryColor = "#5FE2EF", ParticleCount = 8, Radius = 28, DurationMs = 470 }),
        new("eclipse-sword", "Sword of Eclipse", "A dark red energy sword cursor with subtle cursed glints.", "Fantasy", 7, 5, new ClickEffect { Type = "Cursed Sparks", PrimaryColor = "#FF123A", SecondaryColor = "#7E0A18", ParticleCount = 9, Radius = 28, DurationMs = 520 }),
        new("omnitrix", "Omnitrix", "A green alien-tech emblem cursor with pulse rings.", "Sci-Fi", 24, 24, new ClickEffect { Type = "Rings", PrimaryColor = "#B8FF2F", SecondaryColor = "#2DFF7A", ParticleCount = 6, Radius = 30, DurationMs = 520 }),
        new("tardis", "TARDIS", "A compact blue police box cursor with timey glow pulses.", "Sci-Fi", 10, 7, new ClickEffect { Type = "Rings", PrimaryColor = "#4A67FF", SecondaryColor = "#FFFFFF", ParticleCount = 6, Radius = 32, DurationMs = 580 }),
        new("autobot-crest", "Autobot Crest", "A red-and-silver robot faction crest inspired cursor.", "Robots", 24, 4, new ClickEffect { Type = "Energon Sparks", PrimaryColor = "#F04B4B", SecondaryColor = "#D8DEE8", ParticleCount = 9, Radius = 30, DurationMs = 500 }),
        new("decepticon-crest", "Decepticon Crest", "A purple angular robot faction crest inspired cursor.", "Robots", 24, 4, new ClickEffect { Type = "Energon Sparks", PrimaryColor = "#A673FF", SecondaryColor = "#3A234F", ParticleCount = 9, Radius = 30, DurationMs = 500 }),
        new("shera-sword", "Sword of Protection", "A crystal-and-gold heroic sword cursor.", "Fantasy", 24, 2, new ClickEffect { Type = "Sparkles", PrimaryColor = "#BDEFFF", SecondaryColor = "#F2C453", ParticleCount = 11, Radius = 34, DurationMs = 620 }),
        new("ancient-staff", "Ancient Staff", "A carved wooden staff cursor with warm magic motes.", "Fantasy", 31, 6, new ClickEffect { Type = "Fireflies", PrimaryColor = "#C58C58", SecondaryColor = "#F2C453", ParticleCount = 8, Radius = 30, DurationMs = 620 }),
        new("starfleet-delta", "Starfleet Delta", "A clean gold delta insignia cursor with scanner rings.", "Sci-Fi", 24, 3, new ClickEffect { Type = "Rings", PrimaryColor = "#F2D36B", SecondaryColor = "#5FE2EF", ParticleCount = 5, Radius = 26, DurationMs = 500 }),
        new("starship", "Starship", "A tiny silver starship cursor with engine-pink sparks.", "Sci-Fi", 13, 13, new ClickEffect { Type = "Warp Sparks", PrimaryColor = "#5FE2EF", SecondaryColor = "#F25AAE", ParticleCount = 10, Radius = 36, DurationMs = 520 }),
        new("dark-one-dagger", "Dark One Dagger", "A dark ornate dagger cursor with red cursed glints.", "Fantasy", 24, 5, new ClickEffect { Type = "Cursed Sparks", PrimaryColor = "#A6192E", SecondaryColor = "#D9DEE5", ParticleCount = 7, Radius = 24, DurationMs = 560 })
    ];

    private sealed record BuiltInThemeSpec(
        string Id,
        string Name,
        string Description,
        string Category,
        ushort HotspotX,
        ushort HotspotY,
        ClickEffect Effect,
        string AccentColor = "#55F7FF");
}
