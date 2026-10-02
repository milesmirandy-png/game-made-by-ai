using UnityEngine;

namespace Swat
{
    public class EnemyHealth : MonoBehaviour, IDamageable
    {
        float current, max, reduction;
        EnemyAI ai;

        public Team Team { get { return Team.Suspect; } }
        public bool IsAlive { get { return current > 0f; } }
        public float Fraction { get { return max > 0f ? current / max : 0f; } }

        public void Init(EnemyData data, EnemyAI owner)
        {
            max = current = data.maxHealth;
            reduction = data.damageReduction;
            ai = owner;
        }

        public void TakeDamage(DamageInfo info)
        {
            // Suspects don't shoot each other.
            if (!IsAlive || info.attacker == Team.Suspect || info.attacker == Team.Civilian) return;
            float amount = info.amount * (1f - reduction);
            // Less-lethal rounds hurt but never take someone down on their own.
            if (info.lessLethal) amount = Mathf.Min(amount, current - 1f);
            current = Mathf.Max(0f, current - amount);
            ai.OnHit(info, !IsAlive);
        }
    }
}
