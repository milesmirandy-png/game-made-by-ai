using UnityEngine;

namespace Swat
{
    // A suspect's gun. Hitscan shots in short bursts; the chance to hit drops
    // with distance, darkness, smoke, and if the target is moving or crouched.
    public class EnemyWeapon : MonoBehaviour
    {
        EnemyData data;
        Transform muzzle;
        float nextShot, accuracyMultiplier = 1f;
        int burstLeft;

        public void Init(EnemyData profile, Transform muzzlePoint, float difficultyAccuracy)
        {
            data = profile;
            muzzle = muzzlePoint;
            accuracyMultiplier = difficultyAccuracy;
        }

        public void ResetBurst()
        {
            burstLeft = 0;
            nextShot = Mathf.Max(nextShot, Time.time + 0.15f);
        }

        public void TryFire(ICombatTarget target)
        {
            if (Time.time < nextShot || !data.armed) return;
            if (burstLeft <= 0) burstLeft = Mathf.Max(1, data.burstSize);
            Fire(target);
            burstLeft--;
            nextShot = Time.time + (burstLeft > 0 ? 1f / Mathf.Max(0.5f, data.fireRate) : Random.Range(0.7f, 1.2f));
        }

        void Fire(ICombatTarget target)
        {
            Vector3 origin = transform.position + Vector3.up * 1.2f;
            Vector3 aim = target.ChestPosition;
            float distance = Vector3.Distance(origin, aim);

            float chance = data.accuracy * accuracyMultiplier * Mathf.Clamp01(1.25f - distance / Mathf.Max(1f, data.detectionRange));
            if (target.IsMoving) chance -= 0.1f;
            if (target.IsCrouched) chance -= 0.08f;
            if (!AIVisibility.IsLit(target.Position) && !target.FlashlightOn) chance *= 0.6f;
            if (SmokeCloud.Contains(target.Position)) chance *= 0.3f;
            bool onTarget = Random.value < Mathf.Clamp(chance, 0.05f, 0.9f);

            Vector3 direction = (aim - origin).normalized;
            if (!onTarget) direction = Quaternion.Euler(0f, Random.Range(4f, 10f) * (Random.value < 0.5f ? -1f : 1f), 0f) * direction;

            Vector3 from = muzzle != null ? muzzle.position : origin;
            var damage = new DamageInfo { amount = data.weaponDamage, attacker = Team.Suspect };
            WeaponEffects.Shoot(origin, direction, data.detectionRange * 1.5f, damage, from, new Color(1f, 0.5f, 0.25f));
            WeaponEffects.MuzzleFlash(from, Sound.EnemyShot, 0.8f, 20f, NoiseKind.EnemyGunshot);
        }
    }
}
