using UnityEngine;

namespace Swat
{
    public enum EquipmentKind { Flashbang, Smoke, BreachingCharge, DoorWedge, MedicalKit, PortableLight, ReconCamera, CSGas, ChemLight }

    // One type of tactical equipment. Each officer carries a limited amount,
    // limited by equipment capacity (capacityCost per item).
    [CreateAssetMenu(menuName = "SWAT/Equipment", fileName = "NewEquipment")]
    public class EquipmentData : ScriptableObject
    {
        public string id = "equipment";
        public string displayName = "Equipment";
        [TextArea] public string description;
        public EquipmentKind kind;
        public int capacityCost = 1;
        public int maxCarry = 4;
        [Tooltip("Area of effect / light range in metres")] public float radius = 6f;
        [Tooltip("Seconds until it goes off")] public float fuseTime = 1.5f;
        [Tooltip("How long the effect lasts")] public float effectDuration = 5f;
        public float throwRange = 12f;
        public float noiseRadius = 20f;
        public float healAmount;
        [Tooltip("Seconds to use (hold)")] public float useTime;
        [Tooltip("False for reusable tools like the recon camera")] public bool consumable = true;
        public int unlockAfterMissions;
    }
}
