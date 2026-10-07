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
        // Limb hits (missions only): a leg wound means limping (slower, no sprint), an arm wound a
        // shakier aim, until a medical kit treats it.
        public bool LegInjured { get; private set; }
        // Wearing a helmet (set from the loadout's headgear): it takes most of a hit to the head.
        public bool Helmet { get; set; }
        public bool ArmInjured { get; private set; }
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

        // Gun Game leaves the shield at the van.
        public void SetShield(bool carried)
        {
            HasShield = carried;
            if (!carried) SetBracing(false);
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
            if (game == null || !game.WorldRunning) return;
            if (Time.time < ProtectedUntil) return;
            LastHit = info;

            float amount = info.amount;
            Vector3 incoming = -info.direction;
            incoming.y = 0f;
            if (HasShield && incoming.sqrMagnitude > 0.01f)
            {
                // A braced shield stops rounds from the front outright; carried, it takes most of the hit.
                float angle = Vector3.Angle(transform.forward, incoming);
                if (Bracing && angle < 70f)
                {
                    ShieldClang(info);
                    return;
                }
                if (angle < 60f)
                {
                    amount *= 0.2f;
                    ShieldClang(info);
                }
            }
            amount *= 1f - ShieldCoverFor(this);

            bool torso = !info.zoned || info.zone == HitZone.Torso;
            if (info.zoned && !info.lessLethal)
            {
                // Where it landed: the vest covers the torso, a helmet the head, nothing the arms and legs.
                amount *= Ballistics.ZoneMultiplier(info.zone);
                if (info.zone == HitZone.Head) amount *= Helmet ? 1f - Ballistics.ArmorStops(0.55f, info.ammo) : Ballistics.UnarmoredBonus(info.ammo);
                else if (!torso || Armor == null) amount *= Ballistics.UnarmoredBonus(info.ammo);
            }
            if (Armor != null && torso)
            {
                float reduction = Armor.damageReduction * Mathf.Lerp(0.5f, 1f, ArmorCondition);
                if (info.zoned) reduction = Ballistics.ArmorStops(reduction, info.ammo);
                amount *= 1f - Mathf.Clamp01(reduction + extraReduction);
                armorPoints = Mathf.Max(0f, armorPoints - info.amount * 0.5f);
            }

            if (amount >= 6f && !VersusMatch.Active) Wound(info);
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

        void ShieldClang(DamageInfo info)
        {
            Vector3 at = transform.position + transform.forward * 0.55f + Vector3.up * 1.1f;
            EffectsManager.Instance.HitSpark(at, 0.45f);
            AudioManager.Play(Sound.RicochetMetal, at, 0.55f, Random.Range(0.9f, 1.15f), SoundCategory.Weapons);
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

        // Where the round landed: below the hips is a leg, out to the side at chest height an arm.
        void Wound(DamageInfo info)
        {
            if (info.zoned)
            {
                if (info.zone == HitZone.Leg && !LegInjured) { LegInjured = true; OnWounded(true); }
                else if (info.zone == HitZone.Arm && !ArmInjured) { ArmInjured = true; OnWounded(false); }
                return;
            }
            if (info.point == Vector3.zero) return;
            Vector3 local = transform.InverseTransformPoint(info.point);
            if (local.y < 0.85f)
            {
                if (LegInjured) return;
                LegInjured = true;
                OnWounded(true);
            }
            else if (local.y < 1.45f && Mathf.Abs(local.x) > 0.17f)
            {
                if (ArmInjured) return;
                ArmInjured = true;
                OnWounded(false);
            }
        }

        // A medical kit (or a revive) treats wounds as well as health.
        public void TreatWounds()
        {
            LegInjured = ArmInjured = false;
        }

        protected virtual void OnWounded(bool leg) { }

        // Restores health gradually (never an instant full heal).
        public void HealOverTime(float amount, float seconds)
        {
            if (IsDown || amount <= 0f) return;
            TreatWounds();
            healRemaining += amount;
            healPerSecond = healRemaining / Mathf.Max(0.5f, seconds);
        }

        public void Revive(float health)
        {
            if (!IsDown) return;
            Current = Mathf.Min(Max, health);
            TreatWounds();
            OnRevived();
        }

        // Game modes: back to full health and armor after respawning.
        public void RestoreFull()
        {
            bool wasDown = IsDown;
            Current = Max;
            armorPoints = Armor != null ? Armor.durability : 0f;
            healRemaining = 0f;
            TreatWounds();
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
