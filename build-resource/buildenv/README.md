# Build Environment

此目录包含用于构建 Mod 的环境配置和脚本。

## 脚本说明

### download-unityengine.sh
从 NuGet 下载 UnityEngine 模块 DLL 文件到 mokgamedir 目录。

### download-bepinex.sh
从 GitHub Releases 下载 BepInEx 并解压到 assets 目录，同时复制核心 DLL 到 mokgamedir。

## mokgamedir 结构

`mokgamedir` 是一个模拟游戏目录结构，用于 CI/CD 在线构建。

```
mokgamedir/
├── BepInEx/           # 软链接到 ../assets/BepInEx_win_x64/BepInEx
└── Chill With You_Data/
    └── Managed/       # 游戏和 Unity 引擎 DLL
        ├── Assembly-CSharp.dll     # 游戏代码（需手动提供或从游戏安装获取）
        ├── UnityEngine.dll         # 由 download-unityengine.sh 自动下载
        └── ...其他 Unity 模块...
```

## 关于 Assembly-CSharp.dll

**重要：** `Assembly-CSharp.dll` 是游戏特定的程序集，包含游戏的代码。它无法从公共源下载。

- **本地构建：** 使用真实游戏目录，无需手动提供此文件
- **CI 构建：** 需要手动将此文件添加到 `mokgamedir/Chill With You_Data/Managed/` 目录中
  - 该文件已在 `.gitignore` 中设置为例外，可以提交到仓库用于 CI
  - 或者通过 GitHub Secrets 等方式在 CI 运行时提供

如果没有此文件，CI 构建将失败，但本地构建不受影响。
