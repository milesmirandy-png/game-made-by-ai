using UnityEngine;

namespace Swat
{
    public enum OfficerRole { Leader, Shield, Breacher, Medic, Recon, Tactical }

    // A member of the SWAT roster: identity, role, statistics and default kit.
    [CreateAssetMenu(menuName = "SWAT/Officer", fileName = "NewOfficer")]
    public class OfficerData : ScriptableObject
    {
        public string id = "officer";
        public string displayName = "Officer";
        public string callsign = "Unit";
        public OfficerRole role;
        [TextArea] public string personality;
        public float maxHealth = 100f;
        [Range(0f, 0.2f), Tooltip("Extra damage reduction on top of armor")] public float armorRating;
        public float moveSpeed = 1f;
        [Range(0f, 1f)] public float accuracy = 0.65f;
        public float reactionTime = 0.4f;
        public float perceptionRange = 18f;
        [Tooltip("Multiplier on how quickly orders are carried out (higher is faster)")] public float commandResponsiveness = 1f;
        public int equipmentCapacity = 8;
        public string abilityName = "Ability";
        [TextArea] public string abilityDescription;
        public float abilityCooldown = 30f;
        public Color uniformColor = new Color(0.12f, 0.16f, 0.26f);
        public int skinTone;
        public int unlockAfterMissions;
        public OfficerLoadout defaultLoadout = new OfficerLoadout();
    }
}
