using UnityEngine;

namespace Swat
{
    // Body armor. One simple number for protection, one for speed, one for carrying capacity.
    [CreateAssetMenu(menuName = "SWAT/Armor", fileName = "NewArmor")]
    public class ArmorData : ScriptableObject
    {
        public string id = "armor";
        public string displayName = "Armor";
        [TextArea] public string description;
        public int tier = 1;
        [Range(0f, 0.8f)] public float damageReduction = 0.3f;
        public float speedMultiplier = 1f;
        public int capacityBonus;
        [Tooltip("Armor condition; protection weakens as it takes hits")] public float durability = 100f;
        public bool helmet = true;
        public Color color = new Color(0.08f, 0.09f, 0.11f);
        public int unlockAfterMissions;
    }
}
