using UnityEngine;
using UnityEngine.AI;

namespace Swat
{
    // Thin wrapper around NavMeshAgent shared by suspects and civilians.
    [RequireComponent(typeof(NavMeshAgent))]
    public class AgentMover : MonoBehaviour
    {
        NavMeshAgent agent;
        float walkSpeed, runSpeed;

        public bool IsMoving { get { return agent.enabled && agent.velocity.sqrMagnitude > 0.05f; } }

        public bool HasArrived
        {
            get
            {
                if (!agent.enabled || !agent.isOnNavMesh) return true;
                if (agent.pathPending) return false;
                return !agent.hasPath || agent.remainingDistance <= agent.stoppingDistance + 0.15f;
            }
        }

        public void Init(float walk, float run)
        {
            agent = GetComponent<NavMeshAgent>();
            walkSpeed = walk;
            runSpeed = run;
            agent.radius = 0.3f;
            agent.height = 1.8f;
            agent.acceleration = 20f;
            agent.angularSpeed = 540f;
            agent.stoppingDistance = 0.2f;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance; // cheaper
            agent.avoidancePriority = Random.Range(30, 70);
            agent.speed = walk;
        }

        public bool MoveTo(Vector3 destination, bool run)
        {
            if (!agent.enabled || !agent.isOnNavMesh) return false;
            agent.speed = run ? runSpeed : walkSpeed;
            agent.updateRotation = true;
            agent.isStopped = false;
            return agent.SetDestination(destination);
        }

        public void Stop()
        {
            if (!agent.enabled || !agent.isOnNavMesh) return;
            agent.isStopped = true;
            agent.ResetPath();
        }

        // Turns on the spot (the agent stops steering rotation while we do this).
        public void Face(Vector3 point, float degreesPerSecond, float dt)
        {
            Vector3 to = point - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude < 0.01f) return;
            FaceYaw(Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg, degreesPerSecond, dt);
        }

        public void FaceYaw(float yaw, float degreesPerSecond, float dt)
        {
            if (agent.enabled) agent.updateRotation = false;
            float current = Mathf.MoveTowardsAngle(transform.eulerAngles.y, yaw, degreesPerSecond * dt);
            transform.rotation = Quaternion.Euler(0f, current, 0f);
        }

        public bool RandomPointNear(Vector3 center, float radius, out Vector3 point)
        {
            for (int i = 0; i < 6; i++)
            {
                Vector2 offset = Random.insideUnitCircle * radius;
                NavMeshHit hit;
                if (NavMesh.SamplePosition(center + new Vector3(offset.x, 0f, offset.y), out hit, 1.5f, NavMesh.AllAreas))
                {
                    point = hit.position;
                    return true;
                }
            }
            point = center;
            return false;
        }

        public void Disable()
        {
            if (agent.enabled && agent.isOnNavMesh) agent.ResetPath();
            agent.enabled = false;
        }
    }
}
