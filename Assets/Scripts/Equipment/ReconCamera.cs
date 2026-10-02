using System.Collections.Generic;
using UnityEngine;

namespace Swat
{
    // Recon camera: slide it under a closed door to get a quick, partial look
    // at the room beyond. It reports rough counts and marks only the people it
    // can actually see from the door gap, in a simple text panel (no live
    // camera rendering). Reusable, with a short cooldown.
    public static class ReconCamera
    {
        const float ViewRange = 9f;
        static float readyAt;

        public static bool Use(PlayerController player, DoorController door)
        {
            if (door == null)
            {
                UIManager.Notify("Stand at a closed door to use the recon camera");
                return false;
            }
            if (door.IsPassable)
            {
                UIManager.Notify("The door is open - just look");
                return false;
            }
            if (Time.time < readyAt)
            {
                UIManager.Notify("Recon camera ready in " + Mathf.CeilToInt(readyAt - Time.time) + "s");
                return false;
            }
            readyAt = Time.time + 6f;

            var room = door.FarRoom(player.Position);
            Vector3 normal = (door.transform.position - player.Position);
            normal.y = 0f;
            normal = Vector3.Dot(normal, door.transform.forward) >= 0f ? door.transform.forward : -door.transform.forward;
            Vector3 lens = door.transform.position + normal * 0.25f + Vector3.up * 0.15f;

            int armed = 0, unarmed = 0, civilians = 0;
            foreach (var enemy in AIManager.Instance.Enemies)
            {
                if (enemy.IsNeutralized || !Sees(lens, normal, enemy.Position + Vector3.up * 0.5f)) continue;
                if (enemy.Data.armed) armed++;
                else unarmed++;
                var contact = TacticalIntel.Instance.ContactFor(enemy);
                contact.everSeen = true;
                contact.lastSeen = enemy.Position;
                contact.lastSeenTime = Time.time;
                contact.revealedUntil = Mathf.Max(contact.revealedUntil, Time.time + 4f);
            }
            foreach (var civilian in AIManager.Instance.Civilians)
                if (civilian.IsAlive && !civilian.IsEvacuated && Sees(lens, normal, civilian.Position + Vector3.up * 0.5f)) civilians++;

            if (room != null) room.Advance(RoomState.Discovered);
            var lines = new List<string>();
            lines.Add(room != null ? room.DisplayName : "Beyond the door");
            if (armed + unarmed + civilians == 0) lines.Add("No movement visible from the door gap.");
            if (armed > 0) lines.Add(armed + " armed suspect" + (armed > 1 ? "s" : ""));
            if (unarmed > 0) lines.Add(unarmed + " unarmed person" + (unarmed > 1 ? "s" : "") + " (suspect)");
            if (civilians > 0) lines.Add(civilians + " civilian" + (civilians > 1 ? "s" : ""));
            if (room != null && room.IsDark) lines.Add("Room is dark - bring a light.");
            lines.Add("Coverage is partial: corners and furniture block the view.");
            UIManager.ShowRecon(lines);
            AudioManager.Play(Sound.Click, door.transform.position, 0.5f, 1.4f);
            return true;
        }

        // A low, wide view from under the door: range, a 140 degree cone and walls.
        static bool Sees(Vector3 lens, Vector3 forward, Vector3 target)
        {
            Vector3 to = target - lens;
            to.y = 0f;
            if (to.magnitude > ViewRange || Vector3.Angle(forward, to) > 70f) return false;
            return !Physics.Linecast(lens, target, Layers.WorldMask, QueryTriggerInteraction.Ignore);
        }
    }
}
