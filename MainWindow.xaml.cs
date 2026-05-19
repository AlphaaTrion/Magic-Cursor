using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using CursorMagic.Models;
using CursorMagic.Services;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;
using Point = System.Windows.Point;
using WinForms = System.Windows.Forms;

namespace CursorMagic;

public partial class MainWindow : Window
{
    private readonly ThemeService _themeService = new();
    private readonly WindowsCursorService _cursorService = new();
    private readonly SettingsService _settingsService = new();
    private readonly StartupService _startupService = new();
    private readonly CursorPackService _packService = new();
    private readonly ClickEffectOverlayService _overlayService = new();
    private readonly ForegroundAppService _foregroundService = new();
    private readonly ObservableCollection<CursorTheme> _themes = [];
    private readonly ObservableCollection<string> _blockedApps = [];

    private AppSettings _settings = new();
    private WinForms.NotifyIcon? _notifyIcon;
    private string? _importedImagePath;
    private bool _updatingAnimationControls;
    private bool _updatingColorControls;

    public MainWindow()
    {
        InitializeComponent();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            App.Log("MainWindow loaded");
            SetStatus("Opening Cursor Magic...");
            AppPaths.Ensure();
            _settings = _settingsService.Load();
            _settings.StartWithWindows = _startupService.IsEnabled();
            PauseEffectsCheckBox.IsChecked = _settings.EffectsPaused;
            StartWithWindowsCheckBox.IsChecked = _settings.StartWithWindows;
            RestoreOnExitCheckBox.IsChecked = _settings.RestoreOnExit;
            LoadThemes();
            LoadBlocklist();
            SetupTray();
            AgentLauncher.StartIfNeeded();
            _overlayService.Settings = _settings;
            if (_themes.FirstOrDefault() is { } firstTheme)
                ApplyOverlayEffect(firstTheme);
            else
                _overlayService.CurrentEffect = new ClickEffect();
            RefreshForegroundStatus();
            UpdateCreatorPreview();
            SetStatus("Ready. Effects auto-pause over fullscreen apps and anything in the blocklist.");
        }
        catch (Exception ex)
        {
            App.Log($"MainWindow load failed: {ex}");
            SetStatus($"Startup failed: {ex.Message}");
            WinForms.MessageBox.Show(ex.Message, "Cursor Magic startup failed", WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Error);
        }
    }

    private void LoadThemes()
    {
        _themes.Clear();
        foreach (var theme in _themeService.LoadThemes())
        {
            if (_settings.ThemeColorOverrides.TryGetValue(theme.Id, out var color)
                && _themeService.RebuildBuiltInTheme(
                    theme.Id,
                    color,
                    ThemeAnimationService.Get(_settings, theme.Id).Scale,
                    ThemeAnimationService.Get(_settings, theme.Id).Brightness) is { } rebuilt)
            {
                rebuilt.IsActive = string.Equals(rebuilt.Id, _settings.ActiveThemeId, StringComparison.OrdinalIgnoreCase);
                _themes.Add(rebuilt);
                continue;
            }

            theme.IsActive = string.Equals(theme.Id, _settings.ActiveThemeId, StringComparison.OrdinalIgnoreCase);
            _themes.Add(theme);
        }

        ThemesListBox.ItemsSource = _themes;
        ThemesListBox.SelectedIndex = _themes.Count > 0 ? 0 : -1;
    }

    private void LoadBlocklist()
    {
        _blockedApps.Clear();
        foreach (var name in _settings.BlockedProcessNames.OrderBy(name => name))
        {
            _blockedApps.Add(name);
        }

        BlocklistBox.ItemsSource = _blockedApps;
    }

    private void ThemesListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ThemesListBox.SelectedItem is not CursorTheme theme)
        {
            return;
        }

        ThemeNameText.Text = theme.Name;
        ThemeCategoryText.Text = theme.Category;
        ThemeDescriptionText.Text = theme.Description;
        ThemePreviewImage.Source = File.Exists(theme.PreviewPath)
            ? CursorAssetGenerator.LoadPreview(theme.PreviewPath)
            : null;
        ActiveThemeBadge.Visibility = theme.IsActive ? Visibility.Visible : Visibility.Collapsed;
        ApplyOverlayEffect(theme);
        UpdateAnimationControls(theme);
        UpdateColorControls(theme);
    }

    private void ApplyTheme_Click(object sender, RoutedEventArgs e)
    {
        if (ThemesListBox.SelectedItem is not CursorTheme theme)
        {
            return;
        }

        try
        {
            _cursorService.Apply(theme);
            SetActiveTheme(theme.Id);
            ApplyOverlayEffect(theme);
            SetStatus($"Applied {theme.Name} with {ResolveEffect(theme).Type} animation.");
        }
        catch (Exception ex)
        {
            SetStatus($"Could not apply theme: {ex.Message}");
        }
    }

    private void RestoreDefaults_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _cursorService.RestoreBackedUpOrDefaults();
            SetActiveTheme("");
            SetStatus("Restored the saved Windows cursor settings.");
        }
        catch (Exception ex)
        {
            SetStatus($"Could not restore cursors: {ex.Message}");
        }
    }

    private void PreviewEffect_Click(object sender, RoutedEventArgs e)
    {
        if (ThemesListBox.SelectedItem is CursorTheme theme)
        {
            ApplyOverlayEffect(theme);
        }

        _overlayService.Start(installMouseHook: false);
        var point = ThemePreviewImage.PointToScreen(new Point(95, 95));
        _overlayService.PreviewAtScreenPoint(point.X, point.Y);
    }

    private void ExportTheme_Click(object sender, RoutedEventArgs e)
    {
        if (ThemesListBox.SelectedItem is not CursorTheme theme)
        {
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = "Export cursor pack",
            FileName = $"{theme.Name.Replace(' ', '-')}.cmpack",
            Filter = "Cursor Magic pack|*.cmpack|Zip archive|*.zip"
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            _packService.Export(theme, dialog.FileName);
            SetStatus($"Exported {theme.Name}.");
        }
        catch (Exception ex)
        {
            SetStatus($"Could not export pack: {ex.Message}");
        }
    }

    private void ImportTheme_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Import cursor pack",
            Filter = "Cursor Magic packs|*.cmpack;*.zip|All files|*.*"
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            var theme = _packService.Import(dialog.FileName);
            _themes.Add(theme);
            ThemesListBox.SelectedItem = theme;
            SetStatus($"Imported {theme.Name}.");
        }
        catch (Exception ex)
        {
            SetStatus($"Could not import pack: {ex.Message}");
        }
    }

    private void ThemeEffectComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_updatingAnimationControls || !IsLoaded || ThemesListBox.SelectedItem is not CursorTheme theme)
        {
            return;
        }

        var selectedEffect = SelectedComboText(ThemeEffectComboBox);
        if (selectedEffect == "Theme default")
        {
            _settings.ThemeEffectOverrides.Remove(theme.Id);
        }
        else
        {
            _settings.ThemeEffectOverrides[theme.Id] = selectedEffect;
        }

        ApplyOverlayEffect(theme);
        SaveSettings();
        SetStatus($"{theme.Name} now uses {_overlayService.CurrentEffect.Type} click animation.");
    }

    private void ThemeColorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_updatingColorControls || !IsLoaded || ThemesListBox.SelectedItem is not CursorTheme theme)
        {
            return;
        }

        var selectedColor = SelectedColor();
        if (string.IsNullOrWhiteSpace(selectedColor))
        {
            return;
        }

        _settings.ThemeColorOverrides[theme.Id] = selectedColor;
        SaveSettings();

        if (theme.Id == "lightsaber")
        {
            try
            {
                var rebuilt = _themeService.RebuildBuiltInTheme(
                    theme.Id,
                    selectedColor,
                    ThemeAnimationService.Get(_settings, theme.Id).Scale,
                    ThemeAnimationService.Get(_settings, theme.Id).Brightness);
                if (rebuilt is not null)
                {
                    var wasActive = theme.IsActive;
                    var index = _themes.IndexOf(theme);
                    if (index >= 0)
                    {
                        rebuilt.IsActive = wasActive;
                        _themes[index] = rebuilt;
                        ThemesListBox.SelectedItem = rebuilt;
                        ApplyOverlayEffect(rebuilt);
                    }

                    if (wasActive)
                    {
                        _cursorService.Apply(rebuilt);
                        SetActiveTheme(rebuilt.Id);
                    }
                }
            }
            catch (Exception ex)
            {
                SetStatus($"Could not rebuild cursor: {ex.Message}");
                return;
            }
        }

        SetStatus($"{theme.Name} color updated.");
    }

    private void AnimationSlider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsLoaded || _updatingAnimationControls || ThemesListBox.SelectedItem is not CursorTheme theme)
        {
            return;
        }

        ThemeAnimationService.Set(
            _settings,
            theme.Id,
            AnimationSizeSlider.Value,
            AnimationBrightnessSlider.Value);
        ApplyOverlayEffect(theme);
        SaveSettings();
        SetStatus($"{theme.Name} animation size {AnimationSizeSlider.Value:0.00}x, brightness {AnimationBrightnessSlider.Value:0.00}x.");
    }

    private void PauseEffects_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded)
        {
            return;
        }

        _settings.EffectsPaused = PauseEffectsCheckBox.IsChecked == true;
        SaveSettings();
        SetStatus(_settings.EffectsPaused ? "Click effects paused." : "Click effects resumed.");
        UpdateTrayText();
    }

    private void StartWithWindows_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded)
        {
            return;
        }

        try
        {
            _settings.StartWithWindows = StartWithWindowsCheckBox.IsChecked == true;
            _startupService.SetEnabled(_settings.StartWithWindows);
            SaveSettings();
            SetStatus(_settings.StartWithWindows ? "Cursor Magic will start with Windows." : "Cursor Magic startup disabled.");
        }
        catch (Exception ex)
        {
            SetStatus($"Could not change startup setting: {ex.Message}");
        }
    }

    private void RestoreOnExit_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded)
        {
            return;
        }

        _settings.RestoreOnExit = RestoreOnExitCheckBox.IsChecked == true;
        SaveSettings();
        SetStatus(_settings.RestoreOnExit ? "Cursor settings will restore when Cursor Magic exits." : "Restore on exit disabled.");
    }

    private void ImportImage_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Import cursor art",
            Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp;*.gif|All files|*.*"
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        _importedImagePath = dialog.FileName;
        ImportedImageText.Text = Path.GetFileName(_importedImagePath);
        CreatorPreviewImage.Source = LoadBitmap(_importedImagePath);
        UpdateCreatorPreview();
        SetStatus("Imported art. Set the hotspot, pick an effect, then save the theme.");
    }

    private void CreatorPreview_Changed(object sender, RoutedEventArgs e)
    {
        if (IsLoaded)
        {
            UpdateCreatorPreview();
        }
    }

    private void SaveCustomTheme_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_importedImagePath) || !File.Exists(_importedImagePath))
        {
            SetStatus("Import an image before saving a custom theme.");
            return;
        }

        var name = string.IsNullOrWhiteSpace(CreatorNameTextBox.Text)
            ? "My Custom Cursor"
            : CreatorNameTextBox.Text.Trim();

        try
        {
            var theme = _themeService.CreateUserTheme(
                name,
                _importedImagePath,
                (int)Math.Round(HotspotXSlider.Value),
                (int)Math.Round(HotspotYSlider.Value),
                SelectedComboText(EffectComboBox),
                SelectedComboText(DecorationComboBox));

            _themes.Add(theme);
            ThemesListBox.SelectedItem = theme;
            SetStatus($"Saved {theme.Name} and added it to the library.");
        }
        catch (Exception ex)
        {
            SetStatus($"Could not save custom theme: {ex.Message}");
        }
    }

    private void SuspendCurrentApp_Click(object sender, RoutedEventArgs e)
    {
        var processName = SettingsService.NormalizeProcessName(_foregroundService.GetForegroundProcessName());
        if (string.IsNullOrWhiteSpace(processName) || processName.Equals("cursormagic", StringComparison.OrdinalIgnoreCase))
        {
            SetStatus("No external foreground app detected to blocklist.");
            return;
        }

        if (!_settings.BlockedProcessNames.Contains(processName, StringComparer.OrdinalIgnoreCase))
        {
            _settings.BlockedProcessNames.Add(processName);
            _blockedApps.Add(processName);
            SaveSettings();
        }

        SetStatus($"Effects will now pause when {processName} is foreground.");
    }

    private void RemoveBlockedApp_Click(object sender, RoutedEventArgs e)
    {
        if (BlocklistBox.SelectedItem is not string selected)
        {
            return;
        }

        _settings.BlockedProcessNames.RemoveAll(name => string.Equals(name, selected, StringComparison.OrdinalIgnoreCase));
        _blockedApps.Remove(selected);
        SaveSettings();
        SetStatus($"Removed {selected} from the blocklist.");
    }

    private void RefreshForeground_Click(object sender, RoutedEventArgs e) => RefreshForegroundStatus();

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            ToggleMaximize();
            return;
        }

        try
        {
            DragMove();
        }
        catch
        {
            // DragMove can throw if the mouse state changes mid-drag.
        }
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void MaximizeButton_Click(object sender, RoutedEventArgs e)
    {
        ToggleMaximize();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void ToggleMaximize()
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        UpdateChromeState();
    }

    private void RefreshForegroundStatus()
    {
        var processName = _foregroundService.GetForegroundProcessName();
        ForegroundAppText.Text = string.IsNullOrWhiteSpace(processName) ? "Unknown" : processName;
        var fullscreen = _foregroundService.IsForegroundFullscreen();
        var blocked = _foregroundService.IsBlockedForeground(_settings.BlockedProcessNames);
        FullscreenText.Text = $"Fullscreen detected: {(fullscreen ? "yes" : "no")}    Blocklisted: {(blocked ? "yes" : "no")}";
    }

    private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        App.Log("MainWindow closing");
        if (_settings.RestoreOnExit)
        {
            try
            {
                _cursorService.RestoreBackedUpOrDefaults();
            }
            catch (Exception ex)
            {
                App.Log($"Restore on exit failed: {ex}");
            }
        }

        _overlayService.Dispose();
        _notifyIcon?.Dispose();
        System.Windows.Application.Current.Shutdown();
    }

    private void Window_StateChanged(object? sender, EventArgs e)
    {
        UpdateChromeState();
        if (WindowState == WindowState.Minimized)
        {
            Hide();
            SetStatus("Cursor Magic is still running in the tray.");
        }
    }

    private void UpdateChromeState()
    {
        if (MaximizeButton is not null)
        {
            MaximizeButton.Content = WindowState == WindowState.Maximized ? "Restore" : "[]";
        }
    }

    private void SetupTray()
    {
        _notifyIcon = new WinForms.NotifyIcon
        {
            Icon = System.Drawing.SystemIcons.Information,
            Text = "Cursor Magic",
            Visible = true,
            ContextMenuStrip = new WinForms.ContextMenuStrip()
        };

        _notifyIcon.DoubleClick += (_, _) => ShowFromTray();
        _notifyIcon.ContextMenuStrip.Items.Add("Show Cursor Magic", null, (_, _) => ShowFromTray());
        _notifyIcon.ContextMenuStrip.Items.Add("Pause/resume effects", null, (_, _) =>
        {
            PauseEffectsCheckBox.IsChecked = !(PauseEffectsCheckBox.IsChecked == true);
        });
        _notifyIcon.ContextMenuStrip.Items.Add("Suspend current app", null, (_, _) => SuspendCurrentApp_Click(this, new RoutedEventArgs()));
        _notifyIcon.ContextMenuStrip.Items.Add("Restore Windows defaults", null, (_, _) => RestoreDefaults_Click(this, new RoutedEventArgs()));
        _notifyIcon.ContextMenuStrip.Items.Add("Exit", null, (_, _) =>
        {
            Close();
        });
        UpdateTrayText();
    }

    private void ShowFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
        RefreshForegroundStatus();
    }

    private void SaveSettings()
    {
        _settings.AnimationScale = Math.Clamp(_settings.AnimationScale <= 0 ? 1 : _settings.AnimationScale, 0.45, 2.0);
        _settings.AnimationBrightness = Math.Clamp(_settings.AnimationBrightness <= 0 ? 1 : _settings.AnimationBrightness, 0.35, 1.8);
        _settingsService.Save(_settings);
        if (ThemesListBox.SelectedItem is CursorTheme theme)
        {
            _overlayService.Settings = ThemeAnimationService.RuntimeSettings(_settings, theme.Id);
        }
        else
        {
            _overlayService.Settings = _settings;
        }
    }

    private void SetActiveTheme(string themeId)
    {
        _settings.ActiveThemeId = themeId;
        foreach (var theme in _themes)
        {
            theme.IsActive = string.Equals(theme.Id, themeId, StringComparison.OrdinalIgnoreCase);
        }

        if (ThemesListBox.SelectedItem is CursorTheme selected)
        {
            ActiveThemeBadge.Visibility = selected.IsActive ? Visibility.Visible : Visibility.Collapsed;
        }

        ThemesListBox.Items.Refresh();
        SaveSettings();
    }

    private void UpdateTrayText()
    {
        if (_notifyIcon is not null)
        {
            _notifyIcon.Text = _settings.EffectsPaused ? "Cursor Magic - effects paused" : "Cursor Magic - effects active";
        }
    }

    private void UpdateCreatorPreview()
    {
        var scale = 360.0 / 48.0;
        Canvas.SetLeft(HotspotCanvas, HotspotXSlider.Value * scale + 52 - 6);
        Canvas.SetTop(HotspotCanvas, HotspotYSlider.Value * scale + 52 - 6);
    }

    private void SetStatus(string message)
    {
        StatusText.Text = $"{DateTime.Now:t}  {message}";
    }

    private static string SelectedComboText(System.Windows.Controls.ComboBox comboBox)
    {
        return (comboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";
    }

    private void UpdateAnimationControls(CursorTheme theme)
    {
        _updatingAnimationControls = true;
        try
        {
            var themeAnimation = ThemeAnimationService.Get(_settings, theme.Id);
            AnimationSizeSlider.Value = themeAnimation.Scale;
            AnimationBrightnessSlider.Value = themeAnimation.Brightness;

            var selected = _settings.ThemeEffectOverrides.TryGetValue(theme.Id, out var overrideName)
                ? overrideName
                : "Theme default";

            for (var i = 0; i < ThemeEffectComboBox.Items.Count; i++)
            {
                if ((ThemeEffectComboBox.Items[i] as ComboBoxItem)?.Content?.ToString() == selected)
                {
                    ThemeEffectComboBox.SelectedIndex = i;
                    return;
                }
            }

            ThemeEffectComboBox.SelectedIndex = 0;
        }
        finally
        {
            _updatingAnimationControls = false;
        }
    }

    private void UpdateColorControls(CursorTheme theme)
    {
        _updatingColorControls = true;
        try
        {
            ThemeColorComboBox.IsEnabled = theme.Id == "lightsaber";
            var color = _settings.ThemeColorOverrides.TryGetValue(theme.Id, out var savedColor)
                ? savedColor
                : "#55F7FF";

            for (var i = 0; i < ThemeColorComboBox.Items.Count; i++)
            {
                if ((ThemeColorComboBox.Items[i] as ComboBoxItem)?.Tag?.ToString() == color)
                {
                    ThemeColorComboBox.SelectedIndex = i;
                    return;
                }
            }

            ThemeColorComboBox.SelectedIndex = 0;
        }
        finally
        {
            _updatingColorControls = false;
        }
    }

    private string SelectedColor()
    {
        return (ThemeColorComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "";
    }

    private void ApplyOverlayEffect(CursorTheme theme)
    {
        _overlayService.CurrentEffect = EffectResolver.Resolve(theme, _settings);
        _overlayService.Settings = ThemeAnimationService.RuntimeSettings(_settings, theme.Id);
        _overlayService.CurrentGlowCursorPath = UsesCursorSwapAnimation(theme)
            ? theme.GlowCursorPath
            : "";
    }

    private ClickEffect ResolveEffect(CursorTheme theme)
    {
        return EffectResolver.Resolve(theme, _settings);
    }

    private static bool UsesCursorSwapAnimation(CursorTheme theme) =>
        (theme.Id.Equals("lightsaber", StringComparison.OrdinalIgnoreCase)
            || theme.Id.Equals("omnitrix", StringComparison.OrdinalIgnoreCase))
        && !string.IsNullOrWhiteSpace(theme.GlowCursorPath)
        && File.Exists(theme.GlowCursorPath);

    private static BitmapImage LoadBitmap(string path)
    {
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.UriSource = new Uri(path, UriKind.Absolute);
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }
}
