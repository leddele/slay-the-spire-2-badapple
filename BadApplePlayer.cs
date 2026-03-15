using Godot;

namespace BadApple;

public partial class BadApplePlayer : CanvasLayer
{
    private VideoStreamPlayer _vsp;
    public bool IsSmallMode = false; // 是否为小窗模式

    public override void _Ready()
    {
        Layer = 128; 

        _vsp = new VideoStreamPlayer();
        _vsp.Stream = GD.Load<VideoStream>("res://video/bad_apple.ogv");
        _vsp.Expand = true;
        
        if (IsSmallMode)
        {
          
            _vsp.CustomMinimumSize = new Vector2(480, 360);
            _vsp.Size = new Vector2(480, 360);
            
            // 设置位置：右下角
            // 获取屏幕尺寸并偏移
            Vector2 screenSize = GetViewport().GetVisibleRect().Size;
            _vsp.Position = new Vector2(screenSize.X - 500, screenSize.Y - 400);
            
            // 给小窗加个黑边背景（可选）
            var bg = new ColorRect();
            bg.Color = new Color(0, 0, 0, 1);
            bg.Size = _vsp.Size;
            bg.Position = _vsp.Position;
            AddChild(bg);
        }
        else
        {
            // 全屏模式
            _vsp.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        }
        
        AddChild(_vsp);
        _vsp.Play();
        _vsp.Finished += () => this.QueueFree();
    }

    public override void _Input(InputEvent inputEvent)
    {
       
        if (inputEvent is InputEventKey keyEvent && keyEvent.Pressed)
        {
            if (keyEvent.Keycode == Key.H || keyEvent.Keycode == Key.J)
            {
                this.QueueFree();
                GetViewport().SetInputAsHandled();
            }
        }
    }
}