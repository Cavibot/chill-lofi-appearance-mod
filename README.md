# A Custom Skin Mod for Chill with You: Lo-Fi Story

这是一个为 Chill with You: Lo-Fi Story 制作的角色外观替换 Mod，支持自定义 3D 模型。

自带一只 Eku，也可以替换为自己的模型（需要绑定游戏骨骼）。

---

## 📦 安装步骤

### 环境要求

* [BepInEx 5.4.x](https://github.com/BepInEx/BepInEx/releases) 或更高版本。

### 模组安装方法

1. **安装 BepInEx**
* 从上方链接下载 BepInEx。
* 解压至游戏根目录。
* 运行一次游戏以生成 BepInEx 相关文件夹。

2. **安装 Mod**
* 从 Release 下载最新版本的 Mod 压缩包。
* 将 `ChillWithAnyone` 文件夹解压至 `BepInEx/plugins/` 目录下。
* 注意：**请将 `BepInEx/config/BepInEx.cfg` 中的 `HideManagerGameObject` 修改为 true**
* 确保你的文件夹结构如下所示：


```
[游戏根目录]/
└── BepInEx/
    └── plugins/
        └── ChillWithAnyone/
            ├── Cavi.ChillWithAnyone.dll
            ├── assets
            └── config.txt

```

3. **启动游戏**
* Mod 将会自动加载。
* 如果遇到问题，请检查 `BepInEx/LogOutput.log` 文件。

---

## ⚙️ 配置文件

编辑 `BepInEx/plugins/ChillWithAnyone/config.txt`：

```ini
# ChillWithAnyone 配置文件
# 是否显示眼镜 (true=显示, false=隐藏)
ENABLE_GLASSES=false
```

---

## 🎮 使用说明

* 进入游戏后，角色模型会自动完成替换。
* 可以在配置文件中切换配饰状态（修改后需重启游戏）。

---

## 🛠️ 从源码构建

### 构建环境

**Windows 用户：**
* Visual Studio 2019+ 或 Rider
* .NET SDK 6.0+

**Linux 用户：**
* Make (`sudo apt install make`)
* .NET SDK 6.0+ (`sudo apt install dotnet-sdk-8.0` on Debian/Ubuntu)

### 构建步骤

#### 方法 1：使用 Makefile（推荐用于 Linux）

1. 克隆仓库：
```bash
git clone https://github.com/clsty/chill-lofi-appearance-mod.git
cd chill-lofi-appearance-mod
```

2. 使用 Make 构建（需要指定游戏目录）：
```bash
# 默认路径 (Linux Steam)
make

# 或者自定义游戏路径
make GAME_ROOT="/path/to/Chill with You Lo-Fi Story"
```

3. 生成的 DLL 将位于 `src/bin/Release/netstandard2.1/` 目录下。

#### 方法 2：使用 dotnet/Visual Studio（推荐用于 Windows）

1. 克隆仓库：
```bash
git clone https://github.com/clsty/chill-lofi-appearance-mod.git
cd chill-lofi-appearance-mod
```

2. 使用 dotnet 构建（需要指定游戏目录）：
```bash
dotnet build -c Release /p:GamePath="C:\Program Files (x86)\Steam\steamapps\common\Chill with You Lo-Fi Story"
```

或在 Visual Studio 中：
- 打开 `ChillWithAnyoneMod.sln`
- 在项目属性或环境变量中设置 `GamePath` 为你的游戏安装路径
- 构建解决方案

3. 生成的 DLL 将位于 `src/bin/Release/netstandard2.1/` 目录下。

### 注意事项

* 现在**不再需要**手动复制 DLL 文件到 `libs/` 目录
* 构建系统会自动从游戏目录引用所需的 DLL
* **必须**拥有游戏安装才能成功构建（需要游戏的 Assembly-CSharp.dll）
* CI/CD 构建会自动从 NuGet 和 GitHub 下载必要的依赖

---

## 📁 项目结构

```text
Cavi.ChillWithAnyone/
├── Components/           # Unity 组件逻辑 (如 BlendShapeLinker)
│   └── BlendShapeLinker.cs
├── Patches/              # Harmony 补丁逻辑 (核心 Hook 代码)
│   └── CharacterPatches.cs
├── Utils/                # 工具类与扩展方法
│   └── TransformExtensions.cs
├── ChillWithAnyonePlugin.cs   # BepInEx 插件入口文件
└── README.md
```

---

## 🐛 常见问题排查

### 模型没有出现

* 检查 `BepInEx/LogOutput.log` 是否报错。
* 确保 `assets` 资源文件存放在正确的插件目录下。
* 核对游戏版本是否匹配。


---

## 🤝 参与贡献

欢迎提交 Issue 或 Pull Request！

1. Fork 本项目。
2. 创建你的特性分支 (`git checkout -b feature/AmazingFeature`)。
3. 提交你的改动 (`git commit -m 'Add some AmazingFeature'`)。
4. 推送到分支 (`git push origin feature/AmazingFeature`)。
5. 开启一个 Pull Request。

---

## 📜 许可协议

本项目基于 MIT 协议开源 - 详情请参阅 [LICENSE](LICENSE) 文件。

---

## ⚠️ 免责声明

本模组仅供学习与技术交流使用，严禁用于任何商业用途。

模型版权归属原作者，请勿进行二传或二次修改。

模组处于测试阶段，建议安装前备份存档。模组不会对游戏本体进行破坏性修改，如因使用本插件造成损失，作者不承担相关责任。

---
