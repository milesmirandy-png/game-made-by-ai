using UnityEngine;

namespace Swat
{
    // Settings for one type of suspect. Lives in Resources/Enemies; the level
    // asks for a profile by asset name (for example "Suspect" or "Heavy").
    [CreateAssetMenu(menuName = "SWAT/Enemy", fileName = "NewEnemy")]
    public class EnemyData : ScriptableObject
    {
        public float maxHealth = 100f;
        public float walkSpeed = 2f;
        public float runSpeed = 4f;
        public float detectionRange = 16f;
        [Tooltip("Field of view in degrees")] public float fieldOfView = 110f;
        [Range(0f, 1f), Tooltip("Chance to hit at close range")] public float accuracy = 0.45f;
        [Tooltip("Seconds between spotting you and reacting")] public float reactionTime = 0.7f;
        public float weaponDamage = 9f;
        [Tooltip("Shots per second during a burst")] public float fireRate = 3.5f;
        public int burstSize = 3;
        [Range(0f, 1f), Tooltip("Base chance to give up when an officer shouts")] public float surrenderChance = 0.35f;
        public Color shirtColor = new Color(0.55f, 0.12f, 0.1f);

        public static EnemyData Create(string name, float health, float walk, float run, float range, float fov, float accuracy,
            float reaction, float damage, float fireRate, int burst, float surrender)
        {
            var data = CreateInstance<EnemyData>();
            data.name = name;
            data.maxHealth = health;
            data.walkSpeed = walk;
            data.runSpeed = run;
            data.detectionRange = range;
            data.fieldOfView = fov;
            data.accuracy = accuracy;
            data.reactionTime = reaction;
            data.weaponDamage = damage;
            data.fireRate = fireRate;
            data.burstSize = burst;
            data.surrenderChance = surrender;
            if (name == "Heavy") data.shirtColor = new Color(0.25f, 0.25f, 0.28f);
            return data;
        }
    }
}
