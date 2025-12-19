using BepInEx;
using Bulbul;
using Cavi.AppearanceMod.Components;
using Cavi.AppearanceMod.Patches;
using Cavi.AppearanceMod.Utils; // 添加这行
using HarmonyLib;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Cavi.AppearanceMod
{
    [BepInPlugin("com.cavi.bulbulmod", "Eku Skin Mod", "1.0.4")]
    public class AppearancePlugin : BaseUnityPlugin
    {
        public static bool ENABLE_GLASSES = false;
        public const string MY_BODY_MESH_NAME = "Face";


        // 保留这个用于向后兼容，但推荐使用 ModLogger
        internal static BepInEx.Logging.ManualLogSource Log;
        public static AssetBundle myBundle;
        public static GameObject myCustomPrefab;
        public static bool _hasLoaded = false;

        private void Awake()
        {
            Log = Logger;
            ModLogger.Initialize(Logger); // 初始化统一日志

            string modFolder = Path.Combine(Paths.PluginPath, "EkuSkinMod");
            string bundlePath = Path.Combine(modFolder, "assets");
            string configPath = Path.Combine(modFolder, "config.txt");

            LoadConfig(configPath);

            if (!File.Exists(bundlePath))
            {
                ModLogger.Error($"缺失 AssetBundle文件: {bundlePath}");
                return;
            }

            myBundle = AssetBundle.LoadFromFile(bundlePath);
            if (myBundle == null)
            {
                ModLogger.Error("AssetBundle 加载失败！");
                return;
            }

            myCustomPrefab = myBundle.LoadAsset<GameObject>("Eku_Release");
            if (myCustomPrefab == null)
            {
                ModLogger.Error("找不到预制体 'Eku_Release'！");
                return;
            }

            // Harmony.CreateAndPatchAll(typeof(CharacterPatches));
            Harmony.CreateAndPatchAll(typeof(CharacterPatches.RoomManagerPatch));
            ModLogger.Info("插件启动成功，等待角色生成...");
        }

        private void LoadConfig(string configPath)
        {
            try
            {
                if (File.Exists(configPath))
                {
                    string[] lines = File.ReadAllLines(configPath);
                    foreach (string line in lines)
                    {
                        string trimmed = line.Trim();
                        if (trimmed.StartsWith("#") || string.IsNullOrWhiteSpace(trimmed))
                            continue;

                        if (trimmed.StartsWith("ENABLE_GLASSES="))
                        {
                            string value = trimmed.Substring("ENABLE_GLASSES=".Length).Trim().ToLower();
                            ENABLE_GLASSES = value == "true" || value == "1";
                            ModLogger.LogConfig($"眼镜设置: {ENABLE_GLASSES}");
                        }
                    }
                }
                else
                {
                    // 如果配置文件不存在，创建一个默认的
                    string defaultConfig = "# Eku Skin Mod 配置文件\n" +
                                         "# 是否显示眼镜 (true=显示, false=隐藏)\n" +
                                         "ENABLE_GLASSES=false";
                    File.WriteAllText(configPath, defaultConfig);
                    ModLogger.LogConfig($"已创建默认配置文件: {configPath}");
                }
            }
            catch (System.Exception ex)
            {
                ModLogger.Warning($"读取配置文件失败，使用默认值: {ex.Message}");
            }
        }

        void Update()
        {
            if (_hasLoaded) return;

            // 1. 先找到最外层的 Character
            GameObject root = GameObject.Find("Character");
            if (root != null)
            {
                // 2. 递归查找名字包含 "Hips" 的物体
                // 这里的 true 表示即使物体是隐藏(Inactive)状态也要找，以防万一
                Transform hips = root.GetComponentsInChildren<Transform>(true)
                                     .FirstOrDefault(t => t.name == "Character_Hips");

                if (hips != null)
                {
                    string path = hips.name;
                    while (hips.parent != null)
                    {
                        hips = hips.parent;
                        path = hips.name + "/" + path;
                    }

                    ModLogger.Info($"路径: {path}");
                    CharacterPatches.ReplaceHeroineModel(root);
                }
            }
        }
    }
}