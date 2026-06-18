using Microsoft.Win32;

namespace TaskbarLyrics.App;

/// <summary>
/// 管理 Windows 注册表 Run 键，实现开机自启动。
/// </summary>
public static class AutoStartService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "TaskbarLyrics";

    /// <summary>
    /// 检查当前是否已注册为开机自启动。
    /// </summary>
    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
        return key?.GetValue(ValueName) is string existingPath &&
               !string.IsNullOrWhiteSpace(existingPath);
    }

    /// <summary>
    /// 启用或禁用开机自启动。
    /// </summary>
    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
        if (key is null)
        {
            return;
        }

        if (enabled)
        {
            var exePath = Environment.ProcessPath;
            if (!string.IsNullOrWhiteSpace(exePath))
            {
                key.SetValue(ValueName, $"\"{exePath}\"");
            }
        }
        else
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }
}
