using UnityEngine;

namespace Swat
{
    // Shared line-of-sight rules used by suspects, officers, cameras and the
    // tactical map: distance and field of view first (cheap), then a single
    // raycast against walls, then smoke. Darkness, crouching and flashlights
    // scale how far someone can be seen.
    public static class AIVisibility
    {
        public static bool IsLit(Vector3 point)
        {
            var game = GameManager.Instance;
            var room = game != null && game.Level != null ? game.Level.RoomAt(point) : null;
            if (room == null || !room.IsDark) return true;
            return FlashlightController.Illuminates(point) || PortableLight.Illuminates(point);
        }

        // The same for a police officer or player, who shows only a shoulder and an eye
        // while peeking past a corner and so is a little harder to spot.
        public static float VisibilityOf(ICombatTarget target)
        {
            float multiplier = VisibilityOf(target.Position, target.IsCrouched, target.FlashlightOn);
            var player = target as PlayerController;
            if (player != null && player.Peeking) multiplier *= 0.8f;
            var actor = target as NetActor;
            if (actor != null && actor.Peeking) multiplier *= 0.8f;
            return multiplier;
        }

        // How far away (as a multiplier of normal sight range) someone standing here can be seen.
        public static float VisibilityOf(Vector3 point, bool crouched, bool flashlightOn)
        {
            float multiplier = IsLit(point) || flashlightOn ? 1f : 0.45f;
            if (crouched) multiplier *= 0.75f;
            if (flashlightOn) multiplier *= 1.3f; // a lit flashlight gives your position away
            if (SmokeCloud.Contains(point)) multiplier *= 0.3f;
            return multiplier;
        }

        public static bool CanSee(Vector3 eye, Vector3 forward, float fieldOfView, float range, Vector3 target, float visibility)
        {
            Vector3 to = target - eye;
            float distance = to.magnitude;
            if (distance > range * visibility) return false;
            if (distance > 2f && fieldOfView < 359f)
            {
                Vector3 flat = new Vector3(to.x, 0f, to.z);
                Vector3 look = new Vector3(forward.x, 0f, forward.z);
                if (Vector3.Angle(look, flat) > fieldOfView * 0.5f) return false;
            }
            if (Physics.Linecast(eye, target, Layers.WorldMask, QueryTriggerInteraction.Ignore)) return false;
            return !SmokeCloud.Blocks(eye, target);
        }
    }
}
