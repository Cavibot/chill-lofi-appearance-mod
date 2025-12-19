# Eku Skin Mod for [Game Name]

[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
[![BepInEx](https://img.shields.io/badge/BepInEx-5.4.x-green.svg)](https://github.com/BepInEx/BepInEx)
[![Game Version](https://img.shields.io/badge/game-v1.0.4-orange.svg)](#compatibility)

A character appearance replacement mod for [Game Name], featuring custom models with lilToon shader support.

![Preview](docs/preview.png)  <!-- 添加截图 -->

---

## ✨ Features

- 🎨 **Custom Character Model**: Replace the default character with custom 3D models
- 🎭 **Facial Expression Sync**: Automatic blend shape synchronization with game animations
- 👓 **Accessory Control**: Toggle glasses and headphones via config file
- 🖌️ **Shader Support**: Compatible with lilToon and URP Lit shaders
- ⚙️ **Configurable**: Easy-to-edit configuration file

---

## 📦 Installation

### Prerequisites

- [BepInEx 5.4.x](https://github.com/BepInEx/BepInEx/releases) or higher
- [Game Name] version 1.0.4 or compatible

### Steps

1. **Install BepInEx**
   - Download BepInEx from the link above
   - Extract to your game's root directory
   - Run the game once to generate BepInEx folders

2. **Install the Mod**
   - Download the latest release from [Releases](https://github.com/yourusername/yourrepo/releases)
   - Extract `EkuSkinMod` folder to `BepInEx/plugins/`
   - Your folder structure should look like:
     ```
     [Game Root]/
     └── BepInEx/
         └── plugins/
             └── EkuSkinMod/
                 ├── Cavi.AppearanceMod.dll
                 ├── assets
                 └── config.txt
     ```

3. **Launch the Game**
   - The mod will load automatically
   - Check `BepInEx/LogOutput.log` for any errors

---

## ⚙️ Configuration

Edit `BepInEx/plugins/EkuSkinMod/config.txt`:
```
# Eku Skin Mod Configuration
# Enable glasses (true=show, false=hide)
ENABLE_GLASSES=false
```


---

## 🎮 Usage

- The character model is replaced automatically when entering a room
- Facial expressions sync with game animations
- Toggle glasses in the config file (requires restart)

---

## 🛠️ Building from Source

### Requirements

- Visual Studio 2019+ or Rider
- .NET Standard 2.1 SDK
- Unity Editor (for creating AssetBundles)

### Build Steps

1. Clone the repository:
git clone https://github.com/yourusername/yourrepo.git cd yourrepo


2. Restore dependencies:
dotnet restore


3. Copy game assemblies:
   - Copy the following DLLs from `[Game]/[Game]_Data/Managed/` to `libs/`:
     - `Assembly-CSharp.dll`
     - `UnityEngine.dll`
     - `UnityEngine.CoreModule.dll`
     - (Add other required DLLs)

4. Build the project:

dotnet build -c Release

5. Output DLL will be in `bin/Release/netstandard2.1/`

---

## 📁 Project Structure

Cavi.AppearanceMod/ ├── Components/           # Unity components │   └── BlendShapeLinker.cs ├── Patches/             # Harmony patches │   └── CharacterPatches.cs ├── Utils/               # Utility classes │   └── ModLogger.cs ├── AppearancePlugin.cs  # Main plugin entry └── README.md


---

---

## 🐛 Troubleshooting

### Model not appearing
- Check `BepInEx/LogOutput.log` for errors
- Ensure `assets` file exists in the mod folder
- Verify game version compatibility

### Pink materials
- Check if URP Lit shader is available in the game
- Verify AssetBundle was built correctly

### Facial expressions not working
- Check BlendShapeLinker component is attached
- Verify blend shape names match between original and custom model

---

## 🤝 Contributing

Contributions are welcome! Please:

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/AmazingFeature`)
3. Commit your changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to the branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request

---

## 📜 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

---

## 🙏 Credits

- **Game**: [Game Name] by [Developer]
- **BepInEx**: [BepInEx Team](https://github.com/BepInEx/BepInEx)
- **lilToon**: [lilxyzw](https://github.com/lilxyzw/lilToon)
- **Model**: [Artist Name / Source]

---

## ⚠️ Disclaimer

This mod is not affiliated with or endorsed by the game's developers. Use at your own risk.

---

## 📧 Contact

- GitHub: [@yourusername](https://github.com/yourusername)
- Issues: [Report a bug](https://github.com/yourusername/yourrepo/issues)

---

## 🔄 Changelog

See [RELEASES.md](RELEASES.md) for version history.