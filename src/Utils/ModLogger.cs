using BepInEx.Logging;

namespace Cavi.ChillWithAnyone.Utils
{
    /// <summary>
    /// 统一日志输出工具类
    /// </summary>
    public static class ModLogger
    {
        private static ManualLogSource _logger;

        /// <summary>
        /// 初始化日志（在 ChillWithAnyonePlugin.Awake 中调用）
        /// </summary>
        public static void Initialize(ManualLogSource logger)
        {
            _logger = logger;
        }

        public static void Info(string message)
        {
            _logger?.LogInfo($"【Mod】{message}");
        }

        public static void Warning(string message)
        {
            _logger?.LogWarning($"【Mod警告】{message}");
        }

        public static void Error(string message)
        {
            _logger?.LogError($"【Mod错误】{message}");
        }

        public static void Debug(string message)
        {
            _logger?.LogDebug($"【Mod调试】{message}");
        }

        // 带分类的日志方法
        public static void LogOperation(string message)
        {
            _logger?.LogInfo($"【Mod操作】{message}");
        }

        public static void LogInjection(string message)
        {
            _logger?.LogInfo($"【Mod注入】{message}");
        }

        public static void LogConfig(string message)
        {
            _logger?.LogInfo($"【配置】{message}");
        }
    }
}