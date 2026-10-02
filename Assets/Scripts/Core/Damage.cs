using UnityEngine;

namespace Swat
{
    public enum Team { Police, Suspect, Civilian, Environment }

    public struct DamageInfo
    {
        public float amount;
        public Vector3 point;
        public Vector3 direction;
        public Team attacker;
        public bool lessLethal;
        public float stun;
    }

    public interface IDamageable
    {
        Team Team { get; }
        bool IsAlive { get; }
        void TakeDamage(DamageInfo info);
    }

    // Anything the player can use with E: doors, civilians, suspects, consoles...
    public interface IInteractable
    {
        string Prompt { get; }
        Vector3 InteractPosition { get; }
        float InteractDuration(PlayerController player); // 0 = instant, otherwise hold E
        bool CanInteract(PlayerController player);
        void Interact(PlayerController player);
    }

    // A police officer (the player or an AI squadmate) as seen by suspects and cameras.
    public interface ICombatTarget
    {
        Transform Transform { get; }
        Vector3 Position { get; }
        Vector3 ChestPosition { get; }
        bool IsAlive { get; }
        bool IsMoving { get; }
        bool IsCrouched { get; }
        bool FlashlightOn { get; }
        IDamageable Damageable { get; }
    }
}
