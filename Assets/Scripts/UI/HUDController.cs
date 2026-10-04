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
        readonly TacticalMapUI minimap = new TacticalMapUI();
        // The health bar keeps a pale "lost health" segment for a moment after damage.
        float healthGhost = -1f, ghostHoldUntil;

        public void Draw(GameManager game)
        {
            var player = game.Player;
            if (player == null) return;
            float w = UITheme.Width, h = UITheme.Height;
            var settings = SaveManager.Settings;

            // Screen effects (kept mild: no gore, just tints; "reduce flashes" softens them further).
            bool reduce = settings.reduceFlashes;
            if (player.Health.Blind > 0f) UITheme.Fill(new Rect(0f, 0f, w, h), new Color(reduce ? 0.85f : 1f, reduce ? 0.87f : 1f, reduce ? 0.9f : 1f, player.Health.Blind * (reduce ? 0.6f : 0.92f)));
            if (player.Health.DamageFlash > 0f) DrawDamageEdges(player.Health.DamageFlash * (reduce ? 0.45f : 1f));
            if (!player.IsAlive) UITheme.Fill(new Rect(0f, 0f, w, h), new Color(0.2f, 0f, 0f, VersusMatch.Active ? 0.25f : 0.4f));
            DrawHitIndicator(game, player);
            DrawWorldLabels(game);

            bool versus = VersusMatch.Active;
            if (!versus) DrawObjectives(game);
            float y = 30f;
            if (settings.minimap && !game.MapOpen && !game.PlanningMode)
            {
                float size = 220f * Mathf.Clamp(settings.minimapScale, 0.75f, 1.5f);
                var rect = new Rect(w - size - 20f, 18f, size, size);
                minimap.DrawMinimap(game, rect, settings.minimapOpacity);
                y = rect.yMax + 12f;
            }
            if (versus) VersusHUD.Draw(game, VersusMatch.Instance, Mathf.Max(110f, y));
            else
            {
                y = DrawStatusTopRight(game, y);
                DrawSquad(game, Mathf.Max(150f, y + 8f));
            }
            DrawPlayer(game, player);
            DrawWeapon(player);
            DrawPrompt(game, player);
            DrawRadio();
        }

        // Damage shows as a soft red tint creeping in from the screen edges rather than a full-screen flash.
        static void DrawDamageEdges(float amount)
        {
            float w = UITheme.Width, h = UITheme.Height;
            var color = new Color(0.75f, 0.05f, 0.03f, Mathf.Clamp01(amount) * 0.55f);
            float edge = Mathf.Min(w, h) * 0.16f;
            // Gradient shading is opaque at the top of the rect when "upward" is true.
            UITheme.Shade(new Rect(0f, 0f, w, edge), color, true);
            UITheme.Shade(new Rect(0f, h - edge, w, edge), color, false);
            if (Event.current.type != EventType.Repaint) return;
            // Left and right edges: the same gradient turned a quarter turn.
            var matrix = GUI.matrix;
            GUI.matrix = matrix * Matrix4x4.TRS(new Vector3(0f, h, 0f), Quaternion.Euler(0f, 0f, -90f), Vector3.one);
            UITheme.Shade(new Rect(0f, 0f, h, edge), color, true);
            GUI.matrix = matrix * Matrix4x4.TRS(new Vector3(w, 0f, 0f), Quaternion.Euler(0f, 0f, 90f), Vector3.one);
            UITheme.Shade(new Rect(0f, 0f, h, edge), color, true);
            GUI.matrix = matrix;
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

        float DrawStatusTopRight(GameManager game, float y)
        {
            float w = UITheme.Width;
            var alarm = game.Level.alarm;
            if (alarm != null && alarm.State == AlarmState.Triggered)
            {
                // Steady red instead of blinking when flashes are reduced.
                bool flash = !SaveManager.Settings.reduceFlashes && Mathf.Repeat(Time.unscaledTime * 2f, 1f) < 0.5f;
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
            {
                UITheme.ShadowText(new Rect(w - 300f, y, 280f, 24f), "Power out", 15, UITheme.Warn, TextAnchor.UpperRight);
                y += 24f;
            }
            return y;
        }

        // ---- Squad ----

        void DrawSquad(GameManager game, float y)
        {
            var squad = SquadCommandManager.Instance;
            var officers = squad.Squad;
            float w = UITheme.Width;
            if (officers.Count == 0) return;
            UITheme.ShadowText(new Rect(w - 330f, y, 310f, 22f), "SQUAD  -  orders to: " + squad.SelectionLabel, 14, UITheme.Accent, TextAnchor.UpperRight, true);
            y += 26f;
            for (int i = 0; i < officers.Count; i++)
            {
                var officer = officers[i];
                var id = UITheme.RoleColor(officer.Data.role);
                var rect = new Rect(w - 330f, y, 310f, 62f);
                UITheme.Fill(rect, officer.Selected ? new Color(UITheme.AccentDim.r, UITheme.AccentDim.g, UITheme.AccentDim.b, 0.85f) : UITheme.PanelColor);
                UITheme.Fill(new Rect(rect.x, rect.y, 3f, rect.height), officer.Selected ? UITheme.Accent : id);
                // Squad number in the officer's ID colour (matches the armband and marker in the world).
                var badge = new Rect(rect.x + 10f, rect.y + 8f, 30f, 30f);
                UITheme.Fill(badge, officer.IsAlive ? id : UITheme.Faint);
                UITheme.Text(badge, (i + 1).ToString(), 18, new Color(0.03f, 0.04f, 0.06f), TextAnchor.MiddleCenter, true);
                UITheme.Text(new Rect(rect.x + 48f, rect.y + 6f, 170f, 22f), officer.Data.callsign + "  <color=#7c8798>F" + (i + 1) + "</color>", 16, UITheme.TextColor, TextAnchor.UpperLeft, true);
                UIIcons.Order(new Rect(rect.x + 48f, rect.y + 27f, 18f, 18f), officer.Order, DoorAction.None, officer.IsAlive ? UITheme.Accent : UITheme.Faint);
                UITheme.Text(new Rect(rect.x + 70f, rect.y + 26f, 230f, 20f), officer.Status, 14, officer.IsAlive ? UITheme.Dim : UITheme.Bad);
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
            string state = !player.IsAlive ? "DOWN" : player.Health.Bracing ? "Shield braced" : player.IsSliding ? "Sliding"
                : player.Peeking ? (player.Lean < -0.1f ? "Peeking left" : player.Lean > 0.1f ? "Peeking right" : "Peeking") : player.IsSprinting ? "Sprinting" : player.IsCrouched ? "Crouched"
                : player.IsSteadyAiming ? "Steady aim" : player.IsMoving ? "Moving" : "Ready";
            UITheme.Text(new Rect(x, rect.y + 28f, cw, 20f), UITheme.RoleName(player.Officer.role) + "  |  " + state + (player.FlashlightOn ? "  |  Light on" : ""), 13, UITheme.Dim);
            var health = player.Health;
            UITheme.Text(new Rect(x, rect.y + 52f, 70f, 18f), "HEALTH", 12, UITheme.Dim, TextAnchor.MiddleLeft, true);
            var healthRect = new Rect(x + 72f, rect.y + 56f, cw - 120f, 10f);
            if (healthGhost < health.Fraction || healthGhost < 0f) healthGhost = health.Fraction;
            else if (healthGhost > health.Fraction && Time.unscaledTime > ghostHoldUntil) healthGhost = Mathf.MoveTowards(healthGhost, health.Fraction, Time.unscaledDeltaTime * 0.5f);
            if (player.Health.DamageFlash > 0.95f) ghostHoldUntil = Time.unscaledTime + 0.5f;
            UITheme.Bar(healthRect, health.Fraction, HealthColor(health.Fraction));
            if (healthGhost > health.Fraction + 0.002f)
                UITheme.Fill(new Rect(healthRect.x + healthRect.width * health.Fraction, healthRect.y, healthRect.width * (healthGhost - health.Fraction), healthRect.height), new Color(1f, 0.92f, 0.85f, 0.75f));
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
            UIIcons.Weapon(new Rect(rect.x + 8f, rect.y + 10f, 128f, 56f), weapon.Data, UITheme.TextColor, weapons.Inventory.CurrentIndex == 0 ? player.Loadout : null);
            UITheme.Text(new Rect(rect.x + 144f, rect.y + 10f, 250f, 22f), weapon.Data.displayName, 16, UITheme.TextColor, TextAnchor.UpperLeft, true);
            string mode = weapon.ModeName;
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
                    var door = current as DoorController;
                    DrawPromptBadge(new Vector2(w * 0.5f, h - 250f), prompt, door != null ? door.StatusText : null, interaction.IsInteracting ? interaction.Progress : -1f);
                }
            }
            else
            {
                // Doors that can't be used (sealed shut) still explain themselves, without a key prompt.
                var door = InspectDoor(game, player);
                if (door != null) UITheme.ShadowText(new Rect(w * 0.5f - 300f, h - 246f, 600f, 24f), door.StatusText, 16, UITheme.Dim, TextAnchor.MiddleCenter);
            }
            // Extraction zone status when close.
            var extraction = game.Level.extraction;
            if (extraction != null && !VersusMatch.Active && (extraction.Bounds.center - player.Position).sqrMagnitude < 144f)
            {
                bool active = false;
                foreach (var objective in MissionManager.Instance.Objectives)
                    if (objective.Type == ObjectiveType.ReachExtraction && objective.State == ObjectiveState.Active) active = true;
                Vector2 gui;
                if (UITheme.WorldToGui(game.CameraRig.Cam, extraction.Bounds.center + Vector3.up * 2f, out gui))
                    UITheme.ShadowText(new Rect(gui.x - 200f, gui.y - 12f, 400f, 24f), active ? "EXTRACTION POINT - step inside to finish" : "Extraction point (resolve primary objectives first)", 15, active ? UITheme.Accent : UITheme.Dim, TextAnchor.MiddleCenter, true);
            }
        }

        // One consistent prompt: a key cap, the action, a HOLD tag for timed actions,
        // an optional status line and the hold progress bar.
        static void DrawPromptBadge(Vector2 anchor, string prompt, string status, float progress)
        {
            string text = prompt;
            bool hold = false;
            if (text.StartsWith("[E]")) text = text.Substring(3).Trim();
            if (text.EndsWith("(hold)"))
            {
                hold = true;
                text = text.Substring(0, text.Length - 6).Trim();
            }
            string key = GameInput.PromptKey(InputAction.Interact);
            const int size = 19;
            float keyWidth = Mathf.Max(30f, UITheme.TextWidth(key, 16, true) + 14f);
            float textWidth = UITheme.TextWidth(text, size, true);
            float holdWidth = hold ? 52f : 0f;
            float width = Mathf.Max(240f, keyWidth + 12f + textWidth + holdWidth + 32f);
            bool hasStatus = !string.IsNullOrEmpty(status);
            var rect = new Rect(anchor.x - width * 0.5f, anchor.y, width, hasStatus ? 64f : 44f);
            UITheme.Fill(rect, new Color(0.02f, 0.03f, 0.05f, 0.78f));
            UITheme.Fill(new Rect(rect.x, rect.y, 3f, rect.height), UITheme.Accent);
            float x = rect.x + 16f;
            UITheme.KeyCap(new Vector2(x, rect.y + 7f), key, 30f);
            x += keyWidth + 12f;
            UITheme.Text(new Rect(x, rect.y, textWidth + 4f, 44f), text, size, UITheme.TextColor, TextAnchor.MiddleLeft, true);
            if (hold) UITheme.Text(new Rect(rect.xMax - holdWidth - 12f, rect.y, holdWidth, 44f), "HOLD", 12, UITheme.Accent, TextAnchor.MiddleRight, true);
            if (hasStatus) UITheme.Text(new Rect(rect.x + 16f, rect.y + 40f, rect.width - 28f, 20f), status, 13, UITheme.Dim, TextAnchor.UpperLeft);
            if (progress >= 0f) UITheme.Bar(new Rect(rect.x, rect.yMax, rect.width, 5f), progress, UITheme.Accent);
        }

        static DoorController InspectDoor(GameManager game, PlayerController player)
        {
            DoorController best = null;
            float bestDistance = 2.2f * 2.2f;
            foreach (var door in game.Level.doors)
            {
                if (door == null || door.State != DoorState.Disabled) continue;
                Vector3 to = door.transform.position - player.Position;
                to.y = 0f;
                float d = to.sqrMagnitude;
                if (d >= bestDistance || Vector3.Dot(to, player.AimDirection) < 0f) continue;
                best = door;
                bestDistance = d;
            }
            return best != null && !string.IsNullOrEmpty(best.StatusText) ? best : null;
        }

        // Radio chatter as subtitles (toggle and size in Accessibility settings).
        void DrawRadio()
        {
            var settings = SaveManager.Settings;
            if (!settings.subtitles) return;
            float w = UITheme.Width, h = UITheme.Height;
            var radio = SquadCommandManager.Instance.RadioLog;
            int size = Mathf.RoundToInt(16f * Mathf.Clamp(settings.subtitleSize, 0.8f, 1.6f));
            float row = size + 10f;
            float y = h - 300f;
            int shown = 0;
            for (int i = radio.Count - 1; i >= 0 && shown < 4; i--)
            {
                var line = radio[i];
                float age = Time.unscaledTime - line.time;
                if (age > 5f || age < 0f) continue;
                shown++;
                float alpha = Mathf.Clamp01(5f - age) * Mathf.Clamp01(age / 0.15f + 0.2f);
                string text = "<color=#5c9ef2>" + line.speaker + ":</color> " + line.text;
                float width = Mathf.Min(900f, UITheme.TextWidth(line.speaker + ": " + line.text, size) + 24f);
                UITheme.Fill(new Rect(w * 0.5f - width * 0.5f, y - 3f, width, row - 2f), new Color(0f, 0f, 0f, 0.45f * alpha));
                var color = UITheme.TextColor;
                color.a = alpha;
                UITheme.Text(new Rect(w * 0.5f - 450f, y, 900f, row), text, size, color, TextAnchor.UpperCenter);
                y -= row;
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
                int number = SquadCommandManager.Instance.Squad.IndexOf(officer) + 1;
                float nameWidth = UITheme.TextWidth(officer.Data.callsign, 14, true);
                var chip = new Rect(gui.x - nameWidth * 0.5f - 22f, gui.y - 21f, 18f, 18f);
                UITheme.Fill(chip, officer.IsAlive ? UITheme.RoleColor(officer.Data.role) : UITheme.Faint);
                UITheme.Text(chip, number.ToString(), 13, new Color(0.03f, 0.04f, 0.06f), TextAnchor.MiddleCenter, true);
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
            // Order confirmation: a ring that expands and fades where the order points.
            var squadManager = SquadCommandManager.Instance;
            float orderAge = Time.unscaledTime - squadManager.LastOrderTime;
            if (squadManager.LastOrderHasPoint && orderAge < 1.1f)
            {
                Vector2 ping;
                if (UITheme.WorldToGui(cam, squadManager.LastOrderPoint + Vector3.up * 0.1f, out ping))
                {
                    float k = orderAge / 1.1f;
                    UITheme.Ring(ping, Mathf.Lerp(8f, 30f, k), new Color(UITheme.Accent.r, UITheme.Accent.g, UITheme.Accent.b, 1f - k), 2.5f);
                    UITheme.Dot(ping, 4f, new Color(UITheme.Accent.r, UITheme.Accent.g, UITheme.Accent.b, 1f - k));
                }
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
            Vector2 side = new Vector2(-dir.y, dir.x);
            // A chevron around the screen centre pointing toward the shooter.
            Vector2 tip = new Vector2(UITheme.Width * 0.5f, UITheme.Height * 0.5f) + dir * 150f;
            var color = new Color(1f, 0.28f, 0.2f, amount * 0.85f);
            UITheme.LineTo(tip - dir * 18f + side * 22f, tip, color, 5f);
            UITheme.LineTo(tip - dir * 18f - side * 22f, tip, color, 5f);
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
            float hitAge = Time.time - weapons.LastHitTime;
            float killAge = Time.time - weapons.LastKillTime;
            DrawCrosshairShape(c, weapons.Spread, hitAge <= HitMarkerTime ? hitAge : -1f, weapons.Current.Magazine == 0, killAge <= KillMarkerTime ? killAge : -1f);
            if (weapons.IsReloading)
            {
                float size = Mathf.Clamp(SaveManager.Settings.crosshairSize, 0.5f, 2f);
                float radius = (6f + weapons.Spread * 3f) * Mathf.Lerp(1f, size, 0.5f) + 9f * size + 8f;
                float p = weapons.ReloadProgress;
                int dots = 16;
                for (int i = 0; i < dots; i++)
                {
                    float a = -Mathf.PI * 0.5f + i * Mathf.PI * 2f / dots;
                    var col = i / (float)dots <= p ? UITheme.Warn : new Color(1f, 1f, 1f, 0.2f);
                    UITheme.Dot(c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius, 2f, col);
                }
            }
        }

        const float HitMarkerTime = 0.18f, KillMarkerTime = 0.4f;

        // The crosshair itself, using the size, opacity and colour settings. hitAge < 0 means no
        // recent hit; the hit marker only shows when hit confirmation is enabled.
        public static void DrawCrosshairShape(Vector2 c, float spread, float hitAge, bool empty, float killAge = -1f)
        {
            var settings = SaveManager.Settings;
            float size = Mathf.Clamp(settings.crosshairSize, 0.5f, 2f);
            float opacity = Mathf.Clamp(settings.crosshairOpacity, 0.2f, 1f);
            float gap = (6f + spread * 3f) * Mathf.Lerp(1f, size, 0.5f);
            Color color = empty ? UITheme.Warn : UITheme.CrosshairColors[Mathf.Clamp(settings.crosshairColor, 0, UITheme.CrosshairColors.Length - 1)];
            color.a = opacity;
            float len = 9f * size, t = Mathf.Max(1.5f, 2f * Mathf.Sqrt(size));
            var shadow = new Color(0f, 0f, 0f, 0.6f * opacity);
            DrawCross(c + new Vector2(1f, 1f), gap, len, t, shadow);
            DrawCross(c, gap, len, t, color);
            UITheme.Dot(c, 1.5f * size, color);
            if (killAge >= 0f)
            {
                // Takedown: a bigger red X that pops out and fades (shown even with hit markers off).
                float k = killAge / KillMarkerTime;
                var red = new Color(1f, 0.25f, 0.2f, (1f - k) * opacity);
                float pop = 1f + (1f - k) * (1f - k) * 0.6f;
                foreach (var d in Diagonals) UITheme.LineTo(c + d * 7f * size * pop, c + d * 18f * size * pop, red, t + 1.5f);
                UITheme.Ring(c, Mathf.Lerp(10f, 30f, k) * size, new Color(1f, 0.25f, 0.2f, (1f - k) * 0.6f * opacity), 2f);
                return;
            }
            if (hitAge < 0f || !settings.hitMarker) return;
            // Hit confirmation: a small X that fades out quickly.
            float fresh = 1f - hitAge / HitMarkerTime;
            var marker = new Color(1f, 1f, 1f, fresh * opacity);
            float inner = 5f * size * (1f + fresh * 0.3f), outer = 12f * size * (1f + fresh * 0.3f);
            foreach (var d in Diagonals) UITheme.LineTo(c + d * inner, c + d * outer, marker, t);
        }

        static readonly Vector2[] Diagonals = { new Vector2(0.707f, 0.707f), new Vector2(-0.707f, 0.707f), new Vector2(0.707f, -0.707f), new Vector2(-0.707f, -0.707f) };

        static void DrawCross(Vector2 c, float gap, float len, float t, Color color)
        {
            UITheme.Fill(new Rect(c.x - gap - len, c.y - t * 0.5f, len, t), color);
            UITheme.Fill(new Rect(c.x + gap, c.y - t * 0.5f, len, t), color);
            UITheme.Fill(new Rect(c.x - t * 0.5f, c.y - gap - len, t, len), color);
            UITheme.Fill(new Rect(c.x - t * 0.5f, c.y + gap, t, len), color);
        }
    }
}
