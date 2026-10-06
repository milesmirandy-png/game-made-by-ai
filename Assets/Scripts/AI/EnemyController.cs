using UnityEngine;

namespace Swat
{
    // A suspect's body: pose, floor ring colour, the red "!" alert marker and
    // visibility. EnemyAI decides what to do; this makes it visible.
    public class EnemyController : MonoBehaviour
    {
        static readonly Color HostileRing = new Color(1f, 0.25f, 0.2f);
        static readonly Color UnarmedRing = new Color(1f, 0.55f, 0.25f);
        static readonly Color SurrenderRing = new Color(1f, 0.85f, 0.2f);
        static readonly Color RestrainedRing = new Color(0.3f, 0.6f, 1f);

        public CharacterParts Parts { get; private set; }
        public ProceduralAnimator Animator { get; private set; }
        AgentMover mover;
        float alertUntil;
        bool dead, crouched;

        public void Init(CharacterParts parts, AgentMover agentMover, bool armed)
        {
            Parts = parts;
            mover = agentMover;
            Animator = new ProceduralAnimator(parts);
            Animator.SetPose(armed ? Pose.Aim : Pose.Relaxed);
            parts.SetRingColor(armed ? HostileRing : UnarmedRing);
        }

        public void ShowAlert(float seconds)
        {
            alertUntil = Time.time + seconds;
            Parts.alertMarker.SetActive(true);
        }

        public void SetSurrendered()
        {
            Animator.SetPose(Pose.HandsUp);
            Animator.SetCrouch(false);
            Parts.ShowWeapon(false);
            Parts.SetRingColor(SurrenderRing);
            Parts.alertMarker.SetActive(false);
        }

        public void SetRestrained()
        {
            Animator.SetPose(Pose.Cuffed);
            Animator.SetCrouch(true); // kneeling
            Parts.SetRingColor(RestrainedRing);
        }

        public void SetStunned(bool stunned, bool armed)
        {
            Animator.SetPose(stunned ? Pose.Cower : armed ? Pose.Aim : Pose.Relaxed);
        }

        public void SetHiding(bool hiding)
        {
            crouched = hiding;
            Animator.SetCrouch(hiding);
            if (hiding) Animator.SetPose(Pose.Cower);
        }

        // Kneeling behind cover or holding an angle (still aiming, unlike hiding).
        public void SetCrouched(bool crouch)
        {
            crouched = crouch;
            Animator.SetCrouch(crouch);
        }

        // A fake surrender ends: the gun comes back out.
        public void SetArmedAgain()
        {
            Animator.SetPose(Pose.Aim);
            Parts.ShowWeapon(true);
            Parts.SetRingColor(HostileRing);
        }

        public void SetDead()
        {
            dead = true;
            Animator.SetDown(true);
        }

        // Called every frame by the AI manager (no Update of its own).
        public void Animate(float dt)
        {
            if (dead) return;
            if (Parts.alertMarker.activeSelf && Time.time > alertUntil) Parts.alertMarker.SetActive(false);
            Animator.Tick(dt, mover.Speed, mover.IsRunning);
        }

        public bool IsCrouched { get { return crouched; } }
    }
}
