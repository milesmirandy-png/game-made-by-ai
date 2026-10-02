using UnityEngine;

namespace Swat
{
    public class PlayerHealth : MonoBehaviour, IDamageable
    {
        [SerializeField] float maxHealth = 100f;

        public float Current { get; private set; }
        public float Max { get { return maxHealth; } }
        public float Fraction { get { return Current / maxHealth; } }
        public Team Team { get { return Team.Police; } }
        public bool IsAlive { get { return Current > 0f; } }

        // Read by the HUD for screen effects.
        public float DamageFlash { get; private set; }
        public float Blind { get; private set; }

        void Awake()
        {
            Current = maxHealth;
        }

        public void TakeDamage(DamageInfo info)
        {
            var game = GameManager.Instance;
            if (!IsAlive || info.attacker == Team.Police || game == null || !game.IsPlaying) return;

            Current = Mathf.Max(0f, Current - info.amount);
            DamageFlash = 1f;
            AudioManager.Play2D(Sound.Hurt, 0.7f);
            game.CameraRig.Shake(0.25f);
            if (IsAlive) return;

            GetComponent<PlayerController>().Parts.Fall();
            game.OnPlayerDied();
        }

        public void Flashbang(float amount)
        {
            Blind = Mathf.Max(Blind, Mathf.Clamp01(amount));
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (DamageFlash > 0f) DamageFlash = Mathf.MoveTowards(DamageFlash, 0f, dt * 2f);
            if (Blind > 0f) Blind = Mathf.MoveTowards(Blind, 0f, dt * 0.35f);
        }
    }
}
