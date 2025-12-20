using Bulbul;
using Cavi.ChillWithAnyone;
using Cavi.ChillWithAnyone.Components;
using Cavi.ChillWithAnyone.Utils;
using HarmonyLib;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Cavi.ChillWithAnyone.Patches
{
    public static class CharacterPatches
    {
        [HarmonyPatch(typeof(RoomGameManager), "Initialize")]
        public static class RoomManagerPatch
        {
            [HarmonyPostfix]
            public static void Postfix(RoomGameManager __instance)
            {
                if (ChillWithAnyonePlugin.IsModelLoaded) return;

                ModLogger.Info("RoomGameManager initialized");
                GameObject characterObj = GameObject.Find("Character");

                if (characterObj != null)
                {
                    ReplaceCharacterModel(characterObj);
                }
                else
                {
                    TryFindCharacterFromService(__instance);
                }
            }

            private static void TryFindCharacterFromService(RoomGameManager instance)
            {
                var fieldInfo = typeof(RoomGameManager).GetField("_heroineService",
                    BindingFlags.NonPublic | BindingFlags.Instance);

                if (fieldInfo?.GetValue(instance) is MonoBehaviour service)
                {
                    ReplaceCharacterModel(service.gameObject);
                }
            }
        }

        public static void ReplaceCharacterModel(GameObject characterRoot)
        {
            if (ChillWithAnyonePlugin.CustomCharacterPrefab == null || ChillWithAnyonePlugin.IsModelLoaded)
                return;

            ModLogger.Info("Starting character model replacement");

            SkinnedMeshRenderer targetFaceRenderer = FindAndDisableOriginalRenderers(characterRoot);
            if (targetFaceRenderer == null)
            {
                ModLogger.Error("Failed to find original Face component");
                return;
            }

            GameObject modInstance = Object.Instantiate(ChillWithAnyonePlugin.CustomCharacterPrefab);
            Transform rootBone = FindChildRecursive(characterRoot.transform, "Character_Hips");

            if (rootBone == null)
            {
                ModLogger.Error("Character_Hips not found");
                Object.Destroy(modInstance);
                return;
            }

            ReplaceRenderers(modInstance, characterRoot, targetFaceRenderer, rootBone);
            Object.Destroy(modInstance);

            ConfigureAccessories(characterRoot);
            AttachBlendShapeLinker(characterRoot);

            ChillWithAnyonePlugin.SetModelLoaded(true);
            ModLogger.Info("Character model replacement completed");
        }

        private static SkinnedMeshRenderer FindAndDisableOriginalRenderers(GameObject characterRoot)
        {
            var renderers = characterRoot.GetComponentsInChildren<SkinnedMeshRenderer>();
            SkinnedMeshRenderer faceRenderer = null;

            foreach (var renderer in renderers)
            {
                if (renderer.name == "Face" || renderer.gameObject.name == "Face")
                {
                    ModLogger.LogOperation($"Found original Face component: {renderer.name}");
                    faceRenderer = renderer;
                    faceRenderer.enabled = true;
                }
                else
                {
                    renderer.enabled = false;
                }
            }

            return faceRenderer;
        }

        private static void ReplaceRenderers(
            GameObject modInstance,
            GameObject characterRoot,
            SkinnedMeshRenderer targetFaceRenderer,
            Transform rootBone)
        {
            var customRenderers = modInstance.GetComponentsInChildren<SkinnedMeshRenderer>();

            foreach (var customRenderer in customRenderers)
            {
                Material[] materials = PrepareMateria(customRenderer);
                Transform[] bones = RemapBones(customRenderer, characterRoot.transform, rootBone);

                if (customRenderer.name == ChillWithAnyonePlugin.BODY_MESH_NAME)
                {
                    InjectFaceMesh(targetFaceRenderer, customRenderer, materials, bones, rootBone);
                }
                else
                {
                    CreateNewMeshPart(characterRoot, customRenderer, materials, bones, rootBone);
                }
            }
        }

        // Although mats have already been set to Liltoon (or other) in the asset bundle,
        // we need to reassign them to URP Lit shader, otherwise they will disappear! Weird. 
        private static Material[] PrepareMateria(SkinnedMeshRenderer renderer)
        {
            Material[] materials = renderer.materials;
            Shader urpShader = Shader.Find("Universal Render Pipeline/Lit");

            if (urpShader != null)
            {
                foreach (var mat in materials)
                    mat.shader = urpShader;
            }
            else
            {
                ModLogger.Warning("URP Lit shader not found, materials may appear pink");
            }

            return materials;
        }

        private static Transform[] RemapBones(
            SkinnedMeshRenderer renderer,
            Transform characterRoot,
            Transform fallbackBone)
        {
            Transform[] bones = new Transform[renderer.bones.Length];

            for (int i = 0; i < renderer.bones.Length; i++)
            {
                string boneName = renderer.bones[i].name;
                Transform foundBone = FindChildRecursive(characterRoot, boneName);
                bones[i] = foundBone ?? fallbackBone;
            }

            return bones;
        }

        private static void InjectFaceMesh(
            SkinnedMeshRenderer target,
            SkinnedMeshRenderer source,
            Material[] materials,
            Transform[] bones,
            Transform rootBone)
        {
            ModLogger.LogInjection("Injecting custom mesh into Face component");

            target.sharedMesh = source.sharedMesh;
            target.materials = materials;
            target.bones = bones;
            target.rootBone = rootBone;
        }

        private static void CreateNewMeshPart(
            GameObject parent,
            SkinnedMeshRenderer source,
            Material[] materials,
            Transform[] bones,
            Transform rootBone)
        {
            string partName = $"{source.name}_Mod";
            GameObject newPart = new GameObject(partName);
            newPart.transform.SetParent(parent.transform, false);

            SkinnedMeshRenderer newRenderer = newPart.AddComponent<SkinnedMeshRenderer>();
            newRenderer.sharedMesh = source.sharedMesh;
            newRenderer.materials = materials;
            newRenderer.bones = bones;
            newRenderer.rootBone = rootBone;

            ModLogger.Debug($"Created mesh part: {partName}");
        }

        // TODO: how to support different avatars with different accessory names/structures? Using external config?
        private static void ConfigureAccessories(GameObject characterRoot)
        {
            ConfigureGlasses(characterRoot);
            ConfigureHeadphones(characterRoot);
        }

        private static void ConfigureGlasses(GameObject characterRoot)
        {
            Transform glasses = FindChildRecursive(characterRoot.transform, "m_Glasses");
            if (glasses == null) return;

            glasses.gameObject.SetActive(ChillWithAnyonePlugin.EnableGlasses);

            if (ChillWithAnyonePlugin.EnableGlasses)
            {
                glasses.localPosition = new Vector3(-0.008f, 0.008f, 0.012f);
                glasses.localScale = Vector3.one * 1.29f;
            }
            else
            {
                glasses.localPosition = new Vector3(99f, 99f, 99f);
            }
        }

        private static void ConfigureHeadphones(GameObject characterRoot)
        {
            Transform headphones = FindChildRecursive(characterRoot.transform, "m_Headphone_cat");
            if (headphones == null) return;

            // TODO: Determine proper headphone position instead of hiding
            headphones.gameObject.SetActive(false);
            headphones.localPosition = new Vector3(99f, 99f, 99f);
        }

        private static void AttachBlendShapeLinker(GameObject characterRoot)
        {
            var faceRenderer = characterRoot.GetComponentsInChildren<SkinnedMeshRenderer>()
                .FirstOrDefault(smr => smr.name == "Face");

            if (faceRenderer != null)
            {
                var linker = characterRoot.AddComponent<BlendShapeLinker>();
               
                linker.originalRenderer = faceRenderer;
                linker.customRenderer = faceRenderer;

                ModLogger.Info("BlendShapeLinker component attached");
            }
            else
            {
                ModLogger.Warning("Face component not found, cannot attach BlendShapeLinker");
            }
        }

        private static Transform FindChildRecursive(Transform parent, string name)
        {
            if (parent.name == name) return parent;

            foreach (Transform child in parent)
            {
                Transform result = FindChildRecursive(child, name);
                if (result != null) return result;
            }

            return null;
        }
    }
}