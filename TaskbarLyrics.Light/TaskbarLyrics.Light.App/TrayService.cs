using System.Drawing;
using System.Windows;
using Forms = System.Windows.Forms;

namespace TaskbarLyrics.Light.App;

public sealed class TrayService : IDisposable
{
    private readonly Icon _icon;
    private readonly Forms.NotifyIcon _notifyIcon;
    private TrayMenuWindow? _menuWindow;

    public TrayService(Action toggleLyricsWindow, Action openSettings, Action exitApp)
    {
        _icon = AppIconProvider.LoadTrayIcon();
        _notifyIcon = new Forms.NotifyIcon
        {
            Text = "TaskbarLyrics",
            Icon = _icon,
            Visible = true
        };

        _notifyIcon.DoubleClick += (_, _) => toggleLyricsWindow();
        _notifyIcon.MouseUp += (_, e) =>
        {
            if (e.Button == Forms.MouseButtons.Right)
            {
                System.Windows.Application.Current.Dispatcher.BeginInvoke(() =>
                    ShowMenu(toggleLyricsWindow, openSettings, exitApp));
            }
        };
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _menuWindow?.Close();
        _icon.Dispose();
    }

    public void ShowNotification(string title, string message, Forms.ToolTipIcon icon = Forms.ToolTipIcon.Info)
    {
        if (!_notifyIcon.Visible)
        {
            return;
        }

        _notifyIcon.BalloonTipTitle = title;
        _notifyIcon.BalloonTipText = message;
        _notifyIcon.BalloonTipIcon = icon;
        _notifyIcon.ShowBalloonTip(7000);
    }

    private void ShowMenu(Action toggleLyricsWindow, Action openSettings, Action exitApp)
    {
        _menuWindow?.Close();
        _menuWindow = new TrayMenuWindow(toggleLyricsWindow, openSettings, exitApp);
        _menuWindow.Closed += (_, _) => _menuWindow = null;
        _menuWindow.ShowAtCursor();
    }
}
