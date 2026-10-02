using UnityEngine;

namespace Swat
{
    // Gunshot is police gunfire; EnemyGunshot is a suspect's.
    public enum NoiseKind { Footstep, Door, Gunshot, EnemyGunshot, Explosion, Callout, Alarm }

    // Sounds the AI can hear. Anything can make a noise; the AI manager decides who hears it.
    public static class Noise
    {
        public static void Emit(Vector3 position, float radius, NoiseKind kind)
        {
            var ai = AIManager.Instance;
            if (ai != null) ai.HearNoise(position, radius, kind);
        }
    }
}
