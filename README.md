# RepoPlugins

[R.E.P.O.](https://store.steampowered.com/app/3241660/REPO/) 自研 BepInEx 插件合集，针对 **2026-09 版本（Steam build 23363152）** 适配。两个插件同源同修：游戏更新导致一起失效时，改一次、编一次、发一次。

| 插件 | 作用 | 详见 |
|---|---|---|
| **RepoMapSuite** | 地图增强三合一：Tab 地图队友/敌人图标 + 可缩放小地图 + 地图剩余价值 HUD | [RepoMapSuite/README.md](RepoMapSuite/README.md) |
| **MenuLibFix** | MenuLib 2.5.4 在当前游戏版本上的兼容修复伴生插件 | [MenuLibFix/README.md](MenuLibFix/README.md) |

## 安装

需要已安装 [BepInEx 5.4.23（REPO 版）](https://thunderstore.io/c/repo/p/BepInEx/BepInExPack/)。

1. 从 [Releases](../../releases) 下载对应 DLL
2. 放入游戏目录的 `BepInEx/plugins/`
3. （MenuLibFix 需要）先安装 [MenuLib](https://thunderstore.io/c/repo/p/nickklmao/MenuLib/)

## 构建

依赖 .NET SDK 8+。直接引用**本机游戏目录的程序集**编译，保证和当前安装的游戏版本签名严格一致——这是这两个插件能修好旧模组兼容问题的关键：

```bash
# 方式一：命令行指定游戏目录
dotnet build -c Release -p:GameDir="D:\SteamLibrary\steamapps\common\REPO"

# 方式二：设置环境变量后直接构建
export REPO_GAME_DIR="D:\SteamLibrary\steamapps\common\REPO"
dotnet build -c Release
```

MenuLibFix 另需 MenuLib.dll（Thunderstore 下载解压），放到 `MenuLibFix/` 目录下或用 `-p:MenuLibDll=<路径>` 指定。

产物在各自的 `bin/Release/netstandard2.1/<插件名>.dll`。

## 游戏更新后怎么办

这类插件的失效几乎都是游戏 API 变动（方法改签名、成员转私有），表现是 `MissingMethodException` / `FieldAccessException` 刷屏。修法套路：

1. 对着新版本游戏的 `Assembly-CSharp.dll` 编译，让编译器把失效引用全部揪出来
2. 反编译核对变动成员的新签名（[ilspycmd](https://github.com/icsharpcode/ILSpy) 或 Cecil）
3. 修源码 → 重新编译 → 替换 DLL

两个项目的 `.csproj` 已配置好公共化编译（BepInEx.AssemblyPublicizer），照上面构建命令直接编即可。

## 致谢

本仓库代码移植并重构自以下开源项目，感谢原作者：

- **[TheEverythingMap](https://github.com/davidewetzel/TheEverythingMap)**（Nubez）— 小地图与地图图标的原始实现（RepoMapSuite）
- **[MapValueTracker](https://github.com/tansinator/MapValueTracker)**（Tansinator）— 价值 HUD 的原始实现（RepoMapSuite）
- **[BetterMapFix](https://github.com/TooRed2025/BetterMapFix)**（TooRed，MIT）— BetterMap 修复思路（RepoMapSuite 的图标管理）
- **[MenuLib](https://github.com/nickklmao/MenuLib)**（nickklmao）— MenuLibFix 所修复的目标库，钩子语义参考其实现

## 许可

[MIT](LICENSE) © 2026 cat7street。移植部分的权利归原作者所有，致谢见上。
