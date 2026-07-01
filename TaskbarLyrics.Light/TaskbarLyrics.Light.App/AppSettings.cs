namespace TaskbarLyrics.Light.App;

public enum LyricsHorizontalAnchor
{
    Left,
    Center,
    Right
}

public enum ForegroundColorMode
{
    Dark,
    Light,
    Custom
}

public sealed class AppSettings
{
    public const string BundledFontFamily = "Source Han Sans SC";

    public const string DefaultFontFamily = BundledFontFamily;

    public const string DefaultFontWeight = "Bold";

    public const string DarkForegroundColor = "#FF111827";

    public const string LightForegroundColor = "#FFFFFFFF";

    public List<string> SourceRecognitionOrder { get; set; } = new()
    {
        "QQMusic",
        "Netease",
        "Kugou",
        "Spotify"
    };

    public bool EnableNetease { get; set; } = true;

    public bool EnableQQMusic { get; set; } = true;

    public bool EnableKugou { get; set; } = true;

    public bool EnableSpotify { get; set; } = true;

    public bool ShowLyricsOnStartup { get; set; } = true;

    public bool StartWithWindows { get; set; } = true;

    public bool AutoShowLyricsWhenPlayerOpens { get; set; } = true;

    public bool AutoHideLyricsWhenPlayerCloses { get; set; } = true;

    public bool EnablePureMusicSpectrum { get; set; } = true;

    public double FontSize { get; set; } = 14;

    public bool AutoAdjustLineGap { get; set; } = true;

    public double LineGap { get; set; } = 2;

    public double LineGapOffset { get; set; }

    public string FontFamily { get; set; } = DefaultFontFamily;

    public string FontWeight { get; set; } = DefaultFontWeight;

    public ForegroundColorMode ForegroundColorMode { get; set; } = ForegroundColorMode.Light;

    public string ForegroundColor { get; set; } = LightForegroundColor;

    public bool ShowBackground { get; set; } = false;

    public double BackgroundOpacity { get; set; } = 0.55;

    public bool ShowBorder { get; set; } = false;

    public bool ShowTextShadow { get; set; } = false;

    public double WindowWidth { get; set; } = 420;

    public bool AutoAdjustWindowWidth { get; set; } = true;

    public double WindowWidthOffset { get; set; }

    public double WindowHeight { get; set; } = 44;

    public bool AutoAdjustWindowHeight { get; set; } = true;

    public double WindowHeightOffset { get; set; }

    public LyricsHorizontalAnchor HorizontalAnchor { get; set; } = LyricsHorizontalAnchor.Left;

    public double XOffset { get; set; }

    public double YOffset { get; set; }

    // Debug only: show real-time SMTC timeline diagnostics window.
    public bool EnableSmtcTimelineMonitor { get; set; } = false;

    public AppSettings Clone()
    {
        var cloned = (AppSettings)MemberwiseClone();
        cloned.SourceRecognitionOrder = SourceRecognitionOrder.ToList();
        return cloned;
    }
}
