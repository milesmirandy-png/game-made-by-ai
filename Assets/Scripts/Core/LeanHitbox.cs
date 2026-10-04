using UnityEngine;

namespace Swat
{
    // Peeking moves your upper body out past cover, so it moves where you can
    // be hit too: this small capsule follows the lean (on top of the usual
    // body capsule, which stays behind the corner). Off while standing straight.
    public static class LeanHitbox
    {
        public static CapsuleCollider Create(Transform owner, Collider body)
        {
            var go = new GameObject("Lean Hitbox");
            go.transform.SetParent(owner, false);
            go.layer = Layers.Characters;
            var box = go.AddComponent<CapsuleCollider>();
            box.radius = 0.2f;
            box.height = 0.95f;
            box.enabled = false;
            // Your own movement capsule must not bump into it.
            if (body != null) Physics.IgnoreCollision(body, box);
            return box;
        }

        // meters: how far out the upper body is (negative = left).
        public static void Place(CapsuleCollider box, float meters, bool crouched)
        {
            if (box == null) return;
            bool on = Mathf.Abs(meters) > 0.08f;
            if (box.enabled != on) box.enabled = on;
            if (on) box.transform.localPosition = new Vector3(meters, crouched ? 0.8f : 1.2f, 0f);
        }
    }
}
