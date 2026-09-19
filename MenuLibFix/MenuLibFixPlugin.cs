using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using MenuLib;
using MenuLib.MonoBehaviors;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace MenuLibFix
{
    /// <summary>
    /// MenuLib 2.5.4 兼容修复（针对 2026-09 游戏版本 build 23363152）。
    ///
    /// 病因：MenuLib 用 MonoMod ILHook + IL 模式匹配改写 SemiFunc.UIMouseHover 等三个方法，
    /// 游戏更新后 IL 结构变化，模式匹配错位产生非法 IL（InvalidProgramException: stloc.s 13），
    /// 导致 MenuLib.Awake 中断，后两个钩子（StateClosing / StateInactive）从未装上。
    ///
    /// 修法（BetterMapFix 同款伴生插件模式）：不动 MenuLib 本体，用 Harmony prefix 重实现
    /// 三个钩子的效果；UIMouseHover 与 StateClosing 采用"当前版本游戏逻辑完整移植 + MenuLib 定制点"
    /// 的全替换，编译期即校验所有成员引用。
    /// </summary>
    [BepInPlugin(GUID, NAME, VERSION)]
    [BepInDependency("nickklmao.menulib", BepInDependency.DependencyFlags.HardDependency)]
    public class MenuLibFixPlugin : BaseUnityPlugin
    {
        public const string GUID = "cat7street.MenuLibFix";
        public const string NAME = "MenuLib Fix";
        // 注意：与 csproj 的 <Version> 保持一致（BepInPlugin 特性要求编译期常量）
        public const string VERSION = "1.0.1";

        internal static new ManualLogSource Logger;

        private void Awake()
        {
            Logger = base.Logger;
            var harmony = new Harmony(GUID);
            harmony.PatchAll();
            Logger.LogInfo($"{NAME} v{VERSION} 已加载：UIMouseHover / StateClosing / StateInactive 三钩子已用 Harmony 重挂");
        }
    }

    /// <summary>
    /// 替代 SemiFunc_UIMouseHoverILHook。
    /// 逻辑 = 当前版本游戏 UIMouseHover 完整移植，仅把 scrollBox 分支换成
    /// MenuLib 自定义滚动视图（scroller 父容器矩形）判定。
    /// </summary>
    [HarmonyPatch(typeof(SemiFunc), "UIMouseHover")]
    internal static class SemiFunc_UIMouseHover_Patch
    {
        private static string _lastHoverError;

        /// <summary>保险网：prefix 任何异常都按"未悬停"处理，绝不让异常漏进 MenuManager.Update。</summary>
        private static bool Prefix(
            MenuPage parentPage,
            RectTransform rectTransform,
            string menuID,
            float xPadding,
            float yPadding,
            MenuScrollBox scrollBox,
            ScrollRect scrollRect,
            ref bool __result)
        {
            try
            {
                return HoverImpl(parentPage, rectTransform, menuID, xPadding, yPadding,
                                 scrollBox, scrollRect, ref __result);
            }
            catch (Exception e)
            {
                var key = e.GetType().Name + ":" + e.Message;
                if (key != _lastHoverError)
                {
                    _lastHoverError = key;
                    MenuLibFixPlugin.Logger.LogWarning($"UIMouseHover 替代逻辑异常（按未悬停处理）：{e}");
                }
                __result = false;
                return false;
            }
        }

        private static bool HoverImpl(
            MenuPage parentPage,
            RectTransform rectTransform,
            string menuID,
            float xPadding,
            float yPadding,
            MenuScrollBox scrollBox,
            ScrollRect scrollRect,
            ref bool __result)
        {
            if (!parentPage)
            {
                __result = false;
                return false;
            }
            if ((bool)parentPage.parentPage && !parentPage.parentPage.pageActive)
            {
                __result = false;
                return false;
            }

            Vector2 screenPoint = SemiFunc.UIMousePosToUIPos();
            if (MenuManager.instance.mouseHoldPosition != Vector2.zero)
            {
                screenPoint = MenuManager.instance.mouseHoldPosition;
            }

            // ===== MenuLib 定制点：自定义滚动视图边界判定（原游戏用 scrollerEndPosition）=====
            // 关键：scroller 缺失时必须回落到原生判定，不能放行——原版会在鼠标
            // 位于滚动区外时提前 return false，跳过后面的 UIGetRectTransformPositionOnScreen
            // （该方法内部 GetComponentInParent<MenuPage>() 为空时直接 NRE）。
            if ((bool)scrollBox)
            {
                RectTransform scrollerParent = scrollBox.scroller != null
                    ? (RectTransform)((Transform)scrollBox.scroller).parent
                    : null;
                bool inside;
                if (scrollerParent != null)
                {
                    float bottom = ((Transform)scrollerParent).position.y;
                    float top = bottom + scrollerParent.sizeDelta.y;
                    inside = screenPoint.y > bottom && screenPoint.y < top;
                }
                else
                {
                    // 原生判定兜底（游戏 build 23363152 的公式，乘数恒为 1 已省略）
                    float low = scrollBox.transform.position.y - 10f;
                    float high = scrollBox.scrollerEndPosition + 32f;
                    inside = screenPoint.y <= high && screenPoint.y >= low;
                }
                if (!inside)
                {
                    __result = false;
                    return false;
                }
            }

            if ((bool)scrollRect)
            {
                RectTransform viewport = scrollRect.viewport ?? scrollRect.GetComponent<RectTransform>();
                if ((bool)viewport)
                {
                    rectTransform.GetWorldCorners(SemiFunc.s_RectCorners);
                    viewport.GetWorldCorners(SemiFunc.s_ViewportCorners);
                    Vector3 rectMin = SemiFunc.s_RectCorners[0];
                    Vector3 rectMax = SemiFunc.s_RectCorners[2];
                    Vector3 viewMin = SemiFunc.s_ViewportCorners[0];
                    Vector3 viewMax = SemiFunc.s_ViewportCorners[2];
                    if (rectMax.y < viewMin.y || rectMin.y > viewMax.y || rectMax.x < viewMin.x || rectMin.x > viewMax.x)
                    {
                        __result = false;
                        return false;
                    }
                    if (!RectTransformUtility.RectangleContainsScreenPoint(viewport, screenPoint, null))
                    {
                        __result = false;
                        return false;
                    }
                }
            }

            // 不在任何 MenuPage 下、或页面自身 rectTransform 未初始化（刚创建，
            // Start() 尚未跑完）的元素：按未悬停处理。
            // UIGetRectTransformPositionOnScreen 内部对 MenuPage.rectTransform
            // 直接解引用，为空会 NRE，必须在调用前挡掉。
            MenuPage hostPage = rectTransform.GetComponentInParent<MenuPage>();
            if (hostPage == null || hostPage.rectTransform == null)
            {
                __result = false;
                return false;
            }

            Vector2 rectPos = SemiFunc.UIGetRectTransformPositionOnScreen(rectTransform, false);
            float minX = rectPos.x + (rectTransform.rect.xMin - xPadding);
            float maxX = rectPos.x + (rectTransform.rect.xMax + xPadding);
            float minY = rectPos.y + (rectTransform.rect.yMin - yPadding);
            float maxY = rectPos.y + (rectTransform.rect.yMax + yPadding);

            bool result;
            if (screenPoint.x >= minX && screenPoint.x <= maxX && screenPoint.y >= minY && screenPoint.y <= maxY)
            {
                result = true;
                if (!string.IsNullOrEmpty(menuID) && menuID != "-1")
                {
                    if (MenuManager.instance.currentMenuID == "")
                    {
                        MenuManager.instance.currentMenuID = menuID;
                        MenuManager.instance.currentMenuIDTransform = rectTransform;
                    }
                    else if (MenuManager.instance.currentMenuID != menuID
                        && SemiFunc.UIIsRenderedOnTop(rectTransform, MenuManager.instance.currentMenuIDTransform))
                    {
                        MenuManager.instance.currentMenuID = menuID;
                        MenuManager.instance.currentMenuIDTransform = rectTransform;
                    }
                }
            }
            else
            {
                result = false;
            }

            if (!string.IsNullOrEmpty(menuID) && menuID != "-1")
            {
                __result = menuID == MenuManager.instance.currentMenuID;
                return false;
            }

            __result = result;
            return false;
        }
    }

    /// <summary>
    /// 替代 MenuPage_StateClosingILHook。
    /// 逻辑 = 当前版本游戏 StateClosing 完整移植 + 两个 MenuLib 定制点：
    /// 1) stateStart 时为自定义页重设滑出目标位置（滑到父容器下方）；
    /// 2) 销毁阶段对缓存页/未激活页停用组件而非销毁对象。
    /// </summary>
    [HarmonyPatch(typeof(MenuPage), "StateClosing")]
    internal static class MenuPage_StateClosing_Patch
    {
        private static bool Prefix(MenuPage __instance)
        {
            MenuPage page = __instance;

            page.LockAndHide();

            if (page.stateStart)
            {
                // ===== MenuLib 定制点 1：自定义页滑出到下方 =====
                if (MenuAPI.customMenuPages.TryGetValue(page, out var custom))
                {
                    RectTransform pageRect = (RectTransform)((Component)page).transform;
                    Vector2 awayPos = ((Transform)pageRect).position;
                    float below = 0f - pageRect.rect.height;
                    awayPos.y = below - custom.rectTransform.rect.height;
                    page.animateAwayPosition = awayPos;
                }

                if (!page.disableOutroEffect)
                {
                    if (!page.popUpAnimation)
                    {
                        MenuManager.instance.MenuEffectPageOutro();
                    }
                    else
                    {
                        MenuManager.instance.MenuEffectPopUpClose();
                    }
                }
                if (MenuManager.instance.currentMenuPage == page)
                {
                    MenuManager.instance.currentMenuPage = null;
                    MenuManager.instance.PageRemove(page);
                }
            }

            if (Vector2.Distance(page.rectTransform.localPosition, page.animateAwayPosition) < 0.8f)
            {
                page.onPageEnd.Invoke();

                // ===== MenuLib 定制点 2：缓存页停用而非销毁 =====
                if (MenuAPI.customMenuPages.TryGetValue(page, out var cached)
                    && (cached.isCachedPage || !cached.pageWasActivatedOnce))
                {
                    ((Behaviour)page).enabled = false;
                }
                else
                {
                    MenuManager.instance.PageRemove(page);
                    UnityEngine.Object.Destroy((UnityEngine.Object)((Component)page).gameObject);
                }
            }

            float deltaTime = Time.deltaTime;
            page.rectTransform.localPosition = Vector2.Lerp(
                page.rectTransform.localPosition, page.animateAwayPosition, 40f * deltaTime);

            return false;
        }
    }

    /// <summary>
    /// 替代 ChatManager_StateInactiveILHook：
    /// MenuLib 自定义输入框持有焦点时跳过 StateInactive，防止聊天状态机抢焦点/隐藏 UI。
    /// </summary>
    [HarmonyPatch(typeof(ChatManager), "StateInactive")]
    internal static class ChatManager_StateInactive_Patch
    {
        private static bool Prefix()
        {
            return !REPOInputStringSystem.hasAnyFocus;
        }
    }
}
