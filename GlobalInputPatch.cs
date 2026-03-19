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
        if (inputEvent is not InputEventKey keyEvent || !keyEvent.Pressed || keyEvent.Echo)
            return;

        if (keyEvent.Keycode == Key.U)
            HandleMapDrawing(__instance, keyEvent);
    }

    private static void HandleMapDrawing(NGame instance, InputEventKey keyEvent)
    {
        var drawingsNode = instance.GetTree().Root.FindChild("Drawings", true, false) as NMapDrawings;
        if (drawingsNode == null)
        {
            GD.Print("[BadApple] 未找到地图节点，请确保地图已打开！");
            return;
        }

        var drawViewport = drawingsNode.FindChild("DrawViewport", true, false) as SubViewport;
        if (drawViewport == null)
        {
            GD.PushError("[BadApple] 未找到 DrawViewport！");
            return;
        }

        var scanner = drawViewport.GetNodeOrNull<BadApplePixelScanner>("BadAppleScanner");

        if (keyEvent.IsCommandOrControlPressed())
        {
            scanner?.TogglePause();
            return;
        }

        if (keyEvent.ShiftPressed)
        {
            scanner?.ToggleMute();
            return;
        }

        // Toggle: playing → stop, stopped → start
        if (scanner != null && scanner.IsActive)
        {
            scanner.Stop();
        }
        else
        {
            if (scanner == null)
            {
                scanner = new BadApplePixelScanner();
                scanner.Name = "BadAppleScanner";
                scanner.Initialize(drawingsNode);
                drawViewport.AddChild(scanner);
            }
            scanner.Restart();
        }
    }
}
