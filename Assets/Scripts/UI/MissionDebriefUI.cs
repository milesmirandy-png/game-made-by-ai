using System.Collections.Generic;
using UnityEngine;

namespace Swat
{
    // Debriefing: result, rating and score breakdown, objectives, civilians,
    // suspects, squad status, the leader's health, equipment used, unlocks,
    // and Replay / Headquarters / Main menu.
    public class MissionDebriefUI
    {
        MissionResult shown;
        float openedAt;
        const float IntroTime = 1.3f;

        public void Draw(GameManager game)
        {
            var result = game.LastResult;
            if (result == null) return;
            float w = UITheme.Width, h = UITheme.Height;
            if (result != shown)
            {
                shown = result;
                openedAt = Time.unscaledTime;
            }
            // A short intro: the result is stamped in the middle of the screen, then the report fades in.
            float t = Time.unscaledTime - openedAt;
            if (t < IntroTime - 0.35f && t > 0.2f && (GameInput.LeftClick || GameInput.Confirm || GameInput.KeyDown(KeyCode.Space)))
            {
                // Skip to the quick fade-in; buttons stay disabled until it finishes so the same click can't press one.
                openedAt = Time.unscaledTime - (IntroTime - 0.35f);
                t = IntroTime - 0.35f;
            }
            UITheme.Fill(new Rect(0f, 0f, w, h), new Color(0.015f, 0.02f, 0.04f, 0.93f * Mathf.Clamp01(t / 0.3f + 0.4f)));

            string title = result.mission.isTraining ? (result.success ? "Training Complete" : "Training Incomplete") : (result.success ? "Mission Complete" : "Mission Failed");
            Color titleColor = result.success ? UITheme.Good : UITheme.Bad;
            float reveal = Mathf.Clamp01((t - IntroTime + 0.4f) / 0.4f);
            if (reveal < 1f)
            {
                float stamp = Mathf.Clamp01(t / 0.25f) * (1f - reveal);
                float line = Mathf.Clamp01(t / 0.6f) * 520f;
                UITheme.Fill(new Rect(w * 0.5f - line, h * 0.42f + 52f, line * 2f, 2f), new Color(titleColor.r, titleColor.g, titleColor.b, stamp));
                UITheme.ShadowText(new Rect(0f, h * 0.42f - 30f, w, 80f), title.ToUpperInvariant(), 60, new Color(titleColor.r, titleColor.g, titleColor.b, stamp), TextAnchor.MiddleCenter, true);
                UITheme.Text(new Rect(0f, h * 0.42f + 62f, w, 28f), result.mission.displayName, 20, new Color(1f, 1f, 1f, 0.8f * stamp), TextAnchor.UpperCenter);
            }
            UITheme.Alpha = reveal;
            bool wasEnabled = GUI.enabled;
            GUI.enabled = wasEnabled && reveal >= 1f;
            UITheme.Text(new Rect(60f, 34f, w - 400f, 50f), title.ToUpperInvariant(), 40, titleColor, TextAnchor.UpperLeft, true);
            string sub = result.mission.displayName + "  -  " + result.mission.location + "   |   Time " + MissionScoring.FormatTime(result.time) + "   |   " + OfficerSelectionManager.DifficultyNames[result.plan.difficulty] + "   |   Seed " + result.plan.seed;
            UITheme.Text(new Rect(62f, 86f, w - 400f, 24f), sub, 17, UITheme.Dim);
            if (!result.success && !string.IsNullOrEmpty(result.reason)) UITheme.Text(new Rect(62f, 112f, w - 400f, 24f), result.reason, 17, UITheme.Warn);

            // Rating and score.
            var scoreRect = new Rect(w - 330f, 30f, 270f, 120f);
            UITheme.Panel(scoreRect);
            UITheme.Text(new Rect(scoreRect.x + 16f, scoreRect.y + 6f, 100f, 100f), result.rating, 80, RatingColor(result.rating), TextAnchor.MiddleLeft, true);
            UITheme.Text(new Rect(scoreRect.x + 110f, scoreRect.y + 22f, 150f, 40f), result.total.ToString(), 36, UITheme.TextColor, TextAnchor.UpperRight, true);
            UITheme.Text(new Rect(scoreRect.x + 110f, scoreRect.y + 66f, 150f, 22f), result.newBest ? "NEW BEST" : "Best " + Mathf.Max(result.previousBest, result.total), 15, result.newBest ? UITheme.Warn : UITheme.Dim, TextAnchor.UpperRight, true);

            float colW = (w - 160f) / 3f;
            float top = 160f, colH = h - 270f;
            DrawObjectives(new Rect(60f, top, colW, colH), result);
            DrawStats(new Rect(80f + colW, top, colW, colH), result);
            DrawScore(new Rect(100f + colW * 2f, top, colW, colH), result);

            float by = h - 90f;
            if (UITheme.Button(new Rect(60f, by, 240f, 50f), "Replay (same seed)", true, false, 18)) game.RestartMission();
            if (UITheme.Button(new Rect(310f, by, 240f, 50f), "Replay (new seed)", result.mission.seed == 0, false, 18))
            {
                OfficerSelectionManager.NewSeed();
                game.RollPlan();
                game.Deploy();
            }
            if (result.mission.isCustom)
            {
                if (UITheme.Button(new Rect(w - 560f, by, 240f, 50f), "Level creator", true, true, 19)) game.OpenLevelEditor();
            }
            else if (UITheme.Button(new Rect(w - 560f, by, 240f, 50f), "Level select", true, true, 19)) game.GoToHeadquarters();
            if (UITheme.Button(new Rect(w - 300f, by, 240f, 50f), "Main menu", true, false, 18)) game.GoToMainMenu();
            GUI.enabled = wasEnabled;
            UITheme.Alpha = 1f;
        }

        static Color RatingColor(string rating)
        {
            switch (rating)
            {
                case "S": return new Color(1f, 0.85f, 0.35f);
                case "A": return UITheme.Good;
                case "B": return UITheme.Accent;
                case "C": return UITheme.TextColor;
                case "D": return UITheme.Warn;
                default: return UITheme.Bad;
            }
        }

        static void DrawObjectives(Rect rect, MissionResult result)
        {
            UITheme.Panel(rect);
            float x = rect.x + 20f, cw = rect.width - 40f, y = rect.y + 16f;
            UITheme.Text(new Rect(x, y, cw, 22f), "PRIMARY OBJECTIVES", 15, UITheme.Accent, TextAnchor.UpperLeft, true);
            y += 28f;
            foreach (var objective in result.objectives)
                if (!objective.Optional) Row(ref y, x, cw, objective);
            y += 12f;
            UITheme.Text(new Rect(x, y, cw, 22f), "OPTIONAL OBJECTIVES", 15, UITheme.Accent, TextAnchor.UpperLeft, true);
            y += 28f;
            bool any = false;
            foreach (var objective in result.objectives)
                if (objective.Optional) { Row(ref y, x, cw, objective); any = true; }
            if (!any) UITheme.Text(new Rect(x, y, cw, 22f), "None this deployment", 15, UITheme.Faint);
            y += 30f;
            if (result.unlocks.Count > 0)
            {
                UITheme.Text(new Rect(x, y, cw, 22f), "UNLOCKED", 15, UITheme.Warn, TextAnchor.UpperLeft, true);
                y += 26f;
                foreach (var unlock in result.unlocks)
                {
                    UITheme.Text(new Rect(x + 8f, y, cw - 8f, 22f), unlock, 15, UITheme.TextColor);
                    y += 22f;
                }
            }
        }

        static void Row(ref float y, float x, float w, Objective objective)
        {
            bool done = objective.State == ObjectiveState.Completed;
            string mark = done ? "[x]" : "[-]";
            UITheme.Text(new Rect(x, y, w - 60f, 40f), mark + "  " + objective.Label, 15, done ? UITheme.Good : UITheme.Bad);
            UITheme.Text(new Rect(x + w - 60f, y, 60f, 22f), done ? "+" + objective.Points : "0", 15, done ? UITheme.TextColor : UITheme.Faint, TextAnchor.UpperRight);
            y += Mathf.Max(24f, UITheme.TextHeight(objective.Label, 15, w - 60f) + 4f);
        }

        static void DrawStats(Rect rect, MissionResult result)
        {
            UITheme.Panel(rect);
            var s = result.stats;
            var c = s.civilians;
            float x = rect.x + 20f, cw = rect.width - 40f, y = rect.y + 16f;
            Section(ref y, x, cw, "CIVILIANS");
            Line(ref y, x, cw, "Encountered", c.encountered + " / " + c.total);
            Line(ref y, x, cw, "Rescued (under police control)", c.rescued.ToString());
            Line(ref y, x, cw, "Safely evacuated", c.evacuated.ToString());
            Line(ref y, x, cw, "Injured", c.injured.ToString(), c.injured > 0);
            Line(ref y, x, cw, "Casualties", c.killed.ToString(), c.killed > 0);
            y += 8f;
            Section(ref y, x, cw, "SUSPECTS");
            Line(ref y, x, cw, "Arrested", s.suspectsArrested + " / " + s.suspectsTotal);
            Line(ref y, x, cw, "Neutralized", s.suspectsKilled.ToString());
            if (s.suspectsEscaped > 0) Line(ref y, x, cw, "Escaped", s.suspectsEscaped.ToString(), true);
            Line(ref y, x, cw, "Unauthorized use of force", s.unauthorizedForce.ToString(), s.unauthorizedForce > 0);
            y += 8f;
            Section(ref y, x, cw, "TEAM");
            Line(ref y, x, cw, "Team leader health", Mathf.RoundToInt(result.playerHealth * 100f) + "%", result.playerHealth <= 0f);
            foreach (var line in result.squad)
            {
                UITheme.Text(new Rect(x, y, cw, 22f), line, 14, line.Contains("DOWN") ? UITheme.Bad : UITheme.Dim);
                y += 22f;
            }
            if (result.squad.Count == 0) { UITheme.Text(new Rect(x, y, cw, 22f), "Deployed solo", 14, UITheme.Dim); y += 22f; }
            float accuracy = s.shotsFired > 0 ? s.shotsHit / (float)s.shotsFired : 0f;
            Line(ref y, x, cw, "Shots fired / hit", s.shotsFired + " / " + s.shotsHit + (s.shotsFired > 0 ? "  (" + Mathf.RoundToInt(accuracy * 100f) + "%)" : ""));
            Line(ref y, x, cw, "Doors breached", s.doorsBreached.ToString());
            Line(ref y, x, cw, "Squad orders given", s.ordersGiven.ToString());
            y += 8f;
            Section(ref y, x, cw, "EQUIPMENT USED");
            if (s.equipmentUsed.Count == 0) { UITheme.Text(new Rect(x, y, cw, 22f), "None", 14, UITheme.Faint); y += 22f; }
            var keys = new List<EquipmentKind>(s.equipmentUsed.Keys);
            keys.Sort();
            foreach (var kind in keys) Line(ref y, x, cw, Name(kind), s.equipmentUsed[kind].ToString());
            if (s.alarmTriggered) Line(ref y, x, cw, "Alarm", "triggered", true);
            if (s.evidenceSecured > 0) Line(ref y, x, cw, "Evidence secured", s.evidenceSecured.ToString());
        }

        static string Name(EquipmentKind kind)
        {
            foreach (var item in GameData.AllEquipment) if (item.kind == kind) return item.displayName;
            return kind.ToString();
        }

        static void DrawScore(Rect rect, MissionResult result)
        {
            UITheme.Panel(rect);
            float x = rect.x + 20f, cw = rect.width - 40f, y = rect.y + 16f;
            Section(ref y, x, cw, "SCORE BREAKDOWN");
            foreach (var line in result.lines)
            {
                Color color = line.points < 0 ? UITheme.Bad : line.points == 0 ? UITheme.Faint : UITheme.TextColor;
                UITheme.Text(new Rect(x, y, cw - 70f, 22f), line.label, 15, UITheme.Dim);
                UITheme.Text(new Rect(x + cw - 70f, y, 70f, 22f), (line.points > 0 ? "+" : "") + line.points, 15, color, TextAnchor.UpperRight, true);
                y += 24f;
            }
            UITheme.Fill(new Rect(x, y + 4f, cw, 1f), UITheme.Line);
            y += 12f;
            UITheme.Text(new Rect(x, y, cw - 80f, 26f), "TOTAL", 18, UITheme.TextColor, TextAnchor.UpperLeft, true);
            UITheme.Text(new Rect(x + cw - 100f, y, 100f, 26f), result.total.ToString(), 20, UITheme.TextColor, TextAnchor.UpperRight, true);
            y += 40f;
            UITheme.Text(new Rect(x, y, cw, 80f), "Arrests are worth more than force. Civilian safety and lawful use of force matter most. Ratings: S, A, B, C, D (F = failed).", 13, UITheme.Faint);
        }

        static void Section(ref float y, float x, float w, string title)
        {
            UITheme.Text(new Rect(x, y, w, 22f), title, 15, UITheme.Accent, TextAnchor.UpperLeft, true);
            y += 26f;
        }

        static void Line(ref float y, float x, float w, string label, string value, bool bad = false)
        {
            UITheme.Text(new Rect(x, y, w - 120f, 22f), label, 15, UITheme.Dim);
            UITheme.Text(new Rect(x + w - 120f, y, 120f, 22f), value, 15, bad ? UITheme.Bad : UITheme.TextColor, TextAnchor.UpperRight, true);
            y += 22f;
        }
    }
}
