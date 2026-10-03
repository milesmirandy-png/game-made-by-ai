using System.Collections.Generic;
using UnityEngine;

namespace Swat
{
    // Turns WeaponSpriteArt drawings into point-filtered textures (cached per
    // weapon and attachment set) and draws them crisply: whole screen pixels
    // per sprite pixel whenever the space allows. Used for weapon icons in the
    // loadout, HUD and weapon wheel, and for weapon pickups in the game modes.
    public static class WeaponSprites
    {
        static readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            cache.Clear();
        }

        public static Texture2D Get(WeaponData weapon, OfficerLoadout attachments = null)
        {
            if (weapon == null) return null;
            bool fitted = attachments != null && !weapon.isSidearm;
            var options = new WeaponSpriteArt.Options
            {
                r = weapon.accent.r, g = weapon.accent.g, b = weapon.accent.b,
                suppressor = fitted && !string.IsNullOrEmpty(attachments.muzzleId),
                optic = fitted && !string.IsNullOrEmpty(attachments.opticId),
                light = fitted && !string.IsNullOrEmpty(attachments.lightId),
            };
            string key = weapon.id + (options.suppressor ? "s" : "") + (options.optic ? "o" : "") + (options.light ? "l" : "");
            Texture2D texture;
            if (cache.TryGetValue(key, out texture) && texture != null) return texture;

            var image = WeaponSpriteArt.Draw(weapon.category, options);
            texture = new Texture2D(image.Width, image.Height, TextureFormat.RGBA32, false)
            {
                name = "SWAT Sprite " + key,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave,
            };
            var pixels = new Color32[image.Width * image.Height];
            for (int y = 0; y < image.Height; y++)
                for (int x = 0; x < image.Width; x++)
                {
                    uint p = image.Pixels[y * image.Width + x];
                    // The drawing's row 0 is the top; a texture's row 0 is the bottom.
                    pixels[(image.Height - 1 - y) * image.Width + x] = new Color32((byte)(p >> 24), (byte)(p >> 16), (byte)(p >> 8), (byte)p);
                }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            cache[key] = texture;
            return texture;
        }

        // Draws the sprite centred in the rect (GUI units), at a whole number of screen pixels per sprite pixel.
        public static void Draw(Rect rect, Texture2D texture, float alpha)
        {
            if (texture == null || Event.current.type != EventType.Repaint) return;
            float scale = UITheme.Scale;
            float fit = Mathf.Min(rect.width * scale / texture.width, rect.height * scale / texture.height);
            float pixels = fit >= 1f ? Mathf.Floor(fit) : fit;
            float w = texture.width * pixels / scale, h = texture.height * pixels / scale;
            var target = new Rect(rect.center.x - w * 0.5f, rect.center.y - h * 0.5f, w, h);
            // Snap to the screen pixel grid so sprite pixels stay square.
            target.x = Mathf.Round(target.x * scale) / scale;
            target.y = Mathf.Round(target.y * scale) / scale;
            var old = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, alpha * UITheme.Alpha);
            GUI.DrawTexture(target, texture, ScaleMode.StretchToFill, true);
            GUI.color = old;
        }
    }
}
