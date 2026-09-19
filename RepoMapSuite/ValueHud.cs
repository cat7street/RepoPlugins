using HarmonyLib;
using System.Linq;
using TMPro;
using UnityEngine;

namespace RepoMapSuite
{
    /// <summary>
    /// 地图剩余价值 HUD（原 MapValueTracker 功能重制）。
    /// 修复点：PhysGrabObjectImpactDetector.BreakRPC 新签名 (float, Vector3, int, bool, PhotonMessageInfo)，
    /// 旧插件按两参签名 patch 会直接失败。
    /// </summary>
    internal static class ValueTracker
    {
        internal static float TotalValue;
        internal static float TotalValueInit;
        internal static GameObject HudObject;
        internal static TextMeshProUGUI ValueText;

        internal static void ResetValues()
        {
            if (!SemiFunc.RunIsLevel())
            {
                TotalValue = 0f;
                TotalValueInit = 0f;
                HudObject = null;
                ValueText = null;
            }
        }

        internal static void CheckForItems()
        {
            if (RoundDirector.instance == null || RoundDirector.instance.allExtractionPointsCompleted) return;

            TotalValue = 0f;
            foreach (var vo in Object.FindObjectsOfType<ValuableObject>())
            {
                if (vo == null) continue;
                TotalValue += vo.dollarValueCurrent;
            }
        }

        internal static void UpdateHud()
        {
            if (!Cfg.ValueHudEnabled.Value) return;
            if (!Util.IsInLevel()) return;
            if (RoundDirector.instance == null) return;

            int goal = RoundDirector.instance.extractionHaulGoal;
            bool allDone = RoundDirector.instance.allExtractionPointsCompleted;

            if (HudObject == null)
            {
                CreateHud();
                return;
            }

            if (goal == 0 && allDone)
            {
                HudObject.SetActive(false);
                return;
            }

            float shown = Cfg.ValueStartingOnly.Value ? TotalValueInit : TotalValue;
            ValueText.SetText("Map: $" + shown.ToString("N0"));
            ApplyHudPosition();

            if (Cfg.ValueHudAlwaysOn.Value)
            {
                HudObject.SetActive(true);
            }
            else if (Cfg.ValueUseRatio.Value)
            {
                float ratio = goal > 0 ? TotalValue / goal : 0f;
                HudObject.SetActive(ratio <= Cfg.ValueRatio.Value);
            }
            else
            {
                bool mapOpen = (MapToolController.instance != null && MapToolController.instance.mapToggled)
                    || SemiFunc.InputHold(InputKey.Map);
                HudObject.SetActive(mapOpen);
            }
        }

        private static void CreateHud()
        {
            var hud = GameObject.Find("Game Hud");
            if (hud == null) return;

            // 从游戏的 Tax Haul 文本取字体，保持观感一致
            TMP_FontAsset font = null;
            if (HaulUI.instance != null && HaulUI.instance.Text != null)
            {
                font = HaulUI.instance.Text.font;
            }
            if (font == null)
            {
                var taxHaul = GameObject.Find("Tax Haul");
                if (taxHaul != null) font = taxHaul.GetComponent<TMP_Text>()?.font;
            }
            if (font == null) return;

            HudObject = new GameObject("RepoMapSuite Value HUD");
            HudObject.SetActive(false);
            ValueText = HudObject.AddComponent<TextMeshProUGUI>();
            ValueText.font = font;
            ValueText.color = new Color(0.7882f, 0.9137f, 0.902f, 1f);
            ValueText.fontSize = 24f;
            ValueText.alignment = TextAlignmentOptions.BaselineRight;
            HudObject.transform.SetParent(hud.transform, false);

            ApplyHudPosition();
        }

        private static void ApplyHudPosition()
        {
            var rect = HudObject.GetComponent<RectTransform>();
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(1f, -1f);
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.sizeDelta = new Vector2(0f, 0f);

            Vector2 offset = Cfg.ValueHudPosition.Value switch
            {
                Cfg.ValueHudPos.LowerRight => new Vector2(0f, 125f),
                Cfg.ValueHudPos.BottomRight => new Vector2(0f, 0f),
                Cfg.ValueHudPos.Custom => Cfg.ValueHudCustomCoords.Value,
                _ => new Vector2(0f, 225f),
            };
            rect.offsetMax = offset;
            rect.offsetMin = offset;
        }
    }

    // ---------- Harmony 补丁 ----------

    [HarmonyPatch(typeof(RoundDirector), "Update")]
    internal static class RoundDirector_Update_Patch
    {
        private static void Postfix() => Safe.Run(ValueTracker.UpdateHud, "RoundDirector.Update");
    }

    [HarmonyPatch(typeof(RoundDirector), nameof(RoundDirector.ExtractionCompleted))]
    internal static class RoundDirector_ExtractionCompleted_Patch
    {
        private static void Postfix() => Safe.Run(ValueTracker.CheckForItems, "ExtractionCompleted");
    }

    [HarmonyPatch(typeof(ValuableObject), nameof(ValuableObject.DollarValueSetRPC))]
    internal static class ValuableObject_DollarValueSetRPC_Patch
    {
        private static void Postfix(float value) => Safe.Run(() => ValueTracker.TotalValue += value, "DollarValueSetRPC");
    }

    [HarmonyPatch(typeof(ValuableObject), nameof(ValuableObject.DollarValueSetLogic))]
    internal static class ValuableObject_DollarValueSetLogic_Patch
    {
        private static void Postfix(ValuableObject __instance) => Safe.Run(() =>
        {
            if (SemiFunc.IsMasterClientOrSingleplayer())
            {
                ValueTracker.TotalValue += __instance.dollarValueCurrent;
            }
        }, "DollarValueSetLogic");
    }

    /// <summary>物品磕碰损失价值。新签名：BreakRPC(float valueLost, Vector3 _contactPoint, int breakLevel, bool _loseValue, PhotonMessageInfo _info)</summary>
    [HarmonyPatch(typeof(PhysGrabObjectImpactDetector), "BreakRPC")]
    internal static class ImpactDetector_BreakRPC_Patch
    {
        private static void Postfix(float valueLost, bool _loseValue) => Safe.Run(() =>
        {
            if (_loseValue) ValueTracker.TotalValue -= valueLost;
        }, "BreakRPC");
    }

    [HarmonyPatch(typeof(PhysGrabObject), "DestroyPhysGrabObjectRPC")]
    internal static class PhysGrabObject_DestroyPhysGrabObjectRPC_Patch
    {
        private static void Postfix(PhysGrabObject __instance) => Safe.Run(() =>
        {
            if (!Util.IsInLevel()) return;
            var vo = __instance.GetComponent<ValuableObject>();
            if (vo == null) return;
            // 剩余价值高于原值 15% 视为被销毁扣除；低于则视为萃取流程销毁（已由萃取逻辑结算）
            if (vo.dollarValueCurrent >= vo.dollarValueOriginal * 0.15f)
            {
                ValueTracker.TotalValue -= vo.dollarValueCurrent;
            }
        }, "DestroyPhysGrabObjectRPC");
    }

    [HarmonyPatch(typeof(LevelGenerator), nameof(LevelGenerator.GenerateDone))]
    internal static class LevelGenerator_GenerateDone_Value_Patch
    {
        private static void Prefix() => Safe.Run(() =>
        {
            ValueTracker.CheckForItems();
            ValueTracker.TotalValueInit = ValueTracker.TotalValue;
        }, "GenerateDone.Value");
    }
}
