using UnityEngine;

namespace Swat
{
    // Uses the team leader's selected equipment item (G). Throwables go
    // towards the cursor (limited by throw range); door items act on the
    // nearest door; medical kits treat a nearby squadmate or yourself.
    // Each item's actual effect lives in its own class (Flashbang,
    // SmokeGrenade, BreachingCharge, DoorWedge, MedicalKit, ReconCamera,
    // PortableLight).
    public static class TacticalEquipment
    {
        const float DoorReach = 2.2f;

        public static void UseByPlayer(PlayerController player, EquipmentSlot slot)
        {
            var data = slot.Data;
            bool used = false;
            switch (data.kind)
            {
                case EquipmentKind.Flashbang:
                case EquipmentKind.Smoke:
                    used = ThrowFrom(player, data);
                    break;
                case EquipmentKind.BreachingCharge:
                {
                    var door = NearestDoor(player, BreachingCharge.CanUseOn);
                    used = BreachingCharge.Place(door, player);
                    if (!used) UIManager.Notify(door == null ? "Stand next to a breachable door (yellow stripes)" : "That door can't take a charge");
                    break;
                }
                case EquipmentKind.DoorWedge:
                {
                    var door = NearestDoor(player, DoorWedge.CanUseOn);
                    used = DoorWedge.Place(door);
                    if (!used) UIManager.Notify("Stand next to a closed door to wedge it");
                    break;
                }
                case EquipmentKind.MedicalKit:
                    used = MedicalKit.Use(player, data);
                    break;
                case EquipmentKind.PortableLight:
                {
                    Vector3 spot = player.Position + player.AimDirection * 0.8f;
                    spot.y = 0f;
                    PortableLight.Place(spot, data.radius, data.effectDuration, GameManager.Instance.Level.root);
                    UIManager.Notify("Portable light placed");
                    used = true;
                    break;
                }
                case EquipmentKind.ReconCamera:
                    used = ReconCamera.Use(player, NearestDoor(player, d => !d.IsPassable));
                    break;
            }
            if (!used) return;
            if (data.consumable) slot.Count--;
            slot.Used++;
            MissionManager.Instance.ReportEquipment(data.kind);
        }

        static bool ThrowFrom(PlayerController player, EquipmentData data)
        {
            Vector3 from = player.ChestPosition + player.AimDirection * 0.4f;
            Vector3 target = player.AimPoint;
            target.y = 0.1f;
            Vector3 flat = target - player.Position;
            flat.y = 0f;
            if (flat.magnitude > data.throwRange) target = player.Position + flat.normalized * data.throwRange + Vector3.up * 0.1f;
            float power = 1f;
            if (data.kind == EquipmentKind.Flashbang && player.Weapons.Overcharged)
            {
                power = 1.5f;
                player.Weapons.Overcharged = false;
            }
            ThrownGrenade.Throw(data, from, target, true, power);
            player.Animator.Fire(0.6f);
            return true;
        }

        static DoorController NearestDoor(PlayerController player, System.Predicate<DoorController> match)
        {
            return AIManager.Instance.FindDoor(player.Position + player.AimDirection * 0.5f, DoorReach, match);
        }
    }
}
