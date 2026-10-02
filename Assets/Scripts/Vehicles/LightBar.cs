using UnityEngine;

namespace Swat
{
    // Alternates the van's red and blue lights. Only touches materials twice a second.
    public class LightBar : MonoBehaviour
    {
        Renderer red, blue;
        bool phase;
        float next;

        public void Init(Renderer redLight, Renderer blueLight)
        {
            red = redLight;
            blue = blueLight;
        }

        void Update()
        {
            if (Time.unscaledTime < next) return;
            next = Time.unscaledTime + 0.4f;
            phase = !phase;
            red.sharedMaterial = Shapes.Mat(new Color(1f, 0.1f, 0.1f), phase ? 4f : 0.3f);
            blue.sharedMaterial = Shapes.Mat(new Color(0.15f, 0.35f, 1f), phase ? 0.3f : 4f);
        }
    }
}
