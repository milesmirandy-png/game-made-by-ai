using UnityEngine;

namespace Swat
{
    public enum Pose { Aim, Relaxed, HandsUp, Cuffed, Shielding, Interact, Cower, Treating }

    // Lightweight procedural animation for the blocky characters: walking and
    // running with leg swing, idle breathing, crouching, aim and idle poses,
    // firing recoil, reloading, weapon switching, weapon sway, flinching when
    // hit, interacting and surrendering. Everything blends smoothly instead of
    // snapping. No Animator or rig needed; ticked by whoever owns the character.
    public class ProceduralAnimator
    {
        readonly CharacterParts parts;
        Pose pose = Pose.Relaxed;
        float crouch, crouchTarget, recoil, reload, switching, bobPhase, moveBlend, runBlend, lean, breath, flinch;
        Vector3 flinchAxis = Vector3.right;
        bool down;

        public Pose CurrentPose { get { return pose; } }

        public ProceduralAnimator(CharacterParts characterParts)
        {
            parts = characterParts;
            breath = Random.value * 10f;
        }

        public void SetPose(Pose next) { pose = next; }
        public void SetCrouch(bool crouched) { crouchTarget = crouched ? 1f : 0f; }
        public void Fire(float kick = 1f) { recoil = Mathf.Min(1.5f, recoil + kick); }
        public void SetReload(float progress) { reload = progress; }    // 0 = not reloading, 0..1 progress
        public void SetSwitch(float progress) { switching = progress; } // 0 = not switching, 0..1 progress

        // A short flinch away from the hit direction (no gore, just a jolt).
        public void Hit(Vector3 direction)
        {
            flinch = 1f;
            Vector3 local = parts.root != null ? parts.root.InverseTransformDirection(direction) : direction;
            flinchAxis = new Vector3(local.z, 0f, -local.x).normalized;
            if (flinchAxis.sqrMagnitude < 0.01f) flinchAxis = Vector3.right;
        }

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
            flinch = Mathf.MoveTowards(flinch, 0f, dt * 5f);
            breath += dt * 1.6f;

            // Movement blends in and out instead of switching on at a threshold.
            float moving = Mathf.Clamp01(speed / 2.5f);
            moveBlend = Mathf.Lerp(moveBlend, moving, 1f - Mathf.Exp(-10f * dt));
            runBlend = Mathf.Lerp(runBlend, running ? 1f : 0f, 1f - Mathf.Exp(-8f * dt));
            if (speed > 0.05f) bobPhase += dt * Mathf.Lerp(8f + speed, 14f, runBlend);

            float bob = Mathf.Abs(Mathf.Sin(bobPhase)) * Mathf.Lerp(0.04f, 0.07f, runBlend) * moveBlend;
            float idleBreath = Mathf.Sin(breath) * 0.008f * (1f - moveBlend);
            lean = Mathf.Lerp(lean, runBlend * 8f * moveBlend + crouch * 6f, 1f - Mathf.Exp(-8f * dt));
            parts.model.localPosition = new Vector3(0f, bob + idleBreath - crouch * 0.38f, 0f);
            var flinchRotation = Quaternion.AngleAxis(-flinch * 14f, flinchAxis);
            parts.model.localRotation = flinchRotation * Quaternion.Euler(lean, 0f, 0f);

            // Legs: swing when walking, stride further when running, bend when crouched.
            if (parts.leftLeg != null)
            {
                float stride = Mathf.Sin(bobPhase) * Mathf.Lerp(26f, 40f, runBlend) * moveBlend;
                float bend = crouch * -35f;
                float legBlend = 1f - Mathf.Exp(-16f * dt);
                parts.leftLeg.localRotation = Quaternion.Slerp(parts.leftLeg.localRotation, Quaternion.Euler(stride + bend, 0f, 0f), legBlend);
                parts.rightLeg.localRotation = Quaternion.Slerp(parts.rightLeg.localRotation, Quaternion.Euler(-stride + bend, 0f, 0f), legBlend);
            }

            Vector3 left, right;
            switch (pose)
            {
                case Pose.HandsUp: left = new Vector3(180f, 0f, -20f); right = new Vector3(180f, 0f, 20f); break;
                case Pose.Cuffed: left = new Vector3(25f, 0f, 15f); right = new Vector3(25f, 0f, -15f); break;
                case Pose.Shielding: left = new Vector3(-75f, 0f, 35f); right = new Vector3(-80f, 0f, -10f); break;
                case Pose.Interact: left = new Vector3(-70f + Mathf.Sin(breath * 4f) * 6f, 0f, 10f); right = new Vector3(-70f, 0f, -10f); break;
                case Pose.Cower: left = new Vector3(-160f, 0f, 30f); right = new Vector3(-160f, 0f, -30f); break;
                case Pose.Treating: left = new Vector3(-50f + Mathf.Sin(breath * 3f) * 8f, 0f, 15f); right = new Vector3(-50f, 0f, -15f); break;
                case Pose.Aim:
                    left = new Vector3(-80f, 0f, 25f);
                    right = new Vector3(-85f, 0f, -10f);
                    break;
                default:
                    float swing = Mathf.Sin(bobPhase) * 25f * moveBlend;
                    left = new Vector3(swing, 0f, -6f);
                    right = new Vector3(-swing, 0f, 6f);
                    break;
            }
            float switchDip = switching > 0f ? Mathf.Sin(switching * Mathf.PI) : 0f;
            if (pose == Pose.Aim && reload > 0f)
            {
                // Dip the gun and pull the support hand down to the magazine.
                float r = Mathf.Sin(reload * Mathf.PI);
                left += new Vector3(40f * r, 0f, -10f * r);
            }
            if (pose == Pose.Aim)
            {
                right.x -= recoil * 8f;
                right.x += switchDip * 45f;
                left.x += switchDip * 30f;
            }

            float blend = 1f - Mathf.Exp(-14f * dt);
            parts.leftArm.localRotation = Quaternion.Slerp(parts.leftArm.localRotation, Quaternion.Euler(left), blend);
            parts.rightArm.localRotation = Quaternion.Slerp(parts.rightArm.localRotation, Quaternion.Euler(right), blend);

            if (parts.gunRoot != null && parts.gunRoot.gameObject.activeSelf)
            {
                float r = reload > 0f ? Mathf.Sin(reload * Mathf.PI) : 0f;
                bool lowered = pose != Pose.Aim && pose != Pose.Shielding;
                // Small sway while moving and a slow idle drift.
                float swayX = Mathf.Sin(bobPhase * 0.5f) * 2.5f * moveBlend + Mathf.Sin(breath * 0.7f) * 0.6f;
                float swayY = Mathf.Sin(bobPhase) * 1.5f * moveBlend;
                Vector3 targetPosition = new Vector3(0.12f, 1.17f - r * 0.12f - switchDip * 0.25f - (lowered ? 0.25f : 0f), 0.22f - recoil * 0.06f - (lowered ? 0.12f : 0f));
                Quaternion targetRotation = Quaternion.Euler(-recoil * 6f + r * 35f + switchDip * 55f + (lowered ? 45f : 0f) + swayY, swayX, r * -25f);
                // Recoil is instant; everything else eases.
                float gunBlend = recoil > 0.05f ? 1f : 1f - Mathf.Exp(-18f * dt);
                parts.gunRoot.localPosition = Vector3.Lerp(parts.gunRoot.localPosition, targetPosition, gunBlend);
                parts.gunRoot.localRotation = Quaternion.Slerp(parts.gunRoot.localRotation, targetRotation, gunBlend);
            }
        }
    }
}
