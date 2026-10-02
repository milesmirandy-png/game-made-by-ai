using System.Collections.Generic;
using UnityEngine;

namespace Swat
{
    // Registry for all game content. Starts from DefaultContent and replaces
    // any entry for which an edited ScriptableObject asset (same id) exists in
    // a Resources/SWAT/... folder, so designers can tune numbers in the Inspector.
    public static class GameData
    {
        static bool loaded;
        static readonly List<WeaponData> weapons = new List<WeaponData>();
        static readonly List<AttachmentData> attachments = new List<AttachmentData>();
        static readonly List<ArmorData> armor = new List<ArmorData>();
        static readonly List<EquipmentData> equipment = new List<EquipmentData>();
        static readonly List<OfficerData> officers = new List<OfficerData>();
        static readonly List<EnemyData> enemies = new List<EnemyData>();
        static readonly List<MissionData> missions = new List<MissionData>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            loaded = false;
        }

        static void EnsureLoaded()
        {
            if (loaded) return;
            loaded = true;
            Merge(weapons, DefaultContent.Weapons(), Resources.LoadAll<WeaponData>("SWAT/Weapons"), w => w.id);
            Merge(attachments, DefaultContent.Attachments(), Resources.LoadAll<AttachmentData>("SWAT/Attachments"), a => a.id);
            Merge(armor, DefaultContent.ArmorList(), Resources.LoadAll<ArmorData>("SWAT/Armor"), a => a.id);
            Merge(equipment, DefaultContent.EquipmentList(), Resources.LoadAll<EquipmentData>("SWAT/Equipment"), e => e.id);
            Merge(officers, DefaultContent.Officers(), Resources.LoadAll<OfficerData>("SWAT/Officers"), o => o.id);
            Merge(enemies, DefaultContent.Enemies(), Resources.LoadAll<EnemyData>("SWAT/Enemies"), e => e.id);
            Merge(missions, DefaultContent.Missions(), Resources.LoadAll<MissionData>("SWAT/Missions"), m => m.id);
            missions.Sort((a, b) => a.sortOrder.CompareTo(b.sortOrder));
        }

        static void Merge<T>(List<T> target, List<T> defaults, T[] assets, System.Func<T, string> id) where T : Object
        {
            target.Clear();
            target.AddRange(defaults);
            foreach (var asset in assets)
            {
                if (asset == null || string.IsNullOrEmpty(id(asset))) continue;
                int index = target.FindIndex(existing => id(existing) == id(asset));
                if (index >= 0) target[index] = asset;
                else target.Add(asset);
            }
        }

        static T Find<T>(List<T> list, string key, System.Func<T, string> id) where T : Object
        {
            EnsureLoaded();
            if (string.IsNullOrEmpty(key)) return null;
            foreach (var item in list)
                if (id(item) == key) return item;
            return null;
        }

        public static IList<WeaponData> AllWeapons { get { EnsureLoaded(); return weapons; } }
        public static IList<AttachmentData> AllAttachments { get { EnsureLoaded(); return attachments; } }
        public static IList<ArmorData> AllArmor { get { EnsureLoaded(); return armor; } }
        public static IList<EquipmentData> AllEquipment { get { EnsureLoaded(); return equipment; } }
        public static IList<OfficerData> AllOfficers { get { EnsureLoaded(); return officers; } }
        public static IList<EnemyData> AllEnemies { get { EnsureLoaded(); return enemies; } }
        public static IList<MissionData> AllMissions { get { EnsureLoaded(); return missions; } }

        public static WeaponData Weapon(string id) { return Find(weapons, id, w => w.id); }
        public static AttachmentData Attachment(string id) { return Find(attachments, id, a => a.id); }
        public static ArmorData Armor(string id) { return Find(armor, id, a => a.id); }
        public static EquipmentData Equipment(string id) { return Find(equipment, id, e => e.id); }
        public static OfficerData Officer(string id) { return Find(officers, id, o => o.id); }
        public static MissionData Mission(string id) { return Find(missions, id, m => m.id); }

        public static EnemyData Enemy(string id)
        {
            return Find(enemies, id, e => e.id) ?? Find(enemies, "hostile", e => e.id);
        }

        // The saved loadout for an officer, created from their defaults if needed and always valid.
        public static OfficerLoadout LoadoutFor(OfficerData officer)
        {
            var progress = SaveManager.Progress;
            OfficerLoadout loadout = null;
            foreach (var saved in progress.loadouts)
                if (saved.officerId == officer.id) loadout = saved;
            if (loadout == null)
            {
                loadout = officer.defaultLoadout.Clone();
                loadout.officerId = officer.id;
                progress.loadouts.Add(loadout);
            }
            LoadoutRules.Validate(officer, loadout);
            return loadout;
        }
    }
}
