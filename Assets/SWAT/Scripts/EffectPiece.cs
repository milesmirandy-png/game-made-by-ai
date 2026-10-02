using UnityEngine;

namespace Swat
{
    // Drives one piece of an effect: optional falling motion, shrinking and a
    // fading light, then removes itself.
    public class EffectPiece : MonoBehaviour
    {
        public float life = 0.5f;
        public Vector3 velocity;
        public bool physics;
        public bool shrink;

        Light lightSource;
        float startIntensity;
        Vector3 startScale;
        float age;

        void Start()
        {
            lightSource = GetComponent<Light>();
            if (lightSource != null) startIntensity = lightSource.intensity;
            startScale = transform.localScale;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            age += dt;
            float t = Mathf.Clamp01(age / life);

            if (physics)
            {
                velocity += Physics.gravity * dt;
                Vector3 p = transform.position + velocity * dt;
                if (p.y < 0.03f)
                {
                    p.y = 0.03f;
                    velocity = new Vector3(velocity.x * 0.4f, -velocity.y * 0.3f, velocity.z * 0.4f);
                }
                transform.position = p;
            }
            if (shrink) transform.localScale = startScale * (1f - t);
            if (lightSource != null) lightSource.intensity = startIntensity * (1f - t);
            if (age >= life) Destroy(gameObject);
        }
    }
}
