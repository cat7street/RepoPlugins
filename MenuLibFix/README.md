# MenuLibFix

[MenuLib](https://thunderstore.io/c/repo/p/nickklmao/MenuLib/) 2.5.4 的兼容修复伴生插件（GUID：`cat7street.MenuLibFix`），适配 2026-09 版本 R.E.P.O.（build 23363152）。思路与 [BetterMapFix](https://github.com/TooRed2025/BetterMapFix) 相同：不改 MenuLib 本体，用一个伴生插件把有问题的钩子用 Harmony 重挂。

## 修复的问题

MenuLib 用 MonoMod ILHook + IL 模式匹配改写游戏方法，游戏更新后 IL 结构变化会产生非法 IL：

```
InvalidProgramException: Invalid IL code in (wrapper dynamic-method)
SemiFunc:DMD<SemiFunc::UIMouseHover> (...): IL_00e7: stloc.s 13
```

MenuLib.Awake 因此中断，`MenuPage.StateClosing` / `ChatManager.StateInactive` 两个钩子从未装上——依赖 MenuLib 的插件（各类带自绘菜单的模组）随之残血。

本插件用三个 Harmony prefix 重实现钩子效果：

| 补丁 | 做法 |
|---|---|
| `SemiFunc.UIMouseHover` | 当前版本游戏逻辑**完整移植** + MenuLib 自定义滚动框判定分支，全替换 |
| `MenuPage.StateClosing` | 当前版本逻辑移植 + 自定义页滑出定位 / 缓存页停用两个定制点 |
| `ChatManager.StateInactive` | 自定义输入框持焦点时跳过原方法 |

所有成员引用在编译期对着当前游戏程序集校验，游戏再更新时表现为**编译报错**而不是运行时炸。

## 安装

1. 先安装 [MenuLib 2.5.4](https://thunderstore.io/c/repo/p/nickklmao/MenuLib/)（必须）
2. 把 `MenuLibFix.dll` 放入 `BepInEx/plugins/`

## 构建

见[仓库根目录 README](../README.md#构建)。额外需要 MenuLib.dll（Thunderstore 包解压即得）：放到本目录或 `-p:MenuLibDll=<路径>` 指定。
