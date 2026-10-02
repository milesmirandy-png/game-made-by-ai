using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Swat
{
    // Builds the NavMesh at runtime from the level's colliders, so there's
    // nothing to bake in the editor and no extra packages are needed.
    public static class NavMeshBaker
    {
        public static NavMeshDataInstance Bake(Transform root, Bounds bounds)
        {
            var sources = new List<NavMeshBuildSource>();
            NavMeshBuilder.CollectSources(root, Layers.WorldMask, NavMeshCollectGeometry.PhysicsColliders, 0, new List<NavMeshBuildMarkup>(), sources);

            // Doors are left out so doorways stay walkable. Locked doors block
            // paths with their own NavMeshObstacle instead.
            sources.RemoveAll(source => source.component != null && source.component.GetComponentInParent<DoorController>() != null);

            var settings = NavMesh.GetSettingsByID(0);
            settings.agentRadius = 0.35f;
            settings.agentHeight = 1.8f;
            settings.agentClimb = 0.3f;
            settings.agentSlope = 45f;
            settings.overrideVoxelSize = true;
            settings.voxelSize = 0.15f; // coarse enough to build fast on slow PCs

            var data = NavMeshBuilder.BuildNavMeshData(settings, sources, bounds, Vector3.zero, Quaternion.identity);
            return NavMesh.AddNavMeshData(data);
        }
    }
}
