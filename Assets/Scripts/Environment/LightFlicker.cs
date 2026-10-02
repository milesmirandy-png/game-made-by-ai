using UnityEngine;

namespace Swat
{
    // A failing fluorescent tube: the fixture light, its lamp panel and its
    // light pool drop out now and then. Only a few fixtures per map have one.
    public class LightFlicker : MonoBehaviour
    {
        Light fixture;
        Renderer[] lamps;
        Material onMaterial, offMaterial;
        Renderer[] pools;
        float baseIntensity, nextChange;
        bool on = true;
        MaterialPropertyBlock block;

        public void Init(Light light, Renderer[] lampRenderers, Material lit, Material unlit, Renderer[] poolRenderers)
        {
            fixture = light;
            lamps = lampRenderers;
            onMaterial = lit;
            offMaterial = unlit;
            pools = poolRenderers;
            baseIntensity = light != null ? light.intensity : 0f;
            block = new MaterialPropertyBlock();
            nextChange = Time.time + Random.Range(1f, 6f);
        }

        void Update()
        {
            if (Time.time < nextChange) return;
            on = !on;
            // Mostly on, with short bursts of flicker.
            nextChange = Time.time + (on ? Random.Range(1.5f, 7f) : Random.Range(0.04f, 0.18f));
            if (on && Random.value < 0.3f) nextChange = Time.time + Random.Range(0.05f, 0.12f);
            if (fixture != null) fixture.intensity = on ? baseIntensity : baseIntensity * 0.15f;
            if (lamps != null)
                foreach (var lamp in lamps) if (lamp != null) lamp.sharedMaterial = on ? onMaterial : offMaterial;
            block.SetColor("_Color", on ? Color.white : new Color(1f, 1f, 1f, 0.15f));
            if (pools != null)
                foreach (var pool in pools) if (pool != null) pool.SetPropertyBlock(block);
        }
    }
}
