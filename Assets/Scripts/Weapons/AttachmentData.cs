using UnityEngine;

namespace Swat
{
    public enum AttachmentSlot { Light, Optic, Muzzle, Stock }

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
        public int unlockAfterMissions;
    }
}
