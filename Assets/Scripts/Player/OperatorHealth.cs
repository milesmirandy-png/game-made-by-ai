using System.Collections.Generic;
using UnityEngine;

namespace Swat
{
    // Health and armor shared by the player and AI officers. Armor reduces
    // damage and weakens as it takes hits; a ballistic shield blocks most
    // frontal fire, and a braced shield also protects teammates close behind it.
    // Reaching zero health means "downed" (out of action), never anything graphic.
    public class OperatorHealth : MonoBehaviour, IDamageable
    {
        static readonly List<OperatorHealth> bracedShields = new List<OperatorHealth>();

        public float Max { get; private set; }
        public float Current { get; private set; }
        public float Fraction { get { return Max > 0f ? Current / Max : 0f; } }
        public ArmorData Armor { get; private set; }
        public float ArmorCondition { get { return Armor != null && Armor.durability > 0f ? armorPoints / Armor.durability : 0f; } }
        public bool HasShield { get; private set; }
        public bool Bracing { get; private set; }
        public bool IsDown { get { return Current <= 0f; } }
        public Team Team { get { return Team.Police; } }
        public bool IsAlive { get { return !IsDown; } }
        public float DamageTaken { get; private set; }
        public DamageInfo LastHit { get; private set; }
        public float ProtectedUntil { get; set; }   // game modes: a moment of safety after respawning

        float armorPoints, extraReduction, healPerSecond, healRemaining;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            bracedShields.Clear();
        }

        public void Init(float maxHealth, ArmorData armor, float bonusReduction, bool shield)
        {
            Max = Current = maxHealth;
            Armor = armor;
            armorPoints = armor != null ? armor.durability : 0f;
            extraReduction = bonusReduction;
            HasShield = shield;
        }

        public void SetBracing(bool brace)
        {
            Bracing = brace && HasShield && !IsDown;
            if (Bracing && !bracedShields.Contains(this)) bracedShields.Add(this);
            if (!Bracing) bracedShields.Remove(this);
        }

        public virtual void TakeDamage(DamageInfo info)
        {
            if (IsDown || info.attacker == Team.Police) return;
            var game = GameManager.Instance;
            if (game == null || !game.IsPlaying) return;
            if (Time.time < ProtectedUntil) return;
            LastHit = info;

            float amount = info.amount;
            Vector3 incoming = -info.direction;
            incoming.y = 0f;
            if (HasShield && incoming.sqrMagnitude > 0.01f && Vector3.Angle(transform.forward, incoming) < 60f)
                amount *= Bracing ? 0.08f : 0.25f;
            amount *= 1f - ShieldCoverFor(this);

            if (Armor != null)
            {
                float reduction = Armor.damageReduction * Mathf.Lerp(0.5f, 1f, ArmorCondition);
                amount *= 1f - Mathf.Clamp01(reduction + extraReduction);
                armorPoints = Mathf.Max(0f, armorPoints - info.amount * 0.5f);
            }

            Current = Mathf.Max(0f, Current - amount);
            DamageTaken += amount;
            OnDamaged(info, amount);
            if (IsDown)
            {
                SetBracing(false);
                healRemaining = 0f;
                OnDowned();
            }
        }

        // Teammates within 3 m behind a braced shield take 25% less damage.
        static float ShieldCoverFor(OperatorHealth target)
        {
            foreach (var shield in bracedShields)
            {
                if (shield == target || shield == null) continue;
                Vector3 offset = target.transform.position - shield.transform.position;
                offset.y = 0f;
                if (offset.sqrMagnitude < 9f && Vector3.Dot(shield.transform.forward, offset) < 0.3f) return 0.25f;
            }
            return 0f;
        }

        // Restores health gradually (never an instant full heal).
        public void HealOverTime(float amount, float seconds)
        {
            if (IsDown || amount <= 0f) return;
            healRemaining += amount;
            healPerSecond = healRemaining / Mathf.Max(0.5f, seconds);
        }

        public void Revive(float health)
        {
            if (!IsDown) return;
            Current = Mathf.Min(Max, health);
            OnRevived();
        }

        // Game modes: back to full health and armor after respawning.
        public void RestoreFull()
        {
            bool wasDown = IsDown;
            Current = Max;
            armorPoints = Armor != null ? Armor.durability : 0f;
            healRemaining = 0f;
            if (wasDown) OnRevived();
        }

        protected virtual void Update()
        {
            if (healRemaining <= 0f || IsDown) return;
            float step = Mathf.Min(healRemaining, healPerSecond * Time.deltaTime);
            healRemaining -= step;
            Current = Mathf.Min(Max, Current + step);
        }

        protected virtual void OnDamaged(DamageInfo info, float amount) { }
        protected virtual void OnDowned() { }
        protected virtual void OnRevived() { }

        protected virtual void OnDestroy()
        {
            bracedShields.Remove(this);
        }
    }
}
