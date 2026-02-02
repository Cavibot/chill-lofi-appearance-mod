using Cavi.ChillWithAnyone.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Cavi.ChillWithAnyone.Components
{
    /// <summary>
    /// Interactive debugger for monitoring and editing blend shapes.
    /// Press F9 to toggle the debug window.
    /// </summary>
    public class BlendShapeDebugger : MonoBehaviour
    {
        #region Configuration

        [Header("References")]
        public BlendShapeLinker linker;

        [Header("Hotkeys")]
        public KeyCode toggleKey = KeyCode.F9;

        #endregion

        #region GUI State

        private bool _showWindow = false;
        private Rect _windowRect = new Rect(50, 50, 1400, 700);
        private Vector2 _scrollPosition;
        private string _searchFilter = "";
        private bool _showOnlyActive = false;
        private bool _showOnlyConfigured = false;

        // 编辑状态
        private Dictionary<string, string> _editingMultiplier = new Dictionary<string, string>();
        private Dictionary<string, string> _editingLower = new Dictionary<string, string>();
        private Dictionary<string, string> _editingUpper = new Dictionary<string, string>();

        #endregion

        #region Runtime Data

        private class BlendShapeInfo
        {
            public int originalIndex;
            public int customIndex;
            public string name;
            public float originalWeight;
            public float customWeight;
            public BlendShapeConfigItem config;
            public bool hasConfig;
        }

        private List<BlendShapeInfo> _blendShapeList;
        private Animator _animator;

        #endregion

        #region Unity Lifecycle

        private void Start()
        {
            _animator = GetComponentInParent<Animator>();
            if (_animator != null)
            {
                ModLogger.Info($"BlendShapeDebugger: Found Animator on {_animator.gameObject.name}");
            }
        }

        private void Update()
        {
            if (Input.GetKeyDown(toggleKey))
            {
                _showWindow = !_showWindow;
                if (_showWindow)
                {
                    RefreshData();
                    ModLogger.Info("BlendShapeDebugger: Window opened");
                }
            }

            if (_showWindow && linker != null)
            {
                RefreshWeights();
            }
        }

        private void OnGUI()
        {
            if (!_showWindow) return;

            GUI.skin.window.fontSize = 14;
            GUI.skin.label.fontSize = 11;
            GUI.skin.button.fontSize = 11;
            GUI.skin.textField.fontSize = 11;
            GUI.skin.toggle.fontSize = 11;

            _windowRect = GUILayout.Window(
                12345,
                _windowRect,
                DrawDebugWindow,
                "Blend Shape Editor & Monitor",
                GUILayout.MinWidth(1400),
                GUILayout.MinHeight(700)
            );
        }

        #endregion

        #region GUI Drawing

        private void DrawDebugWindow(int windowID)
        {
            GUILayout.BeginVertical();

            DrawStatusBar();
            DrawAnimatorControl();
            

            GUILayout.Space(10);
            DrawGlobalCategories();


            GUILayout.Space(10);
            DrawSearchBar();
            GUILayout.Space(5);
            DrawBlendShapeList();

            GUILayout.EndVertical();

            GUI.DragWindow(new Rect(0, 0, 10000, 20));
        }

        private void DrawStatusBar()
        {
            GUILayout.BeginVertical(GUI.skin.box);

            GUILayout.BeginHorizontal();
            GUILayout.Label($"Linker: {(linker != null ? "✓" : "✗")}", GUILayout.Width(100));
            GUILayout.Label($"Original: {(linker?.originalRenderer != null ? "✓" : "✗")}", GUILayout.Width(100));
            GUILayout.Label($"Custom: {(linker?.customRenderer != null ? "✓" : "✗")}", GUILayout.Width(100));
            GUILayout.Label($"Animator: {(_animator != null ? "✓" : "✗")}", GUILayout.Width(100));
            GUILayout.Label($"Blend Shapes: {_blendShapeList?.Count ?? 0}", GUILayout.Width(150));

            int configuredCount = _blendShapeList?.Count(b => b.hasConfig) ?? 0;
            GUILayout.Label($"Configured: {configuredCount}", GUILayout.Width(120));

            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
        }

        private void DrawAnimatorControl()
        {
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("Animator Control", EditorStyles.boldLabel);

            if (_animator == null)
            {
                GUILayout.Label("⚠ No Animator found");
            }
            else
            {
                GUILayout.BeginHorizontal();

                GUILayout.Label($"Status: {(_animator.enabled ? "RUNNING" : "PAUSED")}", GUILayout.Width(150));
                GUILayout.Label($"Layers: {_animator.layerCount}", GUILayout.Width(100));

                GUILayout.FlexibleSpace();

                Color buttonColor = GUI.backgroundColor;
                GUI.backgroundColor = _animator.enabled ? Color.red : Color.green;

                if (GUILayout.Button(_animator.enabled ? "⏸ PAUSE" : "▶ RESUME", GUILayout.Width(120), GUILayout.Height(30)))
                {
                    _animator.enabled = !_animator.enabled;
                    ModLogger.Info($"Animator {(_animator.enabled ? "RESUMED" : "PAUSED")}");
                }

                GUI.backgroundColor = buttonColor;
                GUILayout.EndHorizontal();

                if (!_animator.enabled)
                {
                    Color oldColor = GUI.backgroundColor;
                    GUI.backgroundColor = new Color(1f, 0.8f, 0f);
                    GUILayout.Box("⚠ Animator is PAUSED - Blend shapes are frozen", GUILayout.ExpandWidth(true));
                    GUI.backgroundColor = oldColor;
                }
            }

            GUILayout.EndVertical();
        }

        private void DrawSearchBar()
        {
            GUILayout.BeginHorizontal();

            GUILayout.Label("Search:", GUILayout.Width(60));
            _searchFilter = GUILayout.TextField(_searchFilter, GUILayout.Width(200));

            _showOnlyActive = GUILayout.Toggle(_showOnlyActive, "Active (>0.1)", GUILayout.Width(110));
            _showOnlyConfigured = GUILayout.Toggle(_showOnlyConfigured, "Configured", GUILayout.Width(100));

            GUILayout.FlexibleSpace();

            // ============ 新增：全局保存按钮 ============
            Color saveColor = GUI.backgroundColor;
            GUI.backgroundColor = Color.green;
            if (GUILayout.Button("💾 SAVE ALL TO FILE", GUILayout.Width(150), GUILayout.Height(25)))
            {
                SaveAllConfigurationsToFile();
            }
            GUI.backgroundColor = saveColor;
            // =========================================

            if (GUILayout.Button("Refresh", GUILayout.Width(100)))
            {
                RefreshData();
            }

            if (GUILayout.Button("Close [F9]", GUILayout.Width(100)))
            {
                _showWindow = false;
            }

            GUILayout.EndHorizontal();
        }

        private void DrawBlendShapeList()
        {
            if (_blendShapeList == null || _blendShapeList.Count == 0)
            {
                GUILayout.Label("No blend shapes available.");
                return;
            }

            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition, GUILayout.ExpandHeight(true));

            // ============ 修复：改进 Active 过滤逻辑 ============
            var filteredList = _blendShapeList.Where(d =>
                (string.IsNullOrEmpty(_searchFilter) || d.name.ToLower().Contains(_searchFilter.ToLower())) &&
                (!_showOnlyActive || IsActiveBlendShape(d)) &&  // ← 使用新的判断方法
                (!_showOnlyConfigured || d.hasConfig)
            ).ToList();
            // ==================================================

            // 表头
            GUILayout.BeginHorizontal(GUI.skin.box);
            GUILayout.Label("Name", GUILayout.Width(250));
            GUILayout.Label("Orig", GUILayout.Width(50));
            GUILayout.Label("→", GUILayout.Width(20));
            GUILayout.Label("Cust", GUILayout.Width(50));
            GUILayout.Label("Diff", GUILayout.Width(50));
            GUILayout.Label("En", GUILayout.Width(30));
            GUILayout.Label("M", GUILayout.Width(60));
            GUILayout.Label("L", GUILayout.Width(60));
            GUILayout.Label("U", GUILayout.Width(60));
            GUILayout.Label("Inv", GUILayout.Width(35));
            GUILayout.Label("Actions", GUILayout.Width(150));
            GUILayout.EndHorizontal();

            // 数据行
            foreach (var info in filteredList)
            {
                DrawBlendShapeEditableRow(info);
            }

            GUILayout.EndScrollView();
        }

        private void DrawBlendShapeEditableRow(BlendShapeInfo info)
        {
            GUILayout.BeginHorizontal(GUI.skin.box);

            // 名称（缩短以节省空间）
            Color originalColor = GUI.contentColor;
            if (info.originalWeight > 50f)
                GUI.contentColor = Color.green;
            else if (info.originalWeight > 10f)
                GUI.contentColor = Color.yellow;
            else if (info.originalWeight > 0.1f)
                GUI.contentColor = Color.gray;

            string shortName = info.name.Length > 30 ? info.name.Substring(0, 27) + "..." : info.name;
            GUILayout.Label(shortName, GUILayout.Width(250));
            GUI.contentColor = originalColor;

            // 原始值
            GUILayout.Label($"{info.originalWeight:F1}", GUILayout.Width(50));
            GUILayout.Label("→", GUILayout.Width(20));

            // 自定义值
            Color customColor = GUI.contentColor;
            if (Mathf.Abs(info.customWeight - info.originalWeight) > 0.1f)
                GUI.contentColor = Color.cyan;
            GUILayout.Label($"{info.customWeight:F1}", GUILayout.Width(50));
            GUI.contentColor = customColor;

            // 差异
            float diff = info.customWeight - info.originalWeight;
            Color diffColor = GUI.contentColor;
            if (diff > 0.1f)
                GUI.contentColor = Color.green;
            else if (diff < -0.1f)
                GUI.contentColor = Color.red;
            GUILayout.Label($"{diff:+0;-0;0}", GUILayout.Width(50));
            GUI.contentColor = diffColor;

            // ============ 编辑控件 ============

            // 确保配置存在
            if (info.config == null)
            {
                info.config = new BlendShapeConfigItem
                {
                    sourceName = info.name,
                    multiplier = 1.0f,
                    upperThreshold = 100.0f,
                    lowerThreshold = 0.0f,
                    isInverted = false,
                    isDisabled = false
                };
                info.hasConfig = false;
            }

            // 启用/禁用开关
            bool newDisabled = GUILayout.Toggle(!info.config.isDisabled, "", GUILayout.Width(30));
            if (newDisabled != !info.config.isDisabled)
            {
                info.config.isDisabled = !newDisabled;
                ApplyConfigToLinker(info);
            }

            // Multiplier 输入框
            if (!_editingMultiplier.ContainsKey(info.name))
                _editingMultiplier[info.name] = info.config.multiplier.ToString("F2");

            _editingMultiplier[info.name] = GUILayout.TextField(_editingMultiplier[info.name], GUILayout.Width(60));

            // Lower Threshold 输入框
            if (!_editingLower.ContainsKey(info.name))
                _editingLower[info.name] = info.config.lowerThreshold.ToString("F0");

            _editingLower[info.name] = GUILayout.TextField(_editingLower[info.name], GUILayout.Width(60));

            // Upper Threshold 输入框
            if (!_editingUpper.ContainsKey(info.name))
                _editingUpper[info.name] = info.config.upperThreshold.ToString("F0");

            _editingUpper[info.name] = GUILayout.TextField(_editingUpper[info.name], GUILayout.Width(60));

            // 反转开关
            bool newInverted = GUILayout.Toggle(info.config.isInverted, "", GUILayout.Width(35));
            if (newInverted != info.config.isInverted)
            {
                info.config.isInverted = newInverted;
                ApplyConfigToLinker(info);
            }

            // 应用按钮
            if (GUILayout.Button("Preview ✓", GUILayout.Width(120)))
            {
                ApplyEditedValues(info);
            }

            // 重置按钮
            if (GUILayout.Button("Reload ↺", GUILayout.Width(120)))
            {
                ResetToDefault(info);
            }

            // 添加/移除配置按钮
            if (info.hasConfig)
            {
                Color removeColor = GUI.backgroundColor;
                GUI.backgroundColor = Color.red;
                if (GUILayout.Button("Discard ✗", GUILayout.Width(120)))
                {
                    RemoveConfig(info);
                }
                GUI.backgroundColor = removeColor;
            }
            else
            {
                Color addColor = GUI.backgroundColor;
                GUI.backgroundColor = Color.green;
                if (GUILayout.Button("Record +", GUILayout.Width(120)))
                {
                    AddConfig(info);
                }
                GUI.backgroundColor = addColor;
            }

            // 立即保存单项
            if (GUILayout.Button("💾", GUILayout.Width(30)))
            {
                SaveSingleConfig(info);
            }

            // ================================

            GUILayout.EndHorizontal();
        }

        private void DrawGlobalCategories()
        {
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("Global Categories (Lower Priority)", EditorStyles.boldLabel);

            // Mouth Global
            DrawGlobalCategory("MOUTH (Mouth_*)", linker?.GetMouthGlobalConfig(), 
                (config) => linker?.SetMouthGlobalConfig(config));

            GUILayout.Space(5);

            // Eye Global
            DrawGlobalCategory("EYE (Eye_* except Blink)", linker?.GetEyeGlobalConfig(), 
                (config) => linker?.SetEyeGlobalConfig(config));

            GUILayout.EndVertical();
        }

        private void DrawGlobalCategory(string name, GlobalCategoryConfig config, System.Action<GlobalCategoryConfig> setter)
        {
            if (config == null) return;

            GUILayout.BeginHorizontal(GUI.skin.box);

            // 启用开关
            Color oldColor = GUI.backgroundColor;
            if (config.enabled)
                GUI.backgroundColor = Color.green;

            bool newEnabled = GUILayout.Toggle(config.enabled, "", GUILayout.Width(30));
            if (newEnabled != config.enabled)
            {
                config.enabled = newEnabled;
                setter(config);
            }

            GUI.backgroundColor = oldColor;

            // 名称
            GUILayout.Label(name, GUILayout.Width(200));

            if (config.enabled)
            {
                // M
                GUILayout.Label("M:", GUILayout.Width(20));
                string mStr = config.multiplier.ToString("F2");
                string newMStr = GUILayout.TextField(mStr, GUILayout.Width(50));
                if (newMStr != mStr && float.TryParse(newMStr, out float m))
                {
                    config.multiplier = Mathf.Clamp(m, 0f, 10f);
                    setter(config);
                }

                // L
                GUILayout.Label("L:", GUILayout.Width(20));
                string lStr = config.lowerThreshold.ToString("F0");
                string newLStr = GUILayout.TextField(lStr, GUILayout.Width(50));
                if (newLStr != lStr && float.TryParse(newLStr, out float l))
                {
                    config.lowerThreshold = Mathf.Clamp(l, 0f, 100f);
                    setter(config);
                }

                // U
                GUILayout.Label("U:", GUILayout.Width(20));
                string uStr = config.upperThreshold.ToString("F0");
                string newUStr = GUILayout.TextField(uStr, GUILayout.Width(50));
                if (newUStr != uStr && float.TryParse(newUStr, out float u))
                {
                    config.upperThreshold = Mathf.Clamp(u, 0f, 100f);
                    setter(config);
                }

                // Inverted
                GUILayout.Label("Inv:", GUILayout.Width(30));
                bool newInv = GUILayout.Toggle(config.isInverted, "", GUILayout.Width(30));
                if (newInv != config.isInverted)
                {
                    config.isInverted = newInv;
                    setter(config);
                }

                // Disabled
                GUILayout.Label("Dis:", GUILayout.Width(30));
                bool newDis = GUILayout.Toggle(config.isDisabled, "", GUILayout.Width(30));
                if (newDis != config.isDisabled)
                {
                    config.isDisabled = newDis;
                    setter(config);
                }
            }
            else
            {
                GUI.contentColor = Color.gray;
                GUILayout.Label("(Disabled - Individual configs take priority)", GUILayout.ExpandWidth(true));
                GUI.contentColor = Color.white;
            }

            GUILayout.EndHorizontal();
        }

        #endregion

        #region Data Management

        private void RefreshData()
        {
            if (linker == null)
            {
                ModLogger.Warning("BlendShapeDebugger: No BlendShapeLinker reference");
                return;
            }

            _blendShapeList = new List<BlendShapeInfo>();

            var blendShapeMap = linker.GetBlendShapeIndexMap();
            if (blendShapeMap == null)
            {
                ModLogger.Warning("BlendShapeDebugger: Could not get blend shape mapping");
                return;
            }

            foreach (var mapping in blendShapeMap)
            {
                int originalIndex = mapping.Key;
                int customIndex = mapping.Value;
                string name = linker.customRenderer.sharedMesh.GetBlendShapeName(customIndex);

                float originalWeight = linker.GetCachedOriginalWeight(customIndex);
                float customWeight = linker.customRenderer.GetBlendShapeWeight(customIndex);

                var config = linker.GetConfigForBlendShape(name);

                _blendShapeList.Add(new BlendShapeInfo
                {
                    originalIndex = originalIndex,
                    customIndex = customIndex,
                    name = name,
                    originalWeight = originalWeight,
                    customWeight = customWeight,
                    config = config,
                    hasConfig = config != null
                });
            }

            ModLogger.Info($"BlendShapeDebugger: Loaded {_blendShapeList.Count} blend shapes");
        }

        private void RefreshWeights()
        {
            if (_blendShapeList == null || linker == null) return;

            foreach (var info in _blendShapeList)
            {
                info.originalWeight = linker.GetCachedOriginalWeight(info.customIndex);
                info.customWeight = linker.customRenderer.GetBlendShapeWeight(info.customIndex);
            }
        }

        #endregion

        #region Edit Actions

        private void ApplyEditedValues(BlendShapeInfo info)
        {
            bool changed = false;

            // 解析 Multiplier
            if (_editingMultiplier.TryGetValue(info.name, out string mStr) &&
                float.TryParse(mStr, out float m))
            {
                info.config.multiplier = Mathf.Clamp(m, 0f, 10f);
                changed = true;
            }

            // 解析 Lower Threshold
            if (_editingLower.TryGetValue(info.name, out string lStr) &&
                float.TryParse(lStr, out float l))
            {
                info.config.lowerThreshold = Mathf.Clamp(l, 0f, 100f);
                changed = true;
            }

            // 解析 Upper Threshold
            if (_editingUpper.TryGetValue(info.name, out string uStr) &&
                float.TryParse(uStr, out float u))
            {
                info.config.upperThreshold = Mathf.Clamp(u, 0f, 100f);
                changed = true;
            }

            if (changed)
            {
                // 更新编辑框显示
                _editingMultiplier[info.name] = info.config.multiplier.ToString("F2");
                _editingLower[info.name] = info.config.lowerThreshold.ToString("F0");
                _editingUpper[info.name] = info.config.upperThreshold.ToString("F0");

                ApplyConfigToLinker(info);
                ModLogger.Info($"Applied: {info.name} M={info.config.multiplier:F2} L={info.config.lowerThreshold:F0} U={info.config.upperThreshold:F0}");
            }
        }

        private void ApplyConfigToLinker(BlendShapeInfo info)
        {
            if (!info.hasConfig)
            {
                info.hasConfig = true;
            }

            // 通过反射更新 linker 的配置
            var configLookup = GetConfigLookup();
            if (configLookup != null)
            {
                configLookup[info.name] = info.config;
            }
        }

        private void ResetToDefault(BlendShapeInfo info)
        {
            info.config.multiplier = 1.0f;
            info.config.lowerThreshold = 0.0f;
            info.config.upperThreshold = 100.0f;
            info.config.isInverted = false;
            info.config.isDisabled = false;

            _editingMultiplier[info.name] = "1.00";
            _editingLower[info.name] = "0";
            _editingUpper[info.name] = "100";

            ApplyConfigToLinker(info);
            ModLogger.Info($"Reset to default: {info.name}");
        }

        private void AddConfig(BlendShapeInfo info)
        {
            info.hasConfig = true;
            ApplyConfigToLinker(info);
            ModLogger.Info($"Added config: {info.name}");
        }

        private void RemoveConfig(BlendShapeInfo info)
        {
            info.hasConfig = false;

            var configLookup = GetConfigLookup();
            if (configLookup != null && configLookup.ContainsKey(info.name))
            {
                configLookup.Remove(info.name);
            }

            ModLogger.Info($"Removed config: {info.name}");
        }

        private Dictionary<string, BlendShapeConfigItem> GetConfigLookup()
        {
            var field = typeof(BlendShapeLinker).GetField("_configLookup",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return field?.GetValue(linker) as Dictionary<string, BlendShapeConfigItem>;
        }

        #endregion

        #region Save to File

        private void SaveSingleConfig(BlendShapeInfo info)
        {
            SaveAllConfigurationsToFile();
            ModLogger.Info($"Saved config for: {info.name}");
        }

        private void SaveAllConfigurationsToFile()
        {
            try
            {
                // ============ 修改：使用模组目录 ============
                string pluginPath = System.IO.Path.GetDirectoryName(typeof(BlendShapeLinker).Assembly.Location);
                string configPath = System.IO.Path.Combine(pluginPath, "blendshape_config.json");
                // ==========================================

                var configuredShapes = _blendShapeList
                    .Where(b => b.hasConfig && b.config != null)
                    .OrderBy(b => b.name)
                    .ToList();

                StringBuilder jsonBuilder = new StringBuilder();
                jsonBuilder.AppendLine("{");
                
                // ============ 新增：保存全局分类配置 ============
                var mouthGlobal = linker?.GetMouthGlobalConfig();
                var eyeGlobal = linker?.GetEyeGlobalConfig();

                if (mouthGlobal != null)
                {
                    jsonBuilder.AppendLine("    \"mouthGlobal\": {");
                    jsonBuilder.AppendLine($"        \"enabled\": {mouthGlobal.enabled.ToString().ToLower()},");
                    jsonBuilder.AppendLine($"        \"multiplier\": {mouthGlobal.multiplier},");
                    jsonBuilder.AppendLine($"        \"upperThreshold\": {mouthGlobal.upperThreshold},");
                    jsonBuilder.AppendLine($"        \"lowerThreshold\": {mouthGlobal.lowerThreshold},");
                    jsonBuilder.AppendLine($"        \"isInverted\": {mouthGlobal.isInverted.ToString().ToLower()},");
                    jsonBuilder.AppendLine($"        \"isDisabled\": {mouthGlobal.isDisabled.ToString().ToLower()}");
                    jsonBuilder.AppendLine("    },");
                }

                if (eyeGlobal != null)
                {
                    jsonBuilder.AppendLine("    \"eyeGlobal\": {");
                    jsonBuilder.AppendLine($"        \"enabled\": {eyeGlobal.enabled.ToString().ToLower()},");
                    jsonBuilder.AppendLine($"        \"multiplier\": {eyeGlobal.multiplier},");
                    jsonBuilder.AppendLine($"        \"upperThreshold\": {eyeGlobal.upperThreshold},");
                    jsonBuilder.AppendLine($"        \"lowerThreshold\": {eyeGlobal.lowerThreshold},");
                    jsonBuilder.AppendLine($"        \"isInverted\": {eyeGlobal.isInverted.ToString().ToLower()},");
                    jsonBuilder.AppendLine($"        \"isDisabled\": {eyeGlobal.isDisabled.ToString().ToLower()}");
                    jsonBuilder.AppendLine("    },");
                }
                // ===========================================

                jsonBuilder.AppendLine("    \"config\": [");

                for (int i = 0; i < configuredShapes.Count; i++)
                {
                    var info = configuredShapes[i];
                    var cfg = info.config;

                    jsonBuilder.AppendLine("        {");
                    jsonBuilder.AppendLine($"            \"sourceName\": \"{cfg.sourceName}\",");
                    jsonBuilder.AppendLine($"            \"multiplier\": {cfg.multiplier},");
                    jsonBuilder.AppendLine($"            \"upperThreshold\": {cfg.upperThreshold},");
                    jsonBuilder.AppendLine($"            \"lowerThreshold\": {cfg.lowerThreshold},");
                    jsonBuilder.AppendLine($"            \"isInverted\": {cfg.isInverted.ToString().ToLower()},");
                    jsonBuilder.AppendLine($"            \"isDisabled\": {cfg.isDisabled.ToString().ToLower()}");

                    if (i < configuredShapes.Count - 1)
                        jsonBuilder.AppendLine("       },");
                    else
                        jsonBuilder.AppendLine("        }");
                }

                jsonBuilder.AppendLine("    ]");
                jsonBuilder.AppendLine("}");

                // ============ 修改：目录自动创建逻辑 ============
                string directory = System.IO.Path.GetDirectoryName(configPath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                // ==============================================

                File.WriteAllText(configPath, jsonBuilder.ToString());

                ModLogger.Info($"✓ Saved {configuredShapes.Count} configurations + 2 global categories to {configPath}");

                // 显示成功提示
                StartCoroutine(ShowSaveSuccessMessage());
            }
            catch (Exception ex)
            {
                ModLogger.Error($"Failed to save config: {ex.Message}");
            }
        }

        private System.Collections.IEnumerator ShowSaveSuccessMessage()
        {
            // 这里可以添加视觉反馈
            yield return new WaitForSeconds(0.1f);
        }

        #endregion

        #region 新增的过滤逻辑

        // ============ 新增：改进的 Active 判断 ============
        /// <summary>
        /// 判断 blend shape 是否应该在 Active 模式下显示
        /// </summary>
        private bool IsActiveBlendShape(BlendShapeInfo info)
        {
            // 原始值活跃
            if (info.originalWeight > 0.1f)
                return true;
            
            // 自定义值活跃（被放大的情况）
            if (info.customWeight > 0.1f)
                return true;
            
            // 已配置且非默认值（用户正在编辑的）
            if (info.hasConfig && info.config != null)
            {
                // 如果有非默认配置，也显示
                if (info.config.multiplier != 1.0f ||
                    info.config.lowerThreshold != 0.0f ||
                    info.config.upperThreshold != 100.0f ||
                    info.config.isInverted ||
                    info.config.isDisabled)
                {
                    return true;
                }
            }
            
            return false;
        }
        // ===============================================

        #endregion
    }

    internal static class EditorStyles
    {
        public static GUIStyle boldLabel
        {
            get
            {
                var style = new GUIStyle(GUI.skin.label);
                style.fontStyle = FontStyle.Bold;
                style.fontSize = 13;
                return style;
            }
        }
    }
}