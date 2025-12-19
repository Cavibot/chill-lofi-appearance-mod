using BepInEx;
using Bulbul;
using Cavi.AppearanceMod.Components;
using HarmonyLib;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace MyCharacterMod
{
    [BepInPlugin("com.cavi.bulbulmod", "Eku Skin Mod", "1.0.4")] // 版本号


    public class Plugin : BaseUnityPlugin
    {
        // ================= 配置区域 =================
        public static bool ENABLE_GLASSES = false;
        public const string MY_BODY_MESH_NAME = "Face";
        // ===========================================

        internal static BepInEx.Logging.ManualLogSource Log;
        public static AssetBundle myBundle;
        public static GameObject myCustomPrefab;

        // 改为静态，方便 Hooks 修改，防止 Update 重复运行
        public static bool _hasLoaded = false;

        private void Awake()
        {
            Log = Logger;
            string modFolder = Path.Combine(Paths.PluginPath, "EkuSkinMod");
            string bundlePath = Path.Combine(modFolder, "assets");
            string configPath = Path.Combine(modFolder, "config.txt");

            // 读取配置文件
            LoadConfig(configPath);

            if (!File.Exists(bundlePath))
            {
                Logger.LogError($"【Mod错误】缺失 AssetBundle文件: {bundlePath}");
                return;
            }

            myBundle = AssetBundle.LoadFromFile(bundlePath);
            if (myBundle == null)
            {
                Logger.LogError("【Mod错误】AssetBundle 加载失败！");
                return;
            }

            myCustomPrefab = myBundle.LoadAsset<GameObject>("Eku_Release");
            if (myCustomPrefab == null)
            {
                Logger.LogError("【Mod错误】找不到预制体 'Eku_Release'！");
                return;
            }

            Harmony.CreateAndPatchAll(typeof(Hooks));
            Logger.LogInfo("插件启动成功，等待角色生成...");
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
                            Logger.LogInfo($"【配置】眼镜设置: {ENABLE_GLASSES}");
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
                    Logger.LogInfo($"【配置】已创建默认配置文件: {configPath}");
                }
            }
            catch (System.Exception ex)
            {
                Logger.LogWarning($"【配置】读取配置文件失败，使用默认值: {ex.Message}");
            }
        }

        void Update()
        {
            Log = Logger;
            //Logger.LogInfo("【Mod日志】正在寻找...");
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

                    Plugin.Log.LogInfo("【Mod日志】路径: " + path);
                    Hooks.ReplaceHeroineModel(root);
                }
            }
        }
    }

    public static class Hooks
    {
        [HarmonyPatch(typeof(RoomGameManager), "Initialize")]
        public static class RoomManagerPatch
        {
            [HarmonyPostfix]
            public static void Postfix(RoomGameManager __instance)
            {
                // 如果已经加载过，就不再重复查找
                if (Plugin._hasLoaded) return;

                Plugin.Log.LogInfo("【Mod日志】RoomGameManager 初始化完成！");
                GameObject characterObj = GameObject.Find("Character");

                if (characterObj != null)
                {
                    ReplaceHeroineModel(characterObj);
                }
                else
                {
                    var fieldInfo = typeof(RoomGameManager).GetField("_heroineService", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (fieldInfo != null)
                    {
                        var service = fieldInfo.GetValue(__instance) as MonoBehaviour;
                        if (service != null)
                        {
                            ReplaceHeroineModel(service.gameObject);
                        }
                    }
                }
            }
        }

        public static void ReplaceHeroineModel(GameObject gameCharacterRoot)
        {
            if (Plugin.myCustomPrefab == null) return;
            if (Plugin._hasLoaded) return; // 双重保险

            Plugin.Log.LogInfo("【Mod日志】开始执行...");

            // ================= 步骤 1: 锁定受害者 (原版 Face) =================
            var originalSMRs = gameCharacterRoot.GetComponentsInChildren<SkinnedMeshRenderer>();

            // 声明变量但不实例化 (不能用 new!)
            SkinnedMeshRenderer targetFaceSMR = null;

            foreach (var smr in originalSMRs)
            {
                if (smr.name == "Face" || smr.gameObject.name == "Face")
                {
                    Plugin.Log.LogInfo($"【Mod操作】锁定原版组件: {smr.name}");
                    targetFaceSMR = smr;
                    // 此时不要 disable 它，因为我们要把数据灌进去
                    targetFaceSMR.enabled = true;
                }
                else
                {
                    // 其他原来的衣服头发统统隐藏
                    smr.enabled = false;
                }
            }

            if (targetFaceSMR == null)
            {
                Plugin.Log.LogError("【Mod错误】严重：找不到原版的 Face 组件，无法进行形态键对接！");
                return;
            }

            // ================= 步骤 2: 生成并注入数据 =================
            GameObject modInstance = Object.Instantiate(Plugin.myCustomPrefab);
            var mySMRs = modInstance.GetComponentsInChildren<SkinnedMeshRenderer>();

            // 查找骨骼根节点
            Transform gameRootBone = FindChildRecursive(gameCharacterRoot.transform, "Character_Hips");
            if (gameRootBone == null)
            {
                Plugin.Log.LogError("【Mod错误】找不到 Character_Hips");
                Object.Destroy(modInstance);
                return;
            }

            foreach (var mySMR in mySMRs)
            {
                // 准备材质和Shader
                Material[] newMaterials = mySMR.materials;
                Shader toonShader = Shader.Find("Universal Render Pipeline/Lit");
                if (toonShader != null)
                {
                    foreach (var mat in newMaterials) mat.shader = toonShader;
                }
                else
                {
                    Plugin.Log.LogWarning("【Mod警告】找不到 URP Lit Shader，可能导致材质粉色。");
                }

                // 准备骨骼重映射 (这一步对 Face 和 新部件都需要)
                Transform[] newBones = new Transform[mySMR.bones.Length];
                for (int i = 0; i < mySMR.bones.Length; i++)
                {
                    string boneName = mySMR.bones[i].name;
                    Transform foundBone = FindChildRecursive(gameCharacterRoot.transform, boneName);
                    // 找不到就回退到 Hip，防止顶点飞到原点
                    newBones[i] = foundBone != null ? foundBone : gameRootBone;
                }

                // >>> 分支 A: 如果是身体/脸 (鸠占鹊巢) <<<
                if (mySMR.name == Plugin.MY_BODY_MESH_NAME)
                {
                    Plugin.Log.LogInfo("【Mod注入】正在将新网格注入原版 Face 组件...");

                    // 1. 替换网格 (这里包含了你用 Blender 排序好的形态键)
                    targetFaceSMR.sharedMesh = mySMR.sharedMesh;

                    // 2. 替换材质
                    targetFaceSMR.materials = newMaterials;

                    // 3. 替换骨骼引用
                    targetFaceSMR.bones = newBones;
                    targetFaceSMR.rootBone = gameRootBone; // 通常保持原版 rootBone 也可以，但保险起见

                    // 4. 【关键】跳过后续步骤！
                    // 不要为这个 Mesh 再创建新物体了，否则会有两个脸！
                    continue;
                }

                // >>> 分支 B: 如果是其他部件 (头发、饰品) <<<
                // 只有不是身体的部件，才创建新物体
                string targetName = mySMR.name + "_Mod";
                GameObject newPart = new GameObject(targetName);
                newPart.transform.SetParent(gameCharacterRoot.transform, false);

                SkinnedMeshRenderer newSMR = newPart.AddComponent<SkinnedMeshRenderer>();
                newSMR.sharedMesh = mySMR.sharedMesh;
                newSMR.materials = newMaterials; // 使用上面处理过 Shader 的材质数组
                newSMR.bones = newBones;
                newSMR.rootBone = gameRootBone;
            }

            Object.Destroy(modInstance);

            // ================= 步骤 3: 配饰处理 =================

            // 处理眼镜
            Transform glassesTr = FindChildRecursive(gameCharacterRoot.transform, "m_Glasses");
            if (glassesTr != null)
            {
                glassesTr.gameObject.SetActive(Plugin.ENABLE_GLASSES);
                if (Plugin.ENABLE_GLASSES)
                {
                    glassesTr.localPosition = new Vector3(-0.008f, 0.008f, 0.012f);
                    glassesTr.localScale = Vector3.one * 1.29f; // 简写
                }
                else
                {
                    glassesTr.localPosition = new Vector3(99f, 99f, 99f);
                }
            }

            // TODO: 确定耳机位置而不是直接隐藏
            // 临时禁用耳机
            Transform headphonesTr = FindChildRecursive(gameCharacterRoot.transform, "m_Headphone_cat");
            if (headphonesTr != null)
            {
                headphonesTr.gameObject.SetActive(Plugin.ENABLE_GLASSES);

                headphonesTr.localPosition = new Vector3(99f, 99f, 99f);

            }
            // ================= 步骤 4: 添加形态键同步组件 =================
            // 找到注入后的 Face 组件
            var finalFaceSMR = gameCharacterRoot.GetComponentsInChildren<SkinnedMeshRenderer>()
                .FirstOrDefault(smr => smr.name == "Face");

            if (finalFaceSMR != null)
            {
                var linker = gameCharacterRoot.AddComponent<BlendShapeLinker>();
                linker.originalSMR = finalFaceSMR;
                linker.myNewSMR = finalFaceSMR;

                Plugin.Log.LogInfo("【Mod日志】已添加 BlendShapeLinker 组件");
            }
            else
            {
                Plugin.Log.LogWarning("【Mod警告】未找到 Face 组件，无法添加形态键同步");
            }


            // 标记加载完成，防止 Update 重复调用
            Plugin._hasLoaded = true;
            Plugin.Log.LogInfo("【Mod日志】替换流程完美结束！");
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


    // TODO: 需要重新整理
    //public class BlendShapeLinker : MonoBehaviour
    //{
    //    public SkinnedMeshRenderer originalSMR;
    //    public SkinnedMeshRenderer myNewSMR;

    //    // 全局强度系数 
    //    public float globalMultiplier = 1.0f;
    //    public float mouthMultiplier = 0.4f;
    //    public float mouthThreshold = 40f;
    //    public float preserveKeysMultiplier = 1.0f;

    //    // 缓存索引映射
    //    private Dictionary<int, int> indexMap = new Dictionary<int, int>();

    //    // 【白名单】这些形态键必须保持 1.0 
    //    private HashSet<string> preserveKeys = new HashSet<string>
    //{
    //    "blendShape1.Eye_blink",
    //};

    //    void Start()
    //    {
    //        Plugin.Log.LogInfo("【BlendShapeLinker】初始化形态键映射...");
    //        if (originalSMR == null || myNewSMR == null) return;
    //        var originalMesh = originalSMR.sharedMesh;
    //        var newMesh = myNewSMR.sharedMesh;

    //        for (int i = 0; i < originalMesh.blendShapeCount; i++)
    //        {
    //            string originalName = originalMesh.GetBlendShapeName(i);
    //            int newIndex = newMesh.GetBlendShapeIndex(originalName);

    //            if (newIndex != -1) indexMap[i] = newIndex;
    //        }
    //    }

    //    void LateUpdate()
    //    {
    //        if (originalSMR == null || myNewSMR == null) return;

    //        foreach (var pair in indexMap)
    //        {
    //            int oldIndex = pair.Key;
    //            int newIndex = pair.Value;

    //            // 1. 读取原版权重
    //            float originalWeight = originalSMR.GetBlendShapeWeight(oldIndex);

    //            // 2. 计算新权重
    //            float finalWeight = originalWeight;

    //            string keyName = myNewSMR.sharedMesh.GetBlendShapeName(newIndex);

    //            // 如果不是眨眼，也不是特殊表情，就通过系数削弱它
    //            if (!preserveKeys.Contains(keyName))
    //            {
    //                // 还可以针对 Mouth 开头的做更狠的削弱
    //                if (keyName.Contains("Mouth"))
    //                {
    //                    finalWeight *= mouthMultiplier;
    //                    if (originalWeight < mouthThreshold || keyName.Contains("_smile2") || keyName.Contains("_n"))
    //                    {
    //                        // 如果原版数值小于 mouthThreshold，直接忽略，强制闭嘴
    //                        finalWeight = 0f;
    //                    }
    //                    //if (originalWeight > 0)
    //                    //Plugin.Log.LogInfo($"【BlendShapeLinker】削弱嘴巴表情: {keyName} 原 {originalWeight} -> {finalWeight}");
    //                }
    //                else
    //                {
    //                    finalWeight *= globalMultiplier; // 其他部位 50%
    //                                                     //if (originalWeight > 0)
    //                                                     //Plugin.Log.LogInfo($"【BlendShapeLinker】削弱其他表情: {keyName} 原 {originalWeight} -> {finalWeight}");
    //                }
    //            }
    //            else
    //            {
    //                finalWeight *= preserveKeysMultiplier; // 眨眼等保留表情 100%
    //                                                       //if (originalWeight > 0)
    //                                                       //Plugin.Log.LogInfo($" 【BlendShapeLinker】调整保留表情: {keyName} 原 {originalWeight} -> {finalWeight}");
    //            }



    //            // 3. 应用
    //            myNewSMR.SetBlendShapeWeight(newIndex, finalWeight);
    //        }
    //    }
    //}


}