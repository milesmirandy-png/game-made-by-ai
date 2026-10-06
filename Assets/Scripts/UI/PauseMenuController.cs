using UnityEngine;

namespace Swat
{
    // Resume, Restart mission, Settings, Controls, Quit mission (back to headquarters), Quit to desktop.
    // The world is fully paused underneath (AI, timers, physics and world audio),
    // except in online matches, where nobody else stops.
    public class PauseMenuController
    {
        string confirm; // which destructive action is waiting for a second click

        public void Draw(GameManager game, UIManager ui)
        {
            float w = UITheme.Width, h = UITheme.Height;
            UITheme.Fill(new Rect(0f, 0f, w, h), new Color(0.01f, 0.02f, 0.04f, 0.72f));
            if (ui.Settings.Open) return;
            var rect = new Rect(w * 0.5f - 230f, h * 0.5f - 280f, 460f, 560f);
            UITheme.Panel(rect);
            UITheme.Header(new Rect(rect.x + 30f, rect.y + 24f, rect.width - 60f, 50f), "Paused", game.Plan != null ? game.Plan.mission.displayName : null);
            var mission = MissionManager.Instance;
            if (mission != null && mission.Mission != null)
            {
                int done = 0, total = 0;
                foreach (var objective in mission.Objectives)
                {
                    if (objective.Optional) continue;
                    total++;
                    if (objective.State == ObjectiveState.Completed) done++;
                }
                UITheme.Text(new Rect(rect.x + 30f, rect.yMax - 58f, rect.width - 60f, 22f), "Mission time " + MissionScoring.FormatTime(mission.Elapsed) + "   |   Objectives " + done + "/" + total, 14, UITheme.Dim, TextAnchor.UpperCenter);
            }
            float x = rect.x + 30f, bw = rect.width - 60f, y = rect.y + 100f, bh = 52f, gap = 10f;
            if (UITheme.Button(new Rect(x, y, bw, bh), "Resume")) { confirm = null; game.Resume(); }
            y += bh + gap;
            bool versus = VersusMatch.Active;
            // Online only the host can restart (for everyone).
            bool canRestart = !NetSession.IsClient;
            if (UITheme.Button(new Rect(x, y, bw, bh), !canRestart ? "Only the host can restart" : confirm == "restart" ? "Click again to restart" : NetSession.IsHost ? "Restart match (everyone)" : versus ? "Restart match" : "Restart mission", canRestart, confirm == "restart"))
            {
                if (confirm == "restart") { confirm = null; game.RestartMission(); }
                else confirm = "restart";
            }
            y += bh + gap;
            if (UITheme.Button(new Rect(x, y, bw, bh), "Settings")) ui.Settings.Show(0);
            y += bh + gap;
            if (UITheme.Button(new Rect(x, y, bw, bh), "Controls")) ui.Settings.Show(SettingsUI.ControlsTab);
            y += bh + gap;
            string leave = NetSession.IsHost ? "End match (everyone to the lobby)" : NetSession.IsClient ? "Leave the online game" : versus ? "Leave match" : "Quit mission";
            if (UITheme.Button(new Rect(x, y, bw, bh), confirm == "hq" ? (versus ? "Click again to leave the match" : "Click again to quit the mission") : leave, true, confirm == "hq"))
            {
                if (confirm == "hq") { confirm = null; game.LeaveMatch(); }
                else confirm = "hq";
            }
            y += bh + gap;
            if (UITheme.Button(new Rect(x, y, bw, bh), confirm == "quit" ? "Click again to quit" : "Quit to desktop", true, confirm == "quit"))
            {
                if (confirm == "quit") game.Quit();
                else confirm = "quit";
            }
            UITheme.Text(new Rect(rect.x, rect.yMax - 34f, rect.width, 22f), NetSession.Online ? "Online match: the game keeps running while this menu is open." : "Progress so far in this mission is lost if you leave.", 13, NetSession.Online ? UITheme.Warn : UITheme.Faint, TextAnchor.UpperCenter);
        }
    }
}
