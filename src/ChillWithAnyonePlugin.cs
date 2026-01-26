using BepInEx;
using Bulbul;
using Cavi.ChillWithAnyone.Components;
using Cavi.ChillWithAnyone.Patches;
using Cavi.ChillWithAnyone.Utils;
using HarmonyLib;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Cavi.ChillWithAnyone
{
    [BepInPlugin("com.cavi.chillwithanyone", "Chill With Anyone", "0.9.0")]
    public class ChillWithAnyonePlugin : BaseUnityPlugin
    {
        // Configuration 
        public static bool EnableGlasses { get; set; } = false;
        public const string BODY_MESH_NAME = "Face";

        // Assets
        public static AssetBundle CustomAssetBundle { get; private set; }
        public static GameObject CustomCharacterPrefab { get; private set; }

        // State
        private static bool s_isModelLoaded = false;
        public static bool IsModelLoaded => s_isModelLoaded;


        private void Awake()
        {
            ModLogger.Initialize(Logger);


            string modFolder = Path.Combine(Paths.PluginPath, "ChillWithAnyone");
            string bundlePath = Path.Combine(modFolder, "assets");
            string configPath = Path.Combine(modFolder, "config.txt");

            LoadConfiguration(configPath);
            LoadAssetBundle(bundlePath);

            if (CustomCharacterPrefab == null)
            {
                ModLogger.Error("Failed to initialize plugin: Missing prefab");
                return;
            }

            Harmony.CreateAndPatchAll(typeof(CharacterPatches.RoomManagerPatch));
            ModLogger.Info("Plugin initialized, waiting for character spawn");
        }

        private void LoadConfiguration(string configPath)
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
                            EnableGlasses = value == "true" || value == "1";
                            ModLogger.LogConfig($"Glasses enabled: {EnableGlasses}");
                        }
                    }
                }
                else
                {
                    CreateDefaultConfig(configPath);
                }
            }
            catch (System.Exception ex)
            {
                ModLogger.Warning($"Failed to load config, using defaults: {ex.Message}");
            }
        }

        private void CreateDefaultConfig(string configPath)
        {
            string defaultConfig = "启用眼镜（true=显示，false=隐藏）\n" +
                                    "# Chill With Anyone Configuration\n" +
                                 "# Enable glasses (true=show, false=hide)\n" +
                                 "ENABLE_GLASSES=false";
            File.WriteAllText(configPath, defaultConfig);
            ModLogger.LogConfig($"Created default config at: {configPath}");
        }

        private void LoadAssetBundle(string bundlePath)
        {
            if (!File.Exists(bundlePath))
            {
                ModLogger.Error($"AssetBundle not found: {bundlePath}");
                return;
            }

            CustomAssetBundle = AssetBundle.LoadFromFile(bundlePath);
            if (CustomAssetBundle == null)
            {
                ModLogger.Error("Failed to load AssetBundle");
                return;
            }

            CustomCharacterPrefab = CustomAssetBundle.LoadAsset<GameObject>("Mod_Character");
            if (CustomCharacterPrefab == null)
            {
                ModLogger.Error("Prefab 'Mod_Character' not found in AssetBundle");
            }
        }

        private void Update()
        {
            if (s_isModelLoaded) return;

            GameObject characterRoot = GameObject.Find("Character");
            if (characterRoot == null) return;

            Transform hips = characterRoot.GetComponentsInChildren<Transform>(true)
                                         .FirstOrDefault(t => t.name == "Character_Hips");

            if (hips != null)
            {
                ModLogger.Debug($"Found character at: {GetTransformPath(hips)}");
                CharacterPatches.ReplaceCharacterModel(characterRoot);
            }
        }

        private static string GetTransformPath(Transform transform)
        {
            string path = transform.name;
            while (transform.parent != null)
            {
                transform = transform.parent;
                path = transform.name + "/" + path;
            }
            return path;
        }

        public static void SetModelLoaded(bool loaded)
        {
            s_isModelLoaded = loaded;
        }
    }
}