using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows;
using CursorMagic.Models;
using WinForms = System.Windows.Forms;

namespace CursorMagic.Services;

public sealed class AgentRuntime : IDisposable
{
    private readonly Mutex _mutex;
    private readonly SettingsService _settingsService = new();
    private readonly ThemeService _themeService = new();
    private readonly WindowsCursorService _cursorService = new();
    private readonly ClickEffectOverlayService _overlayService = new();
    private readonly FileSystemWatcher _settingsWatcher;
    private readonly WinForms.NotifyIcon _notifyIcon;

    private AppSettings _settings = new();
    private bool _disposed;

    public AgentRuntime(out bool alreadyRunning)
    {
        _mutex = new Mutex(true, AgentLauncher.MutexName, out var createdNew);
        alreadyRunning = !createdNew;

        AppPaths.Ensure();
        _settingsWatcher = new FileSystemWatcher(AppPaths.Root, Path.GetFileName(AppPaths.SettingsPath))
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.CreationTime | NotifyFilters.Size,
            EnableRaisingEvents = createdNew
        };
        _settingsWatcher.Changed += (_, _) => ReloadSoon();
        _settingsWatcher.Created += (_, _) => ReloadSoon();

        _notifyIcon = new WinForms.NotifyIcon
        {
            Icon = System.Drawing.SystemIcons.Information,
            Text = "Cursor Magic effects",
            Visible = createdNew,
            ContextMenuStrip = new WinForms.ContextMenuStrip()
        };
        _notifyIcon.DoubleClick += (_, _) => OpenMainUi();
        _notifyIcon.ContextMenuStrip.Items.Add("Open Cursor Magic", null, (_, _) => OpenMainUi());
        _notifyIcon.ContextMenuStrip.Items.Add("Pause/resume effects", null, (_, _) => TogglePause());
        _notifyIcon.ContextMenuStrip.Items.Add("Restore Windows defaults", null, (_, _) => RestoreDefaults());
        _notifyIcon.ContextMenuStrip.Items.Add("Exit effects", null, (_, _) => System.Windows.Application.Current.Shutdown());
    }

    public void Start()
    {
        LoadState();
        _overlayService.Start();
        App.Log("Agent overlay started");
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _settingsWatcher.Dispose();
        _overlayService.Dispose();
        _notifyIcon.Dispose();
        _mutex.Dispose();
    }

    private void ReloadSoon()
    {
        System.Windows.Application.Current.Dispatcher.BeginInvoke(() =>
        {
            try
            {
                LoadState();
            }
            catch (Exception ex)
            {
                App.Log($"Agent reload failed: {ex}");
            }
        }, System.Windows.Threading.DispatcherPriority.Background);
    }

    private void LoadState()
    {
        _settings = _settingsService.Load();
        _overlayService.Settings = _settings;
        _overlayService.CurrentGlowCursorPath = "";

        var activeTheme = LoadActiveTheme();
        if (activeTheme is not null)
        {
            activeTheme = RebuildThemeForSettings(activeTheme);
            var runtimeSettings = ThemeAnimationService.RuntimeSettings(_settings, activeTheme.Id);
            _overlayService.Settings = runtimeSettings;
            _overlayService.CurrentEffect = EffectResolver.Resolve(activeTheme, runtimeSettings);
            _overlayService.CurrentGlowCursorPath = UsesCursorSwapAnimation(activeTheme)
                ? activeTheme.GlowCursorPath
                : "";
        }
        else
        {
            _overlayService.CurrentEffect = new ClickEffect();
        }

        UpdateTrayText();
    }

    private CursorTheme? LoadActiveTheme()
    {
        var themes = _themeService.LoadThemeMetadata();
        if (themes.Count == 0)
        {
            return null;
        }

        return themes.FirstOrDefault(theme => string.Equals(theme.Id, _settings.ActiveThemeId, StringComparison.OrdinalIgnoreCase))
            ?? themes.FirstOrDefault();
    }

    private CursorTheme RebuildThemeForSettings(CursorTheme theme)
    {
        if (theme.Id.Equals("lightsaber", StringComparison.OrdinalIgnoreCase)
            && _themeService.RebuildBuiltInTheme(
                theme.Id,
                _settings.ThemeColorOverrides.GetValueOrDefault(theme.Id, theme.Effect.PrimaryColor),
                ThemeAnimationService.Get(_settings, theme.Id).Scale,
                ThemeAnimationService.Get(_settings, theme.Id).Brightness,
                ThemeCursorSizeService.Get(_settings, theme.Id)) is { } lightsaber)
        {
            return lightsaber;
        }

        if ((_settings.ThemeColorOverrides.TryGetValue(theme.Id, out var color)
                || Math.Abs(ThemeCursorSizeService.Get(_settings, theme.Id) - 1.0) > 0.001)
            && _themeService.RebuildBuiltInTheme(
                theme.Id,
                color ?? theme.Effect.PrimaryColor,
                ThemeAnimationService.Get(_settings, theme.Id).Scale,
                ThemeAnimationService.Get(_settings, theme.Id).Brightness,
                ThemeCursorSizeService.Get(_settings, theme.Id)) is { } rebuilt)
        {
            return rebuilt;
        }

        return theme;
    }

    private static bool UsesCursorSwapAnimation(CursorTheme theme) =>
        (theme.Id.Equals("lightsaber", StringComparison.OrdinalIgnoreCase)
            || theme.Id.Equals("omnitrix", StringComparison.OrdinalIgnoreCase))
        && !string.IsNullOrWhiteSpace(theme.GlowCursorPath)
        && File.Exists(theme.GlowCursorPath);

    private void TogglePause()
    {
        _settings.EffectsPaused = !_settings.EffectsPaused;
        _settingsService.Save(_settings);
        LoadState();
    }

    private void RestoreDefaults()
    {
        try
        {
            _cursorService.RestoreBackedUpOrDefaults();
        }
        catch (Exception ex)
        {
            App.Log($"Agent restore failed: {ex}");
        }
    }

    private void OpenMainUi()
    {
        if (string.IsNullOrWhiteSpace(Environment.ProcessPath))
        {
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = Environment.ProcessPath,
            UseShellExecute = true
        });
    }

    private void UpdateTrayText()
    {
        _notifyIcon.Text = _settings.EffectsPaused
            ? "Cursor Magic effects - paused"
            : "Cursor Magic effects - active";
    }
}
