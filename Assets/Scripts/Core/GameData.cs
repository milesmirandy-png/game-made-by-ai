using System.Collections.Generic;
using UnityEngine;

namespace Swat
{
    // Loads weapon, equipment and enemy settings from ScriptableObject assets in
    // Resources folders. If an asset is missing, built-in defaults are used so
    // the game always runs.
    public static class GameData
    {
        static List<WeaponData> weapons;
        static List<EquipmentData> equipment;
        static Dictionary<string, EnemyData> enemies;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            weapons = null;
            equipment = null;
            enemies = null;
        }

        public static List<WeaponData> Weapons
        {
            get
            {
                if (weapons == null)
                {
                    weapons = new List<WeaponData>(Resources.LoadAll<WeaponData>("Weapons"));
                    if (weapons.Count == 0)
                    {
                        weapons.Add(WeaponData.Create("Pistol", 0, FireMode.SemiAuto, 34f, 5f, 15, 60, 1.3f, 30f, 2f, 1.5f, 1, 20f, 1f, 0.28f, Sound.Pistol));
                        weapons.Add(WeaponData.Create("SMG", 1, FireMode.FullAuto, 18f, 12f, 30, 120, 1.8f, 25f, 4f, 0.8f, 1, 20f, 1f, 0.42f, Sound.Smg));
                        weapons.Add(WeaponData.Create("Assault Rifle", 2, FireMode.FullAuto, 30f, 9f, 30, 90, 2.2f, 45f, 1.5f, 1.1f, 1, 28f, 0.92f, 0.6f, Sound.Rifle));
                        weapons.Add(WeaponData.Create("Shotgun", 3, FireMode.SemiAuto, 14f, 1.4f, 7, 28, 2.6f, 14f, 9f, 3f, 8, 28f, 0.95f, 0.55f, Sound.Shotgun));
                    }
                    weapons.Sort((a, b) => a.slot.CompareTo(b.slot));
                }
                return weapons;
            }
        }

        public static List<EquipmentData> Equipment
        {
            get
            {
                if (equipment == null)
                {
                    equipment = new List<EquipmentData>(Resources.LoadAll<EquipmentData>("Equipment"));
                    if (equipment.Count == 0)
                    {
                        equipment.Add(EquipmentData.Create("Flashbang", 0, EquipmentKind.Flashbang, 3, 7f, 1.4f, 5f, 12f, 25f));
                        equipment.Add(EquipmentData.Create("Smoke Grenade", 1, EquipmentKind.Smoke, 2, 4f, 1.2f, 14f, 12f, 8f));
                        equipment.Add(EquipmentData.Create("Breaching Charge", 2, EquipmentKind.BreachingCharge, 3, 3.5f, 2.5f, 3.5f, 0f, 30f));
                    }
                    equipment.Sort((a, b) => a.slot.CompareTo(b.slot));
                }
                return equipment;
            }
        }

        public static EnemyData Enemy(string id)
        {
            if (enemies == null)
            {
                enemies = new Dictionary<string, EnemyData>();
                foreach (var data in Resources.LoadAll<EnemyData>("Enemies")) enemies[data.name] = data;
                if (!enemies.ContainsKey("Suspect")) enemies["Suspect"] = EnemyData.Create("Suspect", 100f, 2f, 4f, 16f, 110f, 0.45f, 0.7f, 9f, 3.5f, 3, 0.35f);
                if (!enemies.ContainsKey("Heavy")) enemies["Heavy"] = EnemyData.Create("Heavy", 180f, 1.7f, 3.4f, 15f, 100f, 0.5f, 0.6f, 14f, 2f, 2, 0.15f);
            }
            EnemyData result;
            return enemies.TryGetValue(id, out result) ? result : enemies["Suspect"];
        }
    }
}
