using Bulbul;
using Cavi.AppearanceMod;
using Cavi.AppearanceMod.Components;
using Cavi.AppearanceMod.Utils;
using HarmonyLib;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Cavi.AppearanceMod.Patches
{
    public static class CharacterPatches
    {
        [HarmonyPatch(typeof(RoomGameManager), "Initialize")]
        public static class RoomManagerPatch
        {
            [HarmonyPostfix]
            public static void Postfix(RoomGameManager __instance)
            {
                if (AppearancePlugin._hasLoaded) return;

                ModLogger.Info("RoomGameManager 初始化完成！");
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

            ModLogger.Info("开始执行替换逻辑...");

            // ================= 步骤 1: 锁定受害者 (原版 Face) =================
            var originalSMRs = gameCharacterRoot.GetComponentsInChildren<SkinnedMeshRenderer>();
            SkinnedMeshRenderer targetFaceSMR = null;

            foreach (var smr in originalSMRs)
            {
                if (smr.name == "Face" || smr.gameObject.name == "Face")
                {
                    ModLogger.LogOperation($"锁定原版组件: {smr.name}");
                    targetFaceSMR = smr;
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
                ModLogger.Error("严重：找不到原版的 Face 组件，无法进行形态键对接！");
                return;
            }

            // ================= 步骤 2: 生成并注入数据 =================
            GameObject modInstance = Object.Instantiate(AppearancePlugin.myCustomPrefab);
            var mySMRs = modInstance.GetComponentsInChildren<SkinnedMeshRenderer>();

            // 查找骨骼根节点
            Transform gameRootBone = FindChildRecursive(gameCharacterRoot.transform, "Character_Hips");
            if (gameRootBone == null)
            {
                ModLogger.Error("找不到 Character_Hips");
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
                    ModLogger.Warning("找不到 URP Lit Shader，可能导致材质粉色。");
                }

                // 准备骨骼重映射
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
                    ModLogger.LogInjection("正在将新网格注入原版 Face 组件...");

                    targetFaceSMR.sharedMesh = mySMR.sharedMesh;
                    targetFaceSMR.materials = newMaterials;
                    targetFaceSMR.bones = newBones;
                    targetFaceSMR.rootBone = gameRootBone;

                    continue;
                }

                // >>> 分支 B: 如果是其他部件 (头发、饰品) <<<
                string targetName = mySMR.name + "_Mod";
                GameObject newPart = new GameObject(targetName);
                newPart.transform.SetParent(gameCharacterRoot.transform, false);

                SkinnedMeshRenderer newSMR = newPart.AddComponent<SkinnedMeshRenderer>();
                newSMR.sharedMesh = mySMR.sharedMesh;
                newSMR.materials = newMaterials;
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
                    glassesTr.localScale = Vector3.one * 1.29f;
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
            var finalFaceSMR = gameCharacterRoot.GetComponentsInChildren<SkinnedMeshRenderer>()
                .FirstOrDefault(smr => smr.name == "Face");

            if (finalFaceSMR != null)
            {
                var linker = gameCharacterRoot.AddComponent<BlendShapeLinker>();
                linker.originalSMR = finalFaceSMR;
                linker.myNewSMR = finalFaceSMR;

                ModLogger.Info("已添加 BlendShapeLinker 组件");
            }
            else
            {
                ModLogger.Warning("未找到 Face 组件，无法添加形态键同步");
            }

            AppearancePlugin._hasLoaded = true;
            ModLogger.Info("替换流程完美结束！");
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
}