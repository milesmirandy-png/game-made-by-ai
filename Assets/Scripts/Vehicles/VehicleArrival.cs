using UnityEngine;

namespace Swat
{
    // A short scripted arrival: the van drives up to its parking spot, then
    // the team deploys from the back. Purely visual; skippable with a click.
    public class VehicleArrival : MonoBehaviour
    {
        const float Duration = 2.8f;
        Vector3 from, to;
        float startTime;
        public bool Done { get; private set; }

        public static VehicleArrival Begin(Transform van, Vector3 from, Vector3 to)
        {
            var arrival = van.gameObject.AddComponent<VehicleArrival>();
            arrival.from = from;
            arrival.to = to;
            arrival.startTime = Time.time;
            van.position = from;
            return arrival;
        }

        public void Skip()
        {
            transform.position = to;
            Done = true;
            enabled = false;
        }

        void Update()
        {
            float t = Mathf.Clamp01((Time.time - startTime) / Duration);
            float eased = 1f - (1f - t) * (1f - t);   // slow down as it pulls in
            transform.position = Vector3.Lerp(from, to, eased);
            if (t < 1f) return;
            Done = true;
            enabled = false;
        }
    }
}
