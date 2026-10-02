using System.Collections.Generic;

namespace Swat
{
    // Who is deploying on the next mission: the team leader you control and
    // up to three AI squadmates, plus difficulty and the random seed.
    public static class OfficerSelectionManager
    {
        public static MissionData Mission { get; set; }
        public static int Seed { get; set; }
        public static bool KeepSeed { get; set; }

        public static int Difficulty
        {
            get { return SaveManager.Settings.difficulty; }
            set { SaveManager.Settings.difficulty = UnityEngine.Mathf.Clamp(value, 0, 2); }
        }

        public static readonly string[] DifficultyNames = { "Recruit", "Regular", "Veteran" };

        public static int MaxSquad
        {
            get
            {
                return Mission != null ? Mission.maxSquad : 3;
            }
        }

        public static OfficerData Leader
        {
            get
            {
                var leader = GameData.Officer(SaveManager.Progress.leaderId);
                if (leader == null || !Progression.IsAvailable(leader)) leader = GameData.Officer("leader");
                return leader;
            }
        }

        public static List<OfficerData> Squad
        {
            get
            {
                var list = new List<OfficerData>();
                var leader = Leader;
                foreach (var id in SaveManager.Progress.squadIds)
                {
                    var officer = GameData.Officer(id);
                    if (officer == null || officer == leader || !Progression.IsAvailable(officer) || list.Contains(officer)) continue;
                    if (list.Count >= MaxSquad) break;
                    list.Add(officer);
                }
                return list;
            }
        }

        public static void SetLeader(OfficerData officer)
        {
            SaveManager.Progress.leaderId = officer.id;
            SaveManager.Progress.squadIds.Remove(officer.id);
            SaveManager.Save();
        }

        public static bool InSquad(OfficerData officer)
        {
            return Squad.Contains(officer);
        }

        public static void ToggleSquad(OfficerData officer)
        {
            var ids = SaveManager.Progress.squadIds;
            if (ids.Contains(officer.id)) ids.Remove(officer.id);
            else if (officer != Leader)
            {
                // Keep only the ones that are actually in the current squad, then add.
                var current = Squad;
                ids.Clear();
                foreach (var member in current) ids.Add(member.id);
                if (ids.Count >= System.Math.Max(1, MaxSquad)) ids.RemoveAt(0);
                ids.Add(officer.id);
            }
            SaveManager.Save();
        }

        public static int NewSeed()
        {
            Seed = UnityEngine.Random.Range(1, 99999);
            return Seed;
        }
    }
}
