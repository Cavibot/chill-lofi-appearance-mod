using BepInEx;
using HarmonyLib;
using UnityEngine;
using Bulbul;
using Bulbul.MasterData;
using System.Reflection;
using System;

namespace BulbulSystemMod
{
    [BepInPlugin("com.cavi.bulbul.systemfix", "Bulbul System Fix", "1.0.5")]
    public class Plugin : BaseUnityPlugin
    {
        internal static BepInEx.Logging.ManualLogSource Log;

        private void Awake()
        {
            Log = Logger;
            // 1. 语言设置 (保持不变)
            PlayerPrefs.SetInt("Language", 3);
            PlayerPrefs.SetString("Language", "ChineseSimplified");
            PlayerPrefs.Save();

            // 2. 注册补丁
            Harmony.CreateAndPatchAll(typeof(Plugin));
            Logger.LogInfo("插件 v1.0.5 加载：语言强制中文 + 全局音乐静音方案");
        }

        // =============================================================
        // 补丁 1: 拦截 SettingService (语言 + 音量)
        // =============================================================
        [HarmonyPatch(typeof(SettingService), "Setup")]
        [HarmonyPostfix]
        public static void SettingService_Setup_Postfix(SettingService __instance)
        {
             Plugin.Log.LogInfo("[SystemFix] 正在应用设置覆盖...");

            // --- A. 强制中文 ---
            __instance.ChangeGameLanguage((GameLanguageType)3);

            //// --- B. 强制静音 (针对 SettingData) ---
            //var save = SaveDataManager.Instance;
            //if (save != null && save.SettingData != null)
            //{
            //    // 1. 关闭 "启动游戏自动播放" (针对播放器)
            //    if (save.MusicSetting != null)
            //    {
            //        save.MusicSetting.IsGameStartPlayMusic = false;
            //    }

            //    // 2. 针对 BGM (背景音乐) 和 Music (播放器音乐) 开启静音
            //    // 注意：这里我们修改的是存档里的"静音开关"

            //    // MusicVolumeInfo = 播放器音量
            //    if (save.SettingData.MusicVolumeInfo != null)
            //        save.SettingData.MusicVolumeInfo.IsMute.Value = true;

            //    // AmbientBGMVolumeInfo = 环境背景乐音量 (这很可能是你要关的那个)
            //    if (save.SettingData.AmbientBGMVolumeInfo != null)
            //        save.SettingData.AmbientBGMVolumeInfo.IsMute.Value = true;

            //    // AmbientSEVolumeInfo = 环境音效 (风声/雨声等，如果需要关把下面这行注释取消)
            //    // if (save.SettingData.AmbientSEVolumeInfo != null) save.SettingData.AmbientSEVolumeInfo.IsMute.Value = true;

            //     Plugin.Log.LogInfo("[SystemFix] 已强制设置：启动时不播放 & 音量通道静音");
            //}
        }

        // =============================================================
        // 补丁 2: 拦截播放器逻辑 (双重保险)
        // =============================================================
        // 依然保留这个拦截，防止播放器那一侧“偷跑”
        [HarmonyPatch(typeof(FacilityMusic), "UnPauseMusic")]
        [HarmonyPrefix]
        public static bool UnPauseMusic_Prefix()
        {
            // 只要调用 UnPauseMusic，我们检查一下是否允许
            // 这里我们直接永久拦截“自动播放”，只允许玩家手动点击(因为手动点击通常不走UnPause，而是Play)
            // 或者简单点：如果此时 IsGameStartPlayMusic 为 false，就不让它 UnPause

            if (SaveDataManager.Instance != null &&
                SaveDataManager.Instance.MusicSetting != null &&
                !SaveDataManager.Instance.MusicSetting.IsGameStartPlayMusic)
            {
                return false; // 拦截！
            }
            return true;
        }

        // =============================================================
        // 补丁 3: 暴力查找并暂停 AmbientBGMManager (如果存在)
        // =============================================================
        // 在 RoomGameManager 初始化完成后，尝试找到环境音管理器并暂停
        [HarmonyPatch(typeof(RoomGameManager), "Initialize")]
        [HarmonyPostfix]
        public static void RoomGameManager_Init_Postfix()
        {
            // 尝试找 AmbientBGMManager
            // 因为它是 SingletonMonoBehaviour，通常挂在一个 DontDestroyOnLoad 物体上
            // 我们尝试用反射或者 Find 找到它

            var allComponents = Resources.FindObjectsOfTypeAll<MonoBehaviour>();
            foreach (var comp in allComponents)
            {
                if (comp.name.Contains("AmbientBGMManager") || comp.GetType().Name.Contains("AmbientBGMManager"))
                {
                     Plugin.Log.LogInfo($"[SystemFix] 找到环境音管理器: {comp.name}，尝试暂停...");

                    // 尝试调用 Pause 方法
                    var pauseMethod = comp.GetType().GetMethod("Pause", BindingFlags.Public | BindingFlags.Instance);
                    if (pauseMethod != null)
                    {
                        pauseMethod.Invoke(comp, null);
                         Plugin.Log.LogInfo("[SystemFix] AmbientBGMManager.Pause() 调用成功！");
                    }
                }
            }
        }
    }
}