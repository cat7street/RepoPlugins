using BepInEx.Bootstrap;
using BepInEx.Configuration;
using MenuLib.MonoBehaviors;
using RepoMapSuite;
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using static MenuLib.MenuAPI;

// 本文件是 RepoMapSuite 中唯一直接引用 MenuLib 的代码（软依赖）。
// MenuLib 缺失或损坏时 SettingsMenu 不会初始化，插件其余功能不受影响。
namespace RepoMapSuite.Menu
{
    internal static class SettingsMenu
    {
        internal static bool Available;
        private static REPOPopupPage _page;
        private static bool _selectingKey;
        private static REPOButton _keyButton;
        private static ConfigEntry<KeyCode> _keyToSet;

        private const float Step = 62f;
        private static readonly string[] ColorOptions = Util.NamedColorNames();
        private static readonly Dictionary<string, MinimapPosition> PositionOptions = new()
        {
            { "左下", MinimapPosition.BottomLeft }, { "右下", MinimapPosition.BottomRight },
            { "左上", MinimapPosition.TopLeft }, { "右上", MinimapPosition.TopRight },
            { "左中", MinimapPosition.MiddleLeft }, { "右中", MinimapPosition.MiddleRight },
        };

        internal static void Initialize()
        {
            try
            {
                if (!Chainloader.PluginInfos.ContainsKey("nickklmao.menulib"))
                {
                    RepoMapSuite.Logger.LogWarning("未检测到 MenuLib，游戏内设置菜单不可用（功能正常，改配置文件即可）");
                    return;
                }
                AddElementToSettingsMenu(parent =>
                {
                    CreateREPOButton("RepoMapSuite 设置", View, parent, new Vector2(225f, 252f));
                });
                Available = true;
                RepoMapSuite.Logger.LogInfo("游戏内设置菜单已就绪（M 键或 游戏设置 → RepoMapSuite 设置）");
            }
            catch (Exception e)
            {
                RepoMapSuite.Logger.LogWarning($"设置菜单初始化失败（不影响其他功能）：{e.Message}");
            }
        }

        internal static void Update()
        {
            if (!Available) return;

            try
            {
                if (Util.IsInLevel() && !Util.IsChatActive() && !Util.HasLocalMapToolActive()
                    && Input.GetKeyDown(Cfg.MenuKey.Value))
                {
                    if (_page == null) View(); else Close();
                }
                if (Util.IsInLevel() && Util.HasLocalMapToolActive() && _page != null)
                {
                    Close();
                }

                // 按键绑定监听
                if (!_selectingKey || !Input.anyKeyDown) return;
                foreach (KeyCode key in Enum.GetValues(typeof(KeyCode)))
                {
                    if (!Input.GetKeyDown(key)) continue;
                    if (key != KeyCode.Mouse0 && key != KeyCode.Mouse1 && key != KeyCode.Escape)
                    {
                        _keyToSet.Value = key;
                    }
                    if (_keyButton != null && _keyButton.labelTMP != null)
                    {
                        _keyButton.labelTMP.text = KeyLabel(_keyToSet);
                    }
                    _selectingKey = false;
                    _keyToSet = null;
                    _keyButton = null;
                    break;
                }
            }
            catch (Exception e)
            {
                RepoMapSuite.Logger.LogWarning($"设置菜单 Update 异常（已忽略）：{e.Message}");
            }
        }

        private static string KeyLabel(ConfigEntry<KeyCode> entry)
        {
            return entry.Definition.Key + ": " + entry.Value;
        }

        internal static void Close()
        {
            if (_page == null) return;
            MenuManager.instance.PageRemove(_page.menuPage);
            UnityEngine.Object.Destroy(_page.menuPage.gameObject);
            _page.ClosePage(true);
            _page = null;
            _selectingKey = false;
            _keyToSet = null;
            _keyButton = null;
        }

        private static void View()
        {
            Close();
            _page = CreateREPOPopupPage("RepoMapSuite 设置", 0, false, true, 0f);
            _page.AddElement(parent => CreateREPOButton("返回", Close, parent, new Vector2(66f, 18f)));

            float y = 60f;

            // ---- 地图图标 ----
            Label(ref y, "— 地图图标 —");
            Toggle(ref y, "显示队友", Cfg.ShowTeammates, v => { Cfg.ShowTeammates.Value = v; if (v) MapIcons.ShowPlayers(); });
            Option(ref y, "队友颜色", Cfg.TeammateColor, ColorOptions, () => MapIcons.ShowPlayers());
            Option(ref y, "死亡队友颜色", Cfg.DeadTeammateColor, ColorOptions, () => MapIcons.ShowPlayers());
            Toggle(ref y, "显示敌人", Cfg.ShowEnemies, v => { Cfg.ShowEnemies.Value = v; MapIcons.ShowEnemies(); });
            Option(ref y, "敌人颜色", Cfg.EnemyColor, ColorOptions, () => MapIcons.ShowEnemies());
            Toggle(ref y, "显示物品位置", Cfg.ShowItems, v => { Cfg.ShowItems.Value = v; if (v) MapIcons.ShowItems(); });
            Toggle(ref y, "进图全图点亮", Cfg.ExploreAllRooms, v => { Cfg.ExploreAllRooms.Value = v; if (v) MapIcons.ExploreAllRooms(); });

            // ---- 小地图 ----
            Label(ref y, "— 小地图 —");
            Toggle(ref y, "启用小地图", Cfg.MinimapEnabled);
            Option(ref y, "屏幕位置", Cfg.MinimapPreset, PositionOptions.Keys.ToArray());
            IntSlider(ref y, "边长(像素)", Cfg.MinimapSize, 200, 800);
            FloatSlider(ref y, "缩放(小=放大)", Cfg.MinimapZoom, 1.5f, 10f, 1);
            FloatSlider(ref y, "不透明度", Cfg.MinimapOpacity, 0.1f, 1f, 2);
            IntSlider(ref y, "边缘留白(像素)", Cfg.MinimapBuffer, 0, 100);

            // ---- 价值 HUD ----
            Label(ref y, "— 价值 HUD —");
            Toggle(ref y, "启用价值 HUD", Cfg.ValueHudEnabled);
            Toggle(ref y, "价值常显", Cfg.ValueHudAlwaysOn);
            Toggle(ref y, "只显示初始总价值", Cfg.ValueStartingOnly);
            Toggle(ref y, "低于比例才显示", Cfg.ValueUseRatio);
            FloatSlider(ref y, "比例阈值(剩余/目标)", Cfg.ValueRatio, 0.1f, 2f, 2);
            Option(ref y, "HUD 位置", Cfg.ValueHudPosition, HudPosOptions.Keys.ToArray());
            InputField(ref y, "自定义坐标(X,Y)", Cfg.ValueHudCustomCoords);

            // ---- 按键 ----
            Label(ref y, "— 按键（点击后按新键） —");
            KeyButton(ref y, Cfg.MenuKey);
            KeyButton(ref y, Cfg.ZoomInKey);
            KeyButton(ref y, Cfg.ZoomOutKey);

            ResetButton(ref y);
        }

        private static readonly Dictionary<string, Cfg.ValueHudPos> HudPosOptions = new()
        {
            { "默认(右侧目标下)", Cfg.ValueHudPos.Default },
            { "右下偏上", Cfg.ValueHudPos.LowerRight },
            { "右下角", Cfg.ValueHudPos.BottomRight },
            { "自定义坐标", Cfg.ValueHudPos.Custom },
        };

        /// <summary>把全部配置恢复默认并重建菜单刷新控件显示。</summary>
        private static void ResetAll()
        {
            foreach (var entry in new object[] {
                Cfg.ShowTeammates, Cfg.TeammateColor, Cfg.DeadTeammateColor,
                Cfg.ShowEnemies, Cfg.EnemyColor, Cfg.ShowItems, Cfg.ItemColor,
                Cfg.ExploreAllRooms, Cfg.MinimapEnabled, Cfg.MinimapPreset,
                Cfg.MinimapSize, Cfg.MinimapBuffer, Cfg.MinimapZoom, Cfg.MinimapOpacity,
                Cfg.ZoomInKey, Cfg.ZoomOutKey, Cfg.MenuKey, Cfg.ValueHudEnabled,
                Cfg.ValueHudAlwaysOn, Cfg.ValueStartingOnly, Cfg.ValueUseRatio,
                Cfg.ValueRatio, Cfg.ValueHudPosition, Cfg.ValueHudCustomCoords,
            })
            {
                try
                {
                    var boxed = entry.GetType().GetProperty("BoxedValue");
                    var def = entry.GetType().GetProperty("SettingDefaultValue");
                    if (boxed != null && def != null) boxed.SetValue(entry, def.GetValue(entry));
                }
                catch (Exception) { /* 单项失败不影响其余 */ }
            }
            MapIcons.ShowPlayers();
            MapIcons.ShowEnemies();
            View();
        }

        private static void ResetButton(ref float y)
        {
            float py = y;
            _page.AddElementToScrollView(parent =>
            {
                REPOButton btn = CreateREPOButton("恢复默认设置", ResetAll, parent, new Vector2(38f, py));
                btn.overrideButtonSize = new Vector2(btn.GetLabelSize().x * 0.8f, btn.GetLabelSize().y * 0.8f);
                btn.rectTransform.localScale = new Vector3(
                    btn.rectTransform.localScale.x * 0.8f, btn.rectTransform.localScale.y * 0.8f, 1f);
                return btn.rectTransform;
            }, 0f, 0f);
            y += Step;
        }

        private static void InputField(ref float y, string text, ConfigEntry<Vector2> entry)
        {
            string initial = $"{(int)entry.Value.x},{(int)entry.Value.y}";
            _page.AddElementToScrollView(parent =>
                CreateREPOInputField(text, str =>
                {
                    string[] parts = str.Split(',');
                    if (parts.Length == 2
                        && float.TryParse(parts[0].Trim(), System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out float x)
                        && float.TryParse(parts[1].Trim(), System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out float yy))
                    {
                        entry.Value = new Vector2(x, yy);
                    }
                }, parent, Vector2.zero, onlyNotifyOnSubmit: true,
                    placeholder: "如 0,225", defaultValue: initial).rectTransform, 0f, 0f);
            y += Step;
        }

        private static void Option(ref float y, string text, ConfigEntry<Cfg.ValueHudPos> entry, string[] options)
        {
            float py = y;
            string current = HudPosOptions.FirstOrDefault(kv => kv.Value == entry.Value).Key ?? options[0];
            _page.AddElementToScrollView(parent =>
                CreateREPOSlider(text, "", str => entry.Value = HudPosOptions[str], parent,
                    options, current, new Vector2(0f, py), "", "", 0).rectTransform, 0f, 0f);
            y += Step;
        }

        // ---- 控件构造辅助（全部进滚动区，逐行下移） ----

        private static void Label(ref float y, string text)
        {
            float py = y;
            _page.AddElementToScrollView(parent => CreateREPOLabel(text, parent, new Vector2(0f, py)).rectTransform, 0f, 0f);
            y += Step * 0.7f;
        }

        private static void Toggle(ref float y, string text, ConfigEntry<bool> entry, Action<bool> onChanged = null)
        {
            float py = y;
            _page.AddElementToScrollView(parent =>
                CreateREPOToggle(text, v => { entry.Value = v; onChanged?.Invoke(v); }, parent,
                    Vector2.zero, "开", "关", defaultValue: entry.Value).rectTransform, 0f, 0f);
            y += Step;
        }

        private static void Option(ref float y, string text, ConfigEntry<string> entry, string[] options, Action onChanged = null)
        {
            float py = y;
            string current = options.Contains(entry.Value) ? entry.Value : options[0];
            _page.AddElementToScrollView(parent =>
                CreateREPOSlider(text, "", str => { entry.Value = str; onChanged?.Invoke(); }, parent,
                    options, current, new Vector2(0f, py), "", "", 0).rectTransform, 0f, 0f);
            y += Step;
        }

        private static void Option(ref float y, string text, ConfigEntry<MinimapPosition> entry, string[] options)
        {
            float py = y;
            string current = PositionOptions.FirstOrDefault(kv => kv.Value == entry.Value).Key ?? options[0];
            _page.AddElementToScrollView(parent =>
                CreateREPOSlider(text, "", str => entry.Value = PositionOptions[str], parent,
                    options, current, new Vector2(0f, py), "", "", 0).rectTransform, 0f, 0f);
            y += Step;
        }

        private static void IntSlider(ref float y, string text, ConfigEntry<int> entry, int min, int max)
        {
            _page.AddElementToScrollView(parent =>
                CreateREPOSlider(text, "", new Action<int>(v => entry.Value = v), parent,
                    min: min, max: max, defaultValue: entry.Value).rectTransform, 0f, 0f);
            y += Step;
        }

        private static void FloatSlider(ref float y, string text, ConfigEntry<float> entry, float min, float max, int precision)
        {
            _page.AddElementToScrollView(parent =>
                CreateREPOSlider(text, "", new Action<float>(v => entry.Value = v), parent,
                    min: min, max: max, precision: precision, defaultValue: entry.Value).rectTransform, 0f, 0f);
            y += Step;
        }

        private static void KeyButton(ref float y, ConfigEntry<KeyCode> entry)
        {
            _page.AddElementToScrollView(parent =>
            {
                var btnRef = new REPOButton[1];
                btnRef[0] = CreateREPOButton(KeyLabel(entry), () =>
                {
                    _selectingKey = true;
                    _keyToSet = entry;
                    _keyButton = btnRef[0];
                    btnRef[0].labelTMP.text = entry.Definition.Key + ": 按下新按键…";
                }, parent, Vector2.zero);
                return btnRef[0].rectTransform;
            }, 0f, 0f);
            y += Step;
        }
    }
}
