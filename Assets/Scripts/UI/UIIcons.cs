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
            Weapon(rect, weapon, color, null);
        }

        // A custom icon texture if the weapon has one, otherwise its pixel-art sprite
        // (with the loadout's attachments drawn on). Faint colours draw it greyed out.
        public static void Weapon(Rect rect, WeaponData weapon, Color color, OfficerLoadout attachments)
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
            bool dim = color.a < 0.5f || (color.r + color.g + color.b) < (UITheme.Faint.r + UITheme.Faint.g + UITheme.Faint.b) + 0.05f;
            WeaponSprites.Draw(rect, WeaponSprites.Get(weapon, attachments), dim ? 0.35f : 1f);
        }

        // Simple glyphs for squad orders, used on the command wheel and squad panel.
        public static void Order(Rect rect, SquadOrder order, DoorAction action, Color color)
        {
            Vector2 c = rect.center;
            float r = Mathf.Min(rect.width, rect.height) * 0.42f;
            float t = Mathf.Max(2f, r * 0.22f);
            if (order == SquadOrder.Stack)
            {
                // Door frame with dots beside it; the action adds a mark.
                UITheme.Frame(new Rect(c.x - r * 0.35f, c.y - r, r * 0.7f, r * 2f), color, t * 0.7f);
                UITheme.Dot(c + new Vector2(-r * 0.75f, -r * 0.3f), t, color);
                UITheme.Dot(c + new Vector2(-r * 0.75f, r * 0.3f), t, color);
                if (action == DoorAction.Breach) UITheme.Dot(c, t * 1.3f, UITheme.Warn);
                else if (action == DoorAction.Flash) UITheme.Dot(c, t * 1.3f, Color.white);
                else if (action == DoorAction.Open) UITheme.LineTo(c + new Vector2(-r * 0.35f, r), c + new Vector2(r * 0.5f, r * 0.3f), color, t * 0.7f);
                else if (action == DoorAction.Mirror)
                {
                    // A little mirror on a stick, slid under the door.
                    UITheme.LineTo(c + new Vector2(-r * 0.9f, r * 0.9f), c + new Vector2(0f, r * 0.9f), color, t * 0.5f);
                    UITheme.Ring(c + new Vector2(r * 0.2f, r * 0.9f), t * 1.1f, color, t * 0.5f);
                }
                else if (action == DoorAction.Shotgun)
                {
                    UITheme.Dot(c + new Vector2(r * 0.15f, -r * 0.15f), t * 0.9f, new Color(1f, 0.6f, 0.2f));
                    UITheme.Dot(c + new Vector2(r * 0.15f, r * 0.25f), t * 0.9f, new Color(1f, 0.6f, 0.2f));
                }
                return;
            }
            switch (order)
            {
                case SquadOrder.Follow: // chevron pointing up
                    UITheme.LineTo(c + new Vector2(-r * 0.8f, r * 0.4f), c + new Vector2(0f, -r * 0.5f), color, t);
                    UITheme.LineTo(c + new Vector2(0f, -r * 0.5f), c + new Vector2(r * 0.8f, r * 0.4f), color, t);
                    break;
                case SquadOrder.Hold: // hand / stop bar
                    UITheme.Fill(new Rect(c.x - r * 0.8f, c.y - t * 0.6f, r * 1.6f, t * 1.2f), color);
                    UITheme.Fill(new Rect(c.x - t * 0.6f, c.y - r * 0.8f, t * 1.2f, r * 1.6f), color);
                    break;
                case SquadOrder.Regroup: // converging dots
                    UITheme.Dot(c, t * 1.2f, color);
                    for (int i = 0; i < 3; i++)
                    {
                        float a = i * Mathf.PI * 2f / 3f - Mathf.PI * 0.5f;
                        UITheme.Dot(c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r * 0.8f, t * 0.8f, color);
                    }
                    break;
                case SquadOrder.MoveTo: // location pin
                    UITheme.Ring(c + new Vector2(0f, -r * 0.25f), r * 0.45f, color, t * 0.8f);
                    UITheme.LineTo(c + new Vector2(0f, r * 0.25f), c + new Vector2(0f, r * 0.9f), color, t);
                    break;
                case SquadOrder.Cover: // eye
                    UITheme.Ring(c, r * 0.65f, color, t * 0.8f);
                    UITheme.Dot(c, t * 1.1f, color);
                    break;
                case SquadOrder.StayBehind: // anchor bar
                    UITheme.Fill(new Rect(c.x - r * 0.8f, c.y + r * 0.4f, r * 1.6f, t), color);
                    UITheme.Fill(new Rect(c.x - t * 0.5f, c.y - r * 0.8f, t, r * 1.2f), color);
                    break;
                case SquadOrder.ReturnToPlayer: // arrow back
                    UITheme.LineTo(c + new Vector2(r * 0.8f, 0f), c + new Vector2(-r * 0.8f, 0f), color, t);
                    UITheme.LineTo(c + new Vector2(-r * 0.8f, 0f), c + new Vector2(-r * 0.2f, -r * 0.55f), color, t);
                    UITheme.LineTo(c + new Vector2(-r * 0.8f, 0f), c + new Vector2(-r * 0.2f, r * 0.55f), color, t);
                    break;
                case SquadOrder.AssistCivilians: // person with cross
                    UITheme.Dot(c + new Vector2(-r * 0.3f, -r * 0.45f), t * 1.1f, color);
                    UITheme.Fill(new Rect(c.x - r * 0.55f, c.y - r * 0.15f, r * 0.5f, r * 0.9f), color);
                    UITheme.Fill(new Rect(c.x + r * 0.35f, c.y - r * 0.35f, t * 0.8f, r * 0.8f), color);
                    UITheme.Fill(new Rect(c.x + r * 0.15f, c.y - t * 0.4f, r * 0.6f + t * 0.4f, t * 0.8f), color);
                    break;
                default: // wait: pause bars
                    UITheme.Fill(new Rect(c.x - r * 0.5f, c.y - r * 0.6f, t * 1.3f, r * 1.2f), color);
                    UITheme.Fill(new Rect(c.x + r * 0.5f - t * 1.3f, c.y - r * 0.6f, t * 1.3f, r * 1.2f), color);
                    break;
            }
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
