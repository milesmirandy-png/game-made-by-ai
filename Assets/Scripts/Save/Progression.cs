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
        // Training and custom levels are always open; a level stays open once it has been completed.
        public static bool IsAvailable(MissionData item)
        {
            if (item == null) return false;
            if (item.isTraining || item.isCustom || Gate(item.unlockAfterMissions)) return true;
            var record = SaveManager.FindRecord(item.id);
            return record != null && record.completed;
        }

        public static bool IsAvailable(OfficerData officer)
        {
            if (officer == null) return false;
            // The recon officer also unlocks by finishing training.
            if (officer.role == OfficerRole.Recon && SaveManager.Progress.trainingComplete) return true;
            return Gate(officer.unlockAfterMissions);
        }

        // Uniforms are whole kits: shirt, trousers, plate carrier, pouches, helmet and gloves/boots
        // (the blocky figures use the shirt colour; the soldier model wears the whole kit).
        // The last four are camouflage (GearCatalog.CamoFor): the colours below are their average.
        public static readonly string[] UniformNames = { "Navy", "Black", "Urban Gray", "Olive", "Midnight Blue", "Charcoal", "Ranger Green", "Desert Tan", "Woodland", "Gray & Coyote", "Arid Camo", "Woodland Camo", "Urban Camo", "Night Camo" };
        public static readonly float[,] UniformColors =
        {
            { 0.12f, 0.16f, 0.26f }, { 0.07f, 0.07f, 0.08f }, { 0.32f, 0.34f, 0.36f },
            { 0.22f, 0.26f, 0.17f }, { 0.08f, 0.11f, 0.22f }, { 0.18f, 0.18f, 0.2f },
            { 0.2f, 0.27f, 0.15f }, { 0.62f, 0.5f, 0.34f }, { 0.27f, 0.33f, 0.18f }, { 0.4f, 0.41f, 0.43f },
            { 0.6f, 0.52f, 0.37f }, { 0.3f, 0.32f, 0.2f }, { 0.48f, 0.49f, 0.5f }, { 0.13f, 0.13f, 0.14f },
        };
        public static readonly int[] UniformUnlocks = { 0, 0, 1, 2, 3, 4, 0, 1, 2, 3, 0, 1, 2, 3 };

        public struct UniformKit
        {
            public UnityEngine.Color shirt, pants, vest, pouches, helmet, gear;
            public int camo;   // 0 plain, else GearCatalog.CamoFor
        }

        static UnityEngine.Color C(float r, float g, float b) { return new UnityEngine.Color(r, g, b); }

        public static UniformKit Kit(int index)
        {
            index = UnityEngine.Mathf.Clamp(index, 0, UniformNames.Length - 1);
            var shirt = Uniform(index);
            var kit = new UniformKit { shirt = shirt, pants = Shapes.Shade(shirt, 0.82f), vest = C(0.1f, 0.11f, 0.13f), pouches = C(0.15f, 0.16f, 0.18f), helmet = C(0.08f, 0.09f, 0.11f), gear = C(0.07f, 0.075f, 0.085f) };
            switch (UniformNames[index])
            {
                case "Black":
                    kit.pants = C(0.08f, 0.08f, 0.09f); kit.vest = C(0.11f, 0.11f, 0.12f); kit.pouches = C(0.15f, 0.15f, 0.16f); kit.helmet = C(0.09f, 0.09f, 0.1f);
                    break;
                case "Urban Gray":
                    kit.pants = C(0.27f, 0.28f, 0.3f); kit.vest = C(0.2f, 0.21f, 0.23f); kit.pouches = C(0.26f, 0.27f, 0.29f); kit.helmet = C(0.22f, 0.23f, 0.25f);
                    break;
                case "Olive":
                    kit.pants = C(0.19f, 0.22f, 0.14f); kit.vest = C(0.25f, 0.28f, 0.18f); kit.pouches = C(0.3f, 0.33f, 0.21f); kit.helmet = C(0.21f, 0.24f, 0.15f); kit.gear = C(0.17f, 0.14f, 0.1f);
                    break;
                case "Ranger Green":
                    kit.pants = C(0.18f, 0.24f, 0.13f); kit.vest = C(0.24f, 0.31f, 0.18f); kit.pouches = C(0.29f, 0.36f, 0.21f); kit.helmet = C(0.2f, 0.27f, 0.15f); kit.gear = C(0.2f, 0.16f, 0.11f);
                    break;
                case "Desert Tan":
                    kit.pants = C(0.57f, 0.46f, 0.31f); kit.vest = C(0.49f, 0.36f, 0.21f); kit.pouches = C(0.56f, 0.42f, 0.26f); kit.helmet = C(0.47f, 0.35f, 0.21f); kit.gear = C(0.38f, 0.27f, 0.16f);
                    break;
                case "Woodland":
                    kit.pants = C(0.42f, 0.4f, 0.28f); kit.vest = C(0.45f, 0.35f, 0.22f); kit.pouches = C(0.51f, 0.4f, 0.26f); kit.helmet = C(0.26f, 0.31f, 0.17f); kit.gear = C(0.27f, 0.2f, 0.12f);
                    break;
                case "Gray & Coyote":
                    kit.pants = C(0.24f, 0.28f, 0.17f); kit.vest = C(0.5f, 0.38f, 0.24f); kit.pouches = C(0.56f, 0.43f, 0.27f); kit.helmet = C(0.46f, 0.36f, 0.23f); kit.gear = C(0.3f, 0.22f, 0.14f);
                    break;
                case "Arid Camo":
                    kit.camo = 1; kit.pants = shirt; kit.vest = C(0.5f, 0.39f, 0.25f); kit.pouches = C(0.56f, 0.44f, 0.28f); kit.helmet = C(0.52f, 0.42f, 0.28f); kit.gear = C(0.36f, 0.27f, 0.17f);
                    break;
                case "Woodland Camo":
                    kit.camo = 2; kit.pants = shirt; kit.vest = C(0.24f, 0.29f, 0.17f); kit.pouches = C(0.29f, 0.34f, 0.2f); kit.helmet = C(0.22f, 0.27f, 0.15f); kit.gear = C(0.22f, 0.17f, 0.11f);
                    break;
                case "Urban Camo":
                    kit.camo = 3; kit.pants = shirt; kit.vest = C(0.22f, 0.23f, 0.25f); kit.pouches = C(0.28f, 0.29f, 0.31f); kit.helmet = C(0.25f, 0.26f, 0.28f);
                    break;
                case "Night Camo":
                    kit.camo = 4; kit.pants = shirt; kit.vest = C(0.1f, 0.1f, 0.11f); kit.pouches = C(0.14f, 0.14f, 0.15f); kit.helmet = C(0.09f, 0.09f, 0.1f);
                    break;
            }
            return kit;
        }
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
