using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;

namespace BadApple;

[ModInitializer(nameof(Initialize))]
public class BadAppleMain
{
    public static void Initialize()
    {
        GD.Print("[BadApple] === Mod 开始初始化 ===");

        // 检查 PCK 是否加载
        if (Godot.FileAccess.FileExists("res://mods/BadAppleMod/BadAppleMod.pck"))
        {
            ProjectSettings.LoadResourcePack("res://mods/BadAppleMod/BadAppleMod.pck");
            GD.Print("[BadApple] PCK 资源加载成功");
        }

        try {
            Harmony harmony = new("com.leddele.badapple");
            harmony.PatchAll();
            GD.Print("[BadApple] Harmony 补丁注入成功");
        } catch (System.Exception e) {
            GD.PushError("[BadApple] Harmony 注入失败: " + e.Message);
        }
    }
}