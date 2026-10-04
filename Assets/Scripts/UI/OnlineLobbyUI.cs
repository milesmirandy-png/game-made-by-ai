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
            UITheme.Text(new Rect(x, y, cw, 22f), "PLAY WITH FRIENDS  -  LAN OR INTERNET", 14, UITheme.Accent, TextAnchor.UpperLeft, true);
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

            // Not connected: your name and Host (the same game serves the local network and the internet).
            UITheme.Text(new Rect(x, y, 110f, 32f), "Your name", 15, UITheme.Dim, TextAnchor.MiddleLeft);
            string name = Field(new Rect(x + 110f, y, Mathf.Min(240f, cw - 330f), 32f), nameText, 16);
            if (name != nameText) nameText = name;
            if (UITheme.Button(new Rect(x + cw - 210f, y - 2f, 210f, 36f), "Host a game", true, true, 17))
            {
                Remember();
                if (session.Host(nameText)) AudioManager.Play2D(Sound.RadioOrder, 0.5f, 1f, SoundCategory.Interface);
            }
            y += 44f;

            // LAN: games on the same Wi-Fi or wired network appear here by themselves.
            var games = session.LanGames;
            string dots = new string('.', 1 + Mathf.FloorToInt(Time.unscaledTime * 2f) % 3);
            UITheme.Text(new Rect(x, y, cw, 20f), "LAN GAMES ON YOUR NETWORK" + (session.Searching ? "   searching" + dots : ""), 13, UITheme.Dim, TextAnchor.UpperLeft, true);
            y += 22f;
            float listTop = y;
            for (int i = 0; i < games.Count && i < 3; i++)
            {
                var game2 = games[i];
                var row = new Rect(x, y, cw, 30f);
                UITheme.Fill(row, new Color(UITheme.Good.r, UITheme.Good.g, UITheme.Good.b, 0.08f));
                string modeName = game2.mode >= 1 && game2.mode <= 3 ? VersusMatch.ModeNames[game2.mode] : "";
                UITheme.Text(new Rect(row.x + 8f, row.y, cw - 130f, 30f), game2.host + "'s game   " + modeName + "  -  " + MissionBriefing.MapName(game2.map) + "   " + game2.players + "/" + game2.max + (game2.open ? "" : "   (in a match)"),
                    14, UITheme.TextColor, TextAnchor.MiddleLeft);
                if (UITheme.Button(new Rect(row.xMax - 110f, row.y + 2f, 110f, 26f), "Join", game2.open && game2.players < game2.max, true, 14))
                {
                    addressText = game2.address.Address + (game2.address.Port != NetSession.GamePort ? ":" + game2.address.Port : "");
                    Remember();
                    session.Join(addressText, nameText);
                }
                y += 32f;
            }
            if (games.Count == 0)
            {
                string hint = "None yet. When a friend on the same Wi-Fi or network presses Host a game, it shows up here.";
                if (session.SearchingFor > 8f) hint += " Not showing? Let the game through the firewall on private networks (Windows asks the first time), or join by address below.";
                UITheme.Text(new Rect(x, y, cw, 64f), hint, 13, UITheme.Faint);
            }
            y = listTop + 3 * 32f + 4f;

            // Internet (or a LAN that blocks the search): type the host's address.
            UITheme.Text(new Rect(x, y, cw, 20f), "OVER THE INTERNET OR BY ADDRESS", 13, UITheme.Dim, TextAnchor.UpperLeft, true);
            y += 22f;
            UITheme.Text(new Rect(x, y, 110f, 32f), "Host address", 15, UITheme.Dim, TextAnchor.MiddleLeft);
            string address = Field(new Rect(x + 110f, y, Mathf.Min(240f, cw - 330f), 32f), addressText, 64);
            if (address != addressText) addressText = address;
            if (UITheme.Button(new Rect(x + cw - 210f, y - 2f, 210f, 36f), "Join", !string.IsNullOrEmpty(addressText), false, 17))
            {
                Remember();
                session.Join(addressText, nameText);
            }
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
            UITheme.Text(new Rect(x, y, cw, 20f), session.LanVisible ? "Same network (LAN): your game appears in their LAN list. Or they type " + session.Addresses + (session.Port != NetSession.GamePort ? ":" + session.Port : "") + "."
                : "Others join with " + session.Addresses + (session.Port != NetSession.GamePort ? ":" + session.Port : "") + " (the LAN list is busy: another copy of the game hosts on this PC).", 14, UITheme.TextColor);
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
