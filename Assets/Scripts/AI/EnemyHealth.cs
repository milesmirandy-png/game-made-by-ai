using UnityEngine;

namespace Swat
{
    public class EnemyHealth : MonoBehaviour, IDamageable
    {
        float current, max, reduction;
        bool helmet;
        EnemyAI ai;

        public Team Team { get { return Team.Suspect; } }
        public bool IsAlive { get { return current > 0f; } }
        public float Fraction { get { return max > 0f ? current / max : 0f; } }
        // Down but alive (SWAT 4's "incapacitated"): out of the fight until an officer restrains them.
        public bool Incapacitated { get; private set; }

        public void Init(EnemyData data, EnemyAI owner)
        {
            max = current = data.maxHealth;
            reduction = data.damageReduction;
            helmet = data.archetype == EnemyArchetype.Armored;
            ai = owner;
        }

        public void TakeDamage(DamageInfo info)
        {
            // Suspects don't shoot each other.
            if (!IsAlive || info.attacker == Team.Suspect || info.attacker == Team.Civilian) return;
            // Missions are lethal: a few solid hits put a suspect down (see Lethality).
            float amount = info.amount * Lethality.ToSuspects;
            if (info.zoned && !info.lessLethal)
            {
                // Where it landed: armor only covers the torso (and the armored crew wear helmets).
                amount *= Ballistics.ZoneMultiplier(info.zone);
                float armor = info.zone == HitZone.Torso ? reduction : info.zone == HitZone.Head && helmet ? 0.55f : 0f;
                amount *= armor > 0f ? 1f - Ballistics.ArmorStops(armor, info.ammo) : Ballistics.UnarmoredBonus(info.ammo);
            }
            else amount *= 1f - reduction;
            // Less-lethal rounds hurt but never take someone down on their own.
            if (info.lessLethal) amount = Mathf.Min(amount, current - 1f);
            if (Incapacitated)
            {
                // Force against someone who's already down (always unauthorized); a bullet finishes them.
                bool lethal = info.zoned && !info.lessLethal;
                if (lethal) current = 0f;
                ai.OnHit(info, lethal);
                return;
            }
            current = Mathf.Max(0f, current - amount);
            if (!IsAlive && ai.Incapacitates(info))
            {
                current = 1f;
                Incapacitated = true;
                ai.Incapacitate(info);
                return;
            }
            ai.OnHit(info, !IsAlive);
        }
    }
}
