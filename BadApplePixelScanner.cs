using Godot;
using System;
using System.Linq;
using System.Reflection;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Assets;

namespace BadApple.Patches;

public partial class BadApplePixelScanner : Node2D
{
    private VideoStreamPlayer _vsp = null!;
    private SubViewport _viewport = null!;
    private Node _drawingNode = null!;
    private Color _drawColor = Colors.White;
    private bool _isActive = false;

    
    private Vector2I _renderRes = new Vector2I(600, 450);
    private float _pixelSize = 1.1f; 
    private Vector2 _manualOffset = new Vector2(100, 100); 

    public void Initialize(Node drawingNode)
    {
        _drawingNode = drawingNode;
        
        // 获取画笔颜色
        try {
            var nRunInstance = NRun.Instance;
            if (nRunInstance != null) {
                FieldInfo stateField = typeof(NRun).GetField("_state", BindingFlags.NonPublic | BindingFlags.Instance);
                dynamic? runState = stateField?.GetValue(nRunInstance);
                if (runState != null) {
                    var players = runState.Players;
                    if (players != null && players.Count > 0)
                        _drawColor = players[0].Character.MapDrawingColor;
                }
            }
        } catch { _drawColor = new Color(1, 1, 1, 1f); }

        _viewport = new SubViewport();
        _viewport.Size = _renderRes;
        _viewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Always;
        AddChild(_viewport);

        _vsp = new VideoStreamPlayer();
        _vsp.Stream = GD.Load<VideoStream>("res://video/bad_apple.ogv");
        _vsp.Expand = true;
        _vsp.Size = _renderRes;
        _viewport.AddChild(_vsp);

        this.Visible = false;
    }

    public void ToggleActive()
    {
        _isActive = !_isActive;
        if (_isActive) 
        { 
            _vsp.Play(); 
            this.Visible = true; 
            GD.Print("[BadApple] 开启实时影绘");
        }
        else 
        { 
         
            BakeCurrentFrameToMap();
            _vsp.Stop(); 
            this.Visible = false; 
            GD.Print("[BadApple] 影绘已保存至地图层");
        }
    }

    private void BakeCurrentFrameToMap()
    {
        Image frame = _viewport.GetTexture().GetImage();
        if (frame == null) return;

        // 1. 创建墨水贴图
        Image inkImg = Image.Create(_renderRes.X, _renderRes.Y, false, Image.Format.Rgba8);
        for (int y = 0; y < _renderRes.Y; y++) {
            for (int x = 0; x < _renderRes.X; x++) {
                if (frame.GetPixel(x, y).Luminance > 0.45f) {
                    inkImg.SetPixel(x, y, _drawColor);
                } else {
                    inkImg.SetPixel(x, y, new Color(0,0,0,0));
                }
            }
        }

        ImageTexture tex = ImageTexture.CreateFromImage(inkImg);
        Sprite2D bakedSprite = new Sprite2D();
        bakedSprite.Texture = tex;
        bakedSprite.Centered = false;
        bakedSprite.Position = _manualOffset;
        bakedSprite.Scale = new Vector2(_pixelSize, _pixelSize);
        
        // 确保名字带有前缀，方便“一键清除”识别
        // 使用随机后缀防止重复 ID 冲突
        bakedSprite.Name = "BakedBadAppleFrame_" + Time.GetTicksMsec();

        // 寻找正确的注入点DrawViewport
 
        var drawViewport = _drawingNode.FindChild("DrawViewport", true, false);
        if (drawViewport != null) {
         
            var sampleLine = drawViewport.GetChildren().OfType<Line2D>().FirstOrDefault();
            if (sampleLine != null) {
                bakedSprite.Material = sampleLine.Material;
            }
            
            // 挂载到墨水层
            drawViewport.AddChild(bakedSprite);
            GD.Print("[BadApple] 画面已成功注入 DrawViewport");
        }
    }

    public override void _Process(double delta)
    {
        if (_isActive && _vsp.IsPlaying()) QueueRedraw();
    }

    public override void _Draw()
    {
        if (!_isActive) return;
        Image frame = _viewport.GetTexture().GetImage();
        if (frame == null) return;

        // 使用 drawSize 略大于步长，实现像素融合
        float drawSize = _pixelSize * 1.15f; 
        for (int y = 0; y < _renderRes.Y; y++) {
            for (int x = 0; x < _renderRes.X; x++) {
                if (frame.GetPixel(x, y).Luminance > 0.45f) {
                    Vector2 drawPos = _manualOffset + new Vector2(x, y) * _pixelSize;
                    DrawRect(new Rect2(drawPos, new Vector2(drawSize, drawSize)), _drawColor);
                }
            }
        }
    }
}