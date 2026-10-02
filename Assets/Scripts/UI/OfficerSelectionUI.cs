using System.Collections.Generic;
using UnityEngine;

namespace Swat
{
    // The roster: officer cards with portraits and role icons, full stats,
    // ability and preferred kit for the focused officer, a 3D preview in the
    // locker room, and squad composition (who you play as, who comes along).
    public class OfficerSelectionUI
    {
        OfficerData focused;

        public void Draw(GameManager game)
        {
            float w = UITheme.Width, h = UITheme.Height;
            UITheme.Fill(new Rect(0f, 0f, 780f, h), new Color(0.02f, 0.03f, 0.05f, 0.8f));
            UITheme.Fill(new Rect(w - 600f, 0f, 600f, h), new Color(0.02f, 0.03f, 0.05f, 0.8f));
            var mission = OfficerSelectionManager.Mission;
            UITheme.Header(new Rect(60f, 40f, 700f, 60f), "Officer Roster", game.BrowseMode || mission == null ? "Review your officers" : "Choose who deploys on " + mission.displayName);

            if (focused == null) focused = OfficerSelectionManager.Leader;
            DrawLineup(new Rect(60f, 116f, 700f, 112f), game);

            var officers = GameData.AllOfficers;
            for (int i = 0; i < officers.Count; i++)
            {
                var rect = new Rect(60f + (i % 2) * 356f, 250f + (i / 2) * 196f, 344f, 184f);
                if (Card(rect, officers[i])) focused = officers[i];
            }

            Details(new Rect(w - 570f, 40f, 520f, h - 150f), focused);
            var loadout = GameData.LoadoutFor(focused);
            var hq = game.Headquarters;
            CharacterPreview.Show(focused, loadout, hq.rosterTarget + new Vector3(0f, -0.5f, -1.5f), 200f);

            if (game.BrowseMode)
            {
                if (UITheme.Button(new Rect(60f, h - 90f, 200f, 50f), "< Main menu")) game.GoToMainMenu();
                if (UITheme.Button(new Rect(280f, h - 90f, 220f, 50f), "Equipment  >")) game.OpenLoadout(true);
            }
            else
            {
                if (UITheme.Button(new Rect(60f, h - 90f, 200f, 50f), "< Briefing")) game.OpenBriefingKeepPlan();
                if (UITheme.Button(new Rect(w - 380f, h - 90f, 320f, 50f), "Loadout  >", true, true, 21)) game.OpenLoadout(false);
            }
        }

        void DrawLineup(Rect rect, GameManager game)
        {
            UITheme.Panel(rect, false);
            UITheme.Text(new Rect(rect.x + 16f, rect.y + 8f, 300f, 20f), "DEPLOYING TEAM", 14, UITheme.Accent, TextAnchor.UpperLeft, true);
            var leader = OfficerSelectionManager.Leader;
            var squad = OfficerSelectionManager.Squad;
            int max = OfficerSelectionManager.MaxSquad;
            float x = rect.x + 16f;
            Slot(new Rect(x, rect.y + 32f, 150f, 70f), leader, "YOU");
            x += 166f;
            for (int i = 0; i < 3; i++)
            {
                var slot = new Rect(x + i * 172f, rect.y + 32f, 160f, 70f);
                if (i >= max) { UITheme.Fill(slot, new Color(0f, 0f, 0f, 0.4f)); UITheme.Text(slot, "Not available\non this mission", 13, UITheme.Faint, TextAnchor.MiddleCenter); }
                else if (i < squad.Count) Slot(slot, squad[i], "F" + (i + 1));
                else { UITheme.Frame(slot, UITheme.Line); UITheme.Text(slot, "Empty slot", 14, UITheme.Faint, TextAnchor.MiddleCenter); }
            }
        }

        void Slot(Rect rect, OfficerData officer, string tag)
        {
            UITheme.Fill(rect, UITheme.PanelLight);
            UIIcons.Portrait(new Rect(rect.x + 4f, rect.y + 4f, 52f, rect.height - 8f), officer, GameData.LoadoutFor(officer));
            UITheme.Text(new Rect(rect.x + 62f, rect.y + 6f, rect.width - 66f, 20f), officer.callsign, 16, UITheme.TextColor, TextAnchor.UpperLeft, true);
            UITheme.Text(new Rect(rect.x + 62f, rect.y + 26f, rect.width - 66f, 18f), UITheme.RoleName(officer.role), 13, UITheme.RoleColor(officer.role));
            UITheme.Text(new Rect(rect.x + 62f, rect.y + 46f, rect.width - 66f, 18f), tag, 13, UITheme.Dim, TextAnchor.UpperLeft, true);
            if (UITheme.Hover(rect) && GUI.Button(rect, GUIContent.none, GUIStyle.none)) focused = officer;
        }

        bool Card(Rect rect, OfficerData officer)
        {
            bool available = Progression.IsAvailable(officer);
            bool isLeader = officer == OfficerSelectionManager.Leader;
            bool inSquad = OfficerSelectionManager.InSquad(officer);
            bool clicked = UITheme.Button(rect, string.Empty, true, officer == focused);
            UIIcons.Portrait(new Rect(rect.x + 10f, rect.y + 10f, 110f, 130f), officer, GameData.LoadoutFor(officer), available);
            float x = rect.x + 132f, cw = rect.width - 140f;
            UITheme.Text(new Rect(x, rect.y + 12f, cw, 24f), officer.displayName, 18, available ? UITheme.TextColor : UITheme.Faint, TextAnchor.UpperLeft, true);
            UITheme.Text(new Rect(x, rect.y + 36f, cw, 20f), "\"" + officer.callsign + "\"", 15, UITheme.Dim);
            UITheme.Text(new Rect(x, rect.y + 58f, cw, 20f), UITheme.RoleName(officer.role), 15, UITheme.RoleColor(officer.role), TextAnchor.UpperLeft, true);
            var loadout = GameData.LoadoutFor(officer);
            var primary = GameData.Weapon(loadout.useShield ? loadout.sidearmId : loadout.primaryId);
            if (primary != null)
            {
                UIIcons.Weapon(new Rect(x, rect.y + 84f, 90f, 34f), primary, UITheme.Dim);
                UITheme.Text(new Rect(x + 96f, rect.y + 90f, cw - 96f, 20f), loadout.useShield ? "Shield + sidearm" : primary.displayName, 13, UITheme.Dim);
            }
            string tag = !available ? "LOCKED - " + UnlockText(officer) : isLeader ? "TEAM LEADER (YOU)" : inSquad ? "IN SQUAD" : "AVAILABLE";
            Color color = !available ? UITheme.Faint : isLeader ? UITheme.Accent : inSquad ? UITheme.Good : UITheme.Dim;
            UITheme.Text(new Rect(rect.x + 12f, rect.yMax - 32f, rect.width - 24f, 22f), tag, 14, color, TextAnchor.UpperLeft, true);
            return clicked;
        }

        static string UnlockText(OfficerData officer)
        {
            if (officer.role == OfficerRole.Recon) return "finish training or 1 mission";
            return "complete " + officer.unlockAfterMissions + " mission(s)";
        }

        void Details(Rect rect, OfficerData officer)
        {
            UITheme.Panel(rect);
            bool available = Progression.IsAvailable(officer);
            var loadout = GameData.LoadoutFor(officer);
            float x = rect.x + 24f, cw = rect.width - 48f, y = rect.y + 20f;
            UIIcons.Portrait(new Rect(x, y, 120f, 140f), officer, loadout, available);
            UITheme.Text(new Rect(x + 136f, y, cw - 136f, 30f), officer.displayName, 24, UITheme.TextColor, TextAnchor.UpperLeft, true);
            UITheme.Text(new Rect(x + 136f, y + 32f, cw - 136f, 22f), "Callsign \"" + officer.callsign + "\"", 16, UITheme.Dim);
            UITheme.RoleIcon(new Rect(x + 136f, y + 62f, 30f, 30f), officer.role);
            UITheme.Text(new Rect(x + 174f, y + 66f, cw - 174f, 24f), UITheme.RoleName(officer.role), 18, UITheme.RoleColor(officer.role), TextAnchor.UpperLeft, true);
            float ph = UITheme.TextHeight(officer.personality, 14, cw - 136f);
            UITheme.Text(new Rect(x + 136f, y + 98f, cw - 136f, ph), officer.personality, 14, UITheme.Dim);
            y += 156f;

            var armor = GameData.Armor(loadout.armorId);
            float armorValue = (armor != null ? armor.damageReduction : 0f) + officer.armorRating;
            Stat(ref y, x, cw, "Health", Mathf.InverseLerp(70f, 120f, officer.maxHealth), officer.maxHealth.ToString("0"));
            Stat(ref y, x, cw, "Armor", Mathf.InverseLerp(0f, 0.6f, armorValue), Mathf.RoundToInt(armorValue * 100f) + "%");
            Stat(ref y, x, cw, "Movement", Mathf.InverseLerp(0.85f, 1.1f, officer.moveSpeed * (armor != null ? armor.speedMultiplier : 1f)), (officer.moveSpeed * 100f).ToString("0") + "%");
            Stat(ref y, x, cw, "Accuracy", officer.accuracy, Mathf.RoundToInt(officer.accuracy * 100f) + "%");
            Stat(ref y, x, cw, "Reaction", Mathf.InverseLerp(0.55f, 0.3f, officer.reactionTime), officer.reactionTime.ToString("0.00") + "s");
            Stat(ref y, x, cw, "Perception", Mathf.InverseLerp(12f, 26f, officer.perceptionRange), officer.perceptionRange.ToString("0") + "m");
            Stat(ref y, x, cw, "Command response", Mathf.InverseLerp(0.8f, 1.4f, officer.commandResponsiveness), officer.commandResponsiveness.ToString("0.0") + "x");
            Stat(ref y, x, cw, "Equipment capacity", Mathf.InverseLerp(4f, 12f, LoadoutRules.Capacity(officer, loadout)), LoadoutRules.Capacity(officer, loadout).ToString());
            y += 8f;

            UITheme.Text(new Rect(x, y, cw, 22f), "ABILITY: " + officer.abilityName.ToUpperInvariant() + "  (" + UITheme.KeyFor(InputAction.Ability) + ")", 15, UITheme.Accent, TextAnchor.UpperLeft, true);
            y += 24f;
            float ah = UITheme.TextHeight(officer.abilityDescription, 15, cw);
            UITheme.Text(new Rect(x, y, cw, ah), officer.abilityDescription + (officer.role == OfficerRole.Shield ? "" : "  Cooldown " + officer.abilityCooldown.ToString("0") + "s."), 15, UITheme.TextColor);
            y += ah + 14f;

            UITheme.Text(new Rect(x, y, cw, 22f), "PREFERRED EQUIPMENT", 15, UITheme.Accent, TextAnchor.UpperLeft, true);
            y += 24f;
            var kit = officer.defaultLoadout;
            var primary = GameData.Weapon(kit.primaryId);
            var sidearm = GameData.Weapon(kit.sidearmId);
            var kitArmor = GameData.Armor(kit.armorId);
            string kitText = (kit.useShield ? "Ballistic shield, " : "") + (primary != null ? primary.displayName : "-") + ", " + (sidearm != null ? sidearm.displayName : "-")
                + ", " + (kitArmor != null ? kitArmor.displayName : "-");
            var items = new List<string>();
            foreach (var entry in kit.equipment)
            {
                var item = GameData.Equipment(entry.id);
                if (item != null) items.Add(entry.count + "x " + item.displayName);
            }
            if (items.Count > 0) kitText += "\n" + string.Join(", ", items.ToArray());
            float kh = UITheme.TextHeight(kitText, 15, cw);
            UITheme.Text(new Rect(x, y, cw, kh), kitText, 15, UITheme.Dim);

            // Squad buttons.
            float by = rect.yMax - 120f;
            bool isLeader = officer == OfficerSelectionManager.Leader;
            bool inSquad = OfficerSelectionManager.InSquad(officer);
            if (!available)
            {
                UITheme.Text(new Rect(x, by, cw, 50f), "Locked: " + UnlockText(officer) + ".", 17, UITheme.Warn);
                return;
            }
            if (UITheme.Button(new Rect(x, by, cw, 46f), isLeader ? "You are playing as " + officer.callsign : "Play as " + officer.callsign + " (team leader)", !isLeader, isLeader))
                OfficerSelectionManager.SetLeader(officer);
            int max = OfficerSelectionManager.MaxSquad;
            string squadLabel = isLeader ? "The team leader is always deployed" : inSquad ? "Remove from squad" : max == 0 ? "No squadmates on this mission" : "Add to squad";
            if (UITheme.Button(new Rect(x, by + 54f, cw, 46f), squadLabel, !isLeader && (inSquad || max > 0), inSquad))
                OfficerSelectionManager.ToggleSquad(officer);
        }

        static void Stat(ref float y, float x, float w, string label, float fraction, string value)
        {
            UITheme.StatBar(new Rect(x, y, w, 24f), label, fraction, value);
            y += 26f;
        }
    }
}
