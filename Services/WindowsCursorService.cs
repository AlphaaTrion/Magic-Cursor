using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using CursorMagic.Models;
using Microsoft.Win32;

namespace CursorMagic.Services;

public sealed class WindowsCursorService
{
    private const string CursorRegistryPath = @"Control Panel\Cursors";
    private const uint SpiSetCursors = 0x0057;
    private const uint SpifUpdateIniFile = 0x01;
    private const uint SpifSendChange = 0x02;

    private static readonly string[] KnownRoles =
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

    public void Apply(CursorTheme theme)
    {
        AppPaths.Ensure();
        BackupCurrentSettingsOnce();

        using var key = Registry.CurrentUser.CreateSubKey(CursorRegistryPath, true)
            ?? throw new InvalidOperationException("Could not open Windows cursor settings.");

        foreach (var variant in theme.Variants)
        {
            if (File.Exists(variant.AssetPath))
            {
                key.SetValue(variant.Role, variant.AssetPath, RegistryValueKind.String);
            }
        }

        key.SetValue("", theme.Name, RegistryValueKind.String);
        key.SetValue("Scheme Source", "1", RegistryValueKind.String);
        RefreshSystemCursors();
    }

    public static void SwapToGlow(string glowCursorPath)
    {
        if (!File.Exists(glowCursorPath)) return;

        foreach (var ocrId in AllOcrIds)
        {
            var hCursor = NativeMethods.LoadCursorFromFile(glowCursorPath);
            if (hCursor != IntPtr.Zero)
            {
                NativeMethods.SetSystemCursor(hCursor, ocrId);
            }
        }
    }

    public static void SwapBackFromGlow()
    {
        RefreshSystemCursors();
    }

    public void RestoreBackedUpOrDefaults()
    {
        using var key = Registry.CurrentUser.CreateSubKey(CursorRegistryPath, true)
            ?? throw new InvalidOperationException("Could not open Windows cursor settings.");

        if (File.Exists(AppPaths.BackupPath))
        {
            var json = File.ReadAllText(AppPaths.BackupPath);
            var backup = JsonSerializer.Deserialize(json, CursorJsonContext.Default.CursorRegistryBackup);
            if (backup is not null)
            {
                foreach (var role in KnownRoles)
                {
                    key.SetValue(role, backup.Values.GetValueOrDefault(role, ""), RegistryValueKind.String);
                }

                if (backup.Values.TryGetValue("", out var schemeName))
                {
                    key.SetValue("", schemeName, RegistryValueKind.String);
                }
            }
        }
        else
        {
            foreach (var role in KnownRoles)
            {
                key.SetValue(role, "", RegistryValueKind.String);
            }

            key.SetValue("", "Windows Default", RegistryValueKind.String);
            key.SetValue("Scheme Source", "2", RegistryValueKind.String);
        }

        RefreshSystemCursors();
    }

    private static void BackupCurrentSettingsOnce()
    {
        if (File.Exists(AppPaths.BackupPath))
        {
            return;
        }

        using var key = Registry.CurrentUser.OpenSubKey(CursorRegistryPath, false);
        var backup = new CursorRegistryBackup();
        if (key is not null)
        {
            foreach (var role in KnownRoles.Concat([""]))
            {
                backup.Values[role] = key.GetValue(role)?.ToString() ?? "";
            }
        }

        File.WriteAllText(
            AppPaths.BackupPath,
            JsonSerializer.Serialize(backup, CursorJsonContext.Default.CursorRegistryBackup));
    }

    private static readonly uint[] AllOcrIds =
    [
        32512, // OCR_NORMAL (Arrow)
        32513, // OCR_IBEAM
        32514, // OCR_WAIT
        32515, // OCR_CROSS
        32516, // OCR_UP
        32642, // OCR_SIZENWSE
        32643, // OCR_SIZENESW
        32644, // OCR_SIZEWE
        32645, // OCR_SIZENS
        32646, // OCR_SIZEALL
        32648, // OCR_NO
        32649, // OCR_HAND
        32650  // OCR_APPSTARTING
    ];

    private static void RefreshSystemCursors()
    {
        _ = NativeMethods.SystemParametersInfo(SpiSetCursors, 0, IntPtr.Zero, SpifUpdateIniFile | SpifSendChange);
    }

    private static class NativeMethods
    {
        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SystemParametersInfo(uint uiAction, uint uiParam, IntPtr pvParam, uint fWinIni);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern IntPtr LoadCursorFromFile(string lpFileName);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetSystemCursor(IntPtr hcur, uint id);
    }
}
