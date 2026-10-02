using UnityEngine;

namespace Swat
{
    // A thrown stun grenade. After a short fuse it goes off with a blinding
    // flash that stuns every suspect who can see it, and the officer too if
    // they are looking at it.
    public class Flashbang : MonoBehaviour
    {
        float fuse = 1.5f;

        public static void Throw(Vector3 position, Vector3 velocity, Collider thrower)
        {
            var game = GameManager.Instance;
            var go = Shapes.Make(PrimitiveType.Cylinder, "Flashbang", game != null ? game.LevelRoot : null, Vector3.zero,
                new Vector3(0.08f, 0.07f, 0.08f), new Color(0.25f, 0.3f, 0.22f));
            go.transform.SetPositionAndRotation(position, Random.rotation);
            var body = go.AddComponent<Rigidbody>();
            body.mass = 0.4f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.AddForce(velocity, ForceMode.VelocityChange);
            body.AddTorque(Random.insideUnitSphere * 8f, ForceMode.VelocityChange);
            if (thrower != null) Physics.IgnoreCollision(go.GetComponent<Collider>(), thrower);
            go.AddComponent<Flashbang>();
        }

        void Update()
        {
            fuse -= Time.deltaTime;
            if (fuse > 0f) return;

            GetComponent<Collider>().enabled = false; // so it can't block its own line-of-sight checks
            Vector3 position = transform.position + Vector3.up * 0.1f;
            Effects.Flash(position, Color.white, 10f, 16f, 0.3f);
            Effects.Burst(position, Vector3.up, Color.white, 10, 4f, 0.04f, 4f);
            Sfx.PlayAt(Sfx.Flashbang, position, 1f, 12f);
            var game = GameManager.Instance;
            if (game != null) game.OnFlashbang(position);
            Destroy(gameObject);
        }
    }
}
