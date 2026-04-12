using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security;
using Microsoft.Win32;

namespace SimpleWhisper.Services;

/// <summary>
/// Windows autostart via the CurrentUser\Run registry key.
/// </summary>
public class WindowsAutoStartService : IAutoStartService
{
    private const string RegistryKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "SimpleWhisper";

    private static string ExePath => Environment.ProcessPath ?? string.Empty;

    public bool IsEnabled
    {
        get
        {
            if (!OperatingSystem.IsWindows()) return false;
            using var key = Registry.CurrentUser.OpenSubKey(RegistryKey, false);
            return key?.GetValue(AppName) is string val
               && (val == $"\"{ExePath}\" --minimized" || val == ExePath
                   || val == ExePath + " --minimized");
        }
    }

    public void Enable()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var key = Registry.CurrentUser.OpenSubKey(RegistryKey, true);
        key?.SetValue(AppName, $"\"{ExePath}\" --minimized");
    }

    public void Disable()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var key = Registry.CurrentUser.OpenSubKey(RegistryKey, true);
        key?.DeleteValue(AppName, false);
    }
}

/// <summary>
/// Linux autostart via XDG autostart desktop entry in ~/.config/autostart/.
/// </summary>
public class LinuxAutoStartService : IAutoStartService
{
    private static readonly string DesktopFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "autostart", "simple-whisper.desktop");

    private static readonly string LegacyDesktopFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "autostart", "SimpleWhisper.desktop");

    private static string ExePath => Environment.ProcessPath ?? string.Empty;

    /// <summary>
    /// Escapes special characters inside a double-quoted Exec value per the freedesktop Desktop Entry spec.
    /// </summary>
    private static string EscapeDesktopExec(string path)
        => path.Replace(@"\", @"\\").Replace("\"", "\\\"").Replace("$", @"\$").Replace("`", @"\`");

    public bool IsEnabled => File.Exists(DesktopFilePath) || File.Exists(LegacyDesktopFilePath);

    public void Enable()
    {
        File.Delete(LegacyDesktopFilePath);
        Directory.CreateDirectory(Path.GetDirectoryName(DesktopFilePath)!);
        File.WriteAllText(DesktopFilePath,
            $"""
            [Desktop Entry]
            Type=Application
            Name=SimpleWhisper
            Comment=Speech-to-text transcription using Whisper
            Exec="{EscapeDesktopExec(ExePath)}" --minimized
            Icon=/opt/simple-whisper/app-icon.png
            Terminal=false
            StartupWMClass=SimpleWhisper
            Categories=Audio;Utility;
            X-GNOME-Autostart-enabled=true
            """);
    }

    public void Disable()
    {
        File.Delete(DesktopFilePath);
        File.Delete(LegacyDesktopFilePath);
    }
}

/// <summary>
/// macOS autostart via a LaunchAgent plist in ~/Library/LaunchAgents/.
/// </summary>
public class MacAutoStartService : IAutoStartService
{
    private static readonly string PlistPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "Library", "LaunchAgents", "com.simplewhisper.app.plist");

    private static string ExePath => Environment.ProcessPath ?? string.Empty;

    public bool IsEnabled => File.Exists(PlistPath);

    public void Enable()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(PlistPath)!);
        File.WriteAllText(PlistPath,
            $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
            <plist version="1.0">
            <dict>
                <key>Label</key>
                <string>com.simplewhisper.app</string>
                <key>ProgramArguments</key>
                <array>
                    <string>{SecurityElement.Escape(ExePath)}</string>
                    <string>--minimized</string>
                </array>
                <key>RunAtLoad</key>
                <true/>
            </dict>
            </plist>
            """);
    }

    public void Disable()
    {
        if (File.Exists(PlistPath))
            File.Delete(PlistPath);
    }
}
