using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RepoMapSuite
{
    internal static class Util
    {
        private static readonly Dictionary<string, Color> NamedColors = new()
        {
            { "white", Color.white }, { "red", Color.red }, { "green", Color.green },
            { "blue", Color.blue }, { "yellow", Color.yellow }, { "cyan", Color.cyan },
            { "magenta", Color.magenta }, { "black", Color.black },
            { "lilac", new Color(175f/255f, 143f/255f, 233f/255f) },
            { "purple", new Color(164f/255f, 112f/255f, 227f/255f) },
        };

        internal static Color ParseColor(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return Color.white;
            value = value.Trim();
            if (value.StartsWith("#") && (value.Length == 7 || value.Length == 9))
            {
                if (ColorUtility.TryParseHtmlString(value.Length == 7 ? value + "FF" : value, out var c)) return c;
            }
            return NamedColors.TryGetValue(value.ToLowerInvariant(), out var named) ? named : Color.white;
        }

        internal static string[] NamedColorNames()
        {
            return NamedColors.Keys.ToArray();
        }

        internal static bool IsInLevel()
        {
            return SemiFunc.RunIsLevel()
                && GameDirector.instance != null
                && (int)GameDirector.instance.currentState == 2;
        }

        internal static bool IsChatActive()
        {
            return ChatManager.instance != null && ChatManager.instance.chatActive;
        }

        internal static bool HasLocalMapToolActive()
        {
            var pa = PlayerAvatar.instance;
            return pa != null
                && pa.playerAvatarVisuals != null
                && pa.playerAvatarVisuals.playerAvatarRightArm != null
                && pa.playerAvatarVisuals.playerAvatarRightArm.mapToolController != null
                && pa.playerAvatarVisuals.playerAvatarRightArm.mapToolController.Active;
        }

        /// <summary>按敌人类型决定图标尺寸倍率（原 TheEverythingMap 逻辑）。</summary>
        internal static float EnemyScale(EnemyType t) => t switch
        {
            EnemyType.VeryLight => 0.55f,
            EnemyType.Light => 0.7f,
            EnemyType.Medium => 0.85f,
            EnemyType.Heavy => 1f,
            EnemyType.VeryHeavy => 1.15f,
            _ => 1f,
        };

        internal static Sprite CreateCircleSprite(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.ARGB32, false);
            var pixels = new Color[size * size];
            float c = size / 2f;
            float r = size / 2f - 1f;
            for (int i = 0; i < size; i++)
            {
                for (int j = 0; j < size; j++)
                {
                    float dx = j - c, dy = i - c;
                    pixels[i * size + j] = dx * dx + dy * dy <= r * r ? Color.white : Color.clear;
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }
    }
}
