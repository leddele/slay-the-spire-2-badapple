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
            // U: 重播（从头开始） | Ctrl+U: 暂停/继续
            if (keyEvent.Keycode == Key.U)
            {
                var drawingsNode = __instance.GetTree().Root.FindChild("Drawings", true, false) as NMapDrawings;

                if (drawingsNode != null)
                {
                    var drawViewport = drawingsNode.FindChild("DrawViewport", true, false) as SubViewport;

                    if (drawViewport != null)
                    {
                        var scanner = drawViewport.GetNodeOrNull<BadApplePixelScanner>("BadAppleScanner");
                        if (scanner == null)
                        {
                            scanner = new BadApplePixelScanner();
                            scanner.Name = "BadAppleScanner";
                            scanner.Initialize(drawingsNode);
                            drawViewport.AddChild(scanner);
                        }

                        if (keyEvent.IsCommandOrControlPressed())
                            scanner.TogglePause();
                        else
                            scanner.Restart();
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
