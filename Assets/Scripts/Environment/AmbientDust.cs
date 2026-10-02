using UnityEngine;

namespace Swat
{
    // A few slow-drifting dust motes around the team leader indoors. One small
    // particle system (at most 50 particles) that follows the player; only on
    // presets with full effects, and switched off outdoors.
    public class AmbientDust : MonoBehaviour
    {
        ParticleSystem system;
        Transform follow;

        public static AmbientDust Create(Transform parent)
        {
            var go = new GameObject("Ambient Dust");
            go.transform.SetParent(parent, false);
            var dust = go.AddComponent<AmbientDust>();
            dust.system = go.AddComponent<ParticleSystem>();
            dust.system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = dust.system.main;
            main.loop = true;
            main.startLifetime = 7f;
            main.startSpeed = 0.06f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.06f);
            main.startColor = new Color(1f, 0.97f, 0.9f, 0.22f);
            main.maxParticles = 50;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = -0.002f;

            var emission = dust.system.emission;
            emission.rateOverTime = 7f;

            var shape = dust.system.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(14f, 1.6f, 10f);
            shape.position = new Vector3(0f, 1f, 0f);

            var noise = dust.system.noise;
            noise.enabled = true;
            noise.strength = 0.05f;
            noise.frequency = 0.3f;

            var fade = dust.system.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.3f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = Shapes.GlowMaterial(ProceduralTextures.Dot);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return dust;
        }

        public void Follow(Transform target)
        {
            follow = target;
        }

        public void SetActive(bool on)
        {
            on = on && QualityManager.Current.particleScale >= 0.99f;
            if (on && !system.isPlaying) system.Play();
            else if (!on && system.isPlaying) system.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        void LateUpdate()
        {
            if (follow != null) transform.position = follow.position;
        }
    }
}
