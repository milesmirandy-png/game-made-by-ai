using UnityEngine;

namespace Swat
{
    public enum AttachmentSlot { Light, Optic, Muzzle, Stock, Underbarrel, Magazine }

    // What an attachment looks like on the gun (WeaponModels builds each one).
    public enum AttachmentLook { None, Light, RedDot, Reflex, Holo, Scope, Suppressor, Compensator, Brake, FlashHider, FixedStock, SkeletonStock, VerticalGrip, AngledGrip, Laser, ExtendedMag, QuickMag }

    // A small, balanced weapon attachment. Effects multiply the weapon's stats.
    [CreateAssetMenu(menuName = "SWAT/Attachment", fileName = "NewAttachment")]
    public class AttachmentData : ScriptableObject
    {
        public string id = "attachment";
        public string displayName = "Attachment";
        [TextArea] public string description;
        public AttachmentSlot slot;
        public float spreadMultiplier = 1f;
        public float recoilMultiplier = 1f;
        public float noiseMultiplier = 1f;
        public float moveMultiplier = 1f;
        public float lightRangeMultiplier = 1f;
        [Tooltip("Magazine capacity multiplier")] public float magazineMultiplier = 1f;
        [Tooltip("Reload time multiplier")] public float reloadMultiplier = 1f;
        [Tooltip("How fast the sights come up (first person) and the gun swings round")] public float aimSpeedMultiplier = 1f;
        [Tooltip("Optic magnification; above 1 aims through a scope in first person")] public float zoom = 1f;
        [Tooltip("Extra camera reach while steady aiming from above (magnified optics)")] public float lookAhead;
        [Tooltip("Muzzle flash size multiplier")] public float flashMultiplier = 1f;
        [Tooltip("Adds a laser: a red aim line from above, a dot in first person")] public bool laser;
        public AttachmentLook look;
        public int unlockAfterMissions;
    }
}
