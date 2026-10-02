using UnityEngine;

namespace Swat
{
    // The rear doors of the mission van: once per mission the team leader can
    // top up spare magazines to the amount they deployed with. Equipment is not
    // restocked, so planning still matters.
    public class VanSupply : MonoBehaviour, IInteractable
    {
        bool used;

        public string Prompt { get { return "[E] Resupply at Van (hold)"; } }
        public Vector3 InteractPosition { get { return transform.TransformPoint(new Vector3(0f, 1.2f, -2.9f)); } }

        public static VanSupply Attach(Transform van)
        {
            return van.gameObject.AddComponent<VanSupply>();
        }

        public float InteractDuration(PlayerController player) { return 1.2f; }

        public bool CanInteract(PlayerController player)
        {
            if (used || player == null) return false;
            var game = GameManager.Instance;
            if (game == null || game.State != GameState.Playing) return false;
            return NeedsAmmo(player.Weapons.Inventory.Primary) || NeedsAmmo(player.Weapons.Inventory.Sidearm);
        }

        static bool NeedsAmmo(Weapon weapon)
        {
            return weapon != null && weapon.Reserve < weapon.Data.startingReserve;
        }

        public void Interact(PlayerController player)
        {
            if (!CanInteract(player)) return;
            used = true;
            var inventory = player.Weapons.Inventory;
            Refill(inventory.Primary);
            Refill(inventory.Sidearm);
            AudioManager.Play(Sound.Equip, player.Position, 0.6f);
            AudioManager.Play(Sound.Reload, player.Position, 0.4f, 0.9f);
            UIManager.Notify("Spare magazines restocked from the van (once per mission)");
        }

        static void Refill(Weapon weapon)
        {
            if (weapon != null) weapon.Reserve = Mathf.Max(weapon.Reserve, weapon.Data.startingReserve);
        }
    }
}
