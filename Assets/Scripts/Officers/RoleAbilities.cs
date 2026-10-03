using UnityEngine;

namespace Swat
{
    // The team leader's role ability (T). The Shield officer's Brace is a
    // toggle handled by PlayerController; everything else is here. Returns
    // false if the ability couldn't be used, so no cooldown is spent.
    public static class RoleAbilities
    {
        public static bool Use(PlayerController player)
        {
            switch (player.Officer.role)
            {
                case OfficerRole.Leader: return Coordinate(player);
                case OfficerRole.Breacher: return DoorKick(player);
                case OfficerRole.Medic: return Stabilize(player);
                case OfficerRole.Recon: return Scan(player);
                case OfficerRole.Tactical: return Overcharge(player);
                default: return false;
            }
        }

        // Squad reacts faster and shoots straighter for 10 seconds.
        static bool Coordinate(PlayerController player)
        {
            int alive = 0;
            foreach (var officer in AIManager.Instance.Officers) if (officer.IsAlive) alive++;
            if (alive == 0)
            {
                UIManager.Notify("No squadmates to coordinate");
                return false;
            }
            SquadCommandManager.Instance.StartCoordinate(10f);
            int threats = SquadStatusTracker.ThreatsSeenByTeam;
            UIManager.Notify("Coordinate: squad on high alert. Threats in sight: " + threats);
            AudioManager.RadioChirp(1.15f);
            return true;
        }

        // Kicks open the locked (non-electronic) door in front of the player.
        static bool DoorKick(PlayerController player)
        {
            if (NetSession.Online)
            {
                UIManager.Notify("Doors stay as they are in online matches");
                return false;
            }
            Vector3 ahead = player.Position + player.AimDirection * 0.8f;
            var door = AIManager.Instance.FindDoor(ahead, 2f, d => d.State == DoorState.Locked || d.State == DoorState.Closed);
            if (door == null)
            {
                UIManager.Notify("Stand next to a closed or locked door to kick it");
                return false;
            }
            if (door.Electronic && door.State == DoorState.Locked)
            {
                UIManager.Notify("Electronic locks are too strong to kick. Use a charge or a console.");
                return false;
            }
            bool wasLocked = door.State == DoorState.Locked;
            if (!door.Kick(player.Position)) return false;
            GameManager.Instance.CameraRig.Shake(0.3f);
            if (wasLocked)
            {
                UIManager.Notify("Door kicked open");
                MissionManager.Instance.ReportBreach(door, true);
            }
            return true;
        }

        // 25 health over 3 seconds to the nearest hurt teammate, or yourself.
        static bool Stabilize(PlayerController player)
        {
            SquadAI patient = null;
            float best = 3f;
            foreach (var officer in AIManager.Instance.Officers)
            {
                if (!officer.IsAlive || officer.Health.Fraction >= 0.95f) continue;
                float distance = Vector3.Distance(officer.Position, player.Position);
                if (distance < best && officer.Health.Fraction < player.Health.Fraction)
                {
                    best = distance;
                    patient = officer;
                }
            }
            if (patient != null)
            {
                patient.Health.HealOverTime(25f, 3f);
                UIManager.Notify("Stabilizing " + patient.Data.callsign);
            }
            else if (player.Health.Fraction < 0.98f)
            {
                player.Health.HealOverTime(25f, 3f);
                UIManager.Notify("Stabilizing yourself");
            }
            else
            {
                UIManager.Notify("Nobody nearby needs treatment");
                return false;
            }
            AudioManager.Play(Sound.Medkit, player.Position, 0.6f);
            return true;
        }

        // Marks threats within 12 m for 6 seconds, through walls.
        static bool Scan(PlayerController player)
        {
            int found = TacticalIntel.Instance.RevealAround(player.Position, 12f, 6f);
            UIManager.Notify(found > 0 ? "Scan: " + found + " suspect(s) marked nearby (Tab)" : "Scan: no suspects within 12 m");
            return true;
        }

        // The next flashbang or less-lethal round is 50% more effective.
        static bool Overcharge(PlayerController player)
        {
            if (player.Weapons.Overcharged)
            {
                UIManager.Notify("Already overcharged");
                return false;
            }
            player.Weapons.Overcharged = true;
            UIManager.Notify("Overcharge: next flashbang or less-lethal round is 50% stronger");
            return true;
        }
    }
}
