using UnityEngine;
using System.Collections.Generic;

namespace Cavi.AppearanceMod.Components
{
    public class BlendShapeLinker : MonoBehaviour
    {
        public SkinnedMeshRenderer originalSMR;
        public SkinnedMeshRenderer myNewSMR;

        public float globalMultiplier = 1.0f;
        public float mouthMultiplier = 0.4f;
        public float mouthThreshold = 40f;
        public float preserveKeysMultiplier = 1.0f;

        private Dictionary<int, int> indexMap = new Dictionary<int, int>();
        private HashSet<string> preserveKeys = new HashSet<string> { "blendShape1.Eye_blink" };

        void Start()
        {
            // 注意：这里需要引用 AppearancePlugin.Log
            //AppearancePlugin.Log.LogInfo("【BlendShapeLinker】初始化形态键映射...");
            if (originalSMR == null || myNewSMR == null) return;
            var originalMesh = originalSMR.sharedMesh;
            var newMesh = myNewSMR.sharedMesh;

            for (int i = 0; i < originalMesh.blendShapeCount; i++)
            {
                string originalName = originalMesh.GetBlendShapeName(i);
                int newIndex = newMesh.GetBlendShapeIndex(originalName);
                if (newIndex != -1) indexMap[i] = newIndex;
            }
        }

        void LateUpdate()
        {
            if (originalSMR == null || myNewSMR == null) return;
            foreach (var pair in indexMap)
            {
                float originalWeight = originalSMR.GetBlendShapeWeight(pair.Key);
                float finalWeight = originalWeight;
                string keyName = myNewSMR.sharedMesh.GetBlendShapeName(pair.Value);

                if (!preserveKeys.Contains(keyName))
                {
                    if (keyName.Contains("Mouth"))
                    {
                        finalWeight *= mouthMultiplier;
                        if (originalWeight < mouthThreshold || keyName.Contains("_smile2") || keyName.Contains("_n"))
                            finalWeight = 0f;
                    }
                    else
                    {
                        finalWeight *= globalMultiplier;
                    }
                }
                else
                {
                    finalWeight *= preserveKeysMultiplier;
                }
                myNewSMR.SetBlendShapeWeight(pair.Value, finalWeight);
            }
        }
    }
}