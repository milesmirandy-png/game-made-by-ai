using UnityEngine;

namespace Swat
{
    // Smoke grenade: a temporary cloud that blocks suspects' line of sight
    // (see AIVisibility) and hides whoever stands inside it.
    public static class SmokeGrenade
    {
        public static void Detonate(Vector3 position, EquipmentData data)
        {
            SmokeCloud.Spawn(position, data.radius, data.effectDuration);
            Noise.Emit(position, data.noiseRadius, NoiseKind.Door);
        }
    }
}
