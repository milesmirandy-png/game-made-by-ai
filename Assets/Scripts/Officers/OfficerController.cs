using UnityEngine;

namespace Swat
{
    // An AI officer's body: model, flashlight, selection ring and animation.
    // SquadAI decides what to do; this makes it visible.
    public class OfficerController : MonoBehaviour
    {
        static readonly Color Selected = new Color(0.35f, 0.85f, 1f);
        static readonly Color Unselected = new Color(0.15f, 0.3f, 0.65f);
        static readonly Color DownedRing = new Color(0.9f, 0.3f, 0.2f);

        public CharacterParts Parts { get; private set; }
        public ProceduralAnimator Animator { get; private set; }

        public void Init(CharacterParts parts)
        {
            Parts = parts;
            Animator = new ProceduralAnimator(parts);
            Animator.SetPose(Pose.Aim);
        }

        public void ShowSelected(bool selected, bool downed)
        {
            Parts.SetRingColor(downed ? DownedRing : selected ? Selected : Unselected);
        }
    }
}
