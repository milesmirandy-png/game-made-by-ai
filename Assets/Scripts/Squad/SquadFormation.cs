using UnityEngine;
using UnityEngine.AI;

namespace Swat
{
    // Where squadmates stand: behind the player while following, lined up
    // beside a door when stacking, spread out when entering a room.
    public static class SquadFormation
    {
        static readonly Vector2[] FollowOffsets = { new Vector2(-1.3f, -1.6f), new Vector2(1.3f, -1.6f), new Vector2(0f, -3f) };

        public static Vector3 FollowSlot(int index, Vector3 leader, Vector3 heading)
        {
            heading.y = 0f;
            if (heading.sqrMagnitude < 0.01f) heading = Vector3.forward;
            heading.Normalize();
            Vector3 right = new Vector3(heading.z, 0f, -heading.x);
            var offset = FollowOffsets[Mathf.Clamp(index, 0, FollowOffsets.Length - 1)];
            return OnNavMesh(leader + right * offset.x + heading * offset.y, leader);
        }

        // Stack positions on the given side of the door (side = +1 front, -1 back), alternating left/right of the frame.
        public static Vector3 StackSlot(DoorController door, float side, int index)
        {
            Vector3 normal = door.transform.forward * side;
            Vector3 along = door.transform.right;
            // Far enough from the frame (about 1.7 m) to stay clear of a breaching charge's blast.
            float offset = door.Width * 0.5f + 0.75f + (index / 2) * 0.8f;
            float sign = index % 2 == 0 ? -1f : 1f;
            Vector3 point = door.transform.position + normal * 0.8f + along * offset * sign;
            point.y = 0f;
            return OnNavMesh(point, door.transform.position + normal * 1.2f);
        }

        // Points inside a room to clear to: nearest corners first, then the centre.
        public static Vector3 EntryPoint(RoomController room, DoorController door, int index)
        {
            if (room == null) return door.transform.position;
            var b = room.Bounds;
            float inset = 1.2f;
            var candidates = new[]
            {
                new Vector3(b.min.x + inset, 0f, b.min.z + inset), new Vector3(b.max.x - inset, 0f, b.min.z + inset),
                new Vector3(b.min.x + inset, 0f, b.max.z - inset), new Vector3(b.max.x - inset, 0f, b.max.z - inset),
            };
            System.Array.Sort(candidates, (a, c) => Vector3.Distance(a, door.transform.position).CompareTo(Vector3.Distance(c, door.transform.position)));
            Vector3 point = index < 2 ? candidates[index] : new Vector3(b.center.x, 0f, b.center.z);
            return OnNavMesh(point, new Vector3(b.center.x, 0f, b.center.z));
        }

        public static Vector3 Spread(Vector3 point, int index)
        {
            if (index == 0) return OnNavMesh(point, point);
            float angle = index * 120f * Mathf.Deg2Rad;
            return OnNavMesh(point + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 1.1f, point);
        }

        static Vector3 OnNavMesh(Vector3 point, Vector3 fallback)
        {
            NavMeshHit hit;
            if (NavMesh.SamplePosition(point, out hit, 1.2f, NavMesh.AllAreas)) return hit.position;
            if (NavMesh.SamplePosition(fallback, out hit, 2f, NavMesh.AllAreas)) return hit.position;
            return point;
        }
    }
}
