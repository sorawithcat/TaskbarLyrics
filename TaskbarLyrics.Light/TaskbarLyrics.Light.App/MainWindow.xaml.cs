using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Media = System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using TaskbarLyrics.Core.Abstractions;
using TaskbarLyrics.Core.Models;
using TaskbarLyrics.Core.Services;
using TaskbarLyrics.Core.Utilities;

namespace TaskbarLyrics.Light.App;

public partial class MainWindow : Window
{
    private const int WmShowWindow = 0x0018;
    private const int FrameTimerMs = 16;
    private const int LyricsTickInterval = 4;

    private readonly IMusicSessionProvider _musicSessionProvider;
    private readonly SystemAudioSpectrumService _audioSpectrumService = new();
    private readonly DispatcherTimer _frameTimer;
    private readonly uint _taskbarCreatedMessage;
    private readonly DeferredLyricSyncService _lyricSyncService = new();

    private Media.Color _primaryTextColor = Media.Colors.White;
    private Media.Color _secondaryTextColor = Media.Color.FromArgb(190, 255, 255, 255);
    private string _currentLine = "TaskbarLyrics 已启动";
    private string _nextLine = "等待歌词...";
    private string? _lastCoverTrackId;
    private string? _currentCoverVisualTrackId;
    private byte[]? _lastDisplayedCoverBytes;
    private bool _enableSmtcTimelineMonitor;
    private bool _enablePureMusicSpectrum = true;
    private SmtcTimelineMonitorWindow? _smtcTimelineMonitorWindow;
    private bool _isTimerTickRunning;
    private bool _isCurrentFramePureMusic;
    private bool _isCurrentPlaybackPlaying;
    private SpectrumTuningSettings _spectrumTuningSettings = SpectrumTuningSettings.CreateDefault();
    private int _lastCurrentLineIndex = -1;
    private string _lastTrackId = string.Empty;
    private DateTimeOffset _nextTickDiagnosticsLogUtc;
    private int _frameTickCounter;
    private int _spectrumTickInterval = 2;

    public MainWindow()
    {
        InitializeComponent();

        _musicSessionProvider = new SmtcMusicSessionProvider();
        _taskbarCreatedMessage = NativeMethods.RegisterWindowMessage("TaskbarCreated");

        _frameTimer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(FrameTimerMs)
        };
        _frameTimer.Tick += OnFrameTimerTick;

        Loaded += OnLoaded;
        SourceInitialized += OnSourceInitialized;
        SizeChanged += (_, _) => AnchorToTaskbar();
        LyricsDisplay.PreferredHeightChanged += (_, _) =>
        {
            if (System.Windows.Application.Current is App { Settings.AutoAdjustWindowHeight: true })
            {
                AnchorToTaskbar();
            }
        };
        LyricsDisplay.PreferredWidthChanged += (_, _) =>
        {
            if (System.Windows.Application.Current is App { Settings.AutoAdjustWindowWidth: true })
            {
                AnchorToTaskbar();
            }
        };
        IsVisibleChanged += OnIsVisibleChanged;
        Closing += OnClosing;
        Closed += OnClosed;
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;

        if (System.Windows.Application.Current is App app)
        {
            ApplySettings(app.Settings);
        }
    }

    public void ApplySettings(AppSettings settings)
    {
        Log.SetVerboseEnabled(settings.EnableSmtcTimelineMonitor);

        if (_musicSessionProvider is SmtcMusicSessionProvider smtcProvider)
        {
            smtcProvider.SetRecognitionOrder(
                settings.SourceRecognitionOrder,
                BuildEnabledPlayerSources(settings));
        }

        try
        {
            var brush = (Media.Brush?)new Media.BrushConverter().ConvertFromString(settings.ForegroundColor);
            _primaryTextColor = brush is Media.SolidColorBrush solid ? solid.Color : Media.Colors.White;
        }
        catch
        {
            _primaryTextColor = Media.Colors.White;
        }

        _secondaryTextColor = Media.Color.FromArgb(
            (byte)Math.Clamp((int)(_primaryTextColor.A * 0.76), 0, 255),
            _primaryTextColor.R,
            _primaryTextColor.G,
            _primaryTextColor.B);

        _lyricSyncService.UpdateSettings(settings);
        _enablePureMusicSpectrum = settings.EnablePureMusicSpectrum;
        AnchorToTaskbar();
        AttachToTaskbarHost();
        LyricsDisplay.ApplyStyle(settings, _primaryTextColor, _secondaryTextColor);
        PushLyricsToDisplay(_currentLine, _nextLine, 0, _lastCurrentLineIndex, _lastTrackId, false, false);
        _enableSmtcTimelineMonitor = settings.EnableSmtcTimelineMonitor;

        if (IsLoaded)
        {
            UpdateSmtcTimelineMonitorWindow();
        }
    }

    public void ApplySpectrumTuning(SpectrumTuningSettings settings)
    {
        var snapshot = settings.Clone();
        _spectrumTuningSettings = snapshot;
        _audioSpectrumService.ApplyTuning(snapshot);
        _spectrumTickInterval = Math.Max(1, (int)Math.Round(
            Math.Clamp(snapshot.UpdateIntervalMs, 16, 100) / (double)FrameTimerMs,
            MidpointRounding.AwayFromZero));
        LyricsDisplay.SetSpectrumTuning(snapshot);
    }

    private static IReadOnlyCollection<string> BuildEnabledPlayerSources(AppSettings settings)
    {
        var sources = new List<string>();
        if (settings.EnableQQMusic) sources.Add("QQMusic");
        if (settings.EnableNetease) sources.Add("Netease");
        if (settings.EnableKugou) sources.Add("Kugou");
        if (settings.EnableSpotify) sources.Add("Spotify");
        return sources;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        AnchorToTaskbar();
        AttachToTaskbarHost();
        ApplySpectrumTuning(_spectrumTuningSettings);
        PushLyricsToDisplay(_currentLine, _nextLine, 0, _lastCurrentLineIndex, _lastTrackId, false, false);
        UpdateSmtcTimelineMonitorWindow();
        _frameTimer.Start();
        _ = OnLyricsTickAsync();
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        if (PresentationSource.FromVisual(this) is HwndSource source)
        {
            source.AddHook(WndProc);
        }
    }

    private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (IsVisible)
        {
            InvalidateCoverDisplayState();
            if (!_frameTimer.IsEnabled) _frameTimer.Start();
            AnchorToTaskbar();
            AttachToTaskbarHost();
            _ = OnLyricsTickAsync();
        }
        else
        {
            if (_frameTimer.IsEnabled) _frameTimer.Stop();
            UpdateSpectrumCaptureState(false);
        }
    }

    private void InvalidateCoverDisplayState()
    {
        _currentCoverVisualTrackId = null;
        _lastDisplayedCoverBytes = null;
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (System.Windows.Application.Current is App app && !app.IsExiting)
        {
            e.Cancel = true;
            app.MarkLyricsHiddenByUser();
            Hide();
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        CloseSmtcTimelineMonitorWindow();
        _frameTimer.Stop();
        _frameTimer.Tick -= OnFrameTimerTick;
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        UpdateSpectrumCaptureState(false);
        _lyricSyncService.Dispose();
        _audioSpectrumService.Dispose();
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            AnchorToTaskbar();
            AttachToTaskbarHost();
        });
    }

    private void OnFrameTimerTick(object? sender, EventArgs e)
    {
        _frameTickCounter++;
        if (_frameTickCounter % LyricsTickInterval == 0)
        {
            _ = OnLyricsTickAsync();
        }

        if (_isCurrentFramePureMusic &&
            _enablePureMusicSpectrum &&
            _frameTickCounter % _spectrumTickInterval == 0)
        {
            OnSpectrumTick();
        }
    }

    private async Task OnLyricsTickAsync()
    {
        if (_isTimerTickRunning)
        {
            return;
        }

        _isTimerTickRunning = true;
        try
        {
            EnsureVisibleIfExpected();

            var snapshot = await _musicSessionProvider.GetCurrentAsync();
            UpdateCover(snapshot);

            var frame = await _lyricSyncService.GetDisplayFrameAsync(snapshot);
            LogTickDiagnostics(snapshot, frame);

            if (_musicSessionProvider is SmtcMusicSessionProvider smtcProvider)
            {
                smtcProvider.SetCurrentLyricSource(_lyricSyncService.CurrentLyricSourceApp);
            }

            var current = string.IsNullOrWhiteSpace(frame.CurrentLine) ? "等待播放..." : frame.CurrentLine;
            var next = frame.NextLine;
            _lastCurrentLineIndex = frame.CurrentLineIndex;
            _lastTrackId = ResolveDisplayTrackId(snapshot.Track);

            _currentLine = current;
            _nextLine = next;
            _isCurrentFramePureMusic = frame.IsPureMusic && _enablePureMusicSpectrum;
            _isCurrentPlaybackPlaying = snapshot.IsPlaying;
            UpdateSpectrumCaptureState(_isCurrentFramePureMusic);
            PushLyricsToDisplay(current, next, frame.LineProgress, frame.CurrentLineIndex, _lastTrackId, _isCurrentFramePureMusic, snapshot.IsPlaying);
        }
        catch (Exception ex)
        {
            _currentLine = $"歌词服务异常: {ex.Message}";
            _nextLine = string.Empty;
            _isCurrentFramePureMusic = false;
            _isCurrentPlaybackPlaying = false;
            UpdateSpectrumCaptureState(false);
            PushLyricsToDisplay(_currentLine, _nextLine, 0, _lastCurrentLineIndex, _lastTrackId, false, false);
            Debug.WriteLine(ex);
        }
        finally
        {
            _isTimerTickRunning = false;
        }
    }

    private void OnSpectrumTick()
    {
        LyricsDisplay.SetSpectrum(_isCurrentPlaybackPlaying && _audioSpectrumService.IsAvailable
            ? _audioSpectrumService.GetSpectrum()
            : SystemAudioSpectrumService.Silence);
    }

    private void UpdateSpectrumCaptureState(bool shouldCapture)
    {
        if (shouldCapture && _enablePureMusicSpectrum)
        {
            if (!_audioSpectrumService.IsStarted)
            {
                _audioSpectrumService.Start();
            }

            return;
        }

        if (_audioSpectrumService.IsStarted)
        {
            _audioSpectrumService.Stop();
        }
    }

    private void PushLyricsToDisplay(string current, string next, double progress, int lineIndex, string trackId, bool isPureMusic, bool isPlaying)
    {
        LyricsDisplay.SetLyrics(current, next, progress, lineIndex, trackId, isPureMusic, isPlaying);
    }

    private string ResolveDisplayTrackId(TrackInfo? track)
    {
        if (track is null)
        {
            return string.Empty;
        }

        var candidate = LyricSyncService.BuildStableTrackIdentity(track);
        var title = track.Title?.Trim() ?? string.Empty;
        var isWeakMetadata = title.Length == 0 ||
                             title.Equals("Unknown Title", StringComparison.OrdinalIgnoreCase);

        // 网易云等播放器在切歌间隙会短暂上报弱元数据，沿用上一首展示键避免滚动动画被反复重置。
        if (isWeakMetadata &&
            _lastTrackId.Length > 0 &&
            string.Equals(track.SourceApp, ExtractSourceAppFromTrackId(_lastTrackId), StringComparison.Ordinal))
        {
            return _lastTrackId;
        }

        return candidate;
    }

    private static string ExtractSourceAppFromTrackId(string trackId)
    {
        var separator = trackId.IndexOf('|');
        return separator > 0 ? trackId[..separator] : trackId;
    }

    private void UpdateCover(PlaybackSnapshot snapshot)
    {
        var track = snapshot.Track;
        var trackId = track?.Id;
        if (string.IsNullOrEmpty(trackId) && track is not null)
        {
            // 部分播放器在首曲目时不会提供稳定的 Id，使用元数据构造备用键以确保封面能够正常更新。
            trackId = $"{track.SourceApp}|{track.Title}|{track.Artist}";
        }

        if (string.IsNullOrEmpty(trackId))
        {
            return;
        }

        var isSameRequestedTrack = string.Equals(trackId, _lastCoverTrackId, StringComparison.Ordinal);
        var isCurrentTrackVisual = string.Equals(trackId, _currentCoverVisualTrackId, StringComparison.Ordinal);
        if (isSameRequestedTrack && isCurrentTrackVisual)
        {
            // 与原版一致：同一首歌且已展示过封面时，仅在新字节到达后再刷新。
            if (_lastDisplayedCoverBytes is { Length: > 0 } || snapshot.CoverImageBytes is null)
            {
                return;
            }
        }

        _lastCoverTrackId = trackId;
        var sourceApp = track?.SourceApp ?? string.Empty;
        var (fallbackText, fallbackColor) = GetCoverFallback(sourceApp);

        if (snapshot.CoverImageBytes is { Length: > 0 } bytes)
        {
            if (LyricsDisplay.SetCover(bytes, fallbackText, fallbackColor))
            {
                _currentCoverVisualTrackId = trackId;
                _lastDisplayedCoverBytes = bytes;
            }

            return;
        }

        if (snapshot.IsCoverLoading)
        {
            return;
        }

        _currentCoverVisualTrackId = trackId;
        _lastDisplayedCoverBytes = null;
        LyricsDisplay.SetCover(null, fallbackText, fallbackColor);
    }

    private static (string Text, Media.Color Color) GetCoverFallback(string sourceApp)
    {
        if (sourceApp.Equals("QQMusic", StringComparison.OrdinalIgnoreCase))
        {
            return ("Q", Media.Color.FromRgb(41, 182, 246));
        }

        if (sourceApp.Equals("Spotify", StringComparison.OrdinalIgnoreCase))
        {
            return ("S", Media.Color.FromRgb(30, 215, 96));
        }

        if (sourceApp.Equals("Netease", StringComparison.OrdinalIgnoreCase))
        {
            return ("N", Media.Color.FromRgb(229, 57, 53));
        }

        if (sourceApp.Equals("Kugou", StringComparison.OrdinalIgnoreCase))
        {
            return ("K", Media.Color.FromRgb(52, 152, 219));
        }

        return ("♫", Media.Color.FromRgb(99, 102, 241));
    }

    private void LogTickDiagnostics(PlaybackSnapshot snapshot, LyricDisplayFrame frame)
    {
        if (!_enableSmtcTimelineMonitor || DateTimeOffset.UtcNow < _nextTickDiagnosticsLogUtc)
        {
            return;
        }

        _nextTickDiagnosticsLogUtc = DateTimeOffset.UtcNow.AddSeconds(1);
        Debug.WriteLine($"SMTC: {snapshot.Track?.Title} | Sync: {frame.CurrentLine}");
    }

    private void UpdateSmtcTimelineMonitorWindow()
    {
        if (_musicSessionProvider is not SmtcMusicSessionProvider smtcProvider || !_enableSmtcTimelineMonitor)
        {
            CloseSmtcTimelineMonitorWindow();
            return;
        }

        if (_smtcTimelineMonitorWindow is { IsVisible: true })
        {
            return;
        }

        var monitorWindow = new SmtcTimelineMonitorWindow(smtcProvider);
        if (IsLoaded && IsVisible)
        {
            monitorWindow.Owner = this;
        }

        monitorWindow.Closed += OnSmtcTimelineMonitorClosed;
        _smtcTimelineMonitorWindow = monitorWindow;
        monitorWindow.Show();
    }

    private void CloseSmtcTimelineMonitorWindow()
    {
        if (_smtcTimelineMonitorWindow is null)
        {
            return;
        }

        _smtcTimelineMonitorWindow.Closed -= OnSmtcTimelineMonitorClosed;
        _smtcTimelineMonitorWindow.Close();
        _smtcTimelineMonitorWindow = null;
    }

    private void OnSmtcTimelineMonitorClosed(object? sender, EventArgs e)
    {
        if (sender is SmtcTimelineMonitorWindow window)
        {
            window.Closed -= OnSmtcTimelineMonitorClosed;
        }

        _smtcTimelineMonitorWindow = null;
    }

    private void AnchorToTaskbar()
    {
        var workArea = SystemParameters.WorkArea;
        var screenWidth = SystemParameters.PrimaryScreenWidth;
        var screenHeight = SystemParameters.PrimaryScreenHeight;
        const double normalTaskbarHeight = 48;
        var taskbarHeight = Math.Max(normalTaskbarHeight, screenHeight - workArea.Height);
        var settings = (System.Windows.Application.Current as App)?.Settings ?? new AppSettings();
        var desiredWidth = settings.AutoAdjustWindowWidth
            ? Math.Clamp(LyricsDisplay.PreferredWindowWidth + settings.WindowWidthOffset, 320, 1400)
            : Math.Clamp(settings.WindowWidth, 320, 1400);
        var desiredHeight = settings.AutoAdjustWindowHeight
            ? Math.Max(36, LyricsDisplay.PreferredWindowHeight + settings.WindowHeightOffset)
            : Math.Max(36, settings.WindowHeight);
        Width = desiredWidth;
        Height = desiredHeight;

        Left = settings.HorizontalAnchor switch
        {
            LyricsHorizontalAnchor.Left => Math.Max(0, settings.XOffset),
            LyricsHorizontalAnchor.Center => ((screenWidth - Width) / 2.0) + settings.XOffset,
            _ => Math.Max(0, screenWidth - Width - 230 + settings.XOffset)
        };

        Top = screenHeight - taskbarHeight + ((taskbarHeight - Height) / 2.0) + settings.YOffset;
    }

    private void AttachToTaskbarHost()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        NativeMethods.SetWindowPos(
            hwnd,
            NativeMethods.HWND_TOPMOST,
            0, 0, 0, 0,
            NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE |
            NativeMethods.SWP_ASYNCWINDOWPOS | NativeMethods.SWP_SHOWWINDOW);
        NativeMethods.ShowWindow(hwnd, NativeMethods.SW_SHOWNOACTIVATE);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if ((uint)msg == _taskbarCreatedMessage || msg == WmShowWindow)
        {
            Dispatcher.BeginInvoke(() =>
            {
                EnsureVisibleIfExpected();
                AnchorToTaskbar();
                AttachToTaskbarHost();
            });
        }

        return IntPtr.Zero;
    }

    private void EnsureVisibleIfExpected()
    {
        if (System.Windows.Application.Current is not App app || app.IsExiting || !app.UserWantsLyricsVisible)
        {
            return;
        }

        if (!IsVisible)
        {
            Show();
        }

        AnchorToTaskbar();
        AttachToTaskbarHost();
    }
}

internal static class NativeMethods
{
    internal static readonly IntPtr HWND_TOPMOST = new(-1);
    internal const uint SWP_NOSIZE = 0x0001;
    internal const uint SWP_NOMOVE = 0x0002;
    internal const uint SWP_NOACTIVATE = 0x0010;
    internal const uint SWP_ASYNCWINDOWPOS = 0x4000;
    internal const uint SWP_SHOWWINDOW = 0x0040;
    internal const int SW_SHOWNOACTIVATE = 4;

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern uint RegisterWindowMessage(string lpString);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
}
