using UnityEngine;

namespace Swat
{
    // Shared hitscan firing and feedback for every gun in the game (player,
    // squad and suspects): one raycast per projectile, damage, tracer, impact,
    // muzzle flash, sound and the noise the AI can hear.
    public static class WeaponEffects
    {
        static readonly Color Dust = new Color(0.75f, 0.72f, 0.65f);

        // Returns the damageable that was hit (or null).
        public static IDamageable Shoot(Vector3 origin, Vector3 direction, float range, DamageInfo damage, Vector3 muzzle, Color tracer)
        {
            Vector3 end = origin + direction * range;
            IDamageable victim = null;
            RaycastHit hit;
            if (Physics.Raycast(origin, direction, out hit, range, Layers.ShootableMask, QueryTriggerInteraction.Ignore))
            {
                end = hit.point;
                victim = hit.collider.GetComponentInParent<IDamageable>();
                if (victim != null && victim.IsAlive)
                {
                    damage.point = hit.point;
                    damage.direction = direction;
                    victim.TakeDamage(damage);
                    // A neutral "impact" puff rather than anything graphic.
                    EffectsManager.Instance.Burst(hit.point, -direction, new Color(0.85f, 0.85f, 0.85f), 2, 1.5f, 0.05f);
                }
                else
                {
                    victim = null;
                    EffectsManager.Instance.Burst(hit.point, hit.normal, Dust, 3, 2.5f, 0.05f);
                }
            }
            EffectsManager.Instance.SpawnTracer(muzzle, end, tracer);
            return victim;
        }

        public static void MuzzleFlash(Vector3 muzzle, Sound sound, float volume, float noiseRadius, NoiseKind kind)
        {
            EffectsManager.Instance.FlashLight(muzzle, new Color(1f, 0.8f, 0.45f), 2.5f, 6f, 0.06f);
            AudioManager.Play(sound, muzzle, volume, Random.Range(0.94f, 1.06f));
            Noise.Emit(muzzle, noiseRadius, kind);
        }

        // Rotates a direction by a random angle within the spread cone (flat, for top-down aiming).
        public static Vector3 Scatter(Vector3 direction, float spreadDegrees)
        {
            float angle = (Random.value + Random.value - 1f) * spreadDegrees;
            return Quaternion.Euler(0f, angle, 0f) * direction;
        }

        // True if the line of fire is clear of friendly officers and civilians.
        public static bool ClearShot(Vector3 origin, Vector3 target, Transform self)
        {
            Vector3 direction = target - origin;
            RaycastHit hit;
            if (!Physics.Raycast(origin, direction.normalized, out hit, direction.magnitude, Layers.ShootableMask, QueryTriggerInteraction.Ignore)) return true;
            if (self != null && hit.collider.transform.IsChildOf(self)) return true;
            var victim = hit.collider.GetComponentInParent<IDamageable>();
            return victim == null || victim.Team == Team.Suspect;
        }
    }
}
