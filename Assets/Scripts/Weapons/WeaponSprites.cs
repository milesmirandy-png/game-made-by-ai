using System.Collections.Generic;
using UnityEngine;

namespace Swat
{
    // Weapon icons for the loadout, HUD and weapon wheel. Each weapon's sprite
    // comes from the pixel-art gun pack in Resources/SWAT/WeaponSprites (PNG
    // files saved as <weapon id>.bytes), with the loadout's attachments drawn
    // on by WeaponSpritePack; a weapon without a file falls back to the
    // code-drawn WeaponSpriteArt. Textures are cached per weapon and attachment
    // set and drawn crisply: whole screen pixels per sprite pixel whenever the
    // space allows, smoothly filtered when an icon has to be drawn smaller.
    public static class WeaponSprites
    {
        const string Folder = "SWAT/WeaponSprites/";

        static readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();
        static readonly Dictionary<Texture2D, Texture2D> smooth = new Dictionary<Texture2D, Texture2D>();
        static readonly Dictionary<string, WeaponSpriteArt.Image> packImages = new Dictionary<string, WeaponSpriteArt.Image>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            cache.Clear();
            smooth.Clear();
            packImages.Clear();
        }

        public static Texture2D Get(WeaponData weapon, OfficerLoadout attachments = null)
        {
            if (weapon == null) return null;
            var fitting = new WeaponSpritePack.Fitting();
            if (attachments != null && !weapon.isSidearm)
                foreach (var id in Weapon.Ids(attachments))
                {
                    var attachment = GameData.Attachment(id);
                    if (attachment == null) continue;
                    switch (attachment.look)
                    {
                        case AttachmentLook.RedDot: fitting.optic = 1; break;
                        case AttachmentLook.Reflex: fitting.optic = 2; break;
                        case AttachmentLook.Holo: fitting.optic = 3; break;
                        case AttachmentLook.Scope: fitting.optic = 4; break;
                        case AttachmentLook.Suppressor: fitting.muzzle = 1; break;
                        case AttachmentLook.FlashHider: fitting.muzzle = 2; break;
                        case AttachmentLook.Compensator: fitting.muzzle = 3; break;
                        case AttachmentLook.Brake: fitting.muzzle = 4; break;
                        case AttachmentLook.VerticalGrip: fitting.grip = 1; break;
                        case AttachmentLook.AngledGrip: fitting.grip = 2; break;
                        case AttachmentLook.Light: fitting.light = true; break;
                        case AttachmentLook.Laser: fitting.laser = true; break;
                    }
                }
            bool suppressor = fitting.muzzle == 1, optic = fitting.optic > 0, light = fitting.light;
            string key = weapon.id + fitting.Key;
            Texture2D texture;
            if (cache.TryGetValue(key, out texture) && texture != null) return texture;

            var image = PackImage(weapon.id);
            bool drawn = image == null;
            if (!drawn) image = WeaponSpritePack.Fit(image, weapon.id, fitting);
            else
                image = WeaponSpriteArt.Draw(weapon.category, new WeaponSpriteArt.Options
                {
                    r = weapon.accent.r, g = weapon.accent.g, b = weapon.accent.b,
                    suppressor = suppressor, optic = optic, light = light,
                });

            var pixels = new Color32[image.Width * image.Height];
            for (int y = 0; y < image.Height; y++)
                for (int x = 0; x < image.Width; x++)
                {
                    uint p = image.Pixels[y * image.Width + x];
                    // The image's row 0 is the top; a texture's row 0 is the bottom. Code-drawn
                    // sprites point left, so they are mirrored to face right like the pack.
                    int column = drawn ? image.Width - 1 - x : x;
                    pixels[(image.Height - 1 - y) * image.Width + column] = new Color32((byte)(p >> 24), (byte)(p >> 16), (byte)(p >> 8), (byte)p);
                }
            texture = MakeTexture("SWAT Sprite " + key, image.Width, image.Height, pixels, false);
            cache[key] = texture;
            smooth[texture] = MakeTexture("SWAT Sprite (smooth) " + key, image.Width, image.Height, pixels, true);
            return texture;
        }

        static Texture2D MakeTexture(string name, int width, int height, Color32[] pixels, bool filtered)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, filtered)
            {
                name = name,
                filterMode = filtered ? FilterMode.Trilinear : FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave,
            };
            texture.SetPixels32(pixels);
            texture.Apply(filtered, true);
            return texture;
        }

        // The pack sprite for a weapon id, decoded once; null if the weapon has none.
        static WeaponSpriteArt.Image PackImage(string id)
        {
            WeaponSpriteArt.Image image;
            if (packImages.TryGetValue(id, out image)) return image;
            var asset = Resources.Load<TextAsset>(Folder + id);
            if (asset != null)
            {
                var decoded = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (decoded.LoadImage(asset.bytes, false))
                {
                    int w = decoded.width, h = decoded.height;
                    var pixels = decoded.GetPixels32();
                    image = new WeaponSpriteArt.Image { Width = w, Height = h, Pixels = new uint[w * h] };
                    for (int y = 0; y < h; y++)
                        for (int x = 0; x < w; x++)
                        {
                            var c = pixels[(h - 1 - y) * w + x];
                            image.Pixels[y * w + x] = ((uint)c.r << 24) | ((uint)c.g << 16) | ((uint)c.b << 8) | c.a;
                        }
                }
                else Debug.LogWarning("SWAT: could not decode the weapon sprite " + Folder + id + ".bytes");
                if (Application.isPlaying) Object.Destroy(decoded);
                else Object.DestroyImmediate(decoded);
                Resources.UnloadAsset(asset);
            }
            packImages[id] = image;
            return image;
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
            // Shrunk below one screen pixel per sprite pixel, a filtered copy keeps thin parts from vanishing.
            Texture2D source = texture, filtered;
            if (fit < 1f && smooth.TryGetValue(texture, out filtered) && filtered != null) source = filtered;
            var old = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, alpha * UITheme.Alpha);
            GUI.DrawTexture(target, source, ScaleMode.StretchToFill, true);
            GUI.color = old;
        }
    }
}
