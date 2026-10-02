using UnityEngine;

namespace Swat
{
    // Draws the radial squad command menu while the command key is held.
    // Pick with the mouse (release or click) or the number keys.
    public class CommandWheelUI
    {
        public void Draw(GameManager game)
        {
            var squad = SquadCommandManager.Instance;
            var options = squad.Options;
            if (options.Count == 0) return;
            float s = UITheme.Scale;
            Vector2 center = squad.WheelCenter / s;
            float radius = 150f;

            // Target marker in the world.
            Vector2 target;
            if (UITheme.WorldToGui(game.CameraRig.Cam, squad.WheelPoint + Vector3.up * 0.1f, out target))
            {
                UITheme.Ring(target, 12f, UITheme.Accent, 2f);
                UITheme.LineTo(center, target, new Color(0.36f, 0.62f, 0.95f, 0.35f), 2f);
            }

            UITheme.Dot(center, radius + 70f, new Color(0.02f, 0.03f, 0.05f, 0.7f));
            UITheme.Ring(center, radius + 70f, new Color(UITheme.Line.r, UITheme.Line.g, UITheme.Line.b, 0.8f), 2f);
            float step = 360f / options.Count;
            for (int i = 0; i < options.Count; i++)
            {
                var option = options[i];
                float angle = i * step * Mathf.Deg2Rad;
                Vector2 p = center + new Vector2(Mathf.Sin(angle), -Mathf.Cos(angle)) * radius;
                bool hovered = i == squad.Hovered;
                var rect = new Rect(p.x - 86f, p.y - 24f, 172f, 48f);
                UITheme.Fill(rect, hovered ? new Color(UITheme.AccentDim.r, UITheme.AccentDim.g, UITheme.AccentDim.b, 0.95f) : UITheme.PanelLight);
                UITheme.Frame(rect, hovered ? UITheme.Accent : new Color(UITheme.Line.r, UITheme.Line.g, UITheme.Line.b, 0.8f));
                Color text = option.enabled ? UITheme.TextColor : UITheme.Faint;
                UIIcons.Order(new Rect(rect.x + 8f, rect.y + 8f, 32f, 32f), option.order, option.action, option.enabled ? (hovered ? Color.white : UITheme.Accent) : UITheme.Faint);
                UITheme.Text(new Rect(rect.x + 46f, rect.y + 4f, rect.width - 50f, 22f), option.label.ToUpperInvariant(), 15, text, TextAnchor.UpperLeft, true);
                UITheme.Text(new Rect(rect.x + 46f, rect.y + 26f, rect.width - 50f, 18f), "key " + ((i + 1) % 10), 12, UITheme.Faint, TextAnchor.UpperLeft);
            }

            // Centre: what the order applies to.
            var door = squad.WheelDoor;
            string targetText = door != null ? "Door: " + DoorText(door) : "Location";
            UITheme.Text(new Rect(center.x - 90f, center.y - 36f, 180f, 22f), targetText, 15, UITheme.Accent, TextAnchor.MiddleCenter, true);
            UITheme.Text(new Rect(center.x - 90f, center.y - 12f, 180f, 22f), squad.SelectionLabel, 14, UITheme.TextColor, TextAnchor.MiddleCenter);
            string hint = "Release on an order";
            if (squad.Hovered >= 0 && !options[squad.Hovered].enabled) hint = options[squad.Hovered].reason;
            UITheme.Text(new Rect(center.x - 100f, center.y + 12f, 200f, 40f), hint, 12, squad.Hovered >= 0 && !options[squad.Hovered].enabled ? UITheme.Warn : UITheme.Faint, TextAnchor.UpperCenter);
        }

        static string DoorText(DoorController door)
        {
            switch (door.State)
            {
                case DoorState.Locked: return door.Electronic ? "electronic lock" : door.Breachable ? "locked (breachable)" : "locked";
                case DoorState.Wedged: return "wedged";
                case DoorState.Open: return "open";
                case DoorState.Breached: return "breached";
                default: return door.Breachable ? "closed (breachable)" : "closed";
            }
        }
    }
}
