using UnityEngine;

namespace Swat
{
    public enum Team { Police, Suspect, Civilian, Environment }

    // Where a round lands. Body armor covers the torso only; a helmet the head.
    public enum HitZone { Torso, Head, Arm, Leg }

    // SWAT 4-style ammunition, picked per officer in the loadout: full metal jacket goes through
    // armor and thin cover; hollow points hit harder against unprotected targets but are stopped
    // by armor and don't go through anything.
    public enum AmmoType { FMJ, JHP }

    public struct DamageInfo
    {
        public float amount;
        public Vector3 point;
        public Vector3 direction;
        public Team attacker;
        public bool lessLethal;
        public float stun;
        public WeaponData weapon;   // optional: what fired the shot (kill feed, less-lethal effects)
        public bool byPlayer;       // the shot came from the player's own gun
        public MonoBehaviour shooter; // optional: who fired (game modes credit takedowns with it)
        public bool zoned;          // a bullet that hit a body part (zone and ammo below apply); false for melee, blasts, falls
        public HitZone zone;
        public AmmoType ammo;
        public bool penetrated;     // came through a door or a wall first (weaker)
    }

    // Hit zones, armor and penetration (shared by every character type).
    public static class Ballistics
    {
        public static readonly string[] AmmoNames = { "FMJ (full metal jacket)", "JHP (hollow point)" };
        public static readonly string[] AmmoShort = { "FMJ", "JHP" };

        // Which part of a standing or crouching body a point on its collider is: the top seventh is the
        // head, below the hips the legs, and out to the side at chest height an arm.
        public static HitZone ZoneOf(Collider collider, Transform body, Vector3 point)
        {
            var bounds = collider.bounds;
            float t = bounds.size.y > 0.01f ? (point.y - bounds.min.y) / bounds.size.y : 0.5f;
            if (t > 0.86f) return HitZone.Head;
            if (t < 0.46f) return HitZone.Leg;
            if (body != null && Mathf.Abs(body.InverseTransformPoint(point).x) > 0.2f && t < 0.8f) return HitZone.Arm;
            return HitZone.Torso;
        }

        // How hard a hit lands on each part, before armor.
        public static float ZoneMultiplier(HitZone zone)
        {
            switch (zone)
            {
                case HitZone.Head: return 2.2f;
                case HitZone.Arm: return 0.55f;
                case HitZone.Leg: return 0.65f;
                default: return 1f;
            }
        }

        // How much of a hit armor stops, given how much it stops of a standard round.
        public static float ArmorStops(float armor, AmmoType ammo)
        {
            if (armor <= 0f) return 0f;
            return Mathf.Clamp(ammo == AmmoType.FMJ ? armor * 0.6f : armor * 1.35f, 0f, 0.9f);
        }

        // Hollow points against something unprotected.
        public static float UnarmoredBonus(AmmoType ammo) { return ammo == AmmoType.JHP ? 1.2f : 1f; }

        // What a round goes through: 0 nothing, 1 doors, 2 doors and thin interior walls.
        public static int Penetration(WeaponData weapon, AmmoType ammo)
        {
            if (weapon == null || ammo == AmmoType.JHP || weapon.lessLethal || weapon.pellets > 1 || weapon.blastRadius > 0f) return 0;
            switch (weapon.category)
            {
                case WeaponCategory.Rifle: case WeaponCategory.CompactRifle: case WeaponCategory.BurstRifle: case WeaponCategory.Bullpup:
                case WeaponCategory.Marksman: case WeaponCategory.LMG: case WeaponCategory.Carbine: case WeaponCategory.Rotary:
                    return 2;
                case WeaponCategory.Pepperball: case WeaponCategory.StunPistol: case WeaponCategory.LessLethal: case WeaponCategory.GrenadeLauncher:
                    return 0;
                default:
                    return 1;
            }
        }
    }

    public interface IDamageable
    {
        Team Team { get; }
        bool IsAlive { get; }
        void TakeDamage(DamageInfo info);
    }

    // Anything the player can use with E: doors, civilians, suspects, consoles...
    public interface IInteractable
    {
        string Prompt { get; }
        Vector3 InteractPosition { get; }
        float InteractDuration(PlayerController player); // 0 = instant, otherwise hold E
        bool CanInteract(PlayerController player);
        void Interact(PlayerController player);
    }

    // A police officer (the player or an AI squadmate) as seen by suspects and cameras.
    public interface ICombatTarget
    {
        Transform Transform { get; }
        Vector3 Position { get; }
        Vector3 ChestPosition { get; }
        bool IsAlive { get; }
        bool IsMoving { get; }
        bool IsCrouched { get; }
        bool FlashlightOn { get; }
        IDamageable Damageable { get; }
    }
}
