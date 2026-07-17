using VgmSharp.Native;

namespace VgmSharp;

/// <summary>
/// Optional playback/decode configuration, mirroring libvgmstream_config_t.
/// Leave a <see cref="VgmStream"/> opened with <c>config: null</c> to use vgmstream's own defaults.
/// </summary>
public sealed class VgmStreamConfig
{
    /// <summary>Ignore forced (TXTP) config embedded/associated with the file.</summary>
    public bool DisableConfigOverride { get; set; }

    /// <summary>Must be explicitly allowed for infinite-loop files, since callers may not want to handle it.</summary>
    public bool AllowPlayForever { get; set; }

    /// <summary>Keep looping forever (file must have a loop flag set).</summary>
    public bool PlayForever { get; set; }

    /// <summary>Ignore the file's loop points entirely (play through once).</summary>
    public bool IgnoreLoop { get; set; }

    /// <summary>Force a full loop (0..end) for files that don't define loop points.</summary>
    public bool ForceLoop { get; set; }

    /// <summary>Force a full loop even if the file already has loop points.</summary>
    public bool ReallyForceLoop { get; set; }

    /// <summary>Skip the fade-out and play the remaining outro after target loops.</summary>
    public bool IgnoreFade { get; set; }

    /// <summary>Target loop count (e.g. 1.5). Set to -1 to drop the loop section entirely. Default in vgmstream is 1.</summary>
    public double? LoopCount { get; set; }

    /// <summary>Fade-out duration in seconds after the target loop count is reached.</summary>
    public double? FadeTime { get; set; }

    /// <summary>Delay in seconds before the fade-out starts.</summary>
    public double? FadeDelay { get; set; }

    /// <summary>Decode only one 2ch "track" out of a multi-track file (1..N), 0 = disabled.</summary>
    public int StereoTrack { get; set; }

    /// <summary>Downmix if the source has more channels than this. 0 = disabled. Simplistic; avoid unless needed.</summary>
    public int AutoDownmixChannels { get; set; }

    /// <summary>Force the output sample format instead of the codec's natural one.</summary>
    public VgmSampleFormat? ForceSampleFormat { get; set; }

    /// <summary>A commonly useful preset: play once, ignoring internal loop points.</summary>
    public static VgmStreamConfig PlayOnceNoLoop() => new() { IgnoreLoop = true };

    /// <summary>A commonly useful preset matching vgmstream-cli's defaults: 2 loops with a 10s fade.</summary>
    public static VgmStreamConfig TwoLoopsWithFade() => new() { LoopCount = 2.0, FadeTime = 10.0, FadeDelay = 0.0 };

    internal LibvgmstreamConfigT ToNative() => new()
    {
        disable_config_override = this.DisableConfigOverride,
        allow_play_forever = this.AllowPlayForever,
        play_forever = this.PlayForever,
        ignore_loop = this.IgnoreLoop,
        force_loop = this.ForceLoop,
        really_force_loop = this.ReallyForceLoop,
        ignore_fade = this.IgnoreFade,
        loop_count = this.LoopCount ?? 0,
        fade_time = this.FadeTime ?? 0,
        fade_delay = this.FadeDelay ?? 0,
        stereo_track = this.StereoTrack,
        auto_downmix_channels = this.AutoDownmixChannels,
        force_sfmt = this.ForceSampleFormat.HasValue ? (LibvgmstreamSfmt)this.ForceSampleFormat.Value : 0,
    };
}
