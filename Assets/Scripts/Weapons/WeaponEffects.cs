using UnityEngine;

namespace Swat
{
    // Shared hitscan firing and feedback for every gun in the game (player,
    // squad and suspects): one raycast per projectile, damage, tracer, impact,
    // muzzle flash, sound and the noise the AI can hear.
    public static class WeaponEffects
    {
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
                    // A neutral "impact" puff and a white spark rather than anything graphic.
                    EffectsManager.Instance.Burst(hit.point, -direction, new Color(0.85f, 0.85f, 0.85f), 2, 1.5f, 0.05f);
                    EffectsManager.Instance.HitSpark(hit.point - direction * 0.15f, damage.byPlayer ? 0.5f : 0.35f);
                    // Hits shove the target a little (more for heavy weapons).
                    var mover = hit.collider.GetComponentInParent<AgentMover>();
                    if (mover != null && damage.amount > 1f) mover.Nudge(direction * Mathf.Clamp(damage.amount * 0.004f, 0.03f, 0.22f));
                }
                else
                {
                    victim = null;
                    // Doors swing, so they get sparks or splinters but no lasting mark.
                    bool door = hit.collider.GetComponentInParent<DoorController>() != null;
                    EffectsManager.Instance.Impact(hit.point, hit.normal, SurfaceTag.Of(hit.collider), !door);
                }
            }
            float width = damage.weapon != null ? damage.weapon.tracerWidth : 0.04f;
            EffectsManager.Instance.SpawnTracer(muzzle, end, tracer, width, 0.06f);
            if (damage.attacker != Team.Police) NearMiss(origin, end, victim);
            return victim;
        }

        // Shots that pass close to the player without hitting make a sharp whiz.
        static void NearMiss(Vector3 from, Vector3 to, IDamageable victim)
        {
            var game = GameManager.Instance;
            var player = game != null ? game.Player : null;
            if (player == null || !player.IsAlive || victim == (IDamageable)player.Health) return;
            Vector3 line = to - from;
            float length = line.magnitude;
            if (length < 0.5f) return;
            Vector3 dir = line / length;
            float along = Mathf.Clamp(Vector3.Dot(player.ChestPosition - from, dir), 0f, length);
            if (along < 2f) return;
            float miss = Vector3.Distance(from + dir * along, player.ChestPosition);
            if (miss < 1.3f) AudioManager.Play2D(Sound.Whiz, Mathf.Lerp(0.5f, 0.2f, miss / 1.3f), Random.Range(0.9f, 1.15f), SoundCategory.Weapons);
        }

        // Everything that happens at the gun when it fires: flame and star, light flash,
        // the weapon's own sound, and the noise the AI can hear.
        public static void Fired(Transform muzzle, WeaponData weapon, float volume, float noiseRadius, NoiseKind kind, float pitch = 1f)
        {
            if (muzzle == null) return;
            Vector3 position = muzzle.position;
            float size = weapon != null ? weapon.flashSize : 0.5f;
            bool lessLethal = weapon != null && weapon.lessLethal;
            Color color = lessLethal ? new Color(1f, 0.85f, 0.7f) : new Color(1f, 0.82f, 0.42f);
            if (size > 0.3f || !lessLethal) EffectsManager.Instance.MuzzleBurst(position, muzzle.forward, size, color);
            EffectsManager.Instance.FlashLight(position, new Color(1f, 0.8f, 0.45f), 2.2f + size * 2f, 5f + size * 3f, 0.05f);
            Sound sound = weapon != null ? weapon.fireSound : Sound.EnemyShot;
            AudioManager.Play(sound, position, volume, Random.Range(0.95f, 1.05f) * pitch, SoundCategory.Weapons);
            Noise.Emit(position, noiseRadius, kind);
        }

        public static void MuzzleFlash(Vector3 muzzle, Sound sound, float volume, float noiseRadius, NoiseKind kind)
        {
            EffectsManager.Instance.FlashLight(muzzle, new Color(1f, 0.8f, 0.45f), 2.5f, 6f, 0.06f);
            // A brief bright flash at the muzzle, visible on every tier (the light above needs dynamic lights).
            EffectsManager.Instance.Burst(muzzle, Vector3.up, new Color(1f, 0.85f, 0.5f), 2, 0.6f, 0.08f, 2f);
            AudioManager.Play(sound, muzzle, volume, Random.Range(0.94f, 1.06f), SoundCategory.Weapons);
            Noise.Emit(muzzle, noiseRadius, kind);
        }

        public static void EjectShell(Vector3 gun, Vector3 right, bool shotgun)
        {
            EffectsManager.Instance.Shell(gun + right * 0.08f + Vector3.up * 0.05f, right, shotgun);
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
            return ClearShot(origin, target, self, Team.Police);
        }

        // Same for any side: nothing between the shooter and the target except opponents.
        public static bool ClearShot(Vector3 origin, Vector3 target, Transform self, Team shooter)
        {
            Vector3 direction = target - origin;
            RaycastHit hit;
            if (!Physics.Raycast(origin, direction.normalized, out hit, direction.magnitude, Layers.ShootableMask, QueryTriggerInteraction.Ignore)) return true;
            if (self != null && hit.collider.transform.IsChildOf(self)) return true;
            var victim = hit.collider.GetComponentInParent<IDamageable>();
            if (victim == null) return true;
            return shooter == Team.Police ? victim.Team == Team.Suspect : victim.Team == Team.Police;
        }
    }
}
