using UnityEngine;

namespace Swat
{
    public enum RoomState { Undiscovered, Discovered, Investigated, Secured, Complete }

    // One room of a level. Tracks what the team knows about it (shown on the
    // tactical map) and whether it is dark. State is updated a few times a
    // second by TacticalIntel, not with physics triggers.
    public class RoomController : MonoBehaviour
    {
        public string Id { get; private set; }
        public string DisplayName { get; private set; }
        public Bounds Bounds { get; private set; }
        public int Area { get; private set; }
        public bool Indoor { get; private set; }
        public bool IsDark { get; set; }
        public RoomState State { get; private set; }
        public Light Fixture { get; set; }
        public RoomStyle Style { get; set; }
        public GameObject Floor { get; set; }

        public static RoomController Create(Transform parent, string id, string name, Bounds bounds, int area, bool indoor)
        {
            var go = new GameObject("Room " + name);
            go.transform.SetParent(parent, false);
            go.transform.position = bounds.center;
            var room = go.AddComponent<RoomController>();
            room.Id = id;
            room.DisplayName = name;
            room.Bounds = bounds;
            room.Area = area;
            room.Indoor = indoor;
            room.State = indoor ? RoomState.Undiscovered : RoomState.Discovered;
            return room;
        }

        public bool Contains(Vector3 point)
        {
            var b = Bounds;
            return point.x >= b.min.x && point.x <= b.max.x && point.z >= b.min.z && point.z <= b.max.z;
        }

        public void Advance(RoomState next)
        {
            if (next <= State) return;
            State = next;
            if (next == RoomState.Secured && Indoor) UIManager.Notify(DisplayName + " secured");
        }

        public void SetLights(bool on, bool dynamicLights)
        {
            if (Fixture != null) Fixture.enabled = on && dynamicLights;
        }
    }
}
