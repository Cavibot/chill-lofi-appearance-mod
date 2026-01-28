# 在 Debian 13 下测试通过。需要安装 .NET SDK
# sudo apt install -y make dotnet-sdk-8.0

# --- 路径配置 ---
# 游戏根目录
GAME_ROOT ?= $(HOME)/.steam/steam/steamapps/common/Chill with You Lo-Fi Story

# --- 编译配置 ---
CONFIGURATION = Release
PROJECT = src/Cavi.ChillWithAnyone.csproj

.PHONY: all build clean check-path

all: build

check-path:
	@if [ ! -d "$(GAME_ROOT)" ]; then \
		echo "错误: 找不到游戏目录:"; \
		echo "  $(GAME_ROOT)"; \
		echo "你可以通过命令指定路径: make GAME_ROOT='/your/custom/path'"; \
		exit 1; \
	fi
	@if [ ! -d "$(GAME_ROOT)/Chill With You_Data/Managed" ]; then \
		echo "错误: 找不到 Managed 目录:"; \
		echo "  $(GAME_ROOT)/Chill With You_Data/Managed"; \
		echo "你可能需要验证游戏文件完整性或重装游戏"; \
		exit 1; \
	fi
	@echo "已找到游戏路径: $(GAME_ROOT)"

build: check-path
	dotnet build $(PROJECT) \
		-c $(CONFIGURATION) \
		/p:GamePath="$(GAME_ROOT)"

clean:
	rm -rf src/bin/ src/obj/
