using UnityEngine;

namespace Swat
{
    public enum FireMode { SemiAuto, FullAuto }
    public enum WeaponCategory { CompactSMG, SMG, CompactRifle, Rifle, Shotgun, Carbine, LessLethal, ServicePistol, BackupPistol, HeavyPistol }

    // Settings for one firearm. The defaults live in DefaultContent; use the
    // menu SWAT > Create Editable Data Assets to get editable copies.
    [CreateAssetMenu(menuName = "SWAT/Weapon", fileName = "NewWeapon")]
    public class WeaponData : ScriptableObject
    {
        public string id = "weapon";
        public string displayName = "Weapon";
        [TextArea] public string description;
        public WeaponCategory category;
        public bool isSidearm;
        public FireMode fireMode = FireMode.SemiAuto;
        public bool canToggleFireMode;
        public float damage = 25f;
        [Tooltip("Shots per second")] public float fireRate = 5f;
        public int magazineSize = 15;
        public int startingReserve = 60;
        public float reloadTime = 1.5f;
        public float range = 30f;
        [Tooltip("Inaccuracy in degrees")] public float spread = 2f;
        [Tooltip("Extra spread in degrees per shot")] public float recoil = 1f;
        public float recoilRecovery = 10f;
        [Tooltip("Projectiles per shot (shotguns fire several)")] public int pellets = 1;
        public float noiseRadius = 22f;
        public float moveSpeedMultiplier = 1f;
        [Tooltip("Seconds to draw this weapon")] public float switchTime = 0.4f;
        [Tooltip("Fires low-damage incapacitating rounds")] public bool lessLethal;
        public float stunDuration;
        [Tooltip("Bit mask of OfficerRole values allowed to carry it. 0 = everyone.")] public int allowedRoles;
        [Tooltip("Completed missions needed to unlock. 0 = available from the start.")] public int unlockAfterMissions;
        public Texture2D icon;
        [Tooltip("Optional model to show instead of the built-in block model")] public GameObject modelPrefab;
        public Sound fireSound = Sound.Pistol;
        public Color tracerColor = new Color(1f, 0.85f, 0.45f);

        public bool AllowedFor(OfficerRole role)
        {
            return allowedRoles == 0 || (allowedRoles & (1 << (int)role)) != 0;
        }
    }
}
