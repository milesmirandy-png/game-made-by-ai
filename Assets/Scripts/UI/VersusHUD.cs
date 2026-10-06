using UnityEngine;

namespace Swat
{
    // In-match HUD for the game modes: score and clock (top centre), flag,
    // zone, gun ladder or round status, the takedown feed and team list
    // (right), the respawn countdown and who you're watching while out, and
    // labels over players, flags, the zone and your team's pings.
    public static class VersusHUD
    {
        static readonly Color Blue = new Color(0.3f, 0.58f, 1f);
        static readonly Color Red = new Color(1f, 0.32f, 0.26f);

        public static Color SideColor(int side) { return side == 0 ? Blue : Red; }

        public static void Draw(GameManager game, VersusMatch match, float rightTop)
        {
            DrawWorld(game, match);
            DrawScore(match);
            float y = DrawFeed(match, rightTop);
            DrawTeam(match, y + 8f);
            DrawRespawn(game, match);
            if (match.PlayerCarrying)
                UITheme.ShadowText(new Rect(UITheme.Width * 0.5f - 300f, UITheme.Height - 300f, 600f, 30f), "YOU HAVE THE " + VersusMatch.SideName(1 - match.MySide).ToUpperInvariant() + " FLAG - bring it to your base",
                    20, SideColor(1 - match.MySide), TextAnchor.MiddleCenter, true);
        }

        static void DrawScore(VersusMatch match)
        {
            float w = UITheme.Width;
            var rect = new Rect(w * 0.5f - 230f, 14f, 460f, 74f);
            UITheme.Panel(rect);
            string goal = match.Mode == GameMode.GunGame ? match.ScoreLimit + "-GUN LADDER" : match.Mode == GameMode.Elimination ? "FIRST TO " + match.ScoreLimit + " ROUNDS" : "FIRST TO " + match.ScoreLimit;
            UITheme.Text(new Rect(rect.x, rect.y + 4f, rect.width, 18f), VersusMatch.ModeNames[(int)match.Mode].ToUpperInvariant() + "   -   " + goal, 12, UITheme.Dim, TextAnchor.UpperCenter, true);
            var blue = new Rect(rect.x + 14f, rect.y + 24f, 130f, 42f);
            var red = new Rect(rect.xMax - 144f, rect.y + 24f, 130f, 42f);
            UITheme.Fill(blue, new Color(Blue.r * 0.35f, Blue.g * 0.35f, Blue.b * 0.35f, 0.9f));
            UITheme.Fill(red, new Color(Red.r * 0.35f, Red.g * 0.35f, Red.b * 0.35f, 0.9f));
            UITheme.Fill(new Rect(blue.x, blue.yMax - 3f, blue.width * Mathf.Clamp01(match.Score[0] / Mathf.Max(1, match.ScoreLimit)), 3f), Blue);
            UITheme.Fill(new Rect(red.xMax - red.width * Mathf.Clamp01(match.Score[1] / Mathf.Max(1, match.ScoreLimit)), red.yMax - 3f, red.width * Mathf.Clamp01(match.Score[1] / Mathf.Max(1, match.ScoreLimit)), 3f), Red);
            UITheme.Text(blue, "BLUE  " + Mathf.FloorToInt(match.Score[0]), 24, Color.white, TextAnchor.MiddleCenter, true);
            UITheme.Text(red, Mathf.FloorToInt(match.Score[1]) + "  RED", 24, Color.white, TextAnchor.MiddleCenter, true);
            // Elimination shows the round clock in the middle; the other modes the match clock.
            float left = match.Mode == GameMode.Elimination ? match.RoundTimeLeft : match.TimeLeft;
            string clock = match.Mode == GameMode.Elimination && match.RoundOver ? "--:--" : MissionScoring.FormatTime(left);
            UITheme.Text(new Rect(rect.x + 150f, rect.y + 26f, rect.width - 300f, 38f), clock, 24, left < 30f ? UITheme.Warn : UITheme.TextColor, TextAnchor.MiddleCenter, true);

            float y = rect.yMax + 4f;
            if (match.Mode == GameMode.GunGame && match.Ladder != null)
            {
                int rung = Mathf.Clamp(match.PlayerKills, 0, match.Ladder.Count - 1);
                bool last = rung == match.Ladder.Count - 1;
                string next = last ? "FINAL GUN - one more tag-out wins" : "next: " + match.Ladder[rung + 1].displayName;
                UITheme.ShadowText(new Rect(rect.x - 160f, y, rect.width + 320f, 22f), "Your gun " + (rung + 1) + "/" + match.Ladder.Count + ":  " + match.Ladder[rung].displayName + "     " + next, 15,
                    last ? UITheme.Warn : UITheme.TextColor, TextAnchor.UpperCenter, true);
            }
            else if (match.Mode == GameMode.Elimination)
            {
                string state = match.RoundOver ? "next round starting" : "still in  " + match.StillIn(0) + " vs " + match.StillIn(1);
                UITheme.ShadowText(new Rect(rect.x - 120f, y, rect.width + 240f, 22f), "Round " + match.Round + "     " + state + "     match " + MissionScoring.FormatTime(match.TimeLeft), 15, UITheme.TextColor, TextAnchor.UpperCenter, true);
            }
            else if (match.Mode == GameMode.CaptureTheFlag)
            {
                int theirs = 1 - match.MySide;
                UITheme.ShadowText(new Rect(rect.x - 120f, y, rect.width + 240f, 22f), "Your flag: " + FlagStatus(match, match.MySide) + "     " + VersusMatch.SideName(theirs) + " flag: " + FlagStatus(match, theirs), 15, UITheme.TextColor, TextAnchor.UpperCenter);
            }
            else if (match.Mode == GameMode.ZoneControl)
            {
                // Control bar: red on the left, blue on the right, marker at the current control.
                var bar = new Rect(rect.x + 40f, y + 4f, rect.width - 80f, 10f);
                UITheme.Fill(bar, new Color(0f, 0f, 0f, 0.6f));
                float c = match.ZoneControl;
                float mid = bar.center.x;
                if (c > 0f) UITheme.Fill(new Rect(mid, bar.y, bar.width * 0.5f * c, bar.height), Blue);
                else if (c < 0f) UITheme.Fill(new Rect(mid + bar.width * 0.5f * c, bar.y, -bar.width * 0.5f * c, bar.height), Red);
                UITheme.Fill(new Rect(mid - 1f, bar.y - 3f, 2f, bar.height + 6f), Color.white);
                string owner = match.ZoneOwner == 0 ? "Zone: BLUE" : match.ZoneOwner == 1 ? "Zone: RED" : "Zone: neutral";
                string contest = match.ZoneCount[0] > 0 && match.ZoneCount[1] > 0 ? "   CONTESTED" : "";
                UITheme.ShadowText(new Rect(rect.x, y + 16f, rect.width, 22f), owner + "   (in zone " + match.ZoneCount[0] + " vs " + match.ZoneCount[1] + ")" + contest, 15,
                    match.ZoneOwner == 0 ? Blue : match.ZoneOwner == 1 ? Red : UITheme.TextColor, TextAnchor.UpperCenter, true);
            }
        }

        static string FlagStatus(VersusMatch match, int side)
        {
            var flag = match.Flags[side];
            if (flag == null) return "-";
            if (flag.carrier != null) return "carried by " + match.NameOf(flag.carrier);
            if (flag.AtHome) return "at base";
            return "dropped (" + Mathf.CeilToInt(Mathf.Max(0f, 20f - (Time.time - flag.droppedAt))) + "s)";
        }

        static float DrawFeed(VersusMatch match, float y)
        {
            float w = UITheme.Width;
            for (int i = 0; i < match.Feed.Count; i++)
            {
                var line = match.Feed[i];
                float age = Time.unscaledTime - line.time;
                if (age > 7f) continue;
                float alpha = Mathf.Clamp01(7f - age);
                float width = Mathf.Min(420f, UITheme.TextWidth(line.text, 14, true) + 22f);
                var rect = new Rect(w - 20f - width, y, width, 24f);
                UITheme.Fill(rect, new Color(0f, 0f, 0f, 0.5f * alpha));
                var color = SideColor(line.side);
                UITheme.Fill(new Rect(rect.x, rect.y, 3f, rect.height), new Color(color.r, color.g, color.b, alpha));
                UITheme.Text(new Rect(rect.x + 10f, rect.y, rect.width - 14f, rect.height), line.text, 14, new Color(1f, 1f, 1f, alpha), TextAnchor.MiddleLeft, true);
                y += 27f;
            }
            return y;
        }

        // Your teammates: bots and (online) other players, marked with a dot.
        static void DrawTeam(VersusMatch match, float y)
        {
            float w = UITheme.Width;
            UITheme.ShadowText(new Rect(w - 330f, y, 310f, 20f), VersusMatch.SideName(match.MySide).ToUpperInvariant() + " TEAM", 13, SideColor(match.MySide), TextAnchor.UpperRight, true);
            y += 22f;
            foreach (var mate in match.Others)
            {
                if (mate.Side != match.MySide) continue;
                string state = mate.IsAlive ? mate.Kills + " tag-outs" : mate.RespawnAt > 0f && !match.Mirror ? "back in " + Mathf.CeilToInt(Mathf.Max(0f, mate.RespawnAt - Time.time)) : "tagged out";
                if (match.Mode == GameMode.GunGame && match.Ladder != null) state = "gun " + (Mathf.Clamp(mate.Kills, 0, match.Ladder.Count - 1) + 1) + "/" + match.Ladder.Count + (mate.IsAlive ? "" : "   (tagged out)");
                if (match.Mode == GameMode.Elimination && !mate.IsAlive) state = "out this round";
                if (match.IsCarrying(mate)) state = "HAS THE FLAG";
                if (match.Spectating == mate) state += "   [watching]";
                UITheme.ShadowText(new Rect(w - 330f, y, 310f, 20f), (mate.IsHuman ? "* " : "") + mate.Callsign + "   " + state, 14, mate.IsAlive ? UITheme.TextColor : UITheme.Faint, TextAnchor.UpperRight);
                y += 20f;
            }
        }

        static void DrawRespawn(GameManager game, VersusMatch match)
        {
            var player = game.Player;
            if (player == null || player.IsAlive) return;
            bool eliminated = match.Mode == GameMode.Elimination;
            if (match.PlayerRespawnAt <= 0f && !eliminated) return;
            float w = UITheme.Width, h = UITheme.Height;
            // Smaller and higher once you're watching a teammate, so the view stays clear.
            bool watching = match.Spectating != null;
            float top = watching ? 110f : h * 0.36f;
            UITheme.ShadowText(new Rect(0f, top, w, 50f), "TAGGED OUT", watching ? 26 : 42, Red, TextAnchor.MiddleCenter, true);
            UITheme.ShadowText(new Rect(0f, top + (watching ? 34f : 52f), w, 26f), "by " + match.PlayerTaggedBy, watching ? 15 : 18, UITheme.TextColor, TextAnchor.MiddleCenter);
            string when = eliminated ? "Out until the next round" : "Back in " + Mathf.CeilToInt(Mathf.Max(0f, match.PlayerRespawnAt - Time.time));
            UITheme.ShadowText(new Rect(0f, top + (watching ? 56f : 82f), w, 30f), when, watching ? 17 : 22, UITheme.Accent, TextAnchor.MiddleCenter, true);
            if (watching)
                UITheme.ShadowText(new Rect(0f, h - 210f, w, 26f), "Watching " + match.Spectating.Callsign + "   -   " + GameInput.PromptKey(InputAction.Fire) + " for the next teammate", 17, UITheme.TextColor, TextAnchor.MiddleCenter, true);
        }

        // Names over players, and markers for the flags and the zone (pinned to the screen edge when off-screen).
        static void DrawWorld(GameManager game, VersusMatch match)
        {
            var cam = game.CameraRig.Cam;
            foreach (var other in match.Others)
            {
                if (!other.IsAlive || (other.Side != match.MySide && !other.Seen)) continue;
                Vector2 gui;
                if (!UITheme.WorldToGui(cam, other.Position + Vector3.up * 2.35f, out gui)) continue;
                var color = SideColor(other.Side);
                UITheme.ShadowText(new Rect(gui.x - 90f, gui.y - 12f, 180f, 20f), other.Callsign, 13, color, TextAnchor.MiddleCenter, true);
                if (other.Health < other.MaxHealth)
                    UITheme.Bar(new Rect(gui.x - 18f, gui.y + 8f, 36f, 3f), other.Health / other.MaxHealth, color);
            }
            if (match.Mode == GameMode.CaptureTheFlag)
                for (int side = 0; side < 2; side++)
                {
                    var flag = match.Flags[side];
                    if (flag == null || flag.carrier == (ICombatTarget)game.Player) continue;
                    var carrier = flag.carrier as IVersusMember;
                    if (carrier != null && carrier.Side != match.MySide && !carrier.Seen) continue;
                    Marker(cam, flag.position + Vector3.up * 2.7f, side == 0 ? "BLUE FLAG" : "RED FLAG", SideColor(side));
                }
            if (match.Mode == GameMode.ZoneControl)
                Marker(cam, match.Zone.center + Vector3.up * 2.6f, "ZONE", match.ZoneOwner < 0 ? Color.white : SideColor(match.ZoneOwner));
            DrawPings(game, match, cam);
        }

        static readonly Color PingHere = new Color(1f, 0.85f, 0.3f);
        static readonly Color PingEnemy = new Color(1f, 0.45f, 0.2f);

        // Your team's pings: a diamond with who sent it and how far it is, pulsing when new.
        static void DrawPings(GameManager game, VersusMatch match, Camera cam)
        {
            var player = game.Player;
            foreach (var ping in match.Pings)
            {
                float age = Time.unscaledTime - ping.time;
                float fade = Mathf.Clamp01((VersusMatch.PingLife - age) / 1f);
                var color = ping.enemy ? PingEnemy : PingHere;
                color.a = fade;
                Vector2 gui;
                bool front = UITheme.WorldToGui(cam, ping.position + Vector3.up * 0.4f, out gui);
                float w = UITheme.Width, h = UITheme.Height, margin = 60f;
                if (!front || gui.x < margin || gui.x > w - margin || gui.y < margin || gui.y > h - margin)
                {
                    if (!front) gui = new Vector2(w - gui.x, h - gui.y);
                    gui.x = Mathf.Clamp(gui.x, margin, w - margin);
                    gui.y = Mathf.Clamp(gui.y, margin + 80f, h - margin - 140f);
                }
                float size = 9f;
                UITheme.LineTo(gui + new Vector2(0f, -size), gui + new Vector2(size, 0f), color, 3f);
                UITheme.LineTo(gui + new Vector2(size, 0f), gui + new Vector2(0f, size), color, 3f);
                UITheme.LineTo(gui + new Vector2(0f, size), gui + new Vector2(-size, 0f), color, 3f);
                UITheme.LineTo(gui + new Vector2(-size, 0f), gui + new Vector2(0f, -size), color, 3f);
                if (age < 1f) UITheme.Ring(gui, Mathf.Lerp(10f, 34f, age), new Color(color.r, color.g, color.b, (1f - age) * fade), 2f);
                string distance = player != null ? "  " + Mathf.RoundToInt(Vector3.Distance(player.Position, ping.position)) + " m" : "";
                UITheme.ShadowText(new Rect(gui.x - 110f, gui.y - 34f, 220f, 20f), (ping.enemy ? "ENEMY" : "HERE") + distance, 13, color, TextAnchor.MiddleCenter, true);
                UITheme.ShadowText(new Rect(gui.x - 110f, gui.y + 12f, 220f, 18f), ping.from, 12, new Color(1f, 1f, 1f, 0.8f * fade), TextAnchor.MiddleCenter);
            }
        }

        static void Marker(Camera cam, Vector3 world, string label, Color color)
        {
            Vector2 gui;
            bool front = UITheme.WorldToGui(cam, world, out gui);
            float w = UITheme.Width, h = UITheme.Height, margin = 60f;
            bool onScreen = front && gui.x > margin && gui.x < w - margin && gui.y > margin && gui.y < h - margin;
            if (!onScreen)
            {
                if (!front) gui = new Vector2(w - gui.x, h - gui.y);
                gui.x = Mathf.Clamp(gui.x, margin, w - margin);
                gui.y = Mathf.Clamp(gui.y, margin + 80f, h - margin - 140f);
            }
            UITheme.Dot(gui, 7f, color);
            UITheme.Ring(gui, 11f, new Color(color.r, color.g, color.b, 0.7f), 2f);
            UITheme.ShadowText(new Rect(gui.x - 80f, gui.y - 32f, 160f, 20f), label, 13, color, TextAnchor.MiddleCenter, true);
        }
    }
}
