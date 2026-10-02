using UnityEngine;

namespace Swat
{
    // Every sound effect is synthesized when the game starts, so there are no
    // audio files to import.
    public static class Sfx
    {
        public static AudioClip Rifle, Pistol, Flashbang, Breach, Click, HitMarker, Hurt, Radio, Success, Fail;

        const int SampleRate = 44100;
        const float Tau = Mathf.PI * 2f;
        static readonly System.Random rng = new System.Random(7);

        public static void Build()
        {
            if (Rifle != null) return;
            Rifle = Gunshot("Rifle", 13f, 75f, 0.5f);
            Pistol = Gunshot("Pistol", 18f, 120f, 0.65f);
            Flashbang = Bang("Flashbang", 1.6f, 3f, 0.35f, 45f);
            Breach = Bang("Breach", 0.5f, 10f, 0.18f, 60f);
            Click = Make("Click", 0.06f, t => Noise() * Mathf.Exp(-t * 180f) + Mathf.Sin(Tau * 2400f * t) * Mathf.Exp(-t * 120f) * 0.5f);
            HitMarker = Make("HitMarker", 0.07f, t => Mathf.Sin(Tau * 1700f * t) * Mathf.Exp(-t * 55f));
            Hurt = Make("Hurt", 0.3f, t => Mathf.Sin(Tau * (140f - 200f * t) * t) * Mathf.Exp(-t * 10f) + Noise() * 0.3f * Mathf.Exp(-t * 30f));
            Radio = Make("Radio", 0.32f, t => t < 0.12f
                ? Mathf.Sign(Mathf.Sin(Tau * 1200f * t)) * 0.35f + Noise() * 0.15f
                : Noise() * 0.5f * Mathf.Exp(-(t - 0.12f) * 20f));
            Success = Tones("Success", new[] { 523.3f, 659.3f, 784f, 1046.5f }, 0.14f);
            Fail = Tones("Fail", new[] { 392f, 311.1f, 233.1f }, 0.28f);
        }

        // Plays a sound in the world, so it gets quieter with distance.
        public static void PlayAt(AudioClip clip, Vector3 position, float volume, float minDistance = 4f, float pitch = 1f)
        {
            if (clip == null) return;
            var go = new GameObject("Sound " + clip.name);
            go.transform.position = position;
            var source = go.AddComponent<AudioSource>();
            source.clip = clip;
            source.volume = volume;
            source.pitch = pitch;
            source.spatialBlend = 1f;
            source.minDistance = minDistance;
            source.maxDistance = 80f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.Play();
            Object.Destroy(go, clip.length / Mathf.Max(0.1f, pitch) + 0.1f);
        }

        static AudioClip Gunshot(string name, float decay, float thump, float brightness)
        {
            float filtered = 0f;
            return Make(name, 0.6f, t =>
            {
                filtered = Mathf.Lerp(filtered, Noise(), brightness);
                return filtered * Mathf.Exp(-t * decay) + Mathf.Sin(Tau * thump * t) * Mathf.Exp(-t * 28f) * 0.8f;
            });
        }

        static AudioClip Bang(string name, float seconds, float decay, float brightness, float thump)
        {
            float filtered = 0f;
            return Make(name, seconds, t =>
            {
                filtered = Mathf.Lerp(filtered, Noise(), brightness);
                return filtered * Mathf.Exp(-t * decay) + Mathf.Sin(Tau * thump * t) * Mathf.Exp(-t * 8f);
            });
        }

        static AudioClip Tones(string name, float[] notes, float noteLength)
        {
            return Make(name, notes.Length * noteLength + 0.4f, t =>
            {
                int i = Mathf.Min((int)(t / noteLength), notes.Length - 1);
                float local = t - i * noteLength;
                return (Mathf.Sin(Tau * notes[i] * t) + 0.3f * Mathf.Sin(Tau * notes[i] * 2f * t)) * Mathf.Exp(-local * 5f);
            });
        }

        static AudioClip Make(string name, float seconds, System.Func<float, float> wave)
        {
            int count = Mathf.CeilToInt(seconds * SampleRate);
            var data = new float[count];
            float peak = 0.0001f;
            for (int i = 0; i < count; i++)
            {
                data[i] = wave(i / (float)SampleRate);
                peak = Mathf.Max(peak, Mathf.Abs(data[i]));
            }
            float gain = 0.9f / peak;
            for (int i = 0; i < count; i++) data[i] *= gain;

            var clip = AudioClip.Create(name, count, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static float Noise()
        {
            return (float)(rng.NextDouble() * 2.0 - 1.0);
        }
    }
}
