using UnityEngine;

namespace Swat
{
    public enum Pose { Aim, Relaxed, HandsUp, Cuffed, Shielding, Interact, Cower, Treating }

    // Lightweight procedural animation for the blocky characters: walking and
    // running with leg swing, idle breathing, crouching, aim and idle poses,
    // firing recoil, reloading, weapon switching, weapon sway, flinching when
    // hit, peeking (leaning sideways), sliding, toppling over when tagged out,
    // interacting and surrendering. Everything blends smoothly instead of
    // snapping. No Animator or rig needed; ticked by whoever owns the character.
    public class ProceduralAnimator
    {
        readonly CharacterParts parts;
        Pose pose = Pose.Relaxed;
        float crouch, crouchTarget, recoil, reload, switching, bobPhase, moveBlend, runBlend, lean, breath, flinch;
        float sideLean, sideLeanTarget, slide, slideTarget, lastHitTime = -10f;
        Vector3 flinchAxis = Vector3.right, lastHitDirection;
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
        public void SetLean(float amount) { sideLeanTarget = Mathf.Clamp(amount, -1f, 1f); } // -1 left .. +1 right
        public void SetSlide(bool sliding) { slideTarget = sliding ? 1f : 0f; }

        // A short flinch away from the hit direction (no gore, just a jolt).
        public void Hit(Vector3 direction)
        {
            flinch = 1f;
            lastHitDirection = direction;
            lastHitTime = Time.time;
            Vector3 local = parts.root != null ? parts.root.InverseTransformDirection(direction) : direction;
            flinchAxis = new Vector3(local.z, 0f, -local.x).normalized;
            if (flinchAxis.sqrMagnitude < 0.01f) flinchAxis = Vector3.right;
            if (!down) parts.Flash(Color.white, 0.07f);
        }

        public void SetDown(bool isDown)
        {
            SetDown(isDown, Vector3.zero);
        }

        // Down: topple away from the shot (the given direction, or the last hit if recent;
        // straight back if neither). Up again: stand straight.
        public void SetDown(bool isDown, Vector3 direction)
        {
            bool was = down;
            down = isDown;
            if (down)
            {
                if (was) return;
                if (direction.sqrMagnitude < 0.01f && Time.time - lastHitTime < 0.6f) direction = lastHitDirection;
                Vector3 local = direction.sqrMagnitude > 0.01f && parts.root != null ? parts.root.InverseTransformDirection(direction) : Vector3.back;
                parts.Fall(false);
                Topple.Begin(parts.model, local);
            }
            else
            {
                parts.Rise();
                crouch = crouchTarget = recoil = flinch = sideLean = sideLeanTarget = slide = slideTarget = 0f;
            }
        }

        public void Tick(float dt, float speed, bool running)
        {
            parts.UpdateFlash();
            if (down || dt <= 0f) return;
            crouch = Mathf.MoveTowards(crouch, crouchTarget, dt * 5f);
            sideLean = Mathf.Lerp(sideLean, sideLeanTarget, 1f - Mathf.Exp(-14f * dt));
            slide = Mathf.MoveTowards(slide, slideTarget, dt * 8f);
            recoil = Mathf.MoveTowards(recoil, 0f, dt * 7f);
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
            // Peeking: the body shifts and tilts to the side (about 0.55 m at the chest at full lean).
            // Sliding: leaning back, low, legs out in front.
            parts.model.localPosition = new Vector3(sideLean * 0.25f, (bob + idleBreath) * (1f - slide) - crouch * 0.38f - slide * 0.12f, 0f);
            var flinchRotation = Quaternion.AngleAxis(-flinch * 14f, flinchAxis);
            // Firing rocks the body back a touch (heavier guns more).
            parts.model.localRotation = flinchRotation * Quaternion.Euler(lean - recoil * 4f - slide * 26f, 0f, -sideLean * 15f);

            // Legs: swing when walking, stride further when running, bend when crouched.
            if (parts.leftLeg != null)
            {
                float stride = Mathf.Sin(bobPhase) * Mathf.Lerp(26f, 40f, runBlend) * moveBlend * (1f - slide);
                float bend = crouch * -35f * (1f - slide);
                float legBlend = 1f - Mathf.Exp(-16f * dt);
                parts.leftLeg.localRotation = Quaternion.Slerp(parts.leftLeg.localRotation, Quaternion.Euler(stride + bend - slide * 70f, 0f, 0f), legBlend);
                parts.rightLeg.localRotation = Quaternion.Slerp(parts.rightLeg.localRotation, Quaternion.Euler(-stride + bend - slide * 25f, 0f, 0f), legBlend);
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
            // Arms that hold the gun by reach (the imported soldier's) are posed in PoseSoldierArms instead.
            bool gunOut = parts.gunRoot != null && parts.gunRoot.gameObject.activeSelf;
            bool reachRight = parts.rightForearm != null && gunOut && (pose == Pose.Aim || pose == Pose.Shielding);
            bool reachLeft = parts.leftForearm != null && gunOut && pose == Pose.Aim;
            if (!reachLeft) parts.leftArm.localRotation = Quaternion.Slerp(parts.leftArm.localRotation, Quaternion.Euler(left), blend);
            if (!reachRight) parts.rightArm.localRotation = Quaternion.Slerp(parts.rightArm.localRotation, Quaternion.Euler(right), blend);

            if (parts.gunRoot != null && parts.gunRoot.gameObject.activeSelf)
            {
                float r = reload > 0f ? Mathf.Sin(reload * Mathf.PI) : 0f;
                bool lowered = pose != Pose.Aim && pose != Pose.Shielding;
                // Small sway while moving and a slow idle drift.
                float swayX = Mathf.Sin(bobPhase * 0.5f) * 2.5f * moveBlend + Mathf.Sin(breath * 0.7f) * 0.6f;
                float swayY = Mathf.Sin(bobPhase) * 1.5f * moveBlend;
                Vector3 hold = parts.gunHold;
                Vector3 targetPosition = new Vector3(hold.x, hold.y - r * 0.12f - switchDip * 0.25f - (lowered ? 0.25f : 0f), hold.z - recoil * 0.1f - (lowered ? 0.12f : 0f));
                Quaternion targetRotation = Quaternion.Euler(-recoil * 10f + r * 35f + switchDip * 55f + (lowered ? 45f : 0f) + swayY, swayX, r * -25f);
                // Recoil is instant; everything else eases.
                float gunBlend = recoil > 0.05f ? 1f : 1f - Mathf.Exp(-18f * dt);
                parts.gunRoot.localPosition = Vector3.Lerp(parts.gunRoot.localPosition, targetPosition, gunBlend);
                parts.gunRoot.localRotation = Quaternion.Slerp(parts.gunRoot.localRotation, targetRotation, gunBlend);
            }
            if (parts.leftForearm != null) PoseSoldierArms(dt);
        }

        // The imported soldier's arms: aiming, both hands go on the gun (trigger hand on the grip, the
        // other on the handguard, or on the magazine mid-reload), following recoil and sway; in every other
        // pose the forearms straighten and the arms swing as above.
        void PoseSoldierArms(float dt)
        {
            float blend = recoil > 0.05f ? 1f : 1f - Mathf.Exp(-20f * dt);
            bool holding = parts.gunRoot != null && parts.gunRoot.gameObject.activeSelf && (pose == Pose.Aim || pose == Pose.Shielding);
            if (!holding)
            {
                parts.leftForearm.localRotation = Quaternion.Slerp(parts.leftForearm.localRotation, Quaternion.identity, blend);
                parts.rightForearm.localRotation = Quaternion.Slerp(parts.rightForearm.localRotation, Quaternion.identity, blend);
                return;
            }
            float length = parts.muzzle != null ? parts.muzzle.localPosition.z : 0.5f;
            bool pistol = length < 0.35f;
            Reach(parts.rightArm, parts.rightForearm, parts.rightHand, parts.gunRoot.TransformPoint(new Vector3(0f, -0.045f, -0.02f)), 1f, blend);
            if (pose == Pose.Shielding)
            {
                // The other arm holds the shield.
                parts.leftForearm.localRotation = Quaternion.Slerp(parts.leftForearm.localRotation, Quaternion.identity, blend);
                return;
            }
            Vector3 support = pistol ? new Vector3(-0.01f, -0.06f, 0.02f) : new Vector3(0f, -0.025f, Mathf.Min(0.24f, length * 0.45f));
            if (reload > 0f) support = Vector3.Lerp(support, new Vector3(0f, -0.13f, 0.06f), Mathf.Sin(reload * Mathf.PI));
            Reach(parts.leftArm, parts.leftForearm, parts.leftHand, parts.gunRoot.TransformPoint(support), -1f, blend);
        }

        // Two-bone reach: swings the upper arm and forearm (rigid parts) so the hand lands on target,
        // the elbow bending down and out to the side (side: -1 left, +1 right).
        void Reach(Transform upper, Transform forearm, Transform hand, Vector3 target, float side, float blend)
        {
            var model = parts.model;
            Vector3 shoulder = upper.position;
            float l1 = upper.TransformVector(forearm.localPosition).magnitude;
            float l2 = forearm.TransformVector(hand.localPosition).magnitude;
            Vector3 toTarget = target - shoulder;
            if (toTarget.sqrMagnitude < 1e-6f || l1 < 1e-4f || l2 < 1e-4f) return;
            float dist = Mathf.Clamp(toTarget.magnitude, Mathf.Abs(l1 - l2) + 0.01f, l1 + l2 - 0.005f);
            Vector3 dir = toTarget.normalized;
            Vector3 bend = Vector3.ProjectOnPlane(-model.up + model.right * side * 0.6f - model.forward * 0.2f, dir);
            bend = bend.sqrMagnitude > 1e-6f ? bend.normalized : model.right * side;
            float a = (l1 * l1 - l2 * l2 + dist * dist) / (2f * dist);
            float h = Mathf.Sqrt(Mathf.Max(0f, l1 * l1 - a * a));
            Vector3 elbow = shoulder + dir * a + bend * h;
            Vector3 reach = shoulder + dir * dist;
            // Turn each rest direction (shoulder to elbow, elbow to hand, as modelled) onto the new one.
            var upperRotation = Quaternion.FromToRotation(forearm.localPosition, upper.parent.InverseTransformDirection(elbow - shoulder));
            upper.localRotation = Quaternion.Slerp(upper.localRotation, upperRotation, blend);
            var foreRotation = Quaternion.FromToRotation(hand.localPosition, upper.InverseTransformDirection(reach - forearm.position));
            forearm.localRotation = Quaternion.Slerp(forearm.localRotation, foreRotation, blend);
        }
    }
}
