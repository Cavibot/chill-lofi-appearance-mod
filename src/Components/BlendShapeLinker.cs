using Cavi.AppearanceMod.Utils;
using System.Collections.Generic;
using UnityEngine;

namespace Cavi.AppearanceMod.Components
{
    /// <summary>
    /// Synchronizes blend shape weights from the original mesh to the custom mesh.
    /// Applies configurable multipliers to control facial expression intensity.
    /// </summary>
    public class BlendShapeLinker : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Mesh References")]
        [Tooltip("The original skinned mesh renderer (source of blend shape data)")]
        public SkinnedMeshRenderer originalRenderer;

        [Tooltip("The custom skinned mesh renderer (target for blend shape synchronization)")]
        public SkinnedMeshRenderer customRenderer;

        [Header("Weight Multipliers")]
        [Tooltip("Global multiplier for all non-preserved blend shapes")]
        [Range(0f, 2f)]
        public float globalMultiplier = 1.0f;

        [Tooltip("Multiplier specifically for mouth-related blend shapes")]
        [Range(0f, 1f)]
        public float mouthMultiplier = 0.4f;

        [Tooltip("Minimum weight threshold for mouth blend shapes (below this value, weight is set to 0)")]
        [Range(0f, 100f)]
        public float mouthThreshold = 40f;

        [Tooltip("Multiplier for preserved blend shapes (e.g., eye blink)")]
        [Range(0f, 2f)]
        public float preserveKeysMultiplier = 1.0f;

        #endregion

        #region Private Fields

        /// <summary>
        /// Maps blend shape indices from original mesh to custom mesh
        /// Key: Original mesh blend shape index
        /// Value: Custom mesh blend shape index
        /// </summary>
        private Dictionary<int, int> _blendShapeIndexMap;

        /// <summary>
        /// Set of blend shape names that should maintain their original weight (1.0x multiplier by default)
        /// </summary>
        private HashSet<string> _preservedBlendShapes;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            ModLogger.Info($"BlendShapeLinker on {gameObject.name}: Initializing");
            _blendShapeIndexMap = new Dictionary<int, int>();
            _preservedBlendShapes = new HashSet<string>
            {
                "blendShape1.Eye_blink",
                // Add more preserved blend shapes here
            };
        }

        private void Start()
        {
            ModLogger.Info($"BlendShapeLinker on {gameObject.name}: Initializing blend shape mapping");
            if (!ValidateRenderers())
            {
                ModLogger.Error($"BlendShapeLinker on {gameObject.name}: Invalid renderer references");
                enabled = false;
                return;
            }

            InitializeBlendShapeMapping();
        }

        private void LateUpdate()
        {
            if (!ValidateRenderers()) return;

            SynchronizeBlendShapes();
        }

        #endregion

        #region Initialization

        private bool ValidateRenderers()
        {
            return originalRenderer != null && customRenderer != null;
        }

        private void InitializeBlendShapeMapping()
        {
            var originalMesh = originalRenderer.sharedMesh;
            var customMesh = customRenderer.sharedMesh;

            if (originalMesh == null || customMesh == null)
            {
                ModLogger.Error("BlendShapeLinker: Mesh references are null");
                return;
            }

            int mappedCount = 0;
            int totalCount = originalMesh.blendShapeCount;

            for (int i = 0; i < totalCount; i++)
            {
                string blendShapeName = originalMesh.GetBlendShapeName(i);
                int customIndex = customMesh.GetBlendShapeIndex(blendShapeName);
                ModLogger.Info($"BlendShapeLinker: Mapping blend shape '{blendShapeName}' (Original Index: {i}, Custom Index: {customIndex})");
                if (customIndex != -1)
                {
                    _blendShapeIndexMap[i] = customIndex;
                    mappedCount++;
                }                
            }

            ModLogger.Debug($"BlendShapeLinker: Mapped {mappedCount}/{totalCount} blend shapes");
        }

        #endregion

        #region Blend Shape Synchronization

        private void SynchronizeBlendShapes()
        {
            foreach (var mapping in _blendShapeIndexMap)
            {
                int originalIndex = mapping.Key;
                int customIndex = mapping.Value;

                float originalWeight = originalRenderer.GetBlendShapeWeight(originalIndex);
                float adjustedWeight = CalculateAdjustedWeight(customIndex, originalWeight);

                customRenderer.SetBlendShapeWeight(customIndex, adjustedWeight);
            }
        }

        private float CalculateAdjustedWeight(int customIndex, float originalWeight)
        {
            string blendShapeName = customRenderer.sharedMesh.GetBlendShapeName(customIndex);

            // Preserved blend shapes use their dedicated multiplier
            if (_preservedBlendShapes.Contains(blendShapeName))
            {
                return originalWeight * preserveKeysMultiplier;
            }

            // Mouth blend shapes have special handling
            if (IsMouthBlendShape(blendShapeName))
            {
                return CalculateMouthWeight(blendShapeName, originalWeight);
            }

            // All other blend shapes use the global multiplier
            return originalWeight * globalMultiplier;
        }

        private bool IsMouthBlendShape(string blendShapeName)
        {
            return blendShapeName.Contains("Mouth");
        }

        private float CalculateMouthWeight(string blendShapeName, float originalWeight)
        {
            // Suppress specific mouth expressions
            if (ShouldSuppressMouthExpression(blendShapeName, originalWeight))
            {
                return 0f;
            }

            return originalWeight * mouthMultiplier;
        }

        private bool ShouldSuppressMouthExpression(string blendShapeName, float weight)
        {
            // Suppress if weight is below threshold
            if (weight < mouthThreshold)
                return true;

            // Suppress specific mouth expressions
            if (blendShapeName.Contains("_smile2") || blendShapeName.Contains("_n"))
                return true;

            return false;
        }

        #endregion

        #region Runtime Debug Methods

        /// <summary>
        /// Logs all blend shape mappings to the console.
        /// Can be called from UnityExplorer C# Console.
        /// </summary>
        public void LogMappings()
        {
            if (_blendShapeIndexMap == null || _blendShapeIndexMap.Count == 0)
            {
                ModLogger.Info("No blend shape mappings available");
                return;
            }

            ModLogger.Info("=== Blend Shape Mappings ===");
            foreach (var mapping in _blendShapeIndexMap)
            {
                string originalName = originalRenderer.sharedMesh.GetBlendShapeName(mapping.Key);
                string customName = customRenderer.sharedMesh.GetBlendShapeName(mapping.Value);
                ModLogger.Info($"{mapping.Key} ({originalName}) -> {mapping.Value} ({customName})");
            }
            ModLogger.Info("============================");
        }

        /// <summary>
        /// Logs current blend shape weights.
        /// Can be called from UnityExplorer C# Console.
        /// </summary>
        public void LogCurrentWeights()
        {
            if (!ValidateRenderers())
            {
                ModLogger.Warning("Renderers are not valid");
                return;
            }

            ModLogger.Info("=== Current Blend Shape Weights ===");
            foreach (var mapping in _blendShapeIndexMap)
            {
                float originalWeight = originalRenderer.GetBlendShapeWeight(mapping.Key);
                float customWeight = customRenderer.GetBlendShapeWeight(mapping.Value);
                string name = customRenderer.sharedMesh.GetBlendShapeName(mapping.Value);

                if (originalWeight > 0.01f)
                {
                    ModLogger.Info($"{name}: Original={originalWeight:F2}, Custom={customWeight:F2}");
                }
            }
            ModLogger.Info("===================================");
        }

        #endregion

    }
}