using UnityEngine;

namespace Swat
{
    // Online play on the Game Modes screen. Not connected: your name, Host a
    // match, Join by address, and Find games on this network. Hosting: who has
    // joined (click a team to move a player), the address others use, and the
    // match settings you pick as usual. Joined: the host's settings, the
    // players, team buttons and Leave, while you wait for the host to start.
    public class OnlineLobbyUI
    {
        static readonly string[] TimeNames = { "Day", "Evening", "Night" };
        GUIStyle field;
        string nameText, addressText;

        void Styles()
        {
            if (field != null) return;
            field = new GUIStyle(GUI.skin.textField) { fontSize = Mathf.RoundToInt(16 * Mathf.Clamp(SaveManager.Settings.textSize, 0.85f, 1.4f)) };
            field.padding = new RectOffset(8, 8, 6, 6);
            var o = SaveManager.Progress.versus;
            nameText = string.IsNullOrEmpty(o.onlineName) && OfficerSelectionManager.Leader != null ? OfficerSelectionManager.Leader.callsign : o.onlineName ?? "";
            addressText = o.joinAddress ?? "";
        }

        string Field(Rect rect, string value, int max)
        {
            return GUI.TextField(rect, value ?? "", max, field);
        }

        // ---- The panel on the Game Modes screen (not connected, or hosting) ----

        public void DrawPanel(Rect rect, GameManager game)
        {
            Styles();
            var session = NetSession.Instance;
            UITheme.Panel(rect);
            float x = rect.x + 16f, y = rect.y + 10f, cw = rect.width - 32f;
            UITheme.Text(new Rect(x, y, cw, 22f), "ONLINE  -  PEER TO PEER", 14, UITheme.Accent, TextAnchor.UpperLeft, true);
            y += 28f;
            if (session == null) return;
            if (session.Role == NetRole.Host)
            {
                DrawHosting(session, x, y, cw, rect.yMax);
                return;
            }
            if (session.Role == NetRole.Client)
            {
                UITheme.Text(new Rect(x, y, cw, 24f), session.Status, 16, UITheme.TextColor);
                if (UITheme.Button(new Rect(x, y + 34f, 160f, 40f), "Cancel", true, false, 16)) session.Leave("Cancelled.");
                return;
            }

            // Not connected.
            UITheme.Text(new Rect(x, y, 110f, 32f), "Your name", 15, UITheme.Dim, TextAnchor.MiddleLeft);
            string name = Field(new Rect(x + 110f, y, Mathf.Min(260f, cw - 110f), 32f), nameText, 16);
            if (name != nameText) nameText = name;
            if (UITheme.Button(new Rect(x + cw - 210f, y - 2f, 210f, 36f), "Host a match", true, true, 17))
            {
                Remember();
                if (session.Host(nameText)) AudioManager.Play2D(Sound.RadioOrder, 0.5f, 1f, SoundCategory.Interface);
            }
            y += 44f;
            UITheme.Text(new Rect(x, y, 110f, 32f), "Host address", 15, UITheme.Dim, TextAnchor.MiddleLeft);
            string address = Field(new Rect(x + 110f, y, Mathf.Min(260f, cw - 110f), 32f), addressText, 64);
            if (address != addressText) addressText = address;
            if (UITheme.Button(new Rect(x + cw - 210f, y - 2f, 210f, 36f), "Join", !string.IsNullOrEmpty(addressText), false, 17))
            {
                Remember();
                session.Join(addressText, nameText);
            }
            y += 44f;
            if (UITheme.Button(new Rect(x, y, 300f, 34f), session.Searching ? "Searching this network..." : "Find games on this network", !session.Searching, false, 15)) session.SearchLan();
            y += 40f;
            var found = session.Found;
            for (int i = 0; i < found.Count && i < 3; i++)
            {
                string host, map;
                int mode, players, max;
                bool open;
                NetSession.ReadInfo(found[i].info, out host, out mode, out map, out players, out max, out open);
                var row = new Rect(x, y, cw, 30f);
                UITheme.Fill(row, new Color(1f, 1f, 1f, 0.04f));
                string modeName = mode >= 1 && mode <= 3 ? VersusMatch.ModeNames[mode] : "";
                UITheme.Text(new Rect(row.x + 8f, row.y, cw - 130f, 30f), host + "   " + modeName + "  -  " + MissionBriefing.MapName(map) + "   " + players + "/" + max + (open ? "" : "   (in a match)"), 14, UITheme.TextColor, TextAnchor.MiddleLeft);
                if (UITheme.Button(new Rect(row.xMax - 110f, row.y + 2f, 110f, 26f), "Join", open && players < max, false, 14))
                {
                    addressText = found[i].host.Address + (found[i].host.Port != NetSession.GamePort ? ":" + found[i].host.Port : "");
                    Remember();
                    session.Join(addressText, nameText);
                }
                y += 34f;
            }
            if (found.Count == 0 && session.Status == null)
                UITheme.Text(new Rect(x, y, cw, 40f), "Play with friends: one of you hosts, the others join with the host's address (or find it here when on the same network).", 13, UITheme.Faint);
            if (session.Status != null)
                UITheme.Text(new Rect(x, rect.yMax - 46f, cw, 40f), session.Status, 14, session.StatusBad ? UITheme.Bad : UITheme.Dim);
        }

        void Remember()
        {
            var o = SaveManager.Progress.versus;
            o.onlineName = (nameText ?? "").Trim();
            o.joinAddress = (addressText ?? "").Trim();
            SaveManager.Save();
        }

        void DrawHosting(NetSession session, float x, float y, float cw, float bottom)
        {
            UITheme.Text(new Rect(x, y, cw - 130f, 24f), "HOSTING  -  " + session.Players.Count + " / " + NetSession.MaxPlayers + " players", 17, UITheme.Good, TextAnchor.UpperLeft, true);
            if (UITheme.Button(new Rect(x + cw - 120f, y - 4f, 120f, 30f), "Close", true, false, 14)) session.Leave("You closed the online game.");
            y += 28f;
            UITheme.Text(new Rect(x, y, cw, 20f), "Others join with: " + session.Addresses + (session.Port != NetSession.GamePort ? "  (port " + session.Port + ")" : ""), 14, UITheme.TextColor);
            y += 20f;
            UITheme.Text(new Rect(x, y, cw, 20f), "Over the internet, forward UDP port " + session.Port + " on your router to this computer and share your public IP.", 12, UITheme.Faint);
            y += 26f;
            foreach (var player in session.Players)
            {
                var row = new Rect(x, y, cw, 28f);
                UITheme.Fill(row, new Color(VersusHUD.SideColor(player.side).r, VersusHUD.SideColor(player.side).g, VersusHUD.SideColor(player.side).b, 0.12f));
                UITheme.Text(new Rect(row.x + 8f, row.y, cw * 0.5f, 28f), player.name + (player.peerId == 0 ? "  (you, host)" : ""), 15, UITheme.TextColor, TextAnchor.MiddleLeft, player.peerId == 0);
                if (player.peerId != 0)
                {
                    UITheme.Text(new Rect(row.x + cw * 0.5f, row.y, 90f, 28f), player.ping + " ms", 13, player.ping > 150 ? UITheme.Warn : UITheme.Dim, TextAnchor.MiddleLeft);
                    if (UITheme.Button(new Rect(row.xMax - 120f, row.y + 2f, 120f, 24f), VersusMatch.SideName(player.side) + " team", true, false, 13))
                        session.SetSide(player, 1 - player.side);
                }
                else UITheme.Text(new Rect(row.xMax - 120f, row.y, 120f, 28f), "Blue team", 13, VersusHUD.SideColor(0), TextAnchor.MiddleCenter, true);
                y += 31f;
                if (y > bottom - 60f) break;
            }
            UITheme.Text(new Rect(x, bottom - 46f, cw, 40f), session.Players.Count > 1 ? "Click a player's team to move them. Bots fill each team up to the team size." : "Waiting for players to join. You can start alone (with bots) too.", 13, UITheme.Dim);
        }

        // Your team's line-up on the Game Modes screen while hosting.
        public static string Lineup(VersusOptions o, int side)
        {
            var session = NetSession.Instance;
            int humans = 0;
            var names = new System.Collections.Generic.List<string>();
            foreach (var player in session.Players)
                if (player.side == side)
                {
                    humans++;
                    names.Add(player.name + (player.peerId == 0 ? " (you)" : ""));
                }
            int bots = Mathf.Max(0, Mathf.Clamp(o.teamSize, 1, VersusMatch.MaxTeamSize) - humans);
            return (names.Count > 0 ? string.Join(", ", names.ToArray()) : "no players") + (bots > 0 ? "  + " + bots + " bot" + (bots > 1 ? "s" : "") : "");
        }

        // ---- Joined: the host's lobby ----

        public void DrawClientLobby(GameManager game, System.Func<string, MissionData> thumbnail)
        {
            Styles();
            var session = NetSession.Instance;
            float w = UITheme.Width, h = UITheme.Height;
            UITheme.Fill(new Rect(0f, 0f, w, h), new Color(0.02f, 0.03f, 0.05f, 0.86f));
            bool connecting = session.Phase == NetPhase.Connecting;
            if (!connecting && Event.current.type == EventType.Layout) session.SyncProfile();
            UITheme.Header(new Rect(60f, 36f, w - 120f, 60f), "Online lobby", connecting ? "Connecting..." : "Hosted by " + (session.HostName ?? "the host"));

            var o = session.Options;
            float x = 60f, y = 120f, cw = 560f;
            if (o != null && !connecting)
            {
                var mode = (GameMode)Mathf.Clamp(o.mode, 1, 3);
                var card = new Rect(x, y, cw, 118f);
                UITheme.Panel(card);
                UITheme.Fill(new Rect(card.x, card.y, 5f, card.height), VersusSetupUI.ModeColor(mode));
                UITheme.Text(new Rect(card.x + 22f, card.y + 12f, cw - 40f, 30f), VersusMatch.ModeNames[(int)mode].ToUpperInvariant(), 22, Color.white, TextAnchor.UpperLeft, true);
                UITheme.Text(new Rect(card.x + 22f, card.y + 46f, cw - 40f, 64f), VersusMatch.ModeGoals[(int)mode], 15, UITheme.Dim);
                y += 134f;
                var map = new Rect(x, y, cw, 210f);
                UITheme.Panel(map);
                MissionSelectionUI.Thumbnail(new Rect(map.x + 10f, map.y + 10f, map.width - 20f, map.height - 50f), thumbnail(o.mapId), true);
                UITheme.Text(new Rect(map.x + 14f, map.yMax - 36f, map.width - 28f, 28f), MissionBriefing.MapName(o.mapId), 18, Color.white, TextAnchor.MiddleLeft, true);
                y += 226f;
                string[] lines =
                {
                    "Team size:  " + o.teamSize + " vs " + o.teamSize + " (bots fill the gaps)",
                    "Score limit:  " + VersusMatch.ScoreLimitFor(mode, o.scoreIndex) + " " + VersusMatch.ScoreUnit(mode),
                    "Time limit:  " + VersusMatch.MinutesFor(mode, o.timeIndex) + " minutes",
                    "Bot skill:  " + VersusMatch.SkillNames[Mathf.Clamp(o.botSkill, 0, 2)] + "      Time of day:  " + TimeNames[Mathf.Clamp(o.timeOfDay, 0, 2)],
                };
                foreach (var line in lines)
                {
                    UITheme.Text(new Rect(x + 4f, y, cw, 26f), line, 16, UITheme.TextColor);
                    y += 28f;
                }
            }

            // Players and teams.
            float px = x + cw + 40f, pw = w - px - 60f;
            var panel = new Rect(px, 120f, pw, h - 260f);
            UITheme.Panel(panel);
            float py = panel.y + 14f;
            if (connecting)
            {
                UITheme.Text(new Rect(px + 18f, py, pw - 36f, 60f), session.Status, 18, UITheme.TextColor);
            }
            else
            {
                for (int side = 0; side < 2; side++)
                {
                    UITheme.Text(new Rect(px + 18f, py, pw - 36f, 24f), VersusMatch.SideName(side).ToUpperInvariant() + " TEAM", 16, VersusHUD.SideColor(side), TextAnchor.UpperLeft, true);
                    py += 28f;
                    foreach (var player in session.Players)
                    {
                        if (player.side != side) continue;
                        bool me = player.peerId == session.MyPeerId;
                        var row = new Rect(px + 18f, py, pw - 36f, 28f);
                        if (me) UITheme.Fill(row, new Color(1f, 1f, 1f, 0.06f));
                        UITheme.Text(new Rect(row.x + 8f, row.y, row.width - 120f, 28f), player.name + (player.peerId == 0 ? "  (host)" : me ? "  (you)" : ""), 16, UITheme.TextColor, TextAnchor.MiddleLeft, me);
                        if (player.peerId != 0) UITheme.Text(new Rect(row.xMax - 110f, row.y, 100f, 28f), player.ping + " ms", 13, UITheme.Dim, TextAnchor.MiddleRight);
                        py += 30f;
                    }
                    py += 14f;
                }
                float by = panel.yMax - 60f;
                UITheme.Text(new Rect(px + 18f, by - 30f, pw - 36f, 22f), "Your team:", 14, UITheme.Dim);
                if (UITheme.Button(new Rect(px + 18f, by, 200f, 44f), "Blue team", true, session.LocalSide == 0, 17)) session.ChooseSide(0);
                if (UITheme.Button(new Rect(px + 230f, by, 200f, 44f), "Red team", true, session.LocalSide == 1, 17)) session.ChooseSide(1);
            }

            UITheme.Text(new Rect(px, h - 128f, pw, 24f), connecting ? "" : session.Status ?? "Waiting for the host to start the match...", 16, session.StatusBad ? UITheme.Bad : UITheme.Accent, TextAnchor.UpperLeft, true);
            UITheme.Text(new Rect(px, h - 104f, pw, 22f), "You play your own officer and loadout. Tactical equipment is off and doors stay open online.", 13, UITheme.Faint);
            if (UITheme.Button(new Rect(60f, h - 90f, 200f, 50f), "Leave")) session.Leave("You left the lobby.");
            if (!connecting)
            {
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
            }
        }
    }
}
