using UnityEngine;

namespace Swat
{
    public interface IShootable
    {
        void TakeHit(float damage, Vector3 point, Vector3 direction, bool fromPlayer);
    }

    public interface IInteractable
    {
        bool CanInteract { get; }
        string Prompt { get; }
        void Interact(PlayerController player);
    }

    // Put on a collider so bullets know who they hit and how hard (head shots hurt more).
    public class Hitbox : MonoBehaviour
    {
        public IShootable Owner;
        public float multiplier = 1f;
        public bool isHead;
    }
}
