using Godot;
using System;
using System.Collections.Generic;
using System.Reflection;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Multiplayer.Game.PeerInput;
using MegaCrit.Sts2.Core.Multiplayer.Messages.Game.Flavor;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Runs;

namespace BadApple.Patches;

/// <summary>
/// Continuous Bad Apple on the map.
/// LOCAL:  lightweight Sprite2D preview (no lag).
/// REMOTE: sends MapDrawingMessage directly (no local Line2D creation).
///
/// Controls:
///   U       = 重播（从头开始）
///   Ctrl+U  = 暂停 / 继续
///   Shift+U = 切换静音（默认静音）
/// </summary>
public class BadApplePixelScanner : Node2D
{
    private VideoStreamPlayer _vsp = null!;
    private SubViewport _viewport = null!;
    private NMapDrawings _mapDrawings = null!;
    private Sprite2D _previewSprite = null!;
    private ImageTexture? _previewTex;

    private bool _isActive;
    public bool IsActive => _isActive;
    private bool _isPaused;
    private bool _isMuted = true;
    private bool _signalConnected;
    private ulong _lastDrawMsec;

    private readonly Vector2I _renderRes = new(160, 120);
    private const float PixelScale = 2.0f;
    private Vector2 _drawOffset = new(60, 40);
    private const float LuminanceThreshold = 0.45f;
    private const ulong FrameIntervalMs = 150;
    private const int LineDensity = 2;  // 每行像素画几条线（填满行间间隙）

    private Color _drawColor = Colors.White;

    public void Initialize(NMapDrawings drawings)
    {
        _mapDrawings = drawings;

        try
        {
            var nsField = typeof(NMapDrawings).GetField("_netService",
                BindingFlags.NonPublic | BindingFlags.Instance);
            var pcField = typeof(NMapDrawings).GetField("_playerCollection",
                BindingFlags.NonPublic | BindingFlags.Instance);
            dynamic? ns = nsField?.GetValue(drawings);
            dynamic? pc = pcField?.GetValue(drawings);
            if (ns != null && pc != null)
            {
                var player = pc.GetPlayer((ulong)ns.NetId);
                if (player != null)
                    _drawColor = player.Character.MapDrawingColor;
            }
            GD.Print($"[BadApple] 画笔颜色: {_drawColor}");
        }
        catch (System.Exception ex)
        {
            GD.Print($"[BadApple] 取色失败: {ex.Message}");
            _drawColor = Colors.White;
        }

        _viewport = new SubViewport();
        _viewport.Name = "BadAppleVideoViewport";
        _viewport.Size = _renderRes;
        _viewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Always;
        NGame.Instance.AddChild(_viewport);

        _vsp = new VideoStreamPlayer();
        _vsp.Stream = LoadVideo();
        _vsp.Expand = true;
        _vsp.Size = _renderRes;
        _viewport.AddChild(_vsp);

        _previewSprite = new Sprite2D();
        _previewSprite.Centered = false;
        _previewSprite.Position = _drawOffset;
        _previewSprite.Scale = new Vector2(PixelScale, PixelScale);
        _previewSprite.Visible = false;
        AddChild(_previewSprite);

        Visible = true;
    }

    // ================================================================
    //  Public API: Restart / TogglePause
    // ================================================================

    /// <summary>
    /// U 键：从头重播。无论当前状态（未开始 / 播放中 / 暂停），都重置到开头重新播放。
    /// </summary>
    public void Restart()
    {
        _vsp.Stop();
        _vsp.Paused = false;
        _vsp.VolumeDb = _isMuted ? -80.0f : 0.0f;
        _vsp.Play();

        _isActive = true;
        _isPaused = false;
        _previewSprite.Visible = true;
        EnsureSignalConnected();
        GD.Print("[BadApple] 从头播放");
    }

    /// <summary>
    /// Ctrl+U：暂停 / 继续。仅在播放中有效。
    /// </summary>
    public void TogglePause()
    {
        if (!_isActive) return;

        _isPaused = !_isPaused;
        _vsp.Paused = _isPaused;

        if (_isPaused)
            GD.Print("[BadApple] 已暂停");
        else
            GD.Print("[BadApple] 继续播放");
    }

    public void ToggleMute()
    {
        if (!_isActive) return;

        _isMuted = !_isMuted;
        _vsp.VolumeDb = _isMuted ? -80.0f : 0.0f;
        GD.Print($"[BadApple] {(_isMuted ? "已静音" : "已开启声音")}");
    }

    /// <summary>
    /// 完全停止播放（视频 + 音频 + 预览）。
    /// </summary>
    public void Stop()
    {
        _isActive = false;
        _isPaused = false;
        _vsp.Paused = false;
        _vsp.Stop();
        _previewSprite.Visible = false;
        GD.Print("[BadApple] 已停止");
    }

    // ================================================================
    //  Frame loop
    // ================================================================

    private void EnsureSignalConnected()
    {
        if (_signalConnected) return;
        var tree = GetTree();
        if (tree != null)
        {
            tree.ProcessFrame += OnProcessFrame;
            _signalConnected = true;
        }
    }

    private void OnProcessFrame()
    {
        if (!IsInsideTree())
        {
            if (_signalConnected) { GetTree().ProcessFrame -= OnProcessFrame; _signalConnected = false; }
            if (_viewport is { } vp && vp.IsInsideTree()) vp.QueueFree();
            return;
        }
        if (!_isActive) return;
        if (_isPaused) return;

        // Video finished naturally
        if (!_vsp.IsPlaying())
        {
            _isActive = false;
            _previewSprite.Visible = false;
            SendClearToNetwork();
            GD.Print("[BadApple] 播放结束");
            return;
        }

        UpdatePreviewTexture();

        ulong now = Time.GetTicksMsec();
        if (now - _lastDrawMsec < FrameIntervalMs) return;
        _lastDrawMsec = now;

        SendFrameToNetwork();
    }

    // ================================================================
    //  Network-only drawing (no local Line2D — avoids lag)
    // ================================================================

    private void SendClearToNetwork()
    {
        var ns = RunManager.Instance?.NetService;
        if (ns == null || ns.Type == NetGameType.Singleplayer) return;
        ns.SendMessage(default(ClearMapDrawingsMessage));
    }

    private void SendFrameToNetwork()
    {
        var ns = RunManager.Instance?.NetService;
        if (ns == null || ns.Type == NetGameType.Singleplayer) return;

        Image? frame = _viewport.GetTexture().GetImage();
        if (frame == null) return;

        // Build segments — each pixel row emits LineDensity sub-lines to fill gaps
        var segments = new List<(Vector2 start, Vector2 end)>();
        float subStep = PixelScale / LineDensity;
        for (int y = 0; y < _renderRes.Y; y++)
        {
            int? runStart = null;
            for (int x = 0; x < _renderRes.X; x++)
            {
                if (frame.GetPixel(x, y).Luminance > LuminanceThreshold)
                    runStart ??= x;
                else if (runStart.HasValue)
                {
                    for (int sub = 0; sub < LineDensity; sub++)
                        AddSegment(segments, runStart.Value, x, y * PixelScale + sub * subStep);
                    runStart = null;
                }
            }
            if (runStart.HasValue)
                for (int sub = 0; sub < LineDensity; sub++)
                    AddSegment(segments, runStart.Value, _renderRes.X, y * PixelScale + sub * subStep);
        }
        if (segments.Count == 0) return;

        // Clear previous frame on remote
        ns.SendMessage(default(ClearMapDrawingsMessage));

        // Send drawing events directly — no local Line2D created
        var msg = new MapDrawingMessage();
        msg.drawingMode = DrawingMode.Drawing;

        foreach (var (start, end) in segments)
        {
            SendEvent(ns, ref msg, new NetMapDrawingEvent
            {
                type = MapDrawingEventType.BeginLine,
                position = ToNetPos(start),
                overrideDrawingMode = DrawingMode.Drawing
            });
            SendEvent(ns, ref msg, new NetMapDrawingEvent
            {
                type = MapDrawingEventType.ContinueLine,
                position = ToNetPos(end),
                overrideDrawingMode = DrawingMode.Drawing
            });
            SendEvent(ns, ref msg, new NetMapDrawingEvent
            {
                type = MapDrawingEventType.EndLine
            });
        }

        // Flush remaining
        if (msg.Events.Count > 0)
        {
            msg.drawingMode = DrawingMode.Drawing;
            ns.SendMessage(msg);
        }
    }

    private void SendEvent(INetGameService ns, ref MapDrawingMessage msg, NetMapDrawingEvent ev)
    {
        if (!msg.TryAddEvent(ev))
        {
            msg.drawingMode = DrawingMode.Drawing;
            ns.SendMessage(msg);
            msg = new MapDrawingMessage();
            msg.TryAddEvent(ev);
        }
    }

    private Vector2 ToNetPos(Vector2 pos)
    {
        var size = _mapDrawings.Size;
        pos.X -= size.X * 0.5f;
        pos /= new Vector2(960f, size.Y);
        return pos;
    }

    private void AddSegment(List<(Vector2, Vector2)> list, int x1, int x2, float drawY)
    {
        Vector2 start = (_drawOffset + new Vector2(x1 * PixelScale, drawY)) * 2f;
        Vector2 end   = (_drawOffset + new Vector2(x2 * PixelScale, drawY)) * 2f;
        list.Add((start, end));
    }

    // ================================================================
    //  Local preview only (lightweight)
    // ================================================================

    private void UpdatePreviewTexture()
    {
        Image? frame = _viewport.GetTexture().GetImage();
        if (frame == null) return;

        Image preview = Image.CreateEmpty(_renderRes.X, _renderRes.Y, false, Image.Format.Rgba8);
        var transparent = new Color(0, 0, 0, 0);

        for (int y = 0; y < _renderRes.Y; y++)
            for (int x = 0; x < _renderRes.X; x++)
                preview.SetPixel(x, y,
                    frame.GetPixel(x, y).Luminance > LuminanceThreshold
                        ? _drawColor : transparent);

        if (_previewTex == null)
        {
            _previewTex = ImageTexture.CreateFromImage(preview);
            _previewSprite.Texture = _previewTex;
        }
        else
            _previewTex.Update(preview);
    }

    // ================================================================
    //  Video loading: mod folder first, then PCK fallback
    // ================================================================

    internal static VideoStream? LoadVideo()
    {
        try
        {
            string dllDir = System.IO.Path.GetDirectoryName(
                typeof(BadApplePixelScanner).Assembly.Location)!;

            foreach (var file in System.IO.Directory.GetFiles(dllDir, "*.ogv"))
            {
                var stream = ResourceLoader.Load<VideoStream>(
                    ProjectSettings.LocalizePath(file));
                if (stream != null)
                {
                    GD.Print($"[BadApple] 从本地加载视频: {file}");
                    return stream;
                }
            }

            foreach (var file in System.IO.Directory.GetFiles(dllDir, "*.ogv"))
            {
                var stream = ResourceLoader.Load<VideoStream>(file);
                if (stream != null)
                {
                    GD.Print($"[BadApple] 从本地加载视频(abs): {file}");
                    return stream;
                }
            }
        }
        catch (System.Exception ex)
        {
            GD.Print($"[BadApple] 本地视频扫描失败: {ex.Message}");
        }

        var pckStream = GD.Load<VideoStream>("res://video/bad_apple.ogv");
        if (pckStream != null)
            GD.Print("[BadApple] 从 PCK 加载视频");
        else
            GD.PushError("[BadApple] 未找到视频！请将 .ogv 文件放入 mods/BadAppleMod/ 目录");
        return pckStream;
    }
}
