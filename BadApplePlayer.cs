using Godot;

namespace BadApple;

/// <summary>
/// Plays Bad Apple video as a CanvasLayer overlay.
/// Does NOT use Godot lifecycle overrides (_Ready/_Input) because
/// Microsoft.NET.Sdk builds lack the Godot source generator.
/// Call Setup() after AddChild().
/// </summary>
public class BadApplePlayer : CanvasLayer
{
    public bool IsSmallMode = false;

    /// <summary>
    /// Must be called after the node is added to the scene tree.
    /// Replaces _Ready() which won't fire without the source generator.
    /// </summary>
    public void Setup()
    {
        Layer = 128;

        var vsp = new VideoStreamPlayer();
        vsp.Stream = GD.Load<VideoStream>("res://video/bad_apple.ogv");
        vsp.Expand = true;

        if (IsSmallMode)
        {
            vsp.CustomMinimumSize = new Vector2(480, 360);
            vsp.Size = new Vector2(480, 360);

            Vector2 screenSize = GetViewport().GetVisibleRect().Size;
            vsp.Position = new Vector2(screenSize.X - 500, screenSize.Y - 400);

            var bg = new ColorRect();
            bg.Color = new Color(0, 0, 0, 1);
            bg.Size = vsp.Size;
            bg.Position = vsp.Position;
            AddChild(bg);
        }
        else
        {
            vsp.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        }

        AddChild(vsp);
        vsp.Play();
        vsp.Finished += () => this.QueueFree();
    }
}
