using Microsoft.Win32;

namespace CursorMagic.Services;

public sealed class StartupService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "CursorMagic";

    public bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false);
        return !string.IsNullOrWhiteSpace(key?.GetValue(ValueName)?.ToString());
    }

    public void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, true)
            ?? throw new InvalidOperationException("Could not open Windows startup settings.");

        if (enabled)
        {
            key.SetValue(ValueName, $"\"{Environment.ProcessPath}\"", RegistryValueKind.String);
        }
        else
        {
            key.DeleteValue(ValueName, false);
        }
    }
}
