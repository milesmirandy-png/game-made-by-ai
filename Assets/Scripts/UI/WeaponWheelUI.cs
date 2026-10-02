using UnityEngine;

namespace Swat
{
    // Radial weapon and equipment picker shown while the switch-weapon key is
    // held. Aiming freezes while it is open; release on an entry (or click it)
    // to choose, right-click or Esc to cancel.
    public class WeaponWheelUI
    {
        public void Draw(GameManager game)
        {
            var player = game.Player;
            if (player == null) return;
            var weapons = player.Weapons;
            var entries = weapons.WheelEntries;
            if (!weapons.WheelOpen || entries.Count == 0) return;
            float s = UITheme.Scale;
            Vector2 center = weapons.WheelCenter / s;
            float radius = entries.Count <= 4 ? 120f : 145f;

            UITheme.Dot(center, radius + 66f, new Color(0.02f, 0.03f, 0.05f, 0.72f));
            UITheme.Ring(center, radius + 66f, new Color(UITheme.Line.r, UITheme.Line.g, UITheme.Line.b, 0.8f), 2f);
            float step = 360f / entries.Count;
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                float angle = i * step * Mathf.Deg2Rad;
                Vector2 p = center + new Vector2(Mathf.Sin(angle), -Mathf.Cos(angle)) * radius;
                bool hovered = i == weapons.WheelHovered;
                var rect = new Rect(p.x - 82f, p.y - 30f, 164f, 60f);
                UITheme.Fill(rect, hovered ? new Color(UITheme.AccentDim.r, UITheme.AccentDim.g, UITheme.AccentDim.b, 0.95f) : UITheme.PanelLight);
                UITheme.Frame(rect, hovered ? UITheme.Accent : entry.current ? new Color(UITheme.Accent.r, UITheme.Accent.g, UITheme.Accent.b, 0.6f) : new Color(UITheme.Line.r, UITheme.Line.g, UITheme.Line.b, 0.8f));
                Color icon = entry.enabled ? (hovered ? Color.white : UITheme.TextColor) : UITheme.Faint;
                if (entry.weapon >= 0) UIIcons.Weapon(new Rect(rect.x + 6f, rect.y + 6f, 64f, 26f), entry.weaponData, icon);
                else UIIcons.Equipment(new Rect(rect.x + 22f, rect.y + 4f, 30f, 30f), entry.kind, icon);
                UITheme.Text(new Rect(rect.x + 74f, rect.y + 4f, rect.width - 78f, 30f), entry.label, 13, entry.enabled ? UITheme.TextColor : UITheme.Faint, TextAnchor.MiddleLeft, true);
                UITheme.Text(new Rect(rect.x + 8f, rect.y + 36f, rect.width - 16f, 20f), entry.detail + (entry.current ? "   (equipped)" : ""), 12, entry.enabled ? UITheme.Dim : UITheme.Faint, TextAnchor.MiddleLeft);
            }

            string title = "Choose weapon or equipment";
            if (weapons.WheelHovered >= 0 && weapons.WheelHovered < entries.Count)
            {
                var hovered = entries[weapons.WheelHovered];
                title = hovered.enabled ? hovered.label : hovered.label + " unavailable";
            }
            UITheme.Text(new Rect(center.x - 90f, center.y - 22f, 180f, 22f), title, 14, UITheme.Accent, TextAnchor.MiddleCenter, true);
            UITheme.Text(new Rect(center.x - 90f, center.y + 2f, 180f, 36f), GameInput.UsingGamepad ? "Release " + UITheme.KeyFor(InputAction.SwitchWeapon) + " to select\nB to cancel" : "Release to select\nRight-click to cancel", 12, UITheme.Faint, TextAnchor.UpperCenter);
        }
    }
}
