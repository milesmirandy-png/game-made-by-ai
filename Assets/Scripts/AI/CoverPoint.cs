using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Swat
{
    // Spots next to furniture where suspects can take cover. Generated from
    // the map's props and checked against the NavMesh once per mission.
    public static class CoverPoint
    {
        static readonly List<Vector3> points = new List<Vector3>();

        public static void Build(IList<Vector3> candidates)
        {
            points.Clear();
            foreach (var point in candidates)
            {
                NavMeshHit hit;
                if (NavMesh.SamplePosition(point, out hit, 0.6f, NavMesh.AllAreas)) points.Add(hit.position);
            }
        }

        // Nearest cover spot (within reach, not too close) that the threat can't see.
        public static bool Find(Vector3 from, Vector3 threat, float maxDistance, out Vector3 cover)
        {
            cover = from;
            float best = maxDistance * maxDistance;
            bool found = false;
            Vector3 threatEye = threat + Vector3.up * 1.4f;
            int checks = 0;
            foreach (var point in points)
            {
                float sqr = (point - from).sqrMagnitude;
                if (sqr >= best || sqr < 1f) continue;
                if (++checks > 12) break; // keep raycasts bounded
                if (!Physics.Linecast(point + Vector3.up * 1f, threatEye, Layers.WorldMask, QueryTriggerInteraction.Ignore)) continue;
                best = sqr;
                cover = point;
                found = true;
            }
            return found;
        }
    }
}
