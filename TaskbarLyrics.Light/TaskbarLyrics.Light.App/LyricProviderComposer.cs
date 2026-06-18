using TaskbarLyrics.Core.Abstractions;
using TaskbarLyrics.Core.Services;

namespace TaskbarLyrics.Light.App;

internal static class LyricProviderComposer
{
    public static LyricSyncService CreateSyncService(AppSettings settings)
    {
        var providers = new List<ILyricProvider>
        {
            new LazyLyricProvider("LRCLIB", () => new GenericSmtcLyricProvider())
        };

        if (settings.EnableNetease)
        {
            providers.Add(new LazyLyricProvider(
                "Netease",
                () => new LyricifyLyricProvider("Netease", Lyricify.Lyrics.Searchers.Searchers.Netease)));
        }

        if (settings.EnableQQMusic)
        {
            providers.Add(new LazyLyricProvider(
                "QQMusic",
                () => new LyricifyLyricProvider("QQMusic", Lyricify.Lyrics.Searchers.Searchers.QQMusic)));
        }

        if (settings.EnableKugou)
        {
            providers.Add(new LazyLyricProvider(
                "Kugou",
                () => new LyricifyLyricProvider("Kugou", Lyricify.Lyrics.Searchers.Searchers.Kugou)));
        }

        return new LyricSyncService(
            new LyricProviderRegistry(providers),
            _ => false);
    }
}
