using System.Collections.Generic;
using UnityEngine;

namespace Swat
{
    // The Game Modes screen: pick Team Deathmatch, Capture the Flag, Zone
    // Control, Gun Game or Elimination, a map, team size, score and time limits, bot skill and time of
    // day; check your team, then go to the squad or loadout screens or start.
    // The online panel hosts or joins a game with friends; once you have
    // joined someone else's game this screen shows their lobby instead.
    public class VersusSetupUI
    {
        static readonly string[] TimeNames = { "Day", "Evening", "Night" };
        readonly Dictionary<string, MissionData> thumbnails = new Dictionary<string, MissionData>();
        readonly OnlineLobbyUI online = new OnlineLobbyUI();

        public void Draw(GameManager game)
        {
            var session = NetSession.Instance;
            if (session != null && session.Role == NetRole.Client && session.Phase != NetPhase.Connecting)
            {
                online.DrawClientLobby(game, Thumb);
                return;
            }
            bool hosting = NetSession.IsHost;
            float w = UITheme.Width, h = UITheme.Height;
            var o = SaveManager.Progress.versus;
            o.mode = Mathf.Clamp(o.mode, 1, VersusMatch.LastMode);
            if (System.Array.IndexOf(VersusMatch.MapIds, o.mapId) < 0) o.mapId = VersusMatch.MapIds[0];
            var mode = (GameMode)o.mode;
            bool changed = false;

            UITheme.Fill(new Rect(0f, 0f, w, h), new Color(0.02f, 0.03f, 0.05f, 0.86f));
            UITheme.Header(new Rect(60f, 36f, w - 120f, 60f), "Game Modes", hosting ? "Hosting an online game: pick the match, then start it for everyone" : "Training exercises with marking rounds: you and your squad against the Red Team, or online with friends");

            // Modes.
            float x = 60f, y = 120f, cw = 520f;
            for (int i = 1; i <= VersusMatch.LastMode; i++)
            {
                var rect = new Rect(x, y, cw, 76f);
                bool selected = o.mode == i;
                if (UITheme.Button(rect, string.Empty, true, selected)) { o.mode = i; changed = true; }
                UITheme.Fill(new Rect(rect.x, rect.y, 5f, rect.height), ModeColor((GameMode)i));
                UITheme.Text(new Rect(rect.x + 22f, rect.y + 6f, cw - 40f, 26f), VersusMatch.ModeNames[i].ToUpperInvariant(), 19, selected ? Color.white : UITheme.TextColor, TextAnchor.UpperLeft, true);
                UITheme.Text(new Rect(rect.x + 22f, rect.y + 32f, cw - 40f, 42f), VersusMatch.ModeGoals[i], 14, UITheme.Dim);
                y += 84f;
            }

            // Options.
            y += 6f;
            UITheme.Text(new Rect(x, y, cw, 22f), "MATCH SETTINGS", 14, UITheme.Accent, TextAnchor.UpperLeft, true);
            y += 28f;
            var sizes = new[] { "1 vs 1", "2 vs 2", "3 vs 3", "4 vs 4", "5 vs 5", "6 vs 6" };
            int size = UITheme.Stepper(new Rect(x, y, cw, 34f), "Team size", Mathf.Clamp(o.teamSize - 1, 0, 5), sizes);
            if (size != o.teamSize - 1) { o.teamSize = size + 1; changed = true; }
            y += 40f;
            var scores = new string[3];
            for (int i = 0; i < 3; i++) scores[i] = VersusMatch.ScoreLimitFor(mode, i) + " " + VersusMatch.ScoreUnit(mode);
            int score = UITheme.Stepper(new Rect(x, y, cw, 34f), mode == GameMode.GunGame ? "Ladder" : mode == GameMode.Elimination ? "Rounds to win" : "Score limit", Mathf.Clamp(o.scoreIndex, 0, 2), scores);
            if (score != o.scoreIndex) { o.scoreIndex = score; changed = true; }
            y += 40f;
            var times = new string[3];
            for (int i = 0; i < 3; i++) times[i] = VersusMatch.MinutesFor(mode, i) + " minutes";
            int time = UITheme.Stepper(new Rect(x, y, cw, 34f), "Time limit", Mathf.Clamp(o.timeIndex, 0, 2), times);
            if (time != o.timeIndex) { o.timeIndex = time; changed = true; }
            y += 40f;
            int bots = UITheme.Stepper(new Rect(x, y, cw, 34f), "Bot skill", Mathf.Clamp(o.botSkill, 0, 2), VersusMatch.SkillNames);
            if (bots != o.botSkill) { o.botSkill = bots; changed = true; }
            y += 40f;
            int light = UITheme.Stepper(new Rect(x, y, cw, 34f), "Time of day", Mathf.Clamp(o.timeOfDay, 0, 2), TimeNames);
            if (light != o.timeOfDay) { o.timeOfDay = light; changed = true; }

            // Maps.
            float mx = x + cw + 40f, mw = w - mx - 60f;
            UITheme.Text(new Rect(mx, 120f, mw, 22f), "MAP", 14, UITheme.Accent, TextAnchor.UpperLeft, true);
            int columns = 3;
            float tile = (mw - (columns - 1) * 12f) / columns, th = 132f;
            for (int i = 0; i < VersusMatch.MapIds.Length; i++)
            {
                string id = VersusMatch.MapIds[i];
                var rect = new Rect(mx + (i % columns) * (tile + 12f), 148f + (i / columns) * (th + 12f), tile, th);
                bool selected = o.mapId == id;
                if (UITheme.Button(rect, string.Empty, true, selected)) { o.mapId = id; changed = true; }
                MissionSelectionUI.Thumbnail(new Rect(rect.x + 8f, rect.y + 8f, rect.width - 16f, th - 44f), Thumb(id), true);
                UITheme.Text(new Rect(rect.x + 10f, rect.yMax - 32f, rect.width - 20f, 26f), MissionBriefing.MapName(id), 16, selected ? Color.white : UITheme.TextColor, TextAnchor.MiddleLeft, true);
            }

            // Your team and record.
            float ty = 148f + Mathf.Ceil(VersusMatch.MapIds.Length / (float)columns) * (th + 12f) + 8f;
            float th2 = h - ty - 120f;
            float tw = Mathf.Floor(mw * 0.4f);
            var teamRect = new Rect(mx, ty, tw, th2);
            UITheme.Panel(teamRect);
            float tx = teamRect.x + 18f, tcw = tw - 36f;
            if (hosting)
            {
                UITheme.Text(new Rect(tx, teamRect.y + 10f, tcw, 22f), "TEAMS", 14, UITheme.Accent, TextAnchor.UpperLeft, true);
                UITheme.Text(new Rect(tx, teamRect.y + 36f, tcw, 44f), "Blue:  " + OnlineLobbyUI.Lineup(o, 0), 15, VersusHUD.SideColor(0));
                UITheme.Text(new Rect(tx, teamRect.y + 84f, tcw, 44f), "Red:  " + OnlineLobbyUI.Lineup(o, 1), 15, VersusHUD.SideColor(1));
                UITheme.Text(new Rect(tx, teamRect.y + 134f, tcw, 80f), "Everyone plays their own officer and loadout. Tactical equipment is off and doors stay open in online matches. The game doesn't pause online.", 13, UITheme.Dim);
            }
            else
            {
                UITheme.Text(new Rect(tx, teamRect.y + 10f, tcw, 22f), "BLUE TEAM", 14, UITheme.Accent, TextAnchor.UpperLeft, true);
                var names = new List<string> { OfficerSelectionManager.Leader.callsign + " (you)" };
                foreach (var officer in OfficerSelectionManager.Squad) names.Add(officer.callsign);
                int fill = Mathf.Max(0, o.teamSize - names.Count);
                string line = string.Join("   ", names.GetRange(0, Mathf.Min(names.Count, o.teamSize)).ToArray()) + (fill > 0 ? "   + " + fill + " more officer" + (fill > 1 ? "s" : "") : "");
                UITheme.Text(new Rect(tx, teamRect.y + 36f, tcw, 44f), line, 16, UITheme.TextColor);
                UITheme.Text(new Rect(tx, teamRect.y + 84f, tcw, 120f),
                    "Squadmates play as bots with their own loadouts. You use your loadout; your team respawns at the van, the Red Team deep inside. Every usable door starts open. Ammo refills at the van and when you respawn.", 14, UITheme.Dim);
            }
            UITheme.Text(new Rect(tx, teamRect.yMax - 28f, tcw, 22f), "Matches played " + o.matchesPlayed + "   |   Won " + o.matchesWon, 14, UITheme.Faint);
            online.DrawPanel(new Rect(mx + tw + 12f, ty, mw - tw - 12f, th2), game);

            if (changed)
            {
                SaveManager.Save();
                if (hosting) session.SendLobby();
            }

            if (UITheme.Button(new Rect(60f, h - 90f, 200f, 50f), "< Main menu"))
            {
                if (hosting) session.Leave("You closed the online game.");
                game.GoToMainMenu();
            }
            if (UITheme.Button(new Rect(280f, h - 90f, 200f, 50f), "Squad"))
            {
                game.PrepareVersus();
                game.OpenOfficerSelection(false);
            }
            if (UITheme.Button(new Rect(500f, h - 90f, 200f, 50f), "Loadout"))
            {
                game.PrepareVersus();
                game.OpenLoadout(false);
            }
            if (hosting)
            {
                if (UITheme.Button(new Rect(w - 420f, h - 90f, 360f, 50f), "START ONLINE MATCH  >", true, true, 21)) session.HostStartMatch();
            }
            else if (UITheme.Button(new Rect(w - 380f, h - 90f, 320f, 50f), "START MATCH  >", true, true, 22)) game.StartVersus();
        }

        MissionData Thumb(string mapId)
        {
            MissionData mission;
            if (!thumbnails.TryGetValue(mapId, out mission) || mission == null)
            {
                mission = VersusMatch.CreateMission(new VersusOptions { mapId = mapId, mode = 1 });
                mission.thumbnailColor = new Color(0.2f, 0.3f, 0.45f);
                thumbnails[mapId] = mission;
            }
            return mission;
        }

        public static Color ModeColor(GameMode mode)
        {
            switch (mode)
            {
                case GameMode.TeamDeathmatch: return new Color(0.95f, 0.35f, 0.3f);
                case GameMode.CaptureTheFlag: return new Color(0.36f, 0.62f, 0.95f);
                case GameMode.GunGame: return new Color(1f, 0.66f, 0.22f);
                case GameMode.Elimination: return new Color(0.72f, 0.5f, 1f);
                default: return new Color(0.4f, 0.85f, 0.55f);
            }
        }
    }
}
