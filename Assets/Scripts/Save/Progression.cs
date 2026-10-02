using System.Collections.Generic;

namespace Swat
{
    // Light progression: everything core is available from the start; a few
    // extras (attachments, two officers, uniforms, one mission) unlock as you
    // complete missions. Nothing requires grinding.
    public static class Progression
    {
        public static int MissionsCompleted { get { return SaveManager.Progress.missionsCompleted; } }

        static bool Gate(int required) { return MissionsCompleted >= required; }
        public static bool IsAvailable(WeaponData item) { return item != null && Gate(item.unlockAfterMissions); }
        public static bool IsAvailable(ArmorData item) { return item != null && Gate(item.unlockAfterMissions); }
        public static bool IsAvailable(AttachmentData item) { return item != null && Gate(item.unlockAfterMissions); }
        public static bool IsAvailable(EquipmentData item) { return item != null && Gate(item.unlockAfterMissions); }
        public static bool IsAvailable(MissionData item) { return item != null && (item.isTraining || Gate(item.unlockAfterMissions)); }

        public static bool IsAvailable(OfficerData officer)
        {
            if (officer == null) return false;
            // The recon officer also unlocks by finishing training.
            if (officer.role == OfficerRole.Recon && SaveManager.Progress.trainingComplete) return true;
            return Gate(officer.unlockAfterMissions);
        }

        public static readonly string[] UniformNames = { "Navy", "Black", "Urban Gray", "Olive", "Midnight Blue", "Charcoal" };
        public static readonly float[,] UniformColors =
        {
            { 0.12f, 0.16f, 0.26f }, { 0.07f, 0.07f, 0.08f }, { 0.32f, 0.34f, 0.36f },
            { 0.22f, 0.26f, 0.17f }, { 0.08f, 0.11f, 0.22f }, { 0.18f, 0.18f, 0.2f },
        };
        public static readonly int[] UniformUnlocks = { 0, 0, 1, 2, 3, 4 };
        public static bool UniformAvailable(int index) { return index >= 0 && index < UniformUnlocks.Length && Gate(UniformUnlocks[index]); }

        public static UnityEngine.Color Uniform(int index)
        {
            index = UnityEngine.Mathf.Clamp(index, 0, UniformNames.Length - 1);
            return new UnityEngine.Color(UniformColors[index, 0], UniformColors[index, 1], UniformColors[index, 2]);
        }

        // Things that became available because missionsCompleted went from 'before' to 'after'.
        public static List<string> NewUnlocks(int before, int after, bool trainingJustCompleted)
        {
            var list = new List<string>();
            if (after <= before && !trainingJustCompleted) return list;
            foreach (var w in GameData.AllWeapons) if (w.unlockAfterMissions > before && w.unlockAfterMissions <= after) list.Add("Weapon: " + w.displayName);
            foreach (var a in GameData.AllAttachments) if (a.unlockAfterMissions > before && a.unlockAfterMissions <= after) list.Add("Attachment: " + a.displayName);
            foreach (var a in GameData.AllArmor) if (a.unlockAfterMissions > before && a.unlockAfterMissions <= after) list.Add("Armor: " + a.displayName);
            foreach (var e in GameData.AllEquipment) if (e.unlockAfterMissions > before && e.unlockAfterMissions <= after) list.Add("Equipment: " + e.displayName);
            foreach (var o in GameData.AllOfficers)
            {
                bool viaTraining = o.role == OfficerRole.Recon && trainingJustCompleted;
                bool viaMissions = o.unlockAfterMissions > before && o.unlockAfterMissions <= after;
                if ((viaTraining || viaMissions) && !list.Contains("Officer: " + o.displayName)) list.Add("Officer: " + o.displayName);
            }
            foreach (var m in GameData.AllMissions) if (!m.isTraining && m.unlockAfterMissions > before && m.unlockAfterMissions <= after) list.Add("Mission: " + m.displayName);
            for (int i = 0; i < UniformUnlocks.Length; i++) if (UniformUnlocks[i] > before && UniformUnlocks[i] <= after) list.Add("Uniform: " + UniformNames[i]);
            return list;
        }
    }
}
