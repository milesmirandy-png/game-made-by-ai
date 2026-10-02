using UnityEngine;

namespace Swat
{
    // A flashbang going off: disorients suspects and civilians in range who
    // aren't behind a wall (line of sight from the blast). Squadmates and the
    // player get dazzled too if they are too close.
    public static class Flashbang
    {
        public static void Detonate(Vector3 position, EquipmentData data, float power, bool byPlayer)
        {
            Vector3 center = position + Vector3.up * 0.5f;
            float radius = data.radius * Mathf.Lerp(1f, power, 0.5f);
            float duration = data.effectDuration * power;
            EffectsManager.Instance.FlashLight(center, Color.white, 8f, radius * 2.5f, 0.3f);
            EffectsManager.Instance.Burst(center, Vector3.up, Color.white, 10, 4f, 0.05f);
            AudioManager.Play(Sound.Flashbang, center, 1f);
            Noise.Emit(center, data.noiseRadius, NoiseKind.Explosion);
            AIManager.Instance.Stun(center, radius, duration, true);

            var game = GameManager.Instance;
            var room = game.Level != null ? game.Level.RoomAt(position) : null;
            if (room != null) MissionManager.Instance.ReportTarget(ObjectiveType.TrainingFlashbang, room.Id);

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
