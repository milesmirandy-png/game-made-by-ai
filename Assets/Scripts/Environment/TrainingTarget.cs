using UnityEngine;

namespace Swat
{
    // A pop-up target on the firing range. Falls when hit, stands back up later.
    public class TrainingTarget : MonoBehaviour, IDamageable
    {
        Transform board;
        float resetAt;
        public bool Down { get; private set; }
        public Team Team { get { return Team.Suspect; } }
        public bool IsAlive { get { return !Down; } }

        public static TrainingTarget Create(Transform parent, Vector3 position)
        {
            var go = new GameObject("Target");
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            var target = go.AddComponent<TrainingTarget>();
            Shapes.Box("Base", go.transform, new Vector3(0f, 0.1f, 0f), new Vector3(0.6f, 0.2f, 0.4f), new Color(0.3f, 0.3f, 0.3f), false);
            target.board = new GameObject("Board").transform;
            target.board.SetParent(go.transform, false);
            target.board.localPosition = new Vector3(0f, 0.2f, 0f);
            var plate = Shapes.Box("Plate", target.board, new Vector3(0f, 0.8f, 0f), new Vector3(0.6f, 1.2f, 0.06f), new Color(0.9f, 0.9f, 0.85f));
            Shapes.Box("Ring", target.board, new Vector3(0f, 1f, -0.04f), new Vector3(0.3f, 0.3f, 0.02f), new Color(0.9f, 0.2f, 0.15f), false);
            plate.layer = Layers.Characters;
            target.enabled = false;
            return target;
        }

        public void TakeDamage(DamageInfo info)
        {
            if (Down || info.attacker != Team.Police) return;
            Down = true;
            board.localRotation = Quaternion.Euler(-85f, 0f, 0f);
            resetAt = Time.time + 6f;
            enabled = true;
            AudioManager.Play(Sound.TargetHit, transform.position, 0.8f);
            MissionManager.Instance.Report(ObjectiveType.TrainingShootTargets, 1);
        }

        void Update()
        {
            if (Time.time < resetAt) return;
            Down = false;
            board.localRotation = Quaternion.identity;
            enabled = false;
        }
    }
}
