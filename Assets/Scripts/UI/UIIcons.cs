using UnityEngine;

namespace Swat
{
    // Simple drawn icons: officer portraits, weapon silhouettes and equipment
    // glyphs, built from flat shapes in the UI's own style. If a WeaponData
    // has an icon texture assigned, that texture is used instead.
    public static class UIIcons
    {
        public static void Portrait(Rect rect, OfficerData officer, OfficerLoadout loadout, bool available = true)
        {
            UITheme.Fill(rect, new Color(0.06f, 0.09f, 0.14f));
            UITheme.Shade(new Rect(rect.x, rect.y, rect.width, rect.height * 0.6f), new Color(0.2f, 0.32f, 0.5f, 0.35f), false);
            var armor = loadout != null ? GameData.Armor(loadout.armorId) : null;
            Color uniform = Progression.Uniform(loadout != null ? loadout.uniformIndex : 0);
            Color skin = CharacterFactory.Skin(officer.skinTone + officer.id.Length);
            Color vest = armor != null ? armor.color : new Color(0.1f, 0.1f, 0.12f);
            bool helmet = armor == null || armor.helmet;

            float w = rect.width, h = rect.height;
            float cx = rect.x + w * 0.5f;
            // Shoulders and vest.
            UITheme.Fill(new Rect(rect.x + w * 0.12f, rect.y + h * 0.7f, w * 0.76f, h * 0.3f), uniform);
            UITheme.Fill(new Rect(rect.x + w * 0.26f, rect.y + h * 0.72f, w * 0.48f, h * 0.28f), vest);
            UITheme.Fill(new Rect(rect.x + w * 0.3f, rect.y + h * 0.78f, w * 0.4f, h * 0.05f), new Color(0.75f, 0.75f, 0.78f));
            // Neck and head.
            UITheme.Fill(new Rect(cx - w * 0.08f, rect.y + h * 0.58f, w * 0.16f, h * 0.14f), Shapes.Shade(skin, 0.85f));
            UITheme.Dot(new Vector2(cx, rect.y + h * 0.46f), w * 0.2f, skin);
            // Eyes.
            UITheme.Fill(new Rect(cx - w * 0.1f, rect.y + h * 0.46f, w * 0.05f, h * 0.025f), new Color(0.1f, 0.1f, 0.12f));
            UITheme.Fill(new Rect(cx + w * 0.05f, rect.y + h * 0.46f, w * 0.05f, h * 0.025f), new Color(0.1f, 0.1f, 0.12f));
            // Helmet or cap.
            Color hat = helmet ? new Color(0.08f, 0.09f, 0.12f) : Shapes.Shade(uniform, 0.7f);
            UITheme.Fill(new Rect(cx - w * 0.22f, rect.y + h * 0.27f, w * 0.44f, h * 0.13f), hat);
            UITheme.Dot(new Vector2(cx, rect.y + h * 0.33f), w * 0.21f, hat);
            UITheme.Fill(new Rect(cx - w * 0.24f, rect.y + h * 0.38f, w * 0.48f, h * 0.04f), helmet ? Shapes.Shade(hat, 1.6f) : hat);
            // Role badge.
            UITheme.RoleIcon(new Rect(rect.xMax - w * 0.32f, rect.y + h * 0.04f, w * 0.28f, w * 0.28f), officer.role);
            UITheme.Frame(rect, new Color(UITheme.Line.r, UITheme.Line.g, UITheme.Line.b, 0.8f));
            if (!available) UITheme.Fill(rect, new Color(0f, 0f, 0f, 0.6f));
        }

        public static void Weapon(Rect rect, WeaponData weapon, Color color)
        {
            if (weapon == null) return;
            if (weapon.icon != null)
            {
                var old = GUI.color;
                GUI.color = color;
                GUI.DrawTexture(rect, weapon.icon, ScaleMode.ScaleToFit);
                GUI.color = old;
                return;
            }
            // Proportions per category: body length, barrel length, stock, magazine.
            float body = 0.4f, barrel = 0.25f, stock = 0.2f, mag = 0.12f, height = 0.22f;
            switch (weapon.category)
            {
                case WeaponCategory.CompactSMG: body = 0.38f; barrel = 0.08f; stock = 0.08f; mag = 0.18f; break;
                case WeaponCategory.SMG: body = 0.42f; barrel = 0.14f; stock = 0.18f; mag = 0.16f; break;
                case WeaponCategory.CompactRifle: body = 0.4f; barrel = 0.2f; stock = 0.2f; mag = 0.15f; break;
                case WeaponCategory.Rifle: body = 0.4f; barrel = 0.3f; stock = 0.24f; mag = 0.15f; break;
                case WeaponCategory.Shotgun: body = 0.36f; barrel = 0.36f; stock = 0.24f; mag = 0f; height = 0.18f; break;
                case WeaponCategory.Carbine: body = 0.4f; barrel = 0.34f; stock = 0.22f; mag = 0.1f; break;
                case WeaponCategory.LessLethal: body = 0.36f; barrel = 0.22f; stock = 0.18f; mag = 0f; height = 0.3f; break;
                default: body = 0.42f; barrel = 0.05f; stock = 0f; mag = 0f; height = 0.24f; break;
            }
            float total = body + barrel + stock;
            float scale = rect.width * 0.9f / Mathf.Max(0.5f, total);
            float x = rect.x + (rect.width - total * scale) * 0.5f;
            float y = rect.center.y - height * scale * 0.5f;
            float bh = height * scale;
            if (stock > 0f) UITheme.Fill(new Rect(x, y + bh * 0.15f, stock * scale, bh * 0.6f), color);
            UITheme.Fill(new Rect(x + stock * scale, y, body * scale, bh), color);
            float barrelHeight = weapon.category == WeaponCategory.LessLethal ? bh * 0.6f : bh * 0.3f;
            UITheme.Fill(new Rect(x + (stock + body) * scale, y + bh * 0.15f, barrel * scale, barrelHeight), color);
            // Grip and magazine.
            bool pistol = weapon.isSidearm;
            UITheme.Fill(new Rect(x + stock * scale + body * scale * (pistol ? 0.08f : 0.35f), y + bh, bh * 0.45f, bh * (pistol ? 1.3f : 0.9f)), color);
            if (mag > 0f) UITheme.Fill(new Rect(x + stock * scale + body * scale * 0.62f, y + bh, bh * 0.4f, mag * scale), color);
        }

        public static void Equipment(Rect rect, EquipmentKind kind, Color color)
        {
            Vector2 c = rect.center;
            float r = Mathf.Min(rect.width, rect.height) * 0.4f;
            switch (kind)
            {
                case EquipmentKind.Flashbang:
                    UITheme.Fill(new Rect(c.x - r * 0.4f, c.y - r * 0.6f, r * 0.8f, r * 1.4f), color);
                    UITheme.Fill(new Rect(c.x - r * 0.25f, c.y - r * 0.9f, r * 0.5f, r * 0.3f), color);
                    UITheme.Fill(new Rect(c.x + r * 0.2f, c.y - r * 0.9f, r * 0.5f, r * 0.15f), color);
                    break;
                case EquipmentKind.Smoke:
                    UITheme.Dot(c + new Vector2(-r * 0.3f, r * 0.1f), r * 0.5f, color);
                    UITheme.Dot(c + new Vector2(r * 0.3f, 0f), r * 0.55f, color);
                    UITheme.Dot(c + new Vector2(0f, -r * 0.35f), r * 0.5f, color);
                    break;
                case EquipmentKind.BreachingCharge:
                    UITheme.Fill(new Rect(c.x - r, c.y - r * 0.4f, r * 2f, r * 0.8f), color);
                    UITheme.Fill(new Rect(c.x - r * 0.15f, c.y - r * 0.9f, r * 0.3f, r * 0.5f), color);
                    break;
                case EquipmentKind.DoorWedge:
                    UITheme.LineTo(c + new Vector2(-r, r * 0.5f), c + new Vector2(r, r * 0.5f), color, r * 0.3f);
                    UITheme.LineTo(c + new Vector2(-r, r * 0.4f), c + new Vector2(r, -r * 0.3f), color, r * 0.3f);
                    break;
                case EquipmentKind.MedicalKit:
                    UITheme.Frame(new Rect(c.x - r, c.y - r * 0.8f, r * 2f, r * 1.6f), color, 2f);
                    UITheme.Fill(new Rect(c.x - r * 0.15f, c.y - r * 0.55f, r * 0.3f, r * 1.1f), color);
                    UITheme.Fill(new Rect(c.x - r * 0.55f, c.y - r * 0.15f, r * 1.1f, r * 0.3f), color);
                    break;
                case EquipmentKind.PortableLight:
                    UITheme.Fill(new Rect(c.x - r * 0.4f, c.y - r * 0.1f, r * 0.8f, r * 0.9f), color);
                    UITheme.Dot(c + new Vector2(0f, -r * 0.35f), r * 0.45f, color);
                    break;
                default: // recon camera
                    UITheme.Fill(new Rect(c.x - r, c.y - r * 0.1f, r * 1.4f, r * 0.2f), color);
                    UITheme.Dot(c + new Vector2(r * 0.6f, 0f), r * 0.35f, color);
                    break;
            }
        }
    }
}
