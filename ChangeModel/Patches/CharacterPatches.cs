using Bulbul;
using Cavi.AppearanceMod;
using Cavi.AppearanceMod.Components; // 引用第二步建立的组件
using HarmonyLib;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Cavi.AppearanceMod.Patches
{
    public static class CharacterPatches
    {
        // 原本 Hooks 里的核心逻辑
        [HarmonyPatch(typeof(RoomGameManager), "Initialize")]
        public static class RoomManagerPatch
        {
            [HarmonyPostfix]
            public static void Postfix(RoomGameManager __instance)
            {
                if (AppearancePlugin._hasLoaded) return;
                AppearancePlugin.Log.LogInfo("【Mod日志】RoomGameManager 初始化完成！");
                GameObject characterObj = GameObject.Find("Character");

                if (characterObj != null)
                {
                    ReplaceHeroineModel(characterObj);
                }
                else
                {
                    var fieldInfo = typeof(RoomGameManager).GetField("_heroineService", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (fieldInfo != null && fieldInfo.GetValue(__instance) is MonoBehaviour service)
                    {
                        ReplaceHeroineModel(service.gameObject);
                    }
                }
            }
        }

        public static void ReplaceHeroineModel(GameObject gameCharacterRoot)
        {
            if (AppearancePlugin.myCustomPrefab == null || AppearancePlugin._hasLoaded) return;

            AppearancePlugin.Log.LogInfo("【Mod日志】开始执行替换逻辑...");

            // ================= 步骤 1: 锁定受害者 (原版 Face) =================
            var originalSMRs = gameCharacterRoot.GetComponentsInChildren<SkinnedMeshRenderer>();

            // 声明变量但不实例化 (不能用 new!)
            SkinnedMeshRenderer targetFaceSMR = null;

            foreach (var smr in originalSMRs)
            {
                if (smr.name == "Face" || smr.gameObject.name == "Face")
                {
                    AppearancePlugin.Log.LogInfo($"【Mod操作】锁定原版组件: {smr.name}");
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
                AppearancePlugin.Log.LogError("【Mod错误】严重：找不到原版的 Face 组件，无法进行形态键对接！");
                return;
            }

            // ================= 步骤 2: 生成并注入数据 =================
            GameObject modInstance = Object.Instantiate(AppearancePlugin.myCustomPrefab);
            var mySMRs = modInstance.GetComponentsInChildren<SkinnedMeshRenderer>();

            // 查找骨骼根节点
            Transform gameRootBone = FindChildRecursive(gameCharacterRoot.transform, "Character_Hips");
            if (gameRootBone == null)
            {
                AppearancePlugin.Log.LogError("【Mod错误】找不到 Character_Hips");
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
                    AppearancePlugin.Log.LogWarning("【Mod警告】找不到 URP Lit Shader，可能导致材质粉色。");
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
                if (mySMR.name == AppearancePlugin.MY_BODY_MESH_NAME)
                {
                    AppearancePlugin.Log.LogInfo("【Mod注入】正在将新网格注入原版 Face 组件...");

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
                glassesTr.gameObject.SetActive(AppearancePlugin.ENABLE_GLASSES);
                if (AppearancePlugin.ENABLE_GLASSES)
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
                headphonesTr.gameObject.SetActive(AppearancePlugin.ENABLE_GLASSES);

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

                AppearancePlugin.Log.LogInfo("【Mod日志】已添加 BlendShapeLinker 组件");
            }
            else
            {
                AppearancePlugin.Log.LogWarning("【Mod警告】未找到 Face 组件，无法添加形态键同步");
            }
            AppearancePlugin._hasLoaded = true;
            AppearancePlugin.Log.LogInfo("【Mod日志】替换流程完美结束！");
        }

        // 注意：要把 FindChildRecursive 放在这里或者移动到 Utils 中
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
}