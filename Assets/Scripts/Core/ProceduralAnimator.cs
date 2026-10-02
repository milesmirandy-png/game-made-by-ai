using UnityEngine;

namespace Swat
{
    public enum Pose { Aim, Relaxed, HandsUp, Cuffed, Shielding, Interact, Cower, Treating }

    // Lightweight procedural animation for the blocky characters: walk and
    // run bob, crouching, aim and idle poses, firing recoil, reloading,
    // interacting and surrendering. No Animator or rig needed, and it is
    // ticked by whoever owns the character (no Update of its own).
    public class ProceduralAnimator
    {
        readonly CharacterParts parts;
        Pose pose = Pose.Relaxed;
        float crouch, crouchTarget, recoil, reload, bobPhase;
        bool down;

        public Pose CurrentPose { get { return pose; } }

        public ProceduralAnimator(CharacterParts characterParts)
        {
            parts = characterParts;
        }

        public void SetPose(Pose next) { pose = next; }
        public void SetCrouch(bool crouched) { crouchTarget = crouched ? 1f : 0f; }
        public void Fire(float kick = 1f) { recoil = Mathf.Min(1.5f, recoil + kick); }
        public void SetReload(float progress) { reload = progress; }  // 0 = not reloading, 0..1 progress

        public void SetDown(bool isDown)
        {
            down = isDown;
            if (down) parts.Fall();
        }

        public void Tick(float dt, float speed, bool running)
        {
            if (down || dt <= 0f) return;
            crouch = Mathf.MoveTowards(crouch, crouchTarget, dt * 5f);
            recoil = Mathf.MoveTowards(recoil, 0f, dt * 8f);

            // Bob while moving; faster and bigger when running.
            float bob = 0f;
            if (speed > 0.15f)
            {
                bobPhase += dt * (running ? 14f : 9f + speed);
                bob = Mathf.Abs(Mathf.Sin(bobPhase)) * (running ? 0.07f : 0.045f);
            }
            parts.model.localPosition = new Vector3(0f, bob - crouch * 0.38f, 0f);
            parts.model.localRotation = Quaternion.Euler(running ? 8f : crouch * 6f, 0f, 0f);

            Vector3 left, right;
            switch (pose)
            {
                case Pose.HandsUp: left = new Vector3(180f, 0f, -20f); right = new Vector3(180f, 0f, 20f); break;
                case Pose.Cuffed: left = new Vector3(25f, 0f, 15f); right = new Vector3(25f, 0f, -15f); break;
                case Pose.Shielding: left = new Vector3(-75f, 0f, 35f); right = new Vector3(-80f, 0f, -10f); break;
                case Pose.Interact: left = new Vector3(-70f, 0f, 10f); right = new Vector3(-70f, 0f, -10f); break;
                case Pose.Cower: left = new Vector3(-160f, 0f, 30f); right = new Vector3(-160f, 0f, -30f); break;
                case Pose.Treating: left = new Vector3(-50f, 0f, 15f); right = new Vector3(-50f, 0f, -15f); break;
                case Pose.Aim:
                    left = new Vector3(-80f, 0f, 25f);
                    right = new Vector3(-85f, 0f, -10f);
                    break;
                default:
                    float swing = speed > 0.15f ? Mathf.Sin(bobPhase) * 25f : 0f;
                    left = new Vector3(swing, 0f, -6f);
                    right = new Vector3(-swing, 0f, 6f);
                    break;
            }
            if (pose == Pose.Aim && reload > 0f)
            {
                // Dip the gun and pull the support hand down to the magazine.
                float r = Mathf.Sin(reload * Mathf.PI);
                left += new Vector3(40f * r, 0f, -10f * r);
            }
            if (pose == Pose.Aim) right.x -= recoil * 8f;

            float blend = 1f - Mathf.Exp(-14f * dt);
            parts.leftArm.localRotation = Quaternion.Slerp(parts.leftArm.localRotation, Quaternion.Euler(left), blend);
            parts.rightArm.localRotation = Quaternion.Slerp(parts.rightArm.localRotation, Quaternion.Euler(right), blend);

            if (parts.gunRoot != null && parts.gunRoot.gameObject.activeSelf)
            {
                float r = reload > 0f ? Mathf.Sin(reload * Mathf.PI) : 0f;
                bool lowered = pose != Pose.Aim && pose != Pose.Shielding;
                parts.gunRoot.localPosition = new Vector3(0.12f, 1.17f - r * 0.12f - (lowered ? 0.25f : 0f), 0.22f - recoil * 0.06f - (lowered ? 0.12f : 0f));
                parts.gunRoot.localRotation = Quaternion.Euler(-recoil * 6f + r * 35f + (lowered ? 45f : 0f), 0f, r * -25f);
            }
        }
    }
}
