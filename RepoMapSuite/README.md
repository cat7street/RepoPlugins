# RepoMapSuite

R.E.P.O. 地图增强三合一插件（GUID：`cat7street.RepoMapSuite`），合并重制自已停更/失效的三个模组并适配 2026-09 版本游戏（build 23363152）：

| 原模组 | 替代功能 |
|---|---|
| BetterMap（clay，2025-03 停更） | Tab 大地图上的**队友 / 敌人位置图标** |
| TheEverythingMap（Nubez，1.0.8 后未更新） | 屏幕角落**实时小地图**，可缩放、可调位置和透明度 |
| MapValueTracker（Tansinator，2025-05 停更） | **地图剩余价值 HUD**，按 Tab 或打开地图时显示 |

单 DLL、无 MenuLib 依赖、全部补丁带异常隔离（不会像旧模组那样把异常漏进游戏 RPC 链刷屏）。

## 修复的兼容性问题

旧模组在当前游戏版本上的死因，本插件全部做了适配：

- `RoomVolume.SetExplored()` 返回值 `void → bool`（旧模组因此 MissingMethodException，还炸断关卡生成回调）
- `MapCustom.mapCustomEntity`、`EnemyParent.Enemy` 等成员转私有（旧 BetterMap 因此 FieldAccessException）
- `PhysGrabObjectImpactDetector.BreakRPC` 从 2 参数变 5 参数（旧 MapValueTracker 补丁挂载失败）
- 不再依赖 MenuLib（它在当前版本的 `SemiFunc.UIMouseHover` IL 钩子会损坏；设置改走配置文件 + 热键）

## 配置

配置文件 `BepInEx/config/cat7street.RepoMapSuite.cfg`（首次运行生成，中文注释）：

- **1. 地图图标**：ShowTeammates / TeammateColor / DeadTeammateColor / ShowEnemies / EnemyColor / ShowItems / ItemColor / ExploreAllRooms（进图自动点亮全图）
- **2. 小地图**：Enabled / Preset（六宫格位置）/ Size / Buffer / Zoom / Opacity / ZoomInKey（默认 `=`）/ ZoomOutKey（默认 `-`）
- **3. 价值 HUD**：Enabled / AlwaysOn / StartingValueOnly / UseValueRatio / ValueRatio / Position

颜色支持 `white` `red` `green` `blue` `yellow` `cyan` `magenta` `black` `lilac` `purple` 或 `#RRGGBB`。

## 构建

见[仓库根目录 README](../README.md#构建)。产物：`bin/Release/netstandard2.1/RepoMapSuite.dll`。
