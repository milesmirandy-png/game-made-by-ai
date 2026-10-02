using UnityEngine;
using UnityEngine.AI;

namespace Swat
{
    // Thin wrapper around NavMeshAgent shared by suspects, officers and
    // civilians, with simple recovery when an agent gets stuck.
    [RequireComponent(typeof(NavMeshAgent))]
    public class AgentMover : MonoBehaviour
    {
        NavMeshAgent agent;
        float walkSpeed, runSpeed, speedMultiplier = 1f;
        Vector3 lastProgressPosition;
        float lastProgressTime;

        public NavMeshAgent Agent { get { return agent; } }
        public bool IsMoving { get { return agent.enabled && agent.velocity.sqrMagnitude > 0.05f; } }
        public float Speed { get { return agent.enabled ? agent.velocity.magnitude : 0f; } }
        public bool IsRunning { get { return agent.enabled && agent.speed > walkSpeed * speedMultiplier + 0.1f && IsMoving; } }
        public Vector3 Destination { get { return agent.enabled && agent.hasPath ? agent.destination : transform.position; } }

        public bool HasArrived
        {
            get
            {
                if (!agent.enabled || !agent.isOnNavMesh) return true;
                if (agent.pathPending) return false;
                return !agent.hasPath || agent.remainingDistance <= agent.stoppingDistance + 0.15f;
            }
        }

        // True if we've been trying to move but haven't made progress for a while.
        public bool IsStuck
        {
            get
            {
                if (!agent.enabled || HasArrived) return false;
                return Time.time - lastProgressTime > 3f;
            }
        }

        public void Init(float walk, float run, float radius = 0.3f)
        {
            agent = GetComponent<NavMeshAgent>();
            walkSpeed = walk;
            runSpeed = run;
            agent.radius = radius;
            agent.height = 1.8f;
            agent.acceleration = 20f;
            agent.angularSpeed = 540f;
            agent.stoppingDistance = 0.2f;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance; // cheaper
            agent.avoidancePriority = Random.Range(30, 70);
            agent.speed = walk;
            lastProgressTime = Time.time;
        }

        public void SetSpeedMultiplier(float multiplier)
        {
            speedMultiplier = multiplier;
        }

        public bool MoveTo(Vector3 destination, bool run)
        {
            if (!agent.enabled || !agent.isOnNavMesh) return false;
            agent.speed = (run ? runSpeed : walkSpeed) * speedMultiplier;
            agent.updateRotation = true;
            agent.isStopped = false;
            lastProgressTime = Time.time;
            lastProgressPosition = transform.position;
            return agent.SetDestination(destination);
        }

        public void Stop()
        {
            if (!agent.enabled || !agent.isOnNavMesh) return;
            agent.isStopped = true;
            agent.ResetPath();
        }

        public void Warp(Vector3 position)
        {
            if (!agent.enabled) return;
            NavMeshHit hit;
            if (NavMesh.SamplePosition(position, out hit, 2f, NavMesh.AllAreas)) agent.Warp(hit.position);
            else agent.Warp(position);
            lastProgressTime = Time.time;
        }

        // Called a few times a second by the owner's brain.
        public void TrackProgress()
        {
            if ((transform.position - lastProgressPosition).sqrMagnitude > 0.25f || HasArrived)
            {
                lastProgressPosition = transform.position;
                lastProgressTime = Time.time;
            }
        }

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
                if (NavMesh.SamplePosition(center + new Vector3(offset.x, 0f, offset.y), out hit, 1.5f, NavMesh.AllAreas) && CanReach(hit.position))
                {
                    point = hit.position;
                    return true;
                }
            }
            point = center;
            return false;
        }

        static NavMeshPath sharedPath;

        public bool CanReach(Vector3 target)
        {
            if (!agent.enabled || !agent.isOnNavMesh) return false;
            if (sharedPath == null) sharedPath = new NavMeshPath();
            return NavMesh.CalculatePath(transform.position, target, NavMesh.AllAreas, sharedPath) && sharedPath.status == NavMeshPathStatus.PathComplete;
        }

        public void Disable()
        {
            if (agent.enabled && agent.isOnNavMesh) agent.ResetPath();
            agent.enabled = false;
        }

        public void Enable()
        {
            agent.enabled = true;
        }
    }
}
