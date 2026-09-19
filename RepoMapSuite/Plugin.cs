using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Linq;
using UnityEngine;

namespace RepoMapSuite
{
    /// <summary>
    /// RepoMapSuite —— BetterMap + TheEverythingMap + MapValueTracker 三合一重制版。
    /// 功能：Tab 地图上的队友/敌人图标、可缩放的小地图、地图剩余价值 HUD。
    /// 全部签名针对当前安装的游戏版本编译（见 csproj 的本地 Assembly-CSharp 引用），
    /// 并对旧版炸掉的 RoomVolume.SetExplored()（返回值 void→bool）、
    /// MapCustom.mapCustomEntity（转私有）、BreakRPC（新增参数）等 API 变更做了适配。
    /// </summary>
    [BepInPlugin(GUID, NAME, VERSION)]
    [BepInDependency("nickklmao.menulib", BepInDependency.DependencyFlags.SoftDependency)]
    public class RepoMapSuite : BaseUnityPlugin
    {
        public const string GUID = "cat7street.RepoMapSuite";
        public const string NAME = "RepoMapSuite";
        // 注意：与 csproj 的 <Version> 保持一致（BepInPlugin 特性要求编译期常量）
        public const string VERSION = "1.0.1";

        internal static new ManualLogSource Logger;
        internal static Harmony HarmonyInstance;

        // ---- 小地图运行时状态 ----
        private const string MAP_CAMERA_NAME = "Dirt Finder Map Camera";
        private Camera _mapCamera;
        private RenderTexture _renderTexture;
        private float _defaultCameraZoom = -1f;
        internal static Sprite SpriteCircle;

        private void Awake()
        {
            Logger = base.Logger;
            Cfg.Init(Config);

            SpriteCircle = Util.CreateCircleSprite(10);

            HarmonyInstance = new Harmony(GUID);
            HarmonyInstance.PatchAll();

            Menu.SettingsMenu.Initialize();

            Logger.LogInfo($"{NAME} v{VERSION} 已加载（三合一：地图图标 + 小地图 + 价值 HUD）");
        }

        private void Update()
        {
            try
            {
                Menu.SettingsMenu.Update();
                HandleZoomKeys();

                if (!Util.IsInLevel())
                {
                    if (_mapCamera != null || _renderTexture != null)
                    {
                        _mapCamera = null;
                        _renderTexture = null;
                        _defaultCameraZoom = -1f;
                    }
                    return;
                }

                // 找到游戏自带的俯视地图摄像机（Dirt Finder）
                if (_mapCamera == null)
                {
                    _mapCamera = FindObjectsOfType<Camera>(true).FirstOrDefault(cam => cam != null && cam.name == MAP_CAMERA_NAME);
                }

                // 强制保持地图渲染管线活跃，这样小地图才能持续取到画面
                if (Map.Instance != null && !Map.Instance.Active)
                {
                    Map.Instance.ActiveSet(true);
                }

                if (_mapCamera != null && (_renderTexture == null || (_mapCamera.activeTexture != null && _mapCamera.activeTexture != _renderTexture)))
                {
                    _renderTexture = _mapCamera.activeTexture;
                }

                if (_mapCamera != null)
                {
                    if (_defaultCameraZoom < 0f)
                    {
                        _defaultCameraZoom = _mapCamera.orthographicSize;
                    }

                    // 本地玩家自己举着地图道具时用原生缩放，平时用小地图缩放
                    float zoom = Util.HasLocalMapToolActive() ? _defaultCameraZoom : Cfg.MinimapZoom.Value;
                    if (!Mathf.Approximately(_mapCamera.orthographicSize, zoom))
                    {
                        _mapCamera.orthographicSize = zoom;
                    }
                }

                // 观战时让地图上的"自己"跟随被观战者（原 TheEverythingMap 逻辑）
                if (PlayerAvatar.instance != null && PlayerAvatar.instance.spectating
                    && SpectateCamera.instance != null && SpectateCamera.instance.currentState == SpectateCamera.State.Normal)
                {
                    Transform t = SpectateCamera.instance.player != null
                        ? SpectateCamera.instance.player.transform
                        : SpectateCamera.instance.transform;
                    if (t != null && DirtFinderMapPlayer.Instance != null && DirtFinderMapPlayer.Instance.PlayerTransform != null)
                    {
                        DirtFinderMapPlayer.Instance.PlayerTransform.position = t.position;
                        Vector3 e = t.rotation.eulerAngles;
                        DirtFinderMapPlayer.Instance.PlayerTransform.rotation = Quaternion.Euler(0f, e.y, e.z);
                    }
                }
            }
            catch (Exception e)
            {
                Logger.LogWarning($"小地图 Update 异常（已忽略）：{e.Message}");
            }
        }

        private void HandleZoomKeys()
        {
            if (!Util.IsInLevel() || Util.IsChatActive()) return;

            if (Input.GetKeyDown(Cfg.ZoomInKey.Value))
            {
                Cfg.MinimapZoom.Value = Mathf.Max(Cfg.MinimapZoom.Value - 0.5f, 1.5f);
            }
            if (Input.GetKeyDown(Cfg.ZoomOutKey.Value))
            {
                Cfg.MinimapZoom.Value = Mathf.Min(Cfg.MinimapZoom.Value + 0.5f, 10f);
            }
        }

        // ---- 原生 HUD 避让（小地图右上角时把游戏的价值/目标 HUD 下移）----
        private Vector2 _haulInitial, _goalInitial;
        private bool _uiInitialCaptured;

        private void LateUpdate()
        {
            try
            {
                if (!Util.IsInLevel())
                {
                    _uiInitialCaptured = false;
                    return;
                }
                var haul = HaulUI.instance;
                var goal = GoalUI.instance;
                if (haul == null || goal == null) return;

                var haulRect = (RectTransform)haul.transform;
                var goalRect = (RectTransform)goal.transform;
                if (!_uiInitialCaptured)
                {
                    _haulInitial = haulRect.anchoredPosition;
                    _goalInitial = goalRect.anchoredPosition;
                    _uiInitialCaptured = true;
                }

                // 原版 TheEverythingMap 的避让公式：下移 95 × (小地图边长 / 300) 像素
                bool avoid = Cfg.MinimapEnabled.Value
                    && Cfg.MinimapPreset.Value == MinimapPosition.TopRight
                    && !Util.HasLocalMapToolActive();
                if (avoid)
                {
                    float shift = -95f * (Cfg.MinimapSize.Value / 300f);
                    haulRect.anchoredPosition = _haulInitial + new Vector2(0f, shift);
                    goalRect.anchoredPosition = _goalInitial + new Vector2(0f, shift);
                }
                else
                {
                    haulRect.anchoredPosition = _haulInitial;
                    goalRect.anchoredPosition = _goalInitial;
                }
            }
            catch (Exception e)
            {
                Logger.LogWarning($"HUD 避让异常（已忽略）：{e.Message}");
            }
        }

        private void OnGUI()
        {
            if (!Cfg.MinimapEnabled.Value || !Util.IsInLevel() || _renderTexture == null || Util.HasLocalMapToolActive())
            {
                return;
            }

            int size = Cfg.MinimapSize.Value;
            int buffer = Cfg.MinimapBuffer.Value;
            int x, y;
            switch (Cfg.MinimapPreset.Value)
            {
                case MinimapPosition.TopLeft:
                    x = buffer; y = buffer * 2;
                    break;
                case MinimapPosition.TopRight:
                    x = Screen.width - size - buffer; y = buffer * 2;
                    break;
                case MinimapPosition.BottomRight:
                    x = Screen.width - size - buffer; y = Screen.height - size - buffer;
                    break;
                case MinimapPosition.MiddleLeft:
                    x = buffer; y = (Screen.height - size) / 2;
                    break;
                case MinimapPosition.MiddleRight:
                    x = Screen.width - size - buffer; y = (Screen.height - size) / 2;
                    break;
                case MinimapPosition.BottomLeft:
                default:
                    x = buffer; y = Screen.height - size - buffer;
                    break;
            }

            Color prev = GUI.color;
            if (!Mathf.Approximately(Cfg.MinimapOpacity.Value, 1f))
            {
                GUI.color = new Color(1f, 1f, 1f, Cfg.MinimapOpacity.Value);
            }
            GUI.DrawTexture(new Rect(x, y, size, size), _renderTexture, ScaleMode.StretchToFill, false);
            if (!Mathf.Approximately(Cfg.MinimapOpacity.Value, 1f))
            {
                GUI.color = prev;
            }
        }
    }

    internal enum MinimapPosition
    {
        BottomLeft,
        BottomRight,
        TopLeft,
        TopRight,
        MiddleLeft,
        MiddleRight,
    }

    /// <summary>全部配置项。默认值迁移自用户旧配置（敌人显示=开，小地图=500px 左下角）。</summary>
    internal static class Cfg
    {
        // 图标
        internal static ConfigEntry<bool> ShowTeammates;
        internal static ConfigEntry<string> TeammateColor;
        internal static ConfigEntry<string> DeadTeammateColor;
        internal static ConfigEntry<bool> ShowEnemies;
        internal static ConfigEntry<string> EnemyColor;
        internal static ConfigEntry<bool> ShowItems;
        internal static ConfigEntry<string> ItemColor;
        internal static ConfigEntry<bool> ExploreAllRooms;

        // 小地图
        internal static ConfigEntry<bool> MinimapEnabled;
        internal static ConfigEntry<MinimapPosition> MinimapPreset;
        internal static ConfigEntry<int> MinimapSize;
        internal static ConfigEntry<int> MinimapBuffer;
        internal static ConfigEntry<float> MinimapZoom;
        internal static ConfigEntry<float> MinimapOpacity;
        internal static ConfigEntry<KeyCode> ZoomInKey;
        internal static ConfigEntry<KeyCode> ZoomOutKey;
        internal static ConfigEntry<KeyCode> MenuKey;

        // 价值 HUD
        internal static ConfigEntry<bool> ValueHudEnabled;
        internal static ConfigEntry<bool> ValueHudAlwaysOn;
        internal static ConfigEntry<bool> ValueStartingOnly;
        internal static ConfigEntry<bool> ValueUseRatio;
        internal static ConfigEntry<float> ValueRatio;
        internal static ConfigEntry<ValueHudPos> ValueHudPosition;
        internal static ConfigEntry<Vector2> ValueHudCustomCoords;

        internal enum ValueHudPos { Default, LowerRight, BottomRight, Custom }

        internal static void Init(ConfigFile config)
        {
            ShowTeammates = config.Bind("1. 地图图标", "ShowTeammates", true, "在地图上显示队友位置");
            TeammateColor = config.Bind("1. 地图图标", "TeammateColor", "white", "队友图标颜色（white/red/green/blue/yellow/cyan/magenta/black/lilac/purple 或 #RRGGBB）");
            DeadTeammateColor = config.Bind("1. 地图图标", "DeadTeammateColor", "black", "死亡队友图标颜色");
            ShowEnemies = config.Bind("1. 地图图标", "ShowEnemies", true, "在地图上显示敌人位置");
            EnemyColor = config.Bind("1. 地图图标", "EnemyColor", "red", "敌人图标颜色");
            ShowItems = config.Bind("1. 地图图标", "ShowItems", false, "在地图上显示所有物品位置（略微剧透，默认关）");
            ItemColor = config.Bind("1. 地图图标", "ItemColor", "yellow", "物品图标颜色");
            ExploreAllRooms = config.Bind("1. 地图图标", "ExploreAllRooms", false, "进图自动点亮全部房间（去战争迷雾）");

            MinimapEnabled = config.Bind("2. 小地图", "Enabled", true, "是否启用屏幕角落小地图");
            MinimapPreset = config.Bind("2. 小地图", "Preset", MinimapPosition.BottomLeft, "小地图屏幕位置");
            MinimapSize = config.Bind("2. 小地图", "Size", 500, "小地图边长（像素）");
            MinimapBuffer = config.Bind("2. 小地图", "Buffer", 12, "小地图离屏幕边缘的距离（像素）");
            MinimapZoom = config.Bind("2. 小地图", "Zoom", 2.25f, "小地图缩放（数字越小放得越大）");
            MinimapOpacity = config.Bind("2. 小地图", "Opacity", 0.85f, "小地图不透明度 0~1");
            ZoomInKey = config.Bind("2. 小地图", "ZoomInKey", KeyCode.Equals, "小地图放大按键");
            ZoomOutKey = config.Bind("2. 小地图", "ZoomOutKey", KeyCode.Minus, "小地图缩小按键");
            MenuKey = config.Bind("4. 设置菜单", "MenuKey", KeyCode.M, "游戏内打开 RepoMapSuite 设置菜单的按键");

            ValueHudEnabled = config.Bind("3. 价值HUD", "Enabled", true, "是否启用地图剩余价值 HUD");
            ValueHudAlwaysOn = config.Bind("3. 价值HUD", "AlwaysOn", false, "始终显示价值 HUD（否则按住 Tab / 打开地图时显示）");
            ValueStartingOnly = config.Bind("3. 价值HUD", "StartingValueOnly", false, "只显示进图时的初始总价值（不随破坏/萃取实时变化）");
            ValueUseRatio = config.Bind("3. 价值HUD", "UseValueRatio", false, "剩余价值低于目标比例时才显示（配合 ValueRatio）");
            ValueRatio = config.Bind("3. 价值HUD", "ValueRatio", 1.0f, "比例阈值：剩余价值/萃取目标 低于它才显示");
            ValueHudPosition = config.Bind("3. 价值HUD", "Position", ValueHudPos.Default, "HUD 位置（Default=右侧目标下方）");
            ValueHudCustomCoords = config.Bind("3. 价值HUD", "CustomCoords", new Vector2(0f, 225f), "Custom 位置时的 X,Y 偏移");
        }
    }
}
