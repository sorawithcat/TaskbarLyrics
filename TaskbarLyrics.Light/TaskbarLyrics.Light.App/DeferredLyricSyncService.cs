using TaskbarLyrics.Core.Models;
using TaskbarLyrics.Core.Services;

namespace TaskbarLyrics.Light.App;

/// <summary>
/// 在首次需要歌词同步时才创建 <see cref="LyricSyncService"/> 并初始化基础设施。
/// </summary>
internal sealed class DeferredLyricSyncService : IDisposable
{
    private readonly object _gate = new();
    private LyricSyncService? _inner;
    private AppSettings _settings = new();
    private bool _isDisposed;

    public string? CurrentLyricSourceApp => _inner?.CurrentLyricSourceApp;

    public void UpdateSettings(AppSettings settings)
    {
        lock (_gate)
        {
            ThrowIfDisposed();
            var nextSettings = settings.Clone();
            var providersChanged = HasProviderSettingsChanged(_settings, nextSettings);
            _settings = nextSettings;

            if (providersChanged)
            {
                _inner?.Dispose();
                _inner = null;
            }
        }
    }

    public Task<LyricDisplayFrame> GetDisplayFrameAsync(PlaybackSnapshot snapshot)
    {
        if (snapshot.Track is null)
        {
            return Task.FromResult(new LyricDisplayFrame("", "", "", 0, -1));
        }

        return EnsureInner().GetDisplayFrameAsync(snapshot);
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            _inner?.Dispose();
            _inner = null;
        }
    }

    private LyricSyncService EnsureInner()
    {
        lock (_gate)
        {
            ThrowIfDisposed();
            if (_inner is not null)
            {
                return _inner;
            }

            LyricsInfrastructure.EnsureInitialized();
            _inner = LyricProviderComposer.CreateSyncService(_settings);
            return _inner;
        }
    }

    private static bool HasProviderSettingsChanged(AppSettings previous, AppSettings current) =>
        previous.EnableNetease != current.EnableNetease ||
        previous.EnableQQMusic != current.EnableQQMusic ||
        previous.EnableKugou != current.EnableKugou ||
        previous.EnableSpotify != current.EnableSpotify ||
        previous.EnableLocalLyrics != current.EnableLocalLyrics ||
        !previous.LocalMusicFolders.SequenceEqual(current.LocalMusicFolders) ||
        !previous.SourceRecognitionOrder.SequenceEqual(current.SourceRecognitionOrder);

    private void ThrowIfDisposed()
    {
        if (_isDisposed)
        {
            throw new ObjectDisposedException(nameof(DeferredLyricSyncService));
        }
    }
}
