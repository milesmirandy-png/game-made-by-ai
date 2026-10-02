using UnityEngine;

namespace Swat
{
    public enum EnemyArchetype { UnarmedSuspect, Hostile, Guard, Nervous, Armored, Leader, TrainingDummy }

    // Settings for one suspect archetype.
    [CreateAssetMenu(menuName = "SWAT/Enemy", fileName = "NewEnemy")]
    public class EnemyData : ScriptableObject
    {
        public string id = "hostile";
        public string displayName = "Armed Suspect";
        public EnemyArchetype archetype = EnemyArchetype.Hostile;
        public float maxHealth = 100f;
        [Range(0f, 0.8f)] public float damageReduction;
        public float walkSpeed = 2f;
        public float runSpeed = 4f;
        public float detectionRange = 16f;
        public float fieldOfView = 110f;
        [Range(0f, 1f)] public float accuracy = 0.45f;
        public float reactionTime = 0.7f;
        public float weaponDamage = 9f;
        public float fireRate = 3.5f;
        public int burstSize = 3;
        [Range(0f, 1f)] public float surrenderChance = 0.35f;
        [Range(0f, 1f), Tooltip("Chance to run instead of fight when things go badly")] public float fleeChance = 0.15f;
        public bool armed = true;
        [Tooltip("Moves unpredictably")] public bool erratic;
        public bool respondsToAlarms = true;
        [Tooltip("Radius in which this suspect calls friends when it spots police")] public float callForHelpRadius = 12f;
        public Color shirtColor = new Color(0.55f, 0.12f, 0.1f);
        public Color pantsColor = new Color(0.15f, 0.15f, 0.17f);
        [Tooltip("0 hair, 1 cap, 2 balaclava, 3 helmet")] public int headwear = 2;
    }
}
