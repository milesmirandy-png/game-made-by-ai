using UnityEngine;

namespace Swat
{
    // A tagged-out character tipping over: the body falls away from the shot
    // that dropped it, gathering speed like a real fall, slides a little and
    // lands with a thud and a puff of dust. It runs on its own (some owners
    // stop animating a character once it is down) and removes itself when done.
    public class Topple : MonoBehaviour
    {
        const float Duration = 0.34f;

        Quaternion from, to;
        Vector3 fromPosition, toPosition;
        float started;
        bool landed;

        public static void Begin(Transform model, Vector3 localDirection)
        {
            if (model == null) return;
            Stop(model);
            var topple = model.gameObject.AddComponent<Topple>();
            localDirection.y = 0f;
            if (localDirection.sqrMagnitude < 0.01f) localDirection = Vector3.back;
            localDirection.Normalize();
            topple.from = model.localRotation;
            topple.fromPosition = model.localPosition;
            // Lying along the push, slid back a little by it.
            topple.to = Quaternion.FromToRotation(Vector3.up, localDirection);
            topple.toPosition = new Vector3(0f, 0.2f, 0f) + localDirection * 0.45f;
            topple.started = Time.time;
        }

        public static void Stop(Transform model)
        {
            if (model == null) return;
            // Disabled at once (Destroy only happens at the end of the frame, and a fall still
            // running this frame would undo a stand-up made earlier in it).
            foreach (var running in model.GetComponents<Topple>())
            {
                running.enabled = false;
                Destroy(running);
            }
        }

        void Update()
        {
            float t = Mathf.Clamp01((Time.time - started) / Duration);
            float fall = t * t; // speeds up as it goes, like falling
            transform.localRotation = Quaternion.Slerp(from, to, fall);
            transform.localPosition = Vector3.Lerp(fromPosition, toPosition, 1f - (1f - t) * (1f - t));
            if (t < 1f || landed) return;
            landed = true;
            Vector3 where = transform.position;
            AudioManager.Play(Sound.BodyFall, where, 0.5f, Random.Range(0.9f, 1.1f));
            if (EffectsManager.Instance != null) EffectsManager.Instance.Burst(where + Vector3.up * 0.1f, Vector3.up, new Color(0.6f, 0.58f, 0.54f), 5, 1.6f, 0.07f);
            Destroy(this);
        }
    }
}
