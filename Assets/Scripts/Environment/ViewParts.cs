using System.Collections.Generic;
using UnityEngine;

namespace Swat
{
    // The parts of a level that look different from above and in first person. From above, walls
    // and doors are drawn low so you can see into rooms. In first person they're full height, with
    // ceilings, the wall over each doorway and door frame tops, and lamps, signs and wall decor sit
    // higher. A level is built for the top-down view and registers what changes here, so V can
    // switch the view at any time. Colliders keep their full height either way, so switching only
    // moves visuals: where you can walk, see and shoot stays the same.
    public class ViewParts
    {
        struct Stretch
        {
            public Transform transform;
            public BoxCollider box;
            public MeshFilter filter;
            public float low, high, solid, floor, tile;
        }

        struct Lift
        {
            public Transform transform;
            public float low, high;
        }

        readonly List<Stretch> stretched = new List<Stretch>();
        readonly List<Lift> lifted = new List<Lift>();
        readonly List<GameObject> firstPersonOnly = new List<GameObject>();
        public bool FirstPerson { get; private set; }

        // A box (wall, door leaf, frame post) whose height changes, standing on local height 'floor'.
        // Its collider (if any) stays 'solid' tall. 'tile' is the texture tile size of a surfaced box
        // (Shapes.ApplySurface), so the texture keeps its scale; 0 for a plain one.
        public void AddStretch(GameObject go, float low, float high, float solid, float floor = 0f, float tile = 0f)
        {
            if (go == null) return;
            stretched.Add(new Stretch { transform = go.transform, box = go.GetComponent<BoxCollider>(), filter = tile > 0f ? go.GetComponent<MeshFilter>() : null,
                low = low, high = high, solid = solid, floor = floor, tile = tile });
        }

        // Something that sits higher in first person (its local height now, and raised by 'raise').
        public void AddLift(Transform transform, float raise)
        {
            if (transform == null) return;
            float y = transform.localPosition.y;
            lifted.Add(new Lift { transform = transform, low = y, high = y + raise });
        }

        // Ceilings, door headers and frame tops: only there in first person.
        public void AddFirstPersonOnly(GameObject go)
        {
            if (go == null) return;
            go.SetActive(FirstPerson);
            firstPersonOnly.Add(go);
        }

        public void Apply(bool firstPerson)
        {
            FirstPerson = firstPerson;
            foreach (var part in stretched)
            {
                if (part.transform == null) continue;
                float height = firstPerson ? part.high : part.low;
                var scale = part.transform.localScale;
                part.transform.localScale = new Vector3(scale.x, height, scale.z);
                var position = part.transform.localPosition;
                part.transform.localPosition = new Vector3(position.x, part.floor + height * 0.5f, position.z);
                if (part.filter != null) part.filter.sharedMesh = Shapes.TiledCube(part.transform.localScale, part.tile);
                if (part.box == null) continue;
                // The collider is in the box's scaled space: keep it standing on the floor, 'solid' tall.
                float size = part.solid / height;
                part.box.size = new Vector3(part.box.size.x, size, part.box.size.z);
                part.box.center = new Vector3(part.box.center.x, size * 0.5f - 0.5f, part.box.center.z);
            }
            foreach (var part in lifted)
            {
                if (part.transform == null) continue;
                var position = part.transform.localPosition;
                part.transform.localPosition = new Vector3(position.x, firstPerson ? part.high : part.low, position.z);
            }
            foreach (var go in firstPersonOnly)
                if (go != null) go.SetActive(firstPerson);
            Physics.SyncTransforms();
        }
    }
}
