using UnityEngine;

namespace Swat
{
    public enum FireMode { SemiAuto, FullAuto }

    // Settings for one gun. Create more with Assets > Create > SWAT > Weapon and
    // put them in a Resources/Weapons folder; the player picks them up automatically.
    [CreateAssetMenu(menuName = "SWAT/Weapon", fileName = "NewWeapon")]
    public class WeaponData : ScriptableObject
    {
        public string displayName = "Pistol";
        [Tooltip("Order in the player's loadout (number key)")] public int slot;
        public FireMode fireMode = FireMode.SemiAuto;
        public float damage = 30f;
        [Tooltip("Shots per second")] public float fireRate = 4f;
        public int magazineSize = 15;
        public int startingReserve = 60;
        public float reloadTime = 1.4f;
        public float range = 35f;
        [Tooltip("Inaccuracy in degrees")] public float spread = 2.5f;
        [Tooltip("Extra spread in degrees added by each shot")] public float recoil = 1.5f;
        [Tooltip("How fast recoil spread recovers, degrees per second")] public float recoilRecovery = 10f;
        [Tooltip("Bullets per shot (shotguns fire several)")] public int pellets = 1;
        [Tooltip("How far away suspects hear it")] public float noiseRadius = 22f;
        public float moveSpeedMultiplier = 1f;
        public float modelLength = 0.3f;
        public Sound fireSound = Sound.Pistol;
        public Color tracerColor = new Color(1f, 0.85f, 0.45f);

        public static WeaponData Create(string name, int slot, FireMode mode, float damage, float fireRate, int magazine, int reserve,
            float reload, float range, float spread, float recoil, int pellets, float noise, float speed, float length, Sound sound)
        {
            var data = CreateInstance<WeaponData>();
            data.name = name;
            data.displayName = name;
            data.slot = slot;
            data.fireMode = mode;
            data.damage = damage;
            data.fireRate = fireRate;
            data.magazineSize = magazine;
            data.startingReserve = reserve;
            data.reloadTime = reload;
            data.range = range;
            data.spread = spread;
            data.recoil = recoil;
            data.pellets = pellets;
            data.noiseRadius = noise;
            data.moveSpeedMultiplier = speed;
            data.modelLength = length;
            data.fireSound = sound;
            return data;
        }
    }
}
