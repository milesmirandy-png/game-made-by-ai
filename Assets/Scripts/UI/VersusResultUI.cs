using UnityEngine;

namespace Swat
{
    // End of a game-mode match: who won, the final score, both teams'
    // scoreboards and your own numbers, with Rematch / Match setup / Main menu.
    public class VersusResultUI
    {
        VersusResult shown;
        float openedAt;

        public void Draw(GameManager game)
        {
            var result = game.LastMatch;
            if (result == null) return;
            float w = UITheme.Width, h = UITheme.Height;
            if (result != shown)
            {
                shown = result;
                openedAt = Time.unscaledTime;
            }
            float t = Time.unscaledTime - openedAt;
            UITheme.Fill(new Rect(0f, 0f, w, h), new Color(0.015f, 0.02f, 0.04f, 0.93f * Mathf.Clamp01(t / 0.3f + 0.4f)));
            string title = result.winner == 0 ? "VICTORY" : result.winner == 1 ? "DEFEAT" : "DRAW";
            Color color = result.winner == 0 ? UITheme.Good : result.winner == 1 ? UITheme.Bad : UITheme.Warn;
            float pop = 1f + Mathf.Clamp01(1f - t / 0.35f) * 0.3f;
            UITheme.ShadowText(new Rect(0f, 40f, w, 90f), title, Mathf.RoundToInt(64 * pop), color, TextAnchor.MiddleCenter, true);
            UITheme.Text(new Rect(0f, 128f, w, 26f), VersusMatch.ModeNames[(int)result.mode] + "  -  " + result.mapName + "   |   " + MissionScoring.FormatTime(result.time) + "   |   " + result.reason, 18, UITheme.Dim, TextAnchor.UpperCenter);

            // Final score.
            var blue = new Rect(w * 0.5f - 250f, 170f, 220f, 80f);
            var red = new Rect(w * 0.5f + 30f, 170f, 220f, 80f);
            UITheme.Fill(blue, new Color(0.1f, 0.2f, 0.38f, 0.95f));
            UITheme.Fill(red, new Color(0.38f, 0.1f, 0.1f, 0.95f));
            UITheme.Text(blue, "BLUE  " + result.blueScore, 36, Color.white, TextAnchor.MiddleCenter, true);
            UITheme.Text(red, result.redScore + "  RED", 36, Color.white, TextAnchor.MiddleCenter, true);
            UITheme.Text(new Rect(blue.xMax, blue.y, red.x - blue.xMax, blue.height), "-", 36, UITheme.Dim, TextAnchor.MiddleCenter, true);

            // Scoreboards.
            float colW = 520f, top = 280f;
            DrawBoard(new Rect(w * 0.5f - colW - 20f, top, colW, h - top - 130f), result, 0);
            DrawBoard(new Rect(w * 0.5f + 20f, top, colW, h - top - 130f), result, 1);

            UITheme.Text(new Rect(0f, h - 124f, w, 24f), "You: " + result.playerKills + " tag-outs   |   tagged " + result.playerDeaths + " times" + (result.mode == GameMode.CaptureTheFlag ? "   |   " + result.playerCaptures + " captures" : ""), 17, UITheme.TextColor, TextAnchor.UpperCenter, true);

            bool ready = t > 0.6f;
            float by = h - 90f;
            if (UITheme.Button(new Rect(60f, by, 240f, 50f), "Rematch", ready, true, 19)) game.RestartMission();
            if (UITheme.Button(new Rect(w - 560f, by, 240f, 50f), "Match setup", ready, false, 18)) game.OpenVersusSetup();
            if (UITheme.Button(new Rect(w - 300f, by, 240f, 50f), "Main menu", ready, false, 18)) game.GoToMainMenu();
        }

        static void DrawBoard(Rect rect, VersusResult result, int side)
        {
            UITheme.Panel(rect);
            var color = VersusHUD.SideColor(side);
            UITheme.Text(new Rect(rect.x + 18f, rect.y + 10f, rect.width - 36f, 24f), side == 0 ? "BLUE TEAM" : "RED TEAM", 17, color, TextAnchor.UpperLeft, true);
            float y = rect.y + 42f;
            bool flags = result.mode == GameMode.CaptureTheFlag;
            UITheme.Text(new Rect(rect.x + 18f, y, 200f, 20f), "Name", 13, UITheme.Dim, TextAnchor.UpperLeft, true);
            UITheme.Text(new Rect(rect.xMax - 300f, y, 90f, 20f), "Tag-outs", 13, UITheme.Dim, TextAnchor.UpperRight, true);
            UITheme.Text(new Rect(rect.xMax - 200f, y, 90f, 20f), "Tagged", 13, UITheme.Dim, TextAnchor.UpperRight, true);
            if (flags) UITheme.Text(new Rect(rect.xMax - 100f, y, 80f, 20f), "Captures", 13, UITheme.Dim, TextAnchor.UpperRight, true);
            y += 26f;
            foreach (var row in result.rows)
            {
                if (row.side != side) continue;
                if (row.isPlayer) UITheme.Fill(new Rect(rect.x + 8f, y - 2f, rect.width - 16f, 26f), new Color(color.r, color.g, color.b, 0.18f));
                UITheme.Text(new Rect(rect.x + 18f, y, 260f, 24f), row.name, 16, UITheme.TextColor, TextAnchor.UpperLeft, row.isPlayer);
                UITheme.Text(new Rect(rect.xMax - 300f, y, 90f, 24f), row.kills.ToString(), 16, UITheme.TextColor, TextAnchor.UpperRight);
                UITheme.Text(new Rect(rect.xMax - 200f, y, 90f, 24f), row.deaths.ToString(), 16, UITheme.Dim, TextAnchor.UpperRight);
                if (flags) UITheme.Text(new Rect(rect.xMax - 100f, y, 80f, 24f), row.captures.ToString(), 16, UITheme.TextColor, TextAnchor.UpperRight);
                y += 28f;
            }
        }
    }
}
