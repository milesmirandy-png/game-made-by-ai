using System.Collections.Generic;
using System.Text;

namespace Swat
{
    // Builds the briefing text for the briefing screen from the mission data
    // and the rolled plan (so the briefing matches what you'll find).
    public static class MissionBriefing
    {
        public static readonly string[] MapNames = { "office", "Office Complex", "warehouse", "Warehouse", "apartment", "Apartment Building", "training", "Training Ground" };

        public static string MapName(string mapId)
        {
            for (int i = 0; i < MapNames.Length; i += 2) if (MapNames[i] == mapId) return MapNames[i + 1];
            return mapId;
        }

        public static string TypeName(MissionType type)
        {
            switch (type)
            {
                case MissionType.BuildingClearance: return "Building Clearance";
                case MissionType.CivilianRescue: return "Civilian Rescue";
                case MissionType.Investigation: return "Investigation";
                case MissionType.Emergency: return "Emergency Response";
                default: return "Training";
            }
        }

        public static string DifficultyStars(int difficulty)
        {
            return new string('*', difficulty) + new string('-', 3 - difficulty);
        }

        // Situation intel: rough numbers, never exact positions.
        public static List<string> Intel(MissionPlan plan)
        {
            var lines = new List<string>();
            var mission = plan.mission;
            if (mission.isTraining)
            {
                lines.Add("Controlled environment. No live threats.");
                return lines;
            }
            int low = System.Math.Max(1, plan.enemyCount - 1), high = plan.enemyCount + 1;
            lines.Add("Suspects reported: " + low + "-" + high);
            int armored = 0, leader = 0;
            foreach (var group in mission.enemies)
            {
                var data = GameData.Enemy(group.enemyId);
                if (data == null) continue;
                if (data.archetype == EnemyArchetype.Armored) armored += group.count;
                if (data.archetype == EnemyArchetype.Leader) leader += group.count;
            }
            if (armored > 0) lines.Add("Body armor sighted on at least one suspect.");
            if (leader > 0) lines.Add("A known crew leader is believed to be on site.");
            lines.Add(plan.civilianCount > 0 ? "Civilians believed inside: about " + plan.civilianCount : "No civilians expected.");
            lines.Add("Time of day: " + (mission.night ? "night" : "day"));
            if (plan.powerOutage) lines.Add("POWER OUT: indoor rooms are dark. Flashlights and portable lights recommended.");
            if (plan.alarmArmed) lines.Add("The building alarm is armed. Cameras or guards may trigger it.");
            else if (mission.mapId != "training") lines.Add("The building alarm appears to be offline.");
            lines.Add(plan.camerasActive ? "Security cameras are active." : "Security cameras are offline.");
            return lines;
        }

        public static List<string> Recommendations(MissionPlan plan)
        {
            var lines = new List<string>();
            var mission = plan.mission;
            if (mission.randomLockChance > 0.1f) lines.Add("Some doors may be locked: bring breaching charges or a Breacher.");
            if (plan.powerOutage || mission.night) lines.Add("Low light: weapon lights and portable lights help.");
            if (plan.civilianCount > 0) lines.Add("Civilians present: shout before you shoot; consider a less-lethal option.");
            foreach (var group in mission.civilians)
                if (group.type == CivilianType.Injured) { lines.Add("Injured civilians reported: bring medical kits or a Medic."); break; }
            if (plan.camerasActive) lines.Add("Find a security console to shut down the cameras.");
            return lines;
        }

        public static string Objectives(MissionPlan plan)
        {
            var text = new StringBuilder();
            foreach (var objective in plan.mission.objectives) text.Append("- ").Append(objective.description).Append('\n');
            if (plan.optional.Count > 0)
            {
                text.Append("Optional:\n");
                foreach (var objective in plan.optional) text.Append("- ").Append(objective.description).Append('\n');
            }
            return text.ToString();
        }
    }
}
