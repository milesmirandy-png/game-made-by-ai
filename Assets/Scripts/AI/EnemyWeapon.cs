using UnityEngine;

namespace Swat
{
    // A suspect's gun. Shots are hitscan, fired in short bursts; the chance to
    // hit drops with distance, if the officer is moving, or if smoke is in the way.
    public class EnemyWeapon : MonoBehaviour
    {
        EnemyData data;
        Transform muzzle;
        float nextShot;
        int burstLeft;

        public void Init(EnemyData profile, Transform muzzlePoint)
        {
            data = profile;
            muzzle = muzzlePoint;
        }

        public void ResetBurst()
        {
            burstLeft = 0;
            nextShot = Mathf.Max(nextShot, Time.time + 0.15f);
        }

        public void TryFire(PlayerController target)
        {
            if (Time.time < nextShot) return;
            if (burstLeft <= 0) burstLeft = Mathf.Max(1, data.burstSize);
            Fire(target);
            burstLeft--;
            nextShot = Time.time + (burstLeft > 0 ? 1f / Mathf.Max(0.5f, data.fireRate) : Random.Range(0.7f, 1.2f));
        }

        void Fire(PlayerController target)
        {
            Vector3 origin = transform.position + Vector3.up * 1.2f;
            Vector3 aim = target.ChestPosition;
            float distance = Vector3.Distance(origin, aim);

            float chance = data.accuracy * Mathf.Clamp01(1.25f - distance / Mathf.Max(1f, data.detectionRange));
            if (target.IsMoving) chance -= 0.1f;
            if (target.IsSprinting) chance -= 0.1f;
            if (SmokeCloud.Contains(target.Position)) chance *= 0.3f;
            bool onTarget = Random.value < Mathf.Clamp(chance, 0.05f, 0.9f);

            Vector3 direction = (aim - origin).normalized;
            if (!onTarget) direction = Quaternion.Euler(0f, Random.Range(4f, 10f) * (Random.value < 0.5f ? -1f : 1f), 0f) * direction;

            float range = data.detectionRange * 1.5f;
            Vector3 end = origin + direction * range;
            RaycastHit hit;
            if (Physics.Raycast(origin, direction, out hit, range, Layers.ShootableMask, QueryTriggerInteraction.Ignore))
            {
                end = hit.point;
                var victim = hit.collider.GetComponentInParent<IDamageable>();
                if (victim != null)
                {
                    victim.TakeDamage(new DamageInfo { amount = data.weaponDamage, point = hit.point, direction = direction, attacker = Team.Suspect });
                }
                else
                {
                    EffectsManager.Instance.Burst(hit.point, hit.normal, new Color(0.75f, 0.72f, 0.65f), 3, 2.5f, 0.05f);
                }
            }

            Vector3 from = muzzle != null ? muzzle.position : origin;
            EffectsManager.Instance.SpawnTracer(from, end, new Color(1f, 0.5f, 0.25f));
            EffectsManager.Instance.FlashLight(from, new Color(1f, 0.7f, 0.4f), 2f, 5f, 0.06f);
            AudioManager.Play(Sound.EnemyShot, from, 0.8f, Random.Range(0.92f, 1.05f));
            Noise.Emit(origin, 20f, NoiseKind.EnemyGunshot);
        }
    }
}
