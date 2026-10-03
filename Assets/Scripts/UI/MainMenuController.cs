using UnityEngine;

namespace Swat
{
    // Title screen over the HQ diorama: Play (next level), Level Select,
    // Training, Level Creator, Officers, Equipment, Settings, Credits and Quit,
    // with campaign progress across the ten levels and a card for the next one.
    public class MainMenuController
    {
        bool showCredits;

        public void Draw(GameManager game)
        {
            float w = UITheme.Width, h = UITheme.Height;
            // Darken the left side for readability.
            UITheme.Fill(new Rect(0f, 0f, 560f, h), new Color(0.02f, 0.03f, 0.05f, 0.84f));
            UITheme.Shade(new Rect(560f, 0f, 220f, h), new Color(0.02f, 0.03f, 0.05f, 0.84f), false);
            Title(new Rect(70f, 70f, 480f, 200f));

            var next = GameManager.NextMission();
            var training = Training();
            int done, total;
            CampaignProgress(out done, out total);

            float x = 70f, y = 282f, bw = 420f, bh = 48f, gap = 8f;
            string playDetail = next != null ? next.LevelLabel + ": " + next.displayName + (done >= total && total > 0 ? "  (replay)" : "") : "No levels available";
            if (UITheme.BigButton(new Rect(x, y, bw, 68f), done == 0 && !SaveManager.Progress.trainingComplete ? "Play" : "Continue", playDetail, next != null) && next != null)
                game.OpenBriefing(next);
            y += 68f + gap;
            if (UITheme.Button(new Rect(x, y, bw, bh), "Level Select  (" + done + "/" + total + " complete)")) game.GoToHeadquarters();
            y += bh + gap;
            if (UITheme.Button(new Rect(x, y, bw, bh), "Training" + (SaveManager.Progress.trainingComplete ? "  (completed)" : "  (recommended first)"), training != null) && training != null) game.OpenBriefing(training);
            y += bh + gap;
            if (UITheme.Button(new Rect(x, y, bw, bh), "Game Modes  (Deathmatch, Flag, Zone)")) game.OpenVersusSetup();
            y += bh + gap;
            if (UITheme.Button(new Rect(x, y, bw, bh), "Level Creator  (build and play your own)")) game.OpenLevelEditor();
            y += bh + gap;
            float half = (bw - gap) * 0.5f;
            if (UITheme.Button(new Rect(x, y, half, bh), "Officers")) game.OpenOfficerSelection(true);
            if (UITheme.Button(new Rect(x + half + gap, y, half, bh), "Equipment")) game.OpenLoadout(true);
            y += bh + gap;
            if (UITheme.Button(new Rect(x, y, half, bh), "Settings")) UIManager.Instance.Settings.Show(0);
            if (UITheme.Button(new Rect(x + half + gap, y, half, bh), "Credits", true, showCredits)) showCredits = !showCredits;
            y += bh + gap;
            if (UITheme.Button(new Rect(x, y, bw, bh), "Quit")) game.Quit();
            y += bh + 22f;

            // Ten-segment campaign bar.
            UITheme.Text(new Rect(x, y, bw, 20f), "CAMPAIGN  " + done + " / " + total + " LEVELS", 13, UITheme.Accent, TextAnchor.UpperLeft, true);
            y += 22f;
            float seg = (bw - (total - 1) * 4f) / Mathf.Max(1, total);
            int index = 0;
            foreach (var mission in GameData.AllMissions)
            {
                if (mission.levelNumber <= 0) continue;
                var record = SaveManager.FindRecord(mission.id);
                bool complete = record != null && record.completed;
                bool open = Progression.IsAvailable(mission);
                UITheme.Fill(new Rect(x + index * (seg + 4f), y, seg, 10f), complete ? UITheme.Good : open ? UITheme.AccentDim : new Color(1f, 1f, 1f, 0.12f));
                index++;
            }

            var progress = SaveManager.Progress;
            UITheme.Text(new Rect(70f, h - 70f, 480f, 22f), "Arrests: " + progress.totalArrests + "   |   Rescues: " + progress.totalRescues + "   |   Shots fired: " + progress.totalShots, 15, UITheme.Dim);
            UITheme.Text(new Rect(70f, h - 46f, 480f, 22f), "F12 screenshot   |   F10 FPS counter   |   Prototype build", 14, UITheme.Faint);

            if (showCredits) Credits(new Rect(w - 620f, 120f, 560f, 520f));
            else if (next != null) NextCard(new Rect(w - 520f, h - 300f, 460f, 240f), next);
        }

        static void CampaignProgress(out int done, out int total)
        {
            done = total = 0;
            foreach (var mission in GameData.AllMissions)
            {
                if (mission.levelNumber <= 0) continue;
                total++;
                var record = SaveManager.FindRecord(mission.id);
                if (record != null && record.completed) done++;
            }
        }

        // A preview of the next level on the right of the title screen.
        static void NextCard(Rect rect, MissionData mission)
        {
            UITheme.Panel(rect);
            MissionSelectionUI.Thumbnail(new Rect(rect.x + 16f, rect.y + 18f, 150f, 100f), mission, true);
            float tx = rect.x + 182f, tw = rect.width - 198f;
            UITheme.Text(new Rect(tx, rect.y + 16f, tw, 20f), "NEXT UP  -  " + mission.LevelLabel, 13, UITheme.Accent, TextAnchor.UpperLeft, true);
            UITheme.Text(new Rect(tx, rect.y + 38f, tw, 28f), mission.displayName, 21, UITheme.TextColor, TextAnchor.UpperLeft, true);
            UITheme.Text(new Rect(tx, rect.y + 68f, tw, 20f), mission.location, 14, UITheme.Dim);
            MissionSelectionUI.Stars(new Rect(tx - 4f, rect.y + 92f, 80f, 20f), mission.difficulty);
            UITheme.Text(new Rect(tx + 80f, rect.y + 92f, tw - 80f, 20f), LightingProfile.Names[(int)mission.DefaultTime], 14, UITheme.Dim);
            UITheme.Text(new Rect(rect.x + 16f, rect.y + 132f, rect.width - 32f, rect.height - 140f), mission.description, 15, UITheme.TextColor);
        }

        static MissionData Training()
        {
            foreach (var mission in GameData.AllMissions) if (mission.isTraining) return mission;
            return null;
        }

        public static void Title(Rect rect)
        {
            UITheme.Text(new Rect(rect.x, rect.y, rect.width, 90f), "SWAT", 86, UITheme.TextColor, TextAnchor.UpperLeft, true);
            // The light bar under the title pulses gently between red and blue.
            float pulse = SaveManager.Settings.reduceFlashes ? 0.5f : 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 2.2f);
            UITheme.Fill(new Rect(rect.x + 4f, rect.y + 98f, 64f, 4f), Color.Lerp(UITheme.AlertRed * 0.6f, UITheme.AlertRed, pulse));
            UITheme.Fill(new Rect(rect.x + 72f, rect.y + 98f, 64f, 4f), Color.Lerp(UITheme.AlertBlue, UITheme.AlertBlue * 0.6f, pulse));
            UITheme.Text(new Rect(rect.x + 2f, rect.y + 110f, rect.width, 40f), "TACTICAL RESPONSE", 30, UITheme.Accent, TextAnchor.UpperLeft, true);
            UITheme.Text(new Rect(rect.x + 2f, rect.y + 150f, rect.width, 24f), "Port Avalon Police Department  -  Tactical Response Unit", 15, UITheme.Dim);
        }

        static void Credits(Rect rect)
        {
            UITheme.Panel(rect);
            UITheme.Header(new Rect(rect.x + 24f, rect.y + 20f, rect.width - 48f, 50f), "Credits");
            string text =
                "SWAT: Tactical Response - a single-player top-down tactical prototype built in Unity.\n\n" +
                "Design, code, level layouts, procedural models and procedural audio were generated for this project. " +
                "No third-party art, sound or code assets are used.\n\n" +
                "All agencies, places, companies and people are fictional. Port Avalon, the Tactical Response Unit, " +
                "Halvorsen Logistics, Kestrel Freight, Marlow Court, Brightwater Corner Mart, Seaview Motor Inn, Sterling Mutual, " +
                "Harbor Street Clinic, Club Halcyon and Riverside Steelworks do not exist.\n\n" +
                "Weapons and equipment are simplified game abstractions and are not real-world guidance.\n\n" +
                "Built with Unity's built-in modules only: IMGUI interface, runtime NavMesh, primitive meshes.";
            UITheme.Text(new Rect(rect.x + 24f, rect.y + 86f, rect.width - 48f, rect.height - 100f), text, 16, UITheme.TextColor);
        }
    }
}
