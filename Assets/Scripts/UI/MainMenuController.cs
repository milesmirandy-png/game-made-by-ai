using UnityEngine;

namespace Swat
{
    // Title screen over the HQ diorama: Continue, New mission, Mission
    // selection, Training, Officer roster, Equipment, Settings, Credits, Quit.
    public class MainMenuController
    {
        bool showCredits;

        public void Draw(GameManager game)
        {
            float h = UITheme.Height;
            // Darken the left side for readability.
            UITheme.Fill(new Rect(0f, 0f, 560f, h), new Color(0.02f, 0.03f, 0.05f, 0.82f));
            UITheme.Shade(new Rect(560f, 0f, 220f, h), new Color(0.02f, 0.03f, 0.05f, 0.82f), false);
            Title(new Rect(70f, 90f, 480f, 200f));

            var progress = SaveManager.Progress;
            bool hasProgress = progress.trainingComplete || progress.missionsCompleted > 0 || progress.missions.Count > 0;
            var next = GameManager.NextMission();
            var training = Training();

            float x = 70f, y = 320f, w = 420f, bh = 50f, gap = 8f;
            if (UITheme.BigButton(new Rect(x, y, w, 64f), "Continue", hasProgress ? "Headquarters - next: " + (next != null ? next.displayName : "-") : "No saved progress yet", hasProgress))
                game.GoToHeadquarters();
            y += 64f + gap;
            if (UITheme.Button(new Rect(x, y, w, bh), "New Mission", next != null) && next != null) game.OpenBriefing(next);
            y += bh + gap;
            if (UITheme.Button(new Rect(x, y, w, bh), "Mission Selection")) game.GoToHeadquarters();
            y += bh + gap;
            if (UITheme.Button(new Rect(x, y, w, bh), "Training" + (progress.trainingComplete ? "  (completed)" : "  (recommended first)"), training != null) && training != null) game.OpenBriefing(training);
            y += bh + gap;
            if (UITheme.Button(new Rect(x, y, w, bh), "Officer Roster")) game.OpenOfficerSelection(true);
            y += bh + gap;
            if (UITheme.Button(new Rect(x, y, w, bh), "Equipment")) game.OpenLoadout(true);
            y += bh + gap;
            if (UITheme.Button(new Rect(x, y, w, bh), "Settings")) UIManager.Instance.Settings.Show(0);
            y += bh + gap;
            if (UITheme.Button(new Rect(x, y, w, bh), "Credits", true, showCredits)) showCredits = !showCredits;
            y += bh + gap;
            if (UITheme.Button(new Rect(x, y, w, bh), "Quit")) game.Quit();

            UITheme.Text(new Rect(70f, h - 70f, 480f, 22f), "Missions completed: " + progress.missionsCompleted + "   |   Arrests: " + progress.totalArrests + "   |   Rescues: " + progress.totalRescues, 15, UITheme.Dim);
            UITheme.Text(new Rect(70f, h - 46f, 480f, 22f), "F12 screenshot   |   F10 FPS counter   |   Prototype build", 14, UITheme.Faint);
            if (showCredits) Credits(new Rect(UITheme.Width - 620f, 120f, 560f, 520f));
        }

        static MissionData Training()
        {
            foreach (var mission in GameData.AllMissions) if (mission.isTraining) return mission;
            return null;
        }

        public static void Title(Rect rect)
        {
            UITheme.Text(new Rect(rect.x, rect.y, rect.width, 90f), "SWAT", 86, UITheme.TextColor, TextAnchor.UpperLeft, true);
            UITheme.Fill(new Rect(rect.x + 4f, rect.y + 98f, 64f, 4f), UITheme.AlertRed);
            UITheme.Fill(new Rect(rect.x + 72f, rect.y + 98f, 64f, 4f), UITheme.AlertBlue);
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
                "Halvorsen Logistics, Kestrel Freight and Marlow Court do not exist.\n\n" +
                "Weapons and equipment are simplified game abstractions and are not real-world guidance.\n\n" +
                "Built with Unity's built-in modules only: IMGUI interface, runtime NavMesh, primitive meshes.";
            UITheme.Text(new Rect(rect.x + 24f, rect.y + 86f, rect.width - 48f, rect.height - 100f), text, 17, UITheme.TextColor);
        }
    }
}
