using UnityEngine;

namespace Swat
{
    // Mission briefing: situation, rolled intel for this seed, objectives,
    // recommendations, difficulty and the random seed (reroll or keep).
    public class BriefingUI
    {
        public void Draw(GameManager game)
        {
            float w = UITheme.Width, h = UITheme.Height;
            UITheme.Fill(new Rect(0f, 0f, w, h), new Color(0.02f, 0.03f, 0.05f, 0.7f));
            var plan = game.Plan;
            var mission = OfficerSelectionManager.Mission;
            if (plan == null || mission == null)
            {
                game.GoToHeadquarters();
                return;
            }
            UITheme.Header(new Rect(60f, 40f, w - 120f, 60f), "Mission Briefing", mission.displayName + "  -  " + mission.location);

            // Left: situation and objectives.
            var left = new Rect(60f, 120f, w * 0.5f - 80f, h - 240f);
            UITheme.Panel(left);
            float x = left.x + 24f, cw = left.width - 48f, y = left.y + 20f;
            UITheme.Text(new Rect(x, y, cw, 22f), "SITUATION", 15, UITheme.Accent, TextAnchor.UpperLeft, true);
            y += 26f;
            float bh = UITheme.TextHeight(mission.briefing, 18, cw);
            UITheme.Text(new Rect(x, y, cw, bh), mission.briefing, 18, UITheme.TextColor);
            y += bh + 20f;
            UITheme.Text(new Rect(x, y, cw, 22f), "PRIMARY OBJECTIVES", 15, UITheme.Accent, TextAnchor.UpperLeft, true);
            y += 26f;
            foreach (var objective in mission.objectives)
            {
                UITheme.Text(new Rect(x + 8f, y, cw - 8f, 22f), "-  " + objective.description, 17, UITheme.TextColor);
                y += 24f;
            }
            if (plan.optional.Count > 0)
            {
                y += 10f;
                UITheme.Text(new Rect(x, y, cw, 22f), "OPTIONAL OBJECTIVES (this deployment)", 15, UITheme.Accent, TextAnchor.UpperLeft, true);
                y += 26f;
                foreach (var objective in plan.optional)
                {
                    UITheme.Text(new Rect(x + 8f, y, cw - 8f, 22f), "-  " + objective.description + "  (+" + objective.points + ")", 17, UITheme.Dim);
                    y += 24f;
                }
            }

            // Right: intel, recommendations, settings.
            var right = new Rect(w * 0.5f, 120f, w * 0.5f - 60f, h - 240f);
            UITheme.Panel(right);
            x = right.x + 24f;
            cw = right.width - 48f;
            y = right.y + 20f;
            UITheme.Text(new Rect(x, y, cw, 22f), "INTEL", 15, UITheme.Accent, TextAnchor.UpperLeft, true);
            y += 26f;
            foreach (var line in MissionBriefing.Intel(plan))
            {
                bool alert = line.StartsWith("POWER") || line.Contains("armed.");
                UITheme.Text(new Rect(x + 8f, y, cw - 8f, 22f), "-  " + line, 16, alert ? UITheme.Warn : UITheme.TextColor);
                y += 23f;
            }
            y += 12f;
            var tips = MissionBriefing.Recommendations(plan);
            if (tips.Count > 0)
            {
                UITheme.Text(new Rect(x, y, cw, 22f), "RECOMMENDED", 15, UITheme.Accent, TextAnchor.UpperLeft, true);
                y += 26f;
                foreach (var tip in tips)
                {
                    UITheme.Text(new Rect(x + 8f, y, cw - 8f, 22f), "-  " + tip, 16, UITheme.Dim);
                    y += 23f;
                }
                y += 12f;
            }

            UITheme.Text(new Rect(x, y, cw, 22f), "DEPLOYMENT", 15, UITheme.Accent, TextAnchor.UpperLeft, true);
            y += 30f;
            int difficulty = UITheme.Stepper(new Rect(x, y, cw, 36f), "Difficulty", OfficerSelectionManager.Difficulty, OfficerSelectionManager.DifficultyNames);
            if (difficulty != OfficerSelectionManager.Difficulty)
            {
                OfficerSelectionManager.Difficulty = difficulty;
                SaveManager.Save();
                game.RollPlan();
            }
            y += 42f;
            UITheme.Text(new Rect(x, y, cw * 0.36f, 36f), "Random seed", 17, UITheme.TextColor, TextAnchor.MiddleLeft);
            UITheme.Text(new Rect(x + cw * 0.38f, y, 120f, 36f), plan.seed.ToString(), 18, UITheme.TextColor, TextAnchor.MiddleLeft, true);
            bool fixedSeed = mission.seed != 0;
            if (UITheme.Button(new Rect(x + cw * 0.38f + 110f, y + 2f, 150f, 32f), fixedSeed ? "Fixed by mission" : "New seed", !fixedSeed, false, 16))
            {
                OfficerSelectionManager.NewSeed();
                game.RollPlan();
            }
            y += 42f;
            OfficerSelectionManager.KeepSeed = UITheme.Toggle(new Rect(x, y, cw, 30f), "Keep this seed when replaying or re-opening the briefing", OfficerSelectionManager.KeepSeed);
            y += 38f;
            UITheme.Text(new Rect(x, y, cw, 40f), "Squad size: up to " + mission.maxSquad + " AI officers. Difficulty changes suspect accuracy and reaction time and the score multiplier.", 15, UITheme.Faint);

            if (UITheme.Button(new Rect(60f, h - 90f, 220f, 50f), "< Mission board")) game.GoToHeadquarters();
            if (UITheme.Button(new Rect(w - 380f, h - 90f, 320f, 50f), "Select officers  >", true, true, 21)) game.OpenOfficerSelection(false);
        }
    }
}
