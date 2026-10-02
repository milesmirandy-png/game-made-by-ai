using UnityEngine;

namespace Swat
{
    public enum EquipmentKind { Flashbang, Smoke, BreachingCharge }

    // Settings for one piece of tactical equipment. Lives in Resources/Equipment.
    [CreateAssetMenu(menuName = "SWAT/Equipment", fileName = "NewEquipment")]
    public class EquipmentData : ScriptableObject
    {
        public string displayName = "Flashbang";
        public int slot;
        public EquipmentKind kind = EquipmentKind.Flashbang;
        public int startingCount = 3;
        [Tooltip("Area of effect in metres")] public float radius = 7f;
        [Tooltip("Seconds from throw/placement until it goes off")] public float fuseTime = 1.5f;
        [Tooltip("How long the effect lasts (stun or smoke), in seconds")] public float effectDuration = 5f;
        public float throwRange = 12f;
        public float noiseRadius = 25f;

        public static EquipmentData Create(string name, int slot, EquipmentKind kind, int count, float radius, float fuse, float duration, float throwRange, float noise)
        {
            var data = CreateInstance<EquipmentData>();
            data.name = name;
            data.displayName = name;
            data.slot = slot;
            data.kind = kind;
            data.startingCount = count;
            data.radius = radius;
            data.fuseTime = fuse;
            data.effectDuration = duration;
            data.throwRange = throwRange;
            data.noiseRadius = noise;
            return data;
        }
    }
}
