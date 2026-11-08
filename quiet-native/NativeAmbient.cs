using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Tizen.NUI;
using Tizen.NUI.BaseComponents;
using Path = System.IO.Path;

// A single composited decoder sits behind a matching, preloaded Snoopy still.
// The still is only uncovered after the native player's position advances.
// Do not replace DALi's decoded-frame callback: it owns texture presentation.
public sealed class NativeAmbient : IDisposable
{
    // Preserve the original 16:9 media inside the wider hero, with neutral
    // side panels. Both the still and the movie use the same contained bounds.
    const int Width = 1040, Height = 442, VideoWidth = 786, VideoHeight = 442, VideoLeft = 127, VideoTop = 0;
    readonly Action<string> log;
    readonly List<string> videos = new List<string>();
    readonly List<ImageView> posters = new List<ImageView>();
    readonly View root;
    readonly Tizen.NUI.Timer timer;
    readonly Stopwatch clock = new Stopwatch();
    View parent;
    VideoView video;
    int current = -1, requested = -1, shownPoster = -1;
    bool active, disposed, uncovered, finished, nativeUnavailable;
    long clipStarted;

    [DllImport("libcapi-media-player.so.0", CallingConvention = CallingConvention.Cdecl)]
    static extern int player_get_play_position(IntPtr player, out int milliseconds);

    public NativeAmbient(string mediaRoot, Action<string> log = null)
    {
        this.log = log;
        root = new View
        {
            Size2D = new Size2D(Width, Height),
            BackgroundColor = new Color(.035f, .055f, .095f, 1),
            ClippingMode = ClippingModeType.ClipChildren,
            CornerRadius = new Vector4(20, 20, 20, 20)
        };
        foreach (var name in new[] { "cards", "sleep", "cinema", "space", "football", "records", "cardtower", "painting", "stargazing", "stage" })
        {
            var movie = Path.Combine(mediaRoot, "snoopy-" + name + ".mp4");
            var still = Path.Combine(mediaRoot, "snoopy-" + name + ".jpg");
            if (!File.Exists(movie) || !File.Exists(still)) { continue; }
            videos.Add(movie);
            var poster = new ImageView
            {
                Position2D = new Position2D(VideoLeft, VideoTop),
                Size2D = new Size2D(VideoWidth, VideoHeight),
                WidthResizePolicy = ResizePolicyType.Fixed,
                HeightResizePolicy = ResizePolicyType.Fixed,
                DesiredWidth = VideoWidth,
                DesiredHeight = VideoHeight,
                FittingMode = FittingModeType.ShrinkToFit,
                ReleasePolicy = ReleasePolicyType.Never,
                Opacity = 0
            };
            root.Add(poster);
            poster.ResourceUrl = still;
            posters.Add(poster);
        }
        timer = new Tizen.NUI.Timer(100);
        timer.Tick += (sender, args) => Tick();
        if (posters.Count > 0) { ShowPoster(0); }
        Write("ambient ready: " + videos.Count + " local variants");
    }

    public void Attach(View hero)
    {
        if (disposed || hero == null) { return; }
        if (!ReferenceEquals(parent, hero))
        {
            if (parent != null) { parent.Remove(root); }
            parent = hero;
            parent.Add(root);
            root.LowerToBottom();
        }
        if (active) { Start(); }
    }

    // Call before disposing a page's hero. Cached stills and the same decoder
    // survive a home-page rebuild rather than becoming children of a dead view.
    public void Detach()
    {
        if (disposed) { return; }
        if (parent != null) { parent.Remove(root); parent = null; }
    }

    public void SetActive(bool value)
    {
        if (disposed) { return; }
        active = value;
        if (active && parent != null) { Start(); }
        else { timer.Stop(); ReleaseVideo(); }
    }

    void Start()
    {
        if (videos.Count == 0 || parent == null) { return; }
        if (!clock.IsRunning) { clock.Start(); }
        if (video == null && requested < 0) { Request(current < 0 ? 0 : current); }
        timer.Start();
    }

    void Request(int index)
    {
        if (videos.Count == 0) { return; }
        requested = (index + videos.Count) % videos.Count;
        // Keep an existing Snoopy image covering the old player while another
        // poster loads. There is no mountain/background layer in this component.
        if (shownPoster >= 0) { posters[shownPoster].Opacity = 1; }
        uncovered = false;
        finished = false;
        clipStarted = clock.ElapsedMilliseconds;
    }

    void BeginRequested()
    {
        var index = requested;
        if (index < 0 || posters[index].LoadingStatus != ImageView.LoadingStatusType.Ready) { return; }
        ShowPoster(index);
        ReleaseVideo();
        current = index; requested = -1;
        if (!active || parent == null) { return; }
        try
        {
            video = new VideoView
            {
                Position2D = new Position2D(VideoLeft, VideoTop),
                Size2D = new Size2D(VideoWidth, VideoHeight),
                WidthResizePolicy = ResizePolicyType.Fixed,
                HeightResizePolicy = ResizePolicyType.Fixed,
                SizeScalePolicy = SizeScalePolicyType.FitWithAspectRatio,
                Underlay = false,
                Muted = true,
                Looping = true
            };
            // The movie remains fully visible underneath the opaque still so
            // DALi can prepare its texture before we uncover it.
            root.Add(video);
            video.LowerToBottom();
            video.Finished += OnFinished;
            video.ResourceUrl = videos[current];
            video.Muted = true;
            video.Looping = true;
            video.Play();
            clipStarted = clock.ElapsedMilliseconds;
            Write("ambient playing: " + Path.GetFileName(videos[current]));
        }
        catch (Exception error) { Write("ambient poster fallback: " + error.GetType().Name); ReleaseVideo(); }
    }

    bool Tick()
    {
        if (disposed) { return false; }
        if (!active || parent == null) { ReleaseVideo(); return false; }
        try
        {
            if (requested >= 0)
            {
                BeginRequested();
                // A bad still is skipped without uncovering an empty decoder.
                if (requested >= 0 && clock.ElapsedMilliseconds - clipStarted > 8000) { Request(requested + 1); }
                return true;
            }
            var elapsed = clock.ElapsedMilliseconds - clipStarted;
            if (video != null && !uncovered && elapsed >= 300 && PositionAdvanced())
            {
                posters[shownPoster].Opacity = 0;
                uncovered = true;
                Write("ambient poster uncovered after native playback advanced");
            }
            if (video != null && !uncovered && elapsed > 8000)
            {
                Write("ambient still retained: native playback did not confirm readiness");
                ReleaseVideo();
            }
            if (finished || elapsed >= 45000) { Request(current + 1); }
        }
        catch (Exception error) { Write("ambient fallback: " + error.GetType().Name); ReleaseVideo(); }
        return true;
    }

    bool PositionAdvanced()
    {
        if (nativeUnavailable || video == null) { return false; }
        try
        {
            using (var handle = video.NativeHandle)
            {
                if (handle == null || handle.IsInvalid) { return false; }
                int position;
                return player_get_play_position(handle.DangerousGetHandle(), out position) == 0 && position >= 150;
            }
        }
        catch (DllNotFoundException) { nativeUnavailable = true; Write("ambient native position API unavailable; still fallback kept"); }
        catch (EntryPointNotFoundException) { nativeUnavailable = true; Write("ambient native position symbol unavailable; still fallback kept"); }
        catch (Exception) { }
        return false;
    }

    void ShowPoster(int index)
    {
        for (var i = 0; i < posters.Count; i++) { posters[i].Opacity = i == index ? 1 : 0; }
        shownPoster = index;
    }

    void OnFinished(object sender, VideoView.FinishedEventArgs args) { finished = true; }

    void ReleaseVideo()
    {
        if (shownPoster >= 0) { posters[shownPoster].Opacity = 1; }
        uncovered = false;
        if (video == null) { return; }
        var previous = video; video = null;
        previous.Finished -= OnFinished;
        try { previous.Stop(); } catch (Exception) { }
        root.Remove(previous);
        previous.Dispose();
    }

    void Write(string message) { try { if (log != null) { log(message); } } catch (Exception) { } }

    public void Dispose()
    {
        if (disposed) { return; }
        SetActive(false);
        Detach();
        disposed = true;
        timer.Dispose();
        foreach (var poster in posters) { root.Remove(poster); poster.Dispose(); }
        root.Dispose();
    }
}
