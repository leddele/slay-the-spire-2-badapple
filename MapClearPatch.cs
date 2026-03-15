using HarmonyLib;
using Godot;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using System.Linq;

namespace BadApple.Patches;


[HarmonyPatch(typeof(NMapDrawings))]
public static class MapClearBakedApplePatch
{
    // 钩子 1：清除所有人的线
    [HarmonyPatch("ClearAllLines")]
    [HarmonyPostfix]
    public static void PostfixAll(NMapDrawings __instance)
    {
        ClearOurSprites(__instance);
    }

    // 钩子 2：清除特定玩家的线（本地清除按钮主要调这个）
    [HarmonyPatch("ClearAllLinesForPlayer")]
    [HarmonyPostfix]
    public static void PostfixPlayer(NMapDrawings __instance)
    {
        ClearOurSprites(__instance);
    }

    private static void ClearOurSprites(NMapDrawings instance)
    {
        // 递归寻找墨水层
        var drawViewport = instance.FindChild("DrawViewport", true, false) as SubViewport;
        if (drawViewport != null)
        {
            foreach (Node child in drawViewport.GetChildren())
            {
            
                if (child.Name.ToString().Contains("BakedBadAppleFrame"))
                {
                    child.QueueFree();
                }
            }
        }
    }
}