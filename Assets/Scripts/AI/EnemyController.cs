using UnityEngine;

namespace Swat
{
    // A suspect's body: pose, floor ring colour, the red "!" alert marker and
    // the walk bob. EnemyAI decides what to do; this makes it visible.
    public class EnemyController : MonoBehaviour
    {
        static readonly Color HostileRing = new Color(1f, 0.25f, 0.2f);
        static readonly Color SurrenderRing = new Color(1f, 0.85f, 0.2f);
        static readonly Color ArrestedRing = new Color(0.3f, 0.6f, 1f);

        public CharacterParts Parts { get; private set; }
        AgentMover mover;
        float alertUntil;
        bool dead;

        public void Init(CharacterParts parts, AgentMover agentMover)
        {
            Parts = parts;
            mover = agentMover;
            parts.SetRingColor(HostileRing);
        }

        public void ShowAlert(float seconds)
        {
            alertUntil = Time.time + seconds;
            Parts.alertMarker.SetActive(true);
        }

        public void SetSurrendered()
        {
            Parts.PoseHandsUp();
            if (Parts.gun != null) Parts.gun.gameObject.SetActive(false);
            Parts.SetRingColor(SurrenderRing);
            Parts.alertMarker.SetActive(false);
        }

        public void SetArrested()
        {
            Parts.PoseCuffed();
            Parts.SetRingColor(ArrestedRing);
            Parts.model.localPosition = new Vector3(0f, -0.35f, 0f); // kneeling
        }

        public void SetStunned(bool stunned)
        {
            if (stunned) Parts.SetArms(new Vector3(-150f, 0f, 25f), new Vector3(-150f, 0f, -25f)); // shielding eyes
            else Parts.PoseAiming();
        }

        public void SetDead()
        {
            dead = true;
            Parts.Fall();
        }

        // Called every frame by the AI manager (no Update of its own).
        public void Animate()
        {
            if (dead) return;
            if (Parts.alertMarker.activeSelf && Time.time > alertUntil) Parts.alertMarker.SetActive(false);
            float bob = mover.IsMoving ? Mathf.Abs(Mathf.Sin(Time.time * 10f + transform.position.x)) * 0.05f : 0f;
            Vector3 p = Parts.model.localPosition;
            if (p.y >= -0.01f) Parts.model.localPosition = new Vector3(0f, bob, 0f);
        }
    }
}
