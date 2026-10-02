using System.Collections.Generic;
using UnityEngine;

namespace Swat
{
    // The in-mission HUD: objectives and mission status (top left), alarm and
    // threat info (top right), squad status and orders (right), the leader's
    // health/armor/stamina (bottom left), weapon, ammo and equipment (bottom
    // right), interaction prompt and radio chatter (bottom centre), labels
    // over squadmates and waypoints, and the crosshair.
    public class HUDController
    {
        public void Draw(GameManager game)
        {
            var player = game.Player;
            if (player == null) return;
            float w = UITheme.Width, h = UITheme.Height;

            // Screen effects (kept mild: no gore, just tints).
            if (player.Health.Blind > 0f) UITheme.Fill(new Rect(0f, 0f, w, h), new Color(1f, 1f, 1f, player.Health.Blind * 0.92f));
            if (player.Health.DamageFlash > 0f) UITheme.Fill(new Rect(0f, 0f, w, h), new Color(0.7f, 0f, 0f, player.Health.DamageFlash * 0.22f));
            if (!player.IsAlive) UITheme.Fill(new Rect(0f, 0f, w, h), new Color(0.2f, 0f, 0f, 0.4f));
            DrawHitIndicator(game, player);
            DrawWorldLabels(game);

            DrawObjectives(game);
            DrawStatusTopRight(game);
            DrawSquad(game);
            DrawPlayer(game, player);
            DrawWeapon(player);
            DrawPrompt(game, player);
            DrawRadio();
        }

        // ---- Objectives ----

        void DrawObjectives(GameManager game)
        {
            var mission = MissionManager.Instance;
            if (mission.Mission == null) return;
            var room = game.Level.RoomAt(game.Player.Position);
            var objectives = mission.Objectives;
            bool full = game.ShowObjectives;
            var shown = new List<Objective>();
            foreach (var objective in objectives)
            {
                if (objective.Optional && !full) continue;
                if (!full && mission.Tracker.Sequential && objective.State == ObjectiveState.Pending) continue;
                shown.Add(objective);
            }
            float row = 24f;
            var rect = new Rect(18f, 18f, 470f, 92f + shown.Count * row + (full ? 0f : 22f));
            UITheme.Panel(rect);
            UITheme.Text(new Rect(rect.x + 14f, rect.y + 8f, rect.width - 28f, 24f), mission.Mission.displayName.ToUpperInvariant(), 17, UITheme.Accent, TextAnchor.UpperLeft, true);
            string place = room != null ? room.DisplayName : "Outside";
            UITheme.Text(new Rect(rect.x + 14f, rect.y + 32f, rect.width - 28f, 22f), place + "     " + MissionScoring.FormatTime(mission.Elapsed), 15, UITheme.Dim);
            var stats = mission.Stats;
            string tally = "Suspects secured " + (stats.suspectsArrested + stats.suspectsKilled) + "/" + stats.suspectsTotal
                + "    Civilians safe " + stats.civilians.evacuated + "/" + stats.civilians.total;
            UITheme.Text(new Rect(rect.x + 14f, rect.y + 54f, rect.width - 28f, 22f), tally, 14, UITheme.Dim);
            float y = rect.y + 80f;
            foreach (var objective in shown)
            {
                Color color = objective.State == ObjectiveState.Completed ? UITheme.Good
                    : objective.State == ObjectiveState.Failed ? UITheme.Bad
                    : objective.State == ObjectiveState.Pending ? UITheme.Faint : UITheme.TextColor;
                string mark = objective.State == ObjectiveState.Completed ? "[x]" : objective.State == ObjectiveState.Failed ? "[-]" : "[ ]";
                string prefix = objective.Optional ? "<color=#8a96a8>opt</color> " : "";
                UITheme.Text(new Rect(rect.x + 14f, y, rect.width - 28f, row), mark + "  " + prefix + objective.Label, 15, color);
                y += row;
            }
            if (!full) UITheme.Text(new Rect(rect.x + 14f, y, rect.width - 28f, 20f), UITheme.KeyFor(InputAction.Objectives) + ": all objectives    " + UITheme.KeyFor(InputAction.TacticalMap) + ": map    " + UITheme.KeyFor(InputAction.PlanningMode) + ": planning", 13, UITheme.Faint);
        }

        // ---- Top right ----

        void DrawStatusTopRight(GameManager game)
        {
            float w = UITheme.Width;
            float y = 30f;
            var alarm = game.Level.alarm;
            if (alarm != null && alarm.State == AlarmState.Triggered)
            {
                bool flash = Mathf.Repeat(Time.unscaledTime * 2f, 1f) < 0.5f;
                var rect = new Rect(w - 250f, y, 230f, 34f);
                UITheme.Fill(rect, flash ? new Color(UITheme.AlertRed.r, UITheme.AlertRed.g, UITheme.AlertRed.b, 0.85f) : new Color(0.3f, 0.03f, 0.03f, 0.85f));
                UITheme.Text(rect, "ALARM ACTIVE", 18, Color.white, TextAnchor.MiddleCenter, true);
                y += 42f;
            }
            var player = game.Player;
            if (player.Officer.role == OfficerRole.Leader || SquadCommandManager.Instance.CoordinateActive)
            {
                int threats = SquadStatusTracker.ThreatsSeenByTeam;
                UITheme.ShadowText(new Rect(w - 300f, y, 280f, 24f), "Threats in sight: " + threats, 17, threats > 0 ? UITheme.Bad : UITheme.Dim, TextAnchor.UpperRight, true);
                y += 26f;
            }
            if (SquadCommandManager.Instance.CoordinateActive)
            {
                UITheme.ShadowText(new Rect(w - 300f, y, 280f, 24f), "COORDINATE ACTIVE", 15, UITheme.Accent, TextAnchor.UpperRight, true);
                y += 24f;
            }
            if (game.Plan != null && game.Plan.powerOutage)
                UITheme.ShadowText(new Rect(w - 300f, y, 280f, 24f), "Power out", 15, UITheme.Warn, TextAnchor.UpperRight);
        }

        // ---- Squad ----

        void DrawSquad(GameManager game)
        {
            var squad = SquadCommandManager.Instance;
            var officers = squad.Squad;
            float w = UITheme.Width;
            float y = 150f;
            if (officers.Count == 0) return;
            UITheme.ShadowText(new Rect(w - 330f, y, 310f, 22f), "SQUAD  -  orders to: " + squad.SelectionLabel, 14, UITheme.Accent, TextAnchor.UpperRight, true);
            y += 26f;
            for (int i = 0; i < officers.Count; i++)
            {
                var officer = officers[i];
                var rect = new Rect(w - 330f, y, 310f, 62f);
                UITheme.Fill(rect, officer.Selected ? new Color(UITheme.AccentDim.r, UITheme.AccentDim.g, UITheme.AccentDim.b, 0.85f) : UITheme.PanelColor);
                UITheme.Fill(new Rect(rect.x, rect.y, 3f, rect.height), officer.Selected ? UITheme.Accent : UITheme.RoleColor(officer.Data.role));
                UITheme.RoleIcon(new Rect(rect.x + 10f, rect.y + 10f, 30f, 30f), officer.Data.role);
                UITheme.Text(new Rect(rect.x + 48f, rect.y + 6f, 170f, 22f), "F" + (i + 1) + "  " + officer.Data.callsign, 16, UITheme.TextColor, TextAnchor.UpperLeft, true);
                UITheme.Text(new Rect(rect.x + 48f, rect.y + 26f, 250f, 20f), officer.Status, 14, officer.IsAlive ? UITheme.Dim : UITheme.Bad);
                UITheme.Bar(new Rect(rect.x + 48f, rect.y + 50f, 250f, 5f), officer.Health.Fraction, HealthColor(officer.Health.Fraction));
                var weapon = officer.Inventory.Current;
                UITheme.Text(new Rect(rect.x + 200f, rect.y + 6f, 100f, 22f), weapon.Magazine + "/" + weapon.Reserve, 14, UITheme.Dim, TextAnchor.UpperRight);
                y += 68f;
            }
            if (!string.IsNullOrEmpty(squad.LastOrder) && Time.unscaledTime - squad.LastOrderTime < 4f)
                UITheme.ShadowText(new Rect(w - 430f, y, 410f, 22f), "Order: " + squad.LastOrder, 15, UITheme.TextColor, TextAnchor.UpperRight);
            y += 24f;
            UITheme.ShadowText(new Rect(w - 430f, y, 410f, 20f), "Hold " + UITheme.KeyFor(InputAction.CommandWheel) + " for orders   F1-F3 select   F4 all", 13, UITheme.Faint, TextAnchor.UpperRight);
        }

        static Color HealthColor(float fraction)
        {
            return fraction > 0.6f ? UITheme.Good : fraction > 0.3f ? UITheme.Warn : UITheme.Bad;
        }

        // ---- Player ----

        void DrawPlayer(GameManager game, PlayerController player)
        {
            float h = UITheme.Height;
            var rect = new Rect(18f, h - 150f, 420f, 132f);
            UITheme.Panel(rect);
            UIIcons.Portrait(new Rect(rect.x + 10f, rect.y + 12f, 80f, 96f), player.Officer, player.Loadout);
            float x = rect.x + 102f, cw = rect.width - 116f;
            UITheme.Text(new Rect(x, rect.y + 8f, cw, 22f), player.Officer.displayName, 16, UITheme.TextColor, TextAnchor.UpperLeft, true);
            string state = !player.IsAlive ? "DOWN" : player.Health.Bracing ? "Shield braced" : player.IsSprinting ? "Sprinting" : player.IsCrouched ? "Crouched"
                : player.IsSteadyAiming ? "Steady aim" : player.IsMoving ? "Moving" : "Ready";
            UITheme.Text(new Rect(x, rect.y + 28f, cw, 20f), UITheme.RoleName(player.Officer.role) + "  |  " + state + (player.FlashlightOn ? "  |  Light on" : ""), 13, UITheme.Dim);
            var health = player.Health;
            UITheme.Text(new Rect(x, rect.y + 52f, 70f, 18f), "HEALTH", 12, UITheme.Dim, TextAnchor.MiddleLeft, true);
            UITheme.Bar(new Rect(x + 72f, rect.y + 56f, cw - 120f, 10f), health.Fraction, HealthColor(health.Fraction));
            UITheme.Text(new Rect(x + cw - 44f, rect.y + 50f, 44f, 20f), Mathf.CeilToInt(health.Current).ToString(), 15, UITheme.TextColor, TextAnchor.MiddleRight, true);
            UITheme.Text(new Rect(x, rect.y + 74f, 70f, 18f), "ARMOR", 12, UITheme.Dim, TextAnchor.MiddleLeft, true);
            UITheme.Bar(new Rect(x + 72f, rect.y + 78f, cw - 120f, 8f), health.ArmorCondition, UITheme.Accent);
            UITheme.Text(new Rect(x + cw - 44f, rect.y + 72f, 44f, 20f), health.Armor != null ? Mathf.RoundToInt(health.ArmorCondition * 100f) + "%" : "-", 13, UITheme.Dim, TextAnchor.MiddleRight);
            UITheme.Text(new Rect(x, rect.y + 94f, 70f, 18f), "STAMINA", 12, UITheme.Dim, TextAnchor.MiddleLeft, true);
            UITheme.Bar(new Rect(x + 72f, rect.y + 99f, cw - 120f, 6f), player.StaminaFraction, new Color(0.75f, 0.8f, 0.9f));
            string ability = player.Officer.abilityName + " [" + UITheme.KeyFor(InputAction.Ability) + "]";
            if (player.Officer.role != OfficerRole.Shield)
                ability += player.AbilityReadyIn > 0f ? "  " + Mathf.CeilToInt(player.AbilityReadyIn) + "s" : "  ready";
            UITheme.Text(new Rect(rect.x + 10f, rect.y + 110f, rect.width - 20f, 20f), ability + (player.Weapons.Overcharged ? "  |  OVERCHARGED" : ""), 13, player.AbilityReadyIn > 0f ? UITheme.Faint : UITheme.Accent);
        }

        // ---- Weapon ----

        void DrawWeapon(PlayerController player)
        {
            float w = UITheme.Width, h = UITheme.Height;
            var weapons = player.Weapons;
            var weapon = weapons.Current;
            var rect = new Rect(w - 420f, h - 150f, 402f, 132f);
            UITheme.Panel(rect);
            UIIcons.Weapon(new Rect(rect.x + 12f, rect.y + 14f, 120f, 46f), weapon.Data, UITheme.TextColor);
            UITheme.Text(new Rect(rect.x + 144f, rect.y + 10f, 250f, 22f), weapon.Data.displayName, 16, UITheme.TextColor, TextAnchor.UpperLeft, true);
            string mode = weapon.Automatic ? "AUTO" : "SEMI";
            if (weapon.Data.lessLethal) mode += "  LESS-LETHAL";
            UITheme.Text(new Rect(rect.x + 144f, rect.y + 32f, 250f, 18f), mode + (weapons.IsReloading ? "   RELOADING" : weapons.IsSwitching ? "   SWITCHING" : ""), 13, weapons.IsReloading ? UITheme.Warn : UITheme.Dim);
            Color ammoColor = weapon.Magazine == 0 ? UITheme.Bad : weapon.Magazine <= weapon.Data.magazineSize / 4 ? UITheme.Warn : UITheme.TextColor;
            UITheme.Text(new Rect(rect.x + 144f, rect.y + 46f, 120f, 40f), weapon.Magazine.ToString(), 34, ammoColor, TextAnchor.UpperLeft, true);
            UITheme.Text(new Rect(rect.x + 220f, rect.y + 60f, 160f, 24f), "/ " + weapon.Reserve, 20, UITheme.Dim);
            if (weapons.IsReloading) UITheme.Bar(new Rect(rect.x + 144f, rect.y + 88f, 240f, 4f), weapons.ReloadProgress, UITheme.Warn);

            // Equipment strip.
            var inventory = weapons.Inventory;
            float x = rect.x + 12f;
            float y = rect.y + 96f;
            if (inventory.Equipment.Count == 0) UITheme.Text(new Rect(x, y, 300f, 28f), "No equipment", 13, UITheme.Faint, TextAnchor.MiddleLeft);
            for (int i = 0; i < inventory.Equipment.Count; i++)
            {
                var slot = inventory.Equipment[i];
                bool selected = slot == inventory.SelectedSlot;
                var box = new Rect(x + i * 54f, y, 50f, 30f);
                UITheme.Fill(box, selected ? new Color(UITheme.AccentDim.r, UITheme.AccentDim.g, UITheme.AccentDim.b, 0.9f) : new Color(0f, 0f, 0f, 0.35f));
                if (selected) UITheme.Frame(box, UITheme.Accent);
                UIIcons.Equipment(new Rect(box.x + 2f, box.y + 2f, 26f, 26f), slot.Data.kind, slot.Count > 0 ? UITheme.TextColor : UITheme.Faint);
                UITheme.Text(new Rect(box.x + 26f, box.y, 22f, 30f), slot.Data.consumable ? slot.Count.ToString() : "-", 14, slot.Count > 0 ? UITheme.TextColor : UITheme.Faint, TextAnchor.MiddleCenter, true);
            }
            var selectedSlot = inventory.SelectedSlot;
            if (selectedSlot != null)
                UITheme.ShadowText(new Rect(rect.x, rect.y - 24f, rect.width, 22f), selectedSlot.Data.displayName + "  [" + UITheme.KeyFor(InputAction.UseEquipment) + "] use   [" + UITheme.KeyFor(InputAction.Slot3) + "/" + UITheme.KeyFor(InputAction.Slot4) + "] select", 14, UITheme.Dim, TextAnchor.UpperRight);
        }

        // ---- Prompt and radio ----

        void DrawPrompt(GameManager game, PlayerController player)
        {
            float w = UITheme.Width, h = UITheme.Height;
            var interaction = player.Interaction;
            var current = interaction.Current;
            if (current != null)
            {
                string prompt = current.Prompt;
                if (!string.IsNullOrEmpty(prompt))
                {
                    prompt = prompt.Replace("[E]", "[" + UITheme.KeyFor(InputAction.Interact) + "]");
                    var rect = new Rect(w * 0.5f - 260f, h - 250f, 520f, 40f);
                    UITheme.Fill(rect, new Color(0f, 0f, 0f, 0.6f));
                    UITheme.Text(rect, prompt, 19, UITheme.TextColor, TextAnchor.MiddleCenter, true);
                    if (interaction.IsInteracting) UITheme.Bar(new Rect(rect.x, rect.yMax, rect.width, 5f), interaction.Progress, UITheme.Accent);
                }
            }
            // Extraction zone status when close.
            var extraction = game.Level.extraction;
            if (extraction != null && (extraction.Bounds.center - player.Position).sqrMagnitude < 144f)
            {
                bool active = false;
                foreach (var objective in MissionManager.Instance.Objectives)
                    if (objective.Type == ObjectiveType.ReachExtraction && objective.State == ObjectiveState.Active) active = true;
                Vector2 gui;
                if (UITheme.WorldToGui(game.CameraRig.Cam, extraction.Bounds.center + Vector3.up * 2f, out gui))
                    UITheme.ShadowText(new Rect(gui.x - 200f, gui.y - 12f, 400f, 24f), active ? "EXTRACTION POINT - step inside to finish" : "Extraction point (resolve primary objectives first)", 15, active ? UITheme.Accent : UITheme.Dim, TextAnchor.MiddleCenter, true);
            }
        }

        void DrawRadio()
        {
            float w = UITheme.Width, h = UITheme.Height;
            var radio = SquadCommandManager.Instance.RadioLog;
            float y = h - 300f;
            for (int i = radio.Count - 1; i >= 0; i--)
            {
                var line = radio[i];
                float age = Time.unscaledTime - line.time;
                if (age > 5f) continue;
                var color = UITheme.TextColor;
                color.a = Mathf.Clamp01(5f - age);
                UITheme.ShadowText(new Rect(w * 0.5f - 400f, y, 800f, 22f), "<color=#5c9ef2>" + line.speaker + ":</color> " + line.text, 16, color, TextAnchor.UpperCenter);
                y -= 22f;
            }
        }

        // ---- World-space labels ----

        void DrawWorldLabels(GameManager game)
        {
            var cam = game.CameraRig.Cam;
            foreach (var officer in SquadCommandManager.Instance.Squad)
            {
                Vector2 gui;
                if (!UITheme.WorldToGui(cam, officer.Position + Vector3.up * 2.3f, out gui)) continue;
                var color = !officer.IsAlive ? UITheme.Bad : officer.Selected ? UITheme.Accent : UITheme.Dim;
                UITheme.ShadowText(new Rect(gui.x - 90f, gui.y - 22f, 180f, 20f), officer.Data.callsign, 14, color, TextAnchor.MiddleCenter, true);
                UITheme.ShadowText(new Rect(gui.x - 110f, gui.y - 4f, 220f, 18f), officer.Status, 12, color, TextAnchor.MiddleCenter);
                // Waypoints for move orders.
                Vector3 previous = officer.Position;
                foreach (var point in officer.Waypoints)
                {
                    Vector2 a, b;
                    if (UITheme.WorldToGui(cam, previous, out a) && UITheme.WorldToGui(cam, point, out b)) UITheme.LineTo(a, b, new Color(0.36f, 0.62f, 0.95f, 0.5f), 2f);
                    if (UITheme.WorldToGui(cam, point, out b)) UITheme.Dot(b, 5f, UITheme.Accent);
                    previous = point;
                }
                if (officer.StackDoor != null && UITheme.WorldToGui(cam, officer.StackDoor.transform.position + Vector3.up * 2.6f, out gui))
                    UITheme.ShadowText(new Rect(gui.x - 60f, gui.y - 10f, 120f, 20f), "STACK", 13, UITheme.Accent, TextAnchor.MiddleCenter, true);
            }
            foreach (var marker in TacticalIntel.Instance.Markers)
            {
                Vector2 gui;
                if (UITheme.WorldToGui(cam, marker + Vector3.up * 0.2f, out gui))
                {
                    UITheme.Ring(gui, 10f, UITheme.Warn, 2f);
                    UITheme.Dot(gui, 3f, UITheme.Warn);
                }
            }
        }

        void DrawHitIndicator(GameManager game, PlayerController player)
        {
            float amount = player.Health.HitIndicator;
            if (amount <= 0f) return;
            Vector3 from = player.Health.LastHitFrom - player.Position;
            from.y = 0f;
            if (from.sqrMagnitude < 0.01f) return;
            // Screen up is world +z for this camera.
            Vector2 dir = new Vector2(from.x, -from.z).normalized;
            Vector2 center = new Vector2(UITheme.Width * 0.5f, UITheme.Height * 0.5f) + dir * 130f;
            UITheme.Dot(center, 14f, new Color(1f, 0.25f, 0.2f, amount * 0.8f));
        }

        // ---- Crosshair ----

        public void DrawCrosshair(GameManager game)
        {
            var player = game.Player;
            if (player == null || !player.IsAlive) return;
            Vector2 mouse = GameInput.MousePosition;
            float s = UITheme.Scale;
            Vector2 c = new Vector2(mouse.x / s, (Screen.height - mouse.y) / s);
            var weapons = player.Weapons;
            float gap = 6f + weapons.Spread * 3f;
            bool hit = Time.time - weapons.LastHitTime < 0.15f;
            Color color = hit ? UITheme.Bad : weapons.Current.Magazine == 0 ? UITheme.Warn : Color.white;
            float len = 9f, t = 2f;
            var shadow = new Color(0f, 0f, 0f, 0.6f);
            DrawCross(c + new Vector2(1f, 1f), gap, len, t, shadow);
            DrawCross(c, gap, len, t, color);
            UITheme.Dot(c, 1.5f, color);
            if (weapons.IsReloading)
            {
                float p = weapons.ReloadProgress;
                int dots = 16;
                for (int i = 0; i < dots; i++)
                {
                    float a = -Mathf.PI * 0.5f + i * Mathf.PI * 2f / dots;
                    var col = i / (float)dots <= p ? UITheme.Warn : new Color(1f, 1f, 1f, 0.2f);
                    UITheme.Dot(c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (gap + len + 8f), 2f, col);
                }
            }
        }

        static void DrawCross(Vector2 c, float gap, float len, float t, Color color)
        {
            UITheme.Fill(new Rect(c.x - gap - len, c.y - t * 0.5f, len, t), color);
            UITheme.Fill(new Rect(c.x + gap, c.y - t * 0.5f, len, t), color);
            UITheme.Fill(new Rect(c.x - t * 0.5f, c.y - gap - len, t, len), color);
            UITheme.Fill(new Rect(c.x - t * 0.5f, c.y + gap, t, len), color);
        }
    }
}
