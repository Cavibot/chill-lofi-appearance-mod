using BepInEx;
using Bulbul; // 引用游戏原本的命名空间
using HarmonyLib;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace MyCharacterMod
{
  
    public static class Hooks
    {
        // 方案 A: 挂钩 RoomGameManager 的 Initialize 方法
        // 因为这是 Private 方法，所以 HarmonyPatch 写法要稍微注意
        [HarmonyPatch(typeof(RoomGameManager), "Initialize")]
        public static class RoomManagerPatch
        {
            [HarmonyPostfix]
            public static void Postfix(RoomGameManager __instance)
            {
                Plugin.Log.LogInfo("【Mod日志】检测到 RoomGameManager 初始化完成！尝试寻找角色...");

                // 方法 1: 暴力查找场景里的 Character 物体
                // 根据你的截图，物体名字叫 "Character"
                GameObject characterObj = GameObject.Find("Character");

                if (characterObj != null)
                {
                    Plugin.Log.LogInfo($"【Mod日志】通过名字找到了: {characterObj.name}");
                    ReplaceHeroineModel(characterObj);
                }
                else
                {
                    // 方法 2: 如果改名了，通过反射获取 _heroineService 字段
                    Plugin.Log.LogInfo("【Mod日志】没找到 Character 物体，尝试通过反射获取 Service...");

                    // 获取 private 字段 _heroineService
                    var fieldInfo = typeof(RoomGameManager).GetField("_heroineService", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (fieldInfo != null)
                    {
                        var service = fieldInfo.GetValue(__instance) as MonoBehaviour; // 假设 Service 是 MonoBehaviour
                        if (service != null)
                        {
                            ReplaceHeroineModel(service.gameObject);
                        }
                    }
                }
            }
        }

        // 这里放之前的 ReplaceHeroineModel 方法逻辑...
        public static void ReplaceHeroineModel(GameObject gameCharacterRoot)
        {
            if (Plugin.myCustomPrefab == null) return;
            Plugin.Log.LogInfo("【Mod日志】开始执行模型替换逻辑...");

            var originalSMRs = gameCharacterRoot.GetComponentsInChildren<SkinnedMeshRenderer>();
            foreach (var smr in originalSMRs) smr.enabled = false;

            GameObject modInstance = Object.Instantiate(Plugin.myCustomPrefab);
            var mySMRs = modInstance.GetComponentsInChildren<SkinnedMeshRenderer>();
            Transform gameRootBone = FindChildRecursive(gameCharacterRoot.transform, "Character_Hips");

            if (gameRootBone == null)
            {
                Plugin.Log.LogInfo("【Mod错误】找不到 Character_Hips，请检查原版骨骼层级！");
                return;
            }

            foreach (var mySMR in mySMRs)
            {
                GameObject newPart = new GameObject(mySMR.name + "_Mod");
                newPart.transform.SetParent(gameCharacterRoot.transform, false);

                SkinnedMeshRenderer newSMR = newPart.AddComponent<SkinnedMeshRenderer>();
                newSMR.sharedMesh = mySMR.sharedMesh;
                newSMR.materials = mySMR.materials;

                // 简单修复 Shader
                Shader toonShader = Shader.Find("Universal Render Pipeline/Lit");
                if (toonShader != null)
                {
                    foreach (var mat in newSMR.materials) mat.shader = toonShader;
                }

                // 骨骼重映射
                Transform[] newBones = new Transform[mySMR.bones.Length];
                for (int i = 0; i < mySMR.bones.Length; i++)
                {
                    string boneName = mySMR.bones[i].name;
                    Transform foundBone = FindChildRecursive(gameCharacterRoot.transform, boneName);
                    newBones[i] = foundBone != null ? foundBone : gameRootBone;
                }
                newSMR.bones = newBones;
                newSMR.rootBone = gameRootBone;
            }
            Object.Destroy(modInstance);
            Plugin.Log.LogInfo("【Mod日志】替换流程结束！");

        }

        private static Transform FindChildRecursive(Transform parent, string name)
        {
            if (parent.name == name) return parent;
            foreach (Transform child in parent)
            {
                var result = FindChildRecursive(child, name);
                if (result != null) return result;
            }
            return null;
        }
    }


    // 请确保 BepInPlugin 的 GUID 是唯一的
    [BepInPlugin("com.yourname.bulbulmod", "My Heroine Mod", "1.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        internal static BepInEx.Logging.ManualLogSource Log;

        public static AssetBundle myBundle;
        public static GameObject myCustomPrefab;
        private bool _hasLoaded = false;

        void Update()
        {
            if (_hasLoaded) return;

            // 每帧检查有没有叫 Character 的物体
            GameObject target = GameObject.Find("Character");
            if (target != null)
            {
                Plugin.Log.LogInfo("【Mod日志】在 Update 中找到了 Character 物体，准备替换模型...");
                Logger.LogInfo("Found Character object in scene.");
                // 还要确保它已经初始化好了（例如检查有没有 Character_Hips）
                if (target.transform.Find("Character/Character_Hips") != null) // 根据你的截图层级调整
                {
                    Hooks.ReplaceHeroineModel(target); // 调用上面的静态替换方法
                    _hasLoaded = true; // 标记已加载，防止重复执行
                }
            }  
        }

        private void Awake()
        {
            Log = Logger;
            // 1. 加载 AssetBundle (请修改为你的实际文件名)
            string bundlePath = Path.Combine(Paths.PluginPath, "MySkinMod", "manuka_skin");
            myBundle = AssetBundle.LoadFromFile(bundlePath);

            if (myBundle == null)
            {
                Logger.LogError("【Mod错误】找不到 AssetBundle！请确认文件路径。");
                return;
            }

            // 加载你的预制体 (名字要是你打包时的Prefab名字)
            myCustomPrefab = myBundle.LoadAsset<GameObject>("MyAvatar");

            // 2. 启动 Harmony 挂钩
            Harmony.CreateAndPatchAll(typeof(Hooks));
            Logger.LogInfo("插件启动成功，等待角色生成...");
        }
    }
   
}