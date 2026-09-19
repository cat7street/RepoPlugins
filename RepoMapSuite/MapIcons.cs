using HarmonyLib;
using System;
using System.Linq;
using UnityEngine;

namespace RepoMapSuite
{
    /// <summary>
    /// 地图图标功能（原 BetterMap + TheEverythingMap 的图标部分）。
    /// 修复点：RoomVolume.SetExplored() 返回值已从 void 变为 bool（旧版因此 MissingMethodException）、
    /// MapCustom.mapCustomEntity 已转私有（旧 BetterMap 因此 FieldAccessException）。
    /// 所有补丁体均 try/catch 包裹，异常绝不再漏进游戏的 RPC 派发链。
    /// </summary>
    internal static class MapIcons
    {
        internal static void ShowPlayers()
        {
            if (!Cfg.ShowTeammates.Value) return;
            if (GameDirector.instance?.PlayerList == null) return;

            foreach (var player in GameDirector.instance.PlayerList.Where(p => p != null && p.gameObject != null))
            {
                var existing = player.GetComponent<MapCustom>();
                if (existing != null) UnityEngine.Object.Destroy(existing);

                var mc = player.gameObject.AddComponent<MapCustom>();
                mc.sprite = RepoMapSuite.SpriteCircle;
                mc.color = Util.ParseColor(Cfg.TeammateColor.Value);
                mc.Add();
            }

            // 已死亡玩家的头颅图标单独染色
            foreach (var head in UnityEngine.Object.FindObjectsOfType<PlayerDeathHead>())
            {
                if (head != null && head.mapCustom != null)
                {
                    head.mapCustom.color = Util.ParseColor(Cfg.DeadTeammateColor.Value);
                }
            }
        }

        internal static void ShowEnemies()
        {
            if (!Cfg.ShowEnemies.Value) return;
            if (EnemyDirector.instance?.enemiesSpawned == null) return;

            foreach (var ep in EnemyDirector.instance.enemiesSpawned)
            {
                if (ep == null || ep.gameObject == null || ep.Enemy == null) continue;

                bool alive = ep.isActiveAndEnabled
                    && (ep.Enemy.Health == null || !ep.Enemy.Health.dead)
                    && ep.Enemy.CurrentState != EnemyState.Despawn
                    && ep.Enemy.CurrentState != EnemyState.None;

                var mc = ep.Enemy.GetComponent<MapCustom>();
                if (alive)
                {
                    if (mc == null)
                    {
                        mc = ep.Enemy.gameObject.AddComponent<MapCustom>();
                        mc.autoAdd = false;
                        mc.sprite = Util.CreateCircleSprite((int)(Util.EnemyScale(ep.Enemy.Type) * 10f));
                        mc.color = Util.ParseColor(Cfg.EnemyColor.Value);
                        mc.Add();
                    }
                }
                else if (mc != null)
                {
                    // 隐藏并清理死亡/消失敌人的图标
                    if (mc.mapCustomEntity != null && mc.mapCustomEntity.spriteRenderer != null)
                    {
                        mc.mapCustomEntity.spriteRenderer.sprite = null;
                    }
                    mc.Hide();
                    UnityEngine.Object.Destroy(mc);
                }
            }
        }

        internal static void ShowItems()
        {
            if (!Cfg.ShowItems.Value) return;
            if (Map.Instance == null) return;

            foreach (var item in UnityEngine.Object.FindObjectsOfType<ValuableObject>())
            {
                if (item == null) continue;
                item.discovered = true;
                item.Discover(ValuableDiscoverGraphic.State.Discover);
                Map.Instance.AddValuable(item);
            }

            foreach (var mv in UnityEngine.Object.FindObjectsOfType<MapValuable>())
            {
                if (mv != null && mv.spriteRenderer != null)
                {
                    mv.spriteRenderer.color = Util.ParseColor(Cfg.ItemColor.Value);
                }
            }

            foreach (var vdc in UnityEngine.Object.FindObjectsOfType<ValuableDiscoverCustom>())
            {
                vdc?.Discover();
            }
        }

        internal static void ExploreAllRooms()
        {
            if (!Cfg.ExploreAllRooms.Value) return;

            foreach (var room in UnityEngine.Object.FindObjectsOfType<RoomVolume>())
            {
                // 注意：新版游戏 SetExplored() 返回 bool（是否首次探索），返回值忽略即可。
                // 旧插件按 void 签名编译导致 MissingMethodException，这里对当前版本编译自然正确。
                room.SetExplored();
            }
        }

        internal static void RefreshAll()
        {
            ShowPlayers();
            ShowEnemies();
        }
    }

    // ---------- Harmony 补丁 ----------

    [HarmonyPatch(typeof(LevelGenerator), nameof(LevelGenerator.GenerateDone))]
    internal static class LevelGenerator_GenerateDone_Patch
    {
        private static void Postfix()
        {
            Safe.Run(() =>
            {
                if (!Util.IsInLevel()) return;
                MapIcons.ShowPlayers();
                MapIcons.ShowItems();
                MapIcons.ShowEnemies();
                MapIcons.ExploreAllRooms();
            }, "GenerateDone");
        }
    }

    [HarmonyPatch(typeof(LevelGenerator), nameof(LevelGenerator.StartRoomGeneration))]
    internal static class LevelGenerator_StartRoomGeneration_Patch
    {
        private static void Prefix()
        {
            Safe.Run(ValueTracker.ResetValues, "StartRoomGeneration");
        }
    }

    [HarmonyPatch(typeof(EnemyParent), "SpawnRPC")]
    internal static class EnemyParent_SpawnRPC_Patch
    {
        private static void Postfix() => Safe.Run(MapIcons.ShowEnemies, "EnemySpawnRPC");
    }

    [HarmonyPatch(typeof(EnemyParent), "DespawnRPC")]
    internal static class EnemyParent_DespawnRPC_Patch
    {
        private static void Postfix() => Safe.Run(MapIcons.ShowEnemies, "EnemyDespawnRPC");
    }

    [HarmonyPatch(typeof(EnemyHealth), "DeathRPC")]
    internal static class EnemyHealth_DeathRPC_Patch
    {
        private static void Postfix() => Safe.Run(MapIcons.ShowEnemies, "EnemyDeathRPC");
    }

    [HarmonyPatch(typeof(PlayerAvatar))]
    internal static class PlayerAvatar_Patches
    {
        [HarmonyPostfix]
        [HarmonyPatch(nameof(PlayerAvatar.ReviveRPC))]
        private static void ReviveRPC_Postfix() => Safe.Run(MapIcons.ShowPlayers, "ReviveRPC");

        [HarmonyPostfix]
        [HarmonyPatch(nameof(PlayerAvatar.PlayerDeathRPC))]
        private static void PlayerDeathRPC_Postfix() => Safe.Run(MapIcons.ShowPlayers, "PlayerDeathRPC");

        [HarmonyPostfix]
        [HarmonyPatch(nameof(PlayerAvatar.PlayerDeathDone))]
        private static void PlayerDeathDone_Postfix()
        {
            Safe.Run(() =>
            {
                if (GameDirector.instance?.PlayerList == null) return;
                if (!GameDirector.instance.PlayerList.Any(p => p != null && p.gameObject != null)) return;
                foreach (var head in UnityEngine.Object.FindObjectsOfType<PlayerDeathHead>())
                {
                    head?.SeenSetRPC(true);
                }
            }, "PlayerDeathDone");
        }
    }

    /// <summary>补丁安全包装：任何异常只记日志，绝不让它沿 RPC 链上抛炸断游戏逻辑。</summary>
    internal static class Safe
    {
        private static string _lastError;

        internal static void Run(Action action, string where)
        {
            try { action(); }
            catch (Exception e)
            {
                // 同一错误只报一次，避免像旧插件那样刷屏
                var key = where + ":" + e.GetType().Name + ":" + e.Message;
                if (key != _lastError)
                {
                    _lastError = key;
                    RepoMapSuite.Logger.LogWarning($"[{where}] {e}");
                }
            }
        }
    }
}
