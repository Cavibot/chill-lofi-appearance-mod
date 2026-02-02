using Cavi.ChillWithAnyone.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Cavi.ChillWithAnyone.Components
{
    public class BlendShapeLinker : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Mesh References")]
        public SkinnedMeshRenderer originalRenderer;
        public SkinnedMeshRenderer customRenderer;

        #endregion

        #region Private Fields

        private Dictionary<int, int> _blendShapeIndexMap;
        private Dictionary<string, BlendShapeConfigItem> _configLookup;
        private BlendShapeConfigRoot _config;
        private Dictionary<int, float> _cachedOriginalWeights;
        private Animator _animator;
        private bool _lastAnimatorState = true;
        private Dictionary<int, float> _frozenOriginalWeights;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            ModLogger.Info($"BlendShapeLinker on {gameObject.name}: Initializing");
            _blendShapeIndexMap = new Dictionary<int, int>();
            _configLookup = new Dictionary<string, BlendShapeConfigItem>();
            _cachedOriginalWeights = new Dictionary<int, float>();
            _frozenOriginalWeights = new Dictionary<int, float>();
        }

        private void Start()
        {
            ModLogger.Info($"BlendShapeLinker on {gameObject.name}: Starting initialization");

            if (!ValidateRenderers())
            {
                ModLogger.Error($"BlendShapeLinker on {gameObject.name}: Invalid renderer references");
                enabled = false;
                return;
            }

            _animator = GetComponentInParent<Animator>();
            if (_animator != null)
            {
                ModLogger.Info($"BlendShapeLinker: Found Animator on {_animator.gameObject.name}");
            }

            LoadConfiguration();
            InitializeBlendShapeMapping();
        }

        private void LateUpdate()
        {
            if (!ValidateRenderers()) return;

            bool animatorEnabled = _animator != null && _animator.enabled;
            
            if (_lastAnimatorState && !animatorEnabled)
            {
                FreezeOriginalWeights();
                ModLogger.Debug("BlendShapeLinker: Animator paused, froze original weights");
            }
            
            _lastAnimatorState = animatorEnabled;
            
            if (!animatorEnabled)
            {
                CacheOriginalWeightsFromFrozen();
            }
            else
            {
                CacheOriginalWeights();
            }

            SynchronizeBlendShapes();
        }

        #endregion

        #region Configuration

        private void LoadConfiguration()
        {
            // ============ 修改：使用模组目录而非 BepInEx/config ============
            // 获取模组 DLL 所在目录
            string pluginPath = System.IO.Path.GetDirectoryName(typeof(BlendShapeLinker).Assembly.Location);
            string configPath = System.IO.Path.Combine(pluginPath, "blendshape_config.json");
            // ============================================================

            if (!File.Exists(configPath))
            {
                ModLogger.Warning($"BlendShapeLinker: Config file not found at {configPath}");
                ModLogger.Info("BlendShapeLinker: Using passthrough mode (1:1 mapping).");
                _config = CreateDefaultConfig();
                return;
            }

            try
            {
                string json = File.ReadAllText(configPath);

                if (string.IsNullOrWhiteSpace(json))
                {
                    ModLogger.Error("BlendShapeLinker: Config file is empty");
                    _config = CreateDefaultConfig();
                    return;
                }

                ModLogger.Debug($"BlendShapeLinker: Loading config from {configPath}");
                _config = ParseConfigFromJson(json);

                _configLookup.Clear();
                foreach (var item in _config.config)
                {
                    if (item != null && !string.IsNullOrEmpty(item.sourceName))
                    {
                        _configLookup[item.sourceName] = item;
                    }
                }

                ModLogger.Info($"BlendShapeLinker: ✓ Loaded {_configLookup.Count} blend shape configurations from {configPath}");
                ModLogger.Info($"BlendShapeLinker: Mouth Global: {(_config.mouthGlobal.enabled ? "ENABLED" : "DISABLED")}");
                ModLogger.Info($"BlendShapeLinker: Eye Global: {(_config.eyeGlobal.enabled ? "ENABLED" : "DISABLED")}");
            }
            catch (Exception ex)
            {
                ModLogger.Error($"BlendShapeLinker: Failed to load config: {ex.Message}");
                _config = CreateDefaultConfig();
            }
        }

        // ============ 新增：创建默认配置 ============
        private BlendShapeConfigRoot CreateDefaultConfig()
        {
            return new BlendShapeConfigRoot
            {
                config = new BlendShapeConfigItem[0],
                mouthGlobal = new GlobalCategoryConfig { enabled = false },
                eyeGlobal = new GlobalCategoryConfig { enabled = false }
            };
        }

        // ============ 新增：从 JSON 解析配置 ============
        private BlendShapeConfigRoot ParseConfigFromJson(string json)
        {
            var root = new BlendShapeConfigRoot();
            
            // 解析 config 数组
            root.config = ParseConfigManually(json);
            
            // 解析全局分类配置
            root.mouthGlobal = ParseGlobalCategory(json, "mouthGlobal");
            root.eyeGlobal = ParseGlobalCategory(json, "eyeGlobal");
            
            return root;
        }

        private GlobalCategoryConfig ParseGlobalCategory(string json, string categoryName)
        {
            var category = new GlobalCategoryConfig();
            
            try
            {
                int categoryStart = json.IndexOf($"\"{categoryName}\"");
                if (categoryStart < 0)
                {
                    return category; // 返回默认配置
                }

                int objStart = json.IndexOf("{", categoryStart);
                int objEnd = FindMatchingBrace(json, objStart);
                
                if (objStart < 0 || objEnd < 0)
                {
                    return category;
                }

                string objStr = json.Substring(objStart, objEnd - objStart + 1);
                
                category.enabled = ExtractBoolValue(objStr, "enabled", false);
                category.multiplier = ExtractFloatValue(objStr, "multiplier", 1.0f);
                category.upperThreshold = ExtractFloatValue(objStr, "upperThreshold", 100.0f);
                category.lowerThreshold = ExtractFloatValue(objStr, "lowerThreshold", 0.0f);
                category.isInverted = ExtractBoolValue(objStr, "isInverted", false);
                category.isDisabled = ExtractBoolValue(objStr, "isDisabled", false);
            }
            catch (Exception ex)
            {
                ModLogger.Warning($"Failed to parse {categoryName}: {ex.Message}");
            }

            return category;
        }

        private int FindMatchingBrace(string json, int startIndex)
        {
            int braceCount = 0;
            for (int i = startIndex; i < json.Length; i++)
            {
                if (json[i] == '{') braceCount++;
                else if (json[i] == '}')
                {
                    braceCount--;
                    if (braceCount == 0) return i;
                }
            }
            return -1;
        }
        // ============================================

        private BlendShapeConfigItem[] ParseConfigManually(string json)
        {
            var items = new List<BlendShapeConfigItem>();
            
            try
            {
                int configStart = json.IndexOf("\"config\"");
                if (configStart < 0)
                {
                    return new BlendShapeConfigItem[0];
                }

                int arrayStart = json.IndexOf("[", configStart);
                int arrayEnd = json.LastIndexOf("]");
                
                if (arrayStart < 0 || arrayEnd < 0)
                {
                    return new BlendShapeConfigItem[0];
                }

                string arrayContent = json.Substring(arrayStart + 1, arrayEnd - arrayStart - 1);
                var objects = SplitJsonObjects(arrayContent);

                foreach (var objStr in objects)
                {
                    var item = ParseConfigItem(objStr);
                    if (item != null)
                    {
                        items.Add(item);
                    }
                }
            }
            catch (Exception ex)
            {
                ModLogger.Error($"Config parse failed: {ex.Message}");
            }

            return items.ToArray();
        }

        private List<string> SplitJsonObjects(string arrayContent)
        {
            var objects = new List<string>();
            int braceCount = 0;
            int startIndex = 0;

            for (int i = 0; i < arrayContent.Length; i++)
            {
                if (arrayContent[i] == '{')
                {
                    if (braceCount == 0)
                        startIndex = i;
                    braceCount++;
                }
                else if (arrayContent[i] == '}')
                {
                    braceCount--;
                    if (braceCount == 0)
                    {
                        objects.Add(arrayContent.Substring(startIndex, i - startIndex + 1));
                    }
                }
            }

            return objects;
        }

        private BlendShapeConfigItem ParseConfigItem(string objStr)
        {
            try
            {
                var item = new BlendShapeConfigItem();
                
                item.sourceName = ExtractStringValue(objStr, "sourceName");
                item.multiplier = ExtractFloatValue(objStr, "multiplier", 1.0f);
                item.upperThreshold = ExtractFloatValue(objStr, "upperThreshold", 100.0f);
                item.lowerThreshold = ExtractFloatValue(objStr, "lowerThreshold", 0.0f);
                item.isInverted = ExtractBoolValue(objStr, "isInverted", false);
                item.isDisabled = ExtractBoolValue(objStr, "isDisabled", false);

                return item;
            }
            catch
            {
                return null;
            }
        }

        private string ExtractStringValue(string json, string key)
        {
            string pattern = $"\"{key}\"\\s*:\\s*\"([^\"]+)\"";
            var match = System.Text.RegularExpressions.Regex.Match(json, pattern);
            return match.Success ? match.Groups[1].Value : "";
        }

        private float ExtractFloatValue(string json, string key, float defaultValue)
        {
            string pattern = $"\"{key}\"\\s*:\\s*([0-9.]+)";
            var match = System.Text.RegularExpressions.Regex.Match(json, pattern);
            return match.Success && float.TryParse(match.Groups[1].Value, out float result) ? result : defaultValue;
        }

        private bool ExtractBoolValue(string json, string key, bool defaultValue)
        {
            string pattern = $"\"{key}\"\\s*:\\s*(true|false)";
            var match = System.Text.RegularExpressions.Regex.Match(json, pattern);
            return match.Success ? match.Groups[1].Value == "true" : defaultValue;
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

                if (customIndex != -1)
                {
                    _blendShapeIndexMap[i] = customIndex;
                    mappedCount++;
                }
            }

            ModLogger.Info($"BlendShapeLinker: Mapped {mappedCount}/{totalCount} blend shapes");
        }

        #endregion

        #region Blend Shape Synchronization

        private void FreezeOriginalWeights()
        {
            _frozenOriginalWeights.Clear();
            
            foreach (var mapping in _blendShapeIndexMap)
            {
                int originalIndex = mapping.Key;
                int customIndex = mapping.Value;
                float originalWeight = originalRenderer.GetBlendShapeWeight(originalIndex);
                _frozenOriginalWeights[customIndex] = originalWeight;
            }
        }

        private void CacheOriginalWeightsFromFrozen()
        {
            _cachedOriginalWeights.Clear();
            foreach (var kvp in _frozenOriginalWeights)
            {
                _cachedOriginalWeights[kvp.Key] = kvp.Value;
            }
        }

        private void CacheOriginalWeights()
        {
            _cachedOriginalWeights.Clear();

            foreach (var mapping in _blendShapeIndexMap)
            {
                int originalIndex = mapping.Key;
                int customIndex = mapping.Value;
                float originalWeight = originalRenderer.GetBlendShapeWeight(originalIndex);
                _cachedOriginalWeights[customIndex] = originalWeight;
            }
        }

        private void SynchronizeBlendShapes()
        {
            foreach (var mapping in _blendShapeIndexMap)
            {
                int originalIndex = mapping.Key;
                int customIndex = mapping.Value;

                string blendShapeName = customRenderer.sharedMesh.GetBlendShapeName(customIndex);
                float originalWeight = _cachedOriginalWeights[customIndex];

                // ============ 新增：优先级系统 ============
                // 优先级1: 单独配置
                if (_configLookup.TryGetValue(blendShapeName, out var config))
                {
                    if (config.isDisabled)
                    {
                        customRenderer.SetBlendShapeWeight(customIndex, 0f);
                        continue;
                    }

                    float adjustedWeight = CalculateWeightWithConfig(originalWeight, config);
                    customRenderer.SetBlendShapeWeight(customIndex, adjustedWeight);
                    continue;
                }

                // 优先级2: 全局分类配置
                GlobalCategoryConfig globalConfig = GetApplicableGlobalConfig(blendShapeName);
                if (globalConfig != null && globalConfig.enabled)
                {
                    if (globalConfig.isDisabled)
                    {
                        customRenderer.SetBlendShapeWeight(customIndex, 0f);
                        continue;
                    }

                    float adjustedWeight = CalculateWeightWithGlobalConfig(originalWeight, globalConfig);
                    customRenderer.SetBlendShapeWeight(customIndex, adjustedWeight);
                    continue;
                }

                // 优先级3: 默认直通
                customRenderer.SetBlendShapeWeight(customIndex, originalWeight);
                // ========================================
            }
        }

        // ============ 新增：获取适用的全局配置 ============
        private GlobalCategoryConfig GetApplicableGlobalConfig(string blendShapeName)
        {
            // Mouth 分类：以 "Mouth" 开头
            if (blendShapeName.Contains("Mouth"))
            {
                return _config.mouthGlobal;
            }

            // Eye 分类：包含 "Eye" 但不包含 "blink"（不区分大小写）
            if (blendShapeName.Contains("Eye") && !blendShapeName.ToLower().Contains("blink"))
            {
                return _config.eyeGlobal;
            }

            return null;
        }
        // =============================================

        private float CalculateWeightWithConfig(float sourceValue, BlendShapeConfigItem config)
        {
            return CalculateWeight(sourceValue, config.lowerThreshold, config.upperThreshold, 
                config.multiplier, config.isInverted);
        }

        // ============ 新增：全局配置计算 ============
        private float CalculateWeightWithGlobalConfig(float sourceValue, GlobalCategoryConfig config)
        {
            return CalculateWeight(sourceValue, config.lowerThreshold, config.upperThreshold, 
                config.multiplier, config.isInverted);
        }
        // =========================================

        private float CalculateWeight(float sourceValue, float lower, float upper, float multiplier, bool isInverted)
        {
            float value = isInverted ? (100f - sourceValue) : sourceValue;

            float normalized;
            if (upper - lower > 0.001f)
            {
                normalized = Mathf.Clamp01((value - lower) / (upper - lower));
            }
            else
            {
                normalized = value > lower ? 1f : 0f;
            }

            float result = normalized * 100f * multiplier;
            return Mathf.Clamp(result, 0f, 100f);
        }

        #endregion

        #region Public API for Debugger

        public Dictionary<int, int> GetBlendShapeIndexMap() => _blendShapeIndexMap;

        public BlendShapeConfigItem GetConfigForBlendShape(string blendShapeName)
        {
            return _configLookup.TryGetValue(blendShapeName, out var config) ? config : null;
        }

        public float GetCachedOriginalWeight(int customIndex)
        {
            return _cachedOriginalWeights.TryGetValue(customIndex, out float weight) ? weight : 0f;
        }

        // ============ 新增：获取全局配置 ============
        public GlobalCategoryConfig GetMouthGlobalConfig() => _config?.mouthGlobal;
        public GlobalCategoryConfig GetEyeGlobalConfig() => _config?.eyeGlobal;
        
        public void SetMouthGlobalConfig(GlobalCategoryConfig config)
        {
            if (_config != null)
                _config.mouthGlobal = config;
        }

        public void SetEyeGlobalConfig(GlobalCategoryConfig config)
        {
            if (_config != null)
                _config.eyeGlobal = config;
        }
        // =========================================

        #endregion
    }
}