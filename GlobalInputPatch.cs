using HarmonyLib;
using Godot;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;

namespace BadApple.Patches;

[HarmonyPatch(typeof(NGame), "_Input")]
public static class GlobalInputPatch
{
    public static void Postfix(NGame __instance, InputEvent inputEvent)
    {
        if (inputEvent is InputEventKey keyEvent && keyEvent.Pressed)
        {
           
            if (keyEvent.Keycode == Key.H || keyEvent.Keycode == Key.J)
            {
                var existing = __instance.GetNodeOrNull<BadApplePlayer>("BadAppleLayer");
                if (existing != null) existing.QueueFree();
                var player = new BadApplePlayer();
                player.Name = "BadAppleLayer";
                player.IsSmallMode = (keyEvent.Keycode == Key.J);
                __instance.AddChild(player);
            }
            
            // U: 开启地图影绘
            else if (keyEvent.Keycode == Key.U)
            {
                GD.Print("[BadApple] 检测到 U 键，开始递归搜索绘图系统...");

                // 找到 Drawings 节点
                var drawingsNode = __instance.GetTree().Root.FindChild("Drawings", true, false) as NMapDrawings;

                if (drawingsNode != null)
                {
                    //使用 FindChild 递归寻找 DrawViewport
                    // 因为它嵌套在 _playerDrawingPath 生成的子节点里
                    var drawViewport = drawingsNode.FindChild("DrawViewport", true, false) as SubViewport;

                    if (drawViewport != null)
                    {
                        GD.Print("[BadApple] 成功进入墨水渲染层 (DrawViewport)！");
                        
                        var scanner = drawViewport.GetNodeOrNull<BadApplePixelScanner>("BadAppleScanner");
                        if (scanner == null)
                        {
                            scanner = new BadApplePixelScanner();
                            scanner.Name = "BadAppleScanner";
                            scanner.Initialize(drawingsNode);
                            drawViewport.AddChild(scanner);
                        }
                        scanner.ToggleActive();
                    }
                    else
                    {
                
                        GD.PushError("[BadApple] 递归搜索失败！当前 Drawings 下的所有子节点如下：");
                        foreach(var child in drawingsNode.GetChildren()) {
                            GD.Print(" - " + child.Name + " (Type: " + child.GetType().Name + ")");
                        }
                    }
                }
                else
                {
                    GD.Print("[BadApple] 未找到地图节点，请确保地图已打开！");
                }
            }
        }
    }
}