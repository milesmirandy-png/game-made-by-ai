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
    }

    public interface IDamageable
    {
        Team Team { get; }
        bool IsAlive { get; }
        void TakeDamage(DamageInfo info);
    }

    // Anything the player can use with E: doors, civilians, surrendered suspects.
    public interface IInteractable
    {
        string Prompt { get; }
        Vector3 InteractPosition { get; }
        bool CanInteract(PlayerController player);
        void Interact(PlayerController player);
    }
}
