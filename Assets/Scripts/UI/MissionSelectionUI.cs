using System.Collections.Generic;
using UnityEngine;

namespace Swat
{
    // Level select (the HQ mission board): Training and the ten levels as a
    // grid of cards with thumbnail, level number, status and difficulty, plus
    // details of the selected level and a shortcut to custom levels.
    public class MissionSelectionUI
    {
        public void Draw(GameManager game)
        {
            float w = UITheme.Width, h = UITheme.Height;
            UITheme.Fill(new Rect(0f, 0f, w, h), new Color(0.02f, 0.03f, 0.05f, 0.6f));
            UITheme.Header(new Rect(60f, 34f, 900f, 60f), "Level Select", "TRU Headquarters - Training and ten levels; complete levels to unlock the next ones");

            var missions = new List<MissionData>(GameData.AllMissions);
            missions.Sort((a, b) => a.sortOrder.CompareTo(b.sortOrder));
            var selected = OfficerSelectionManager.Mission;
            if ((selected == null || selected.isCustom) && missions.Count > 0)
            {
                selected = GameManager.NextMission() ?? missions[0];
                OfficerSelectionManager.Mission = selected;
            }

            const float cardW = 300f, cardH = 84f, gapX = 14f, gapY = 10f;
            float top = 112f;
            for (int i = 0; i < missions.Count; i++)
            {
                var mission = missions[i];
                var rect = new Rect(60f + (i % 2) * (cardW + gapX), top + (i / 2) * (cardH + gapY), cardW, cardH);
                if (Card(rect, mission, mission == selected)) OfficerSelectionManager.Mission = mission;
            }
            float gridBottom = top + Mathf.CeilToInt(missions.Count / 2f) * (cardH + gapY);
            if (UITheme.BigButton(new Rect(60f, gridBottom + 6f, cardW * 2f + gapX, 60f), "Custom levels and Level Creator", "Build your own levels, then play them with your squad")) game.OpenLevelEditor();

            float detailsX = 60f + cardW * 2f + gapX + 30f;
            if (selected != null) Details(new Rect(detailsX, top, Mathf.Min(w - detailsX - 60f, 900f), h - top - 110f), selected, game);

            if (UITheme.Button(new Rect(60f, h - 80f, 200f, 50f), "< Main menu")) game.GoToMainMenu();
            bool available = selected != null && Progression.IsAvailable(selected);
            if (UITheme.Button(new Rect(w - 360f, h - 80f, 300f, 50f), "Open briefing  >", available, true, 21)) game.OpenBriefing(selected);
        }

        static bool Card(Rect rect, MissionData mission, bool selected)
        {
            bool available = Progression.IsAvailable(mission);
            bool clicked = UITheme.Button(rect, string.Empty, true, selected);
            Thumbnail(new Rect(rect.x + 8f, rect.y + 8f, 104f, rect.height - 16f), mission, available);
            float x = rect.x + 122f, tw = rect.width - 128f;
            UITheme.Text(new Rect(x, rect.y + 7f, tw, 18f), mission.LevelLabel, 12, available ? UITheme.Accent : UITheme.Faint, TextAnchor.UpperLeft, true);
            UITheme.Text(new Rect(x, rect.y + 24f, tw, 24f), mission.displayName, 16, available ? UITheme.TextColor : UITheme.Faint, TextAnchor.UpperLeft, true);
            var record = SaveManager.FindRecord(mission.id);
            string status;
            Color color;
            if (!available) { status = "LOCKED (" + mission.unlockAfterMissions + " levels)"; color = UITheme.Faint; }
            else if (record != null && record.completed) { status = "DONE  " + record.bestScore + " (" + record.bestRating + ")"; color = UITheme.Good; }
            else if (record != null && record.attempts > 0) { status = "ATTEMPTED (" + record.attempts + ")"; color = UITheme.Warn; }
            else { status = "NEW"; color = UITheme.Accent; }
            UITheme.Text(new Rect(x, rect.y + 52f, tw - 70f, 20f), status, 12, color, TextAnchor.UpperLeft, true);
            Stars(new Rect(rect.xMax - 78f, rect.y + 52f, 70f, 20f), mission.difficulty);
            return clicked;
        }

        public static void Stars(Rect rect, int difficulty)
        {
            for (int i = 0; i < 3; i++)
            {
                var c = new Vector2(rect.x + 10f + i * 24f, rect.center.y);
                UITheme.Dot(c, 7f, i < difficulty ? UITheme.Warn : new Color(1f, 1f, 1f, 0.15f));
            }
        }

        // A stylized thumbnail: the mission's color with a schematic of its building type.
        public static void Thumbnail(Rect rect, MissionData mission, bool available)
        {
            var color = mission.thumbnailColor;
            UITheme.Fill(rect, Shapes.Shade(color, 0.6f));
            UITheme.Shade(rect, new Color(color.r, color.g, color.b, 0.9f), true);
            var line = new Color(1f, 1f, 1f, 0.55f);
            float x = rect.x, y = rect.y, w = rect.width, h = rect.height;
            string map = mission.mapId ?? "";
            if (map.StartsWith(CustomLevelStore.MapPrefix)) map = "custom";
            switch (map)
            {
                case "warehouse":
                    UITheme.Frame(new Rect(x + w * 0.1f, y + h * 0.2f, w * 0.8f, h * 0.6f), line, 2f);
                    for (int i = 1; i < 4; i++) UITheme.Fill(new Rect(x + w * (0.1f + i * 0.2f), y + h * 0.3f, 2f, h * 0.4f), line);
                    UITheme.Fill(new Rect(x + w * 0.1f, y + h * 0.8f, w * 0.3f, 3f), UITheme.Warn);
                    break;
                case "apartment":
                    for (int i = 0; i < 3; i++) UITheme.Frame(new Rect(x + w * 0.25f, y + h * (0.15f + i * 0.24f), w * 0.5f, h * 0.22f), line, 2f);
                    UITheme.Fill(new Rect(x + w * 0.45f, y + h * 0.15f, 2f, h * 0.7f), line);
                    break;
                case "training":
                    for (int i = 0; i < 4; i++) UITheme.Dot(new Vector2(x + w * (0.2f + i * 0.2f), y + h * 0.35f), h * 0.07f, line);
                    UITheme.Frame(new Rect(x + w * 0.15f, y + h * 0.55f, w * 0.7f, h * 0.3f), line, 2f);
                    break;
                case "store":
                    UITheme.Frame(new Rect(x + w * 0.25f, y + h * 0.35f, w * 0.5f, h * 0.45f), line, 2f);
                    UITheme.Fill(new Rect(x + w * 0.2f, y + h * 0.28f, w * 0.6f, h * 0.08f), new Color(0.9f, 0.35f, 0.3f, 0.7f));
                    UITheme.Fill(new Rect(x + w * 0.45f, y + h * 0.55f, w * 0.1f, h * 0.25f), line);
                    break;
                case "motel":
                    for (int i = 0; i < 6; i++) UITheme.Frame(new Rect(x + w * (0.08f + i * 0.14f), y + h * 0.35f, w * 0.12f, h * 0.35f), line, 1.5f);
                    UITheme.Fill(new Rect(x + w * 0.08f, y + h * 0.75f, w * 0.84f, 2f), line);
                    break;
                case "bank":
                    UITheme.Fill(new Rect(x + w * 0.2f, y + h * 0.25f, w * 0.6f, 3f), line);
                    for (int i = 0; i < 5; i++) UITheme.Fill(new Rect(x + w * (0.24f + i * 0.13f), y + h * 0.32f, 3f, h * 0.42f), line);
                    UITheme.Fill(new Rect(x + w * 0.18f, y + h * 0.76f, w * 0.64f, 3f), line);
                    break;
                case "clinic":
                    UITheme.Fill(new Rect(x + w * 0.45f, y + h * 0.2f, w * 0.1f, h * 0.6f), new Color(1f, 1f, 1f, 0.7f));
                    UITheme.Fill(new Rect(x + w * 0.3f, y + h * 0.42f, w * 0.4f, h * 0.16f), new Color(1f, 1f, 1f, 0.7f));
                    break;
                case "nightclub":
                    UITheme.Frame(new Rect(x + w * 0.15f, y + h * 0.2f, w * 0.7f, h * 0.6f), line, 2f);
                    for (int i = 0; i < 3; i++) UITheme.Dot(new Vector2(x + w * (0.35f + i * 0.15f), y + h * 0.5f), h * 0.06f, new Color(0.9f, 0.4f, 1f, 0.8f));
                    break;
                case "factory":
                    for (int i = 0; i < 4; i++)
                        UITheme.LineTo(new Vector2(x + w * (0.12f + i * 0.17f), y + h * 0.45f), new Vector2(x + w * (0.29f + i * 0.17f), y + h * 0.3f), line, 2f);
                    UITheme.Frame(new Rect(x + w * 0.12f, y + h * 0.45f, w * 0.68f, h * 0.35f), line, 2f);
                    UITheme.Fill(new Rect(x + w * 0.82f, y + h * 0.2f, w * 0.06f, h * 0.6f), line);
                    break;
                case "custom":
                    for (int i = 0; i < 4; i++) UITheme.Fill(new Rect(x + w * (0.15f + i * 0.2f), y + h * 0.2f, 1.5f, h * 0.6f), new Color(1f, 1f, 1f, 0.25f));
                    UITheme.Frame(new Rect(x + w * 0.2f, y + h * 0.3f, w * 0.35f, h * 0.4f), line, 2f);
                    UITheme.Frame(new Rect(x + w * 0.55f, y + h * 0.3f, w * 0.25f, h * 0.25f), line, 2f);
                    break;
                default:
                    UITheme.Frame(new Rect(x + w * 0.12f, y + h * 0.18f, w * 0.76f, h * 0.64f), line, 2f);
                    UITheme.Fill(new Rect(x + w * 0.12f, y + h * 0.5f, w * 0.76f, 2f), line);
                    UITheme.Fill(new Rect(x + w * 0.5f, y + h * 0.18f, 2f, h * 0.32f), line);
                    UITheme.Fill(new Rect(x + w * 0.3f, y + h * 0.5f, 2f, h * 0.32f), line);
                    break;
            }
            var time = mission.DefaultTime;
            if (time == TimeOfDay.Night) UITheme.Dot(new Vector2(x + w * 0.86f, y + h * 0.16f), h * 0.07f, new Color(0.9f, 0.92f, 1f, 0.8f));
            else if (time == TimeOfDay.Evening) UITheme.Dot(new Vector2(x + w * 0.86f, y + h * 0.16f), h * 0.07f, new Color(1f, 0.6f, 0.3f, 0.85f));
            UITheme.Frame(rect, new Color(0f, 0f, 0f, 0.5f));
            if (!available) UITheme.Fill(rect, new Color(0f, 0f, 0f, 0.55f));
        }

        static void Details(Rect rect, MissionData mission, GameManager game)
        {
            UITheme.Panel(rect);
            float x = rect.x + 26f, w = rect.width - 52f, y = rect.y + 22f;
            Thumbnail(new Rect(x, y, 260f, 150f), mission, Progression.IsAvailable(mission));
            float tx = x + 280f, tw = w - 280f;
            UITheme.Text(new Rect(tx, y - 4f, tw, 20f), mission.LevelLabel, 14, UITheme.Accent, TextAnchor.UpperLeft, true);
            y += 14f;
            UITheme.Text(new Rect(tx, y, tw, 34f), mission.displayName, 28, UITheme.TextColor, TextAnchor.UpperLeft, true);
            UITheme.Text(new Rect(tx, y + 38f, tw, 22f), mission.location, 17, UITheme.Accent);
            UITheme.Text(new Rect(tx, y + 64f, tw, 22f), MissionBriefing.TypeName(mission.missionType) + "  |  " + MissionBriefing.MapName(mission.mapId) + ("  |  " + LightingProfile.Names[(int)mission.DefaultTime]), 16, UITheme.Dim);
            UITheme.Text(new Rect(tx, y + 92f, 90f, 22f), "Difficulty", 16, UITheme.Dim);
            Stars(new Rect(tx + 86f, y + 92f, 80f, 22f), mission.difficulty);
            UITheme.Text(new Rect(tx, y + 120f, tw, 22f), "Squad: up to " + mission.maxSquad + " officers  |  Par time " + MissionScoring.FormatTime(mission.parTime), 16, UITheme.Dim);
            y += 172f;

            UITheme.Text(new Rect(x, y, w, 50f), mission.description, 18, UITheme.TextColor);
            y += 44f;
            float bh = UITheme.TextHeight(mission.briefing, 16, w);
            UITheme.Text(new Rect(x, y, w, bh), mission.briefing, 16, UITheme.Dim);
            y += bh + 16f;

            UITheme.Text(new Rect(x, y, w, 24f), "PRIMARY OBJECTIVES", 15, UITheme.Accent, TextAnchor.UpperLeft, true);
            y += 26f;
            foreach (var objective in mission.objectives)
            {
                UITheme.Text(new Rect(x + 10f, y, w - 10f, 22f), "-  " + objective.description, 16, UITheme.TextColor);
                y += 22f;
            }
            y += 8f;
            if (mission.optionalCount > 0 && mission.optionalPool.Count > 0)
            {
                UITheme.Text(new Rect(x, y, w, 22f), "OPTIONAL: " + mission.optionalCount + " drawn at random from " + mission.optionalPool.Count + " possibilities each deployment", 15, UITheme.Accent, TextAnchor.UpperLeft, true);
                y += 30f;
            }

            UITheme.Text(new Rect(x, y, w, 24f), "AVAILABLE OFFICERS", 15, UITheme.Accent, TextAnchor.UpperLeft, true);
            y += 28f;
            float ix = x;
            foreach (var officer in GameData.AllOfficers)
            {
                bool available = Progression.IsAvailable(officer);
                UIIcons.Portrait(new Rect(ix, y, 54f, 64f), officer, GameData.LoadoutFor(officer), available);
                ix += 62f;
            }
            y += 74f;
            var record = SaveManager.FindRecord(mission.id) ?? new MissionRecord();
            string status = record.completed ? "Completed. Best score " + record.bestScore + " (rating " + record.bestRating + "), best time " + MissionScoring.FormatTime(record.bestTime)
                : record.attempts > 0 ? "Not yet completed (" + record.attempts + " attempt(s))" : "Not attempted";
            if (!Progression.IsAvailable(mission)) status = "Locked: complete " + mission.unlockAfterMissions + " level(s) to unlock.";
            UITheme.Text(new Rect(x, Mathf.Min(y, rect.yMax - 40f), w, 24f), status, 16, record.completed ? UITheme.Good : UITheme.Dim);
        }
    }
}
