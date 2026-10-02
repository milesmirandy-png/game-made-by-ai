using UnityEngine;

namespace Swat
{
    // What happens when tactical equipment goes off.
    public static class Equipment
    {
        // Disorients every suspect and civilian in range who isn't behind a wall.
        // Officers get dazzled too, so don't stand next to one.
        public static void Flashbang(Vector3 position, EquipmentData data)
        {
            Vector3 center = position + Vector3.up * 0.5f;
            EffectsManager.Instance.FlashLight(center, Color.white, 8f, data.radius * 2.5f, 0.3f);
            EffectsManager.Instance.Burst(center, Vector3.up, Color.white, 10, 4f, 0.05f);
            AudioManager.Play(Sound.Flashbang, center, 1f);
            Noise.Emit(center, data.noiseRadius, NoiseKind.Explosion);
            AIManager.Instance.Stun(center, data.radius, data.effectDuration);

            var game = GameManager.Instance;
            var player = game.Player;
            if (player == null) return;
            float reach = data.radius * 1.3f;
            float distance = Vector3.Distance(center, player.ChestPosition);
            if (distance < reach && !Physics.Linecast(center, player.ChestPosition, Layers.WorldMask, QueryTriggerInteraction.Ignore))
                player.Health.Flashbang(1f - distance / reach);
            game.CameraRig.Shake(0.3f);
        }
    }
}
