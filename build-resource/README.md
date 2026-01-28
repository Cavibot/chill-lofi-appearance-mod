# Build Resources

此目录包含用于构建和打包 Mod 的资源文件。

## 目录结构

```
build-resource/
├── buildenv/           # 构建环境脚本
│   ├── mokgamedir/    # 模拟游戏目录（CI 构建时使用）
│   ├── download-unityengine.sh  # 从 NuGet 下载 Unity 引擎 DLL
│   └── download-bepinex.sh      # 从 GitHub 下载 BepInEx
└── assets/            # 最终打包资源
    └── BepInEx_win_x64/  # BepInEx 前置（CI 构建时下载）
```

## 说明

- `buildenv/mokgamedir/` 是一个模拟的游戏目录结构，用于 CI/CD 在线构建时提供必要的 DLL 引用。
- 本地构建时，使用真实的游戏目录（通过 `GAME_ROOT` 参数指定）。
- CI 构建时，使用 `mokgamedir` 目录，其中的 DLL 由下载脚本自动获取。
