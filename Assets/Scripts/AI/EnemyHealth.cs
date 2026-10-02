using UnityEngine;

namespace Swat
{
    public class EnemyHealth : MonoBehaviour, IDamageable
    {
        float current, max;
        EnemyAI ai;

        public Team Team { get { return Team.Suspect; } }
        public bool IsAlive { get { return current > 0f; } }
        public float Fraction { get { return max > 0f ? current / max : 0f; } }

        public void Init(float maxHealth, EnemyAI owner)
        {
            max = current = maxHealth;
            ai = owner;
        }

        public void TakeDamage(DamageInfo info)
        {
            // Suspects don't shoot each other.
            if (!IsAlive || info.attacker == Team.Suspect || info.attacker == Team.Civilian) return;
            current = Mathf.Max(0f, current - info.amount);
            ai.OnHit(info, !IsAlive);
        }
    }
}
