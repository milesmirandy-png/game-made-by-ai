using System.Collections.Generic;
using UnityEngine;

namespace Swat
{
    public struct ScoreLine
    {
        public string label;
        public int points;
        public ScoreLine(string label, int points)
        {
            this.label = label;
            this.points = points;
        }
    }

    // Turns what happened into points. Arrests are worth far more than
    // force, civilian safety matters most, and unauthorized force costs a lot.
    // The same inputs always give the same score.
    public static class MissionScoring
    {
        public const int ArrestPoints = 40;
        public const int NeutralizedPoints = 10;
        public const int IncapacitatedPoints = 30;
        public const int EvacuatedPoints = 50;
        public const int CivilianInjuredPenalty = -50;
        public const int CivilianKilledPenalty = -200;
        public const int UnauthorizedForcePenalty = -100;
        public const int OfficerDownPenalty = -75;
        public const int MaxHealthBonus = 100;
        public const int MaxTimeBonus = 200;
        public const int ReportPoints = 10;
        public const int UnreportedPenalty = -15;
        public const int WeaponSecuredPoints = 10;
        public const int WeaponLeftPenalty = -20;
        static readonly float[] DifficultyMultiplier = { 0.8f, 1f, 1.25f };

        public static List<ScoreLine> Calculate(MissionManager mission, bool success, float playerHealth, out int total, out string rating)
        {
            var lines = new List<ScoreLine>();
            var stats = mission.Stats;
            int mandatory = 0, optional = 0;
            foreach (var objective in mission.Objectives)
            {
                if (objective.State != ObjectiveState.Completed) continue;
                if (objective.Optional) optional += objective.Points;
                else mandatory += objective.Points;
            }
            lines.Add(new ScoreLine("Primary objectives", mandatory));
            lines.Add(new ScoreLine("Optional objectives", optional));
            lines.Add(new ScoreLine("Suspects arrested (" + stats.suspectsArrested + ")", stats.suspectsArrested * ArrestPoints));
            if (stats.suspectsIncapacitated > 0) lines.Add(new ScoreLine("Suspects incapacitated (" + stats.suspectsIncapacitated + ")", stats.suspectsIncapacitated * IncapacitatedPoints));
            lines.Add(new ScoreLine("Suspects neutralized (" + stats.suspectsKilled + ")", stats.suspectsKilled * NeutralizedPoints));
            lines.Add(new ScoreLine("Civilians evacuated (" + stats.civilians.evacuated + ")", stats.civilians.evacuated * EvacuatedPoints));
            if (stats.civilians.injured > 0) lines.Add(new ScoreLine("Civilians injured (" + stats.civilians.injured + ")", stats.civilians.injured * CivilianInjuredPenalty));
            if (stats.civilians.killed > 0) lines.Add(new ScoreLine("Civilian casualties (" + stats.civilians.killed + ")", stats.civilians.killed * CivilianKilledPenalty));
            if (stats.unauthorizedForce > 0) lines.Add(new ScoreLine("Unauthorized use of force (" + stats.unauthorizedForce + ")", stats.unauthorizedForce * UnauthorizedForcePenalty));
            if (stats.officersDowned > 0) lines.Add(new ScoreLine("Officers downed (" + stats.officersDowned + ")", stats.officersDowned * OfficerDownPenalty));
            if (stats.tocReports > 0) lines.Add(new ScoreLine("Reports to TOC (" + stats.tocReports + ")", stats.tocReports * ReportPoints));
            int unreported = TocReports.Unreported();
            if (unreported > 0) lines.Add(new ScoreLine("Not reported to TOC (" + unreported + ")", unreported * UnreportedPenalty));
            if (stats.weaponsSecured > 0) lines.Add(new ScoreLine("Weapons secured (" + stats.weaponsSecured + ")", stats.weaponsSecured * WeaponSecuredPoints));
            int left = Mathf.Max(0, stats.weaponsDropped - stats.weaponsSecured);
            if (left > 0) lines.Add(new ScoreLine("Weapons left behind (" + left + ")", left * WeaponLeftPenalty));
            lines.Add(new ScoreLine("Team leader health", Mathf.RoundToInt(Mathf.Clamp01(playerHealth) * MaxHealthBonus)));
            float par = Mathf.Max(60f, mission.Mission.parTime);
            int timeBonus = success && mission.Elapsed < par ? Mathf.RoundToInt((par - mission.Elapsed) / par * MaxTimeBonus) : 0;
            lines.Add(new ScoreLine("Time bonus (par " + FormatTime(par) + ")", timeBonus));

            int sum = 0;
            foreach (var line in lines) sum += line.points;
            float multiplier = DifficultyMultiplier[Mathf.Clamp(mission.Plan.difficulty, 0, 2)];
            total = Mathf.Max(0, Mathf.RoundToInt(sum * multiplier));
            if (!Mathf.Approximately(multiplier, 1f))
                lines.Add(new ScoreLine("Difficulty x" + multiplier.ToString("0.00") + " (" + OfficerSelectionManager.DifficultyNames[mission.Plan.difficulty] + ")", total - sum));

            int max = MaxScore(mission);
            float fraction = max > 0 ? sum / (float)max : 0f;
            rating = !success ? "F" : fraction >= 0.9f ? "S" : fraction >= 0.75f ? "A" : fraction >= 0.6f ? "B" : fraction >= 0.45f ? "C" : "D";
            return lines;
        }

        // A perfect run before the difficulty multiplier, used for the rating.
        static int MaxScore(MissionManager mission)
        {
            int max = MaxHealthBonus + MaxTimeBonus;
            foreach (var objective in mission.Objectives) max += objective.Points;
            max += mission.Stats.suspectsTotal * ArrestPoints;
            max += mission.Stats.civilians.total * EvacuatedPoints;
            max += (mission.Stats.suspectsTotal + mission.Stats.civilians.total) * ReportPoints;
            max += mission.Stats.armedSuspects * WeaponSecuredPoints;
            return max;
        }

        public static string FormatTime(float seconds)
        {
            int s = Mathf.Max(0, Mathf.FloorToInt(seconds));
            return (s / 60) + ":" + (s % 60).ToString("00");
        }
    }
}
