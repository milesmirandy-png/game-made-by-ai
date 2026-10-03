using UnityEngine;

namespace Swat
{
    public class PlayerHealth : OperatorHealth
    {
        // Read by the HUD for screen effects.
        public float DamageFlash { get; private set; }
        public float Blind { get; private set; }
        public Vector3 LastHitFrom { get; private set; }
        public float HitIndicator { get; private set; }

        protected override void OnDamaged(DamageInfo info, float amount)
        {
            DamageFlash = 1f;
            HitIndicator = 1f;
            LastHitFrom = transform.position - info.direction * 5f;
            AudioManager.Play2D(Sound.Hurt, 0.7f);
            var player = GetComponent<PlayerController>();
            if (player != null && player.Animator != null) player.Animator.Hit(info.direction);
            GameManager.Instance.CameraRig.Shake(0.25f);
        }

        protected override void OnDowned()
        {
            var player = GetComponent<PlayerController>();
            player.Animator.SetDown(true);
            GameManager.Instance.OnPlayerDown();
        }

        protected override void OnRevived()
        {
            var player = GetComponent<PlayerController>();
            if (player != null) player.Animator.SetDown(false);
            DamageFlash = HitIndicator = Blind = 0f;
        }

        public void Flashbang(float amount)
        {
            Blind = Mathf.Max(Blind, Mathf.Clamp01(amount));
        }

        protected override void Update()
        {
            base.Update();
            float dt = Time.deltaTime;
            if (DamageFlash > 0f) DamageFlash = Mathf.MoveTowards(DamageFlash, 0f, dt * 2f);
            if (HitIndicator > 0f) HitIndicator = Mathf.MoveTowards(HitIndicator, 0f, dt);
            if (Blind > 0f) Blind = Mathf.MoveTowards(Blind, 0f, dt * 0.35f);
        }
    }
}
