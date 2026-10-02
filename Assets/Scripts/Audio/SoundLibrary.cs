using UnityEngine;

namespace Swat
{
    public enum Sound
    {
        Pistol, Smg, Rifle, Shotgun, EnemyShot, Reload, Empty, Footstep, DoorOpen, DoorLocked,
        Breach, Flashbang, Smoke, Throw, Detect, Hit, Hurt, Shout, Rescue, Click, Complete, Fail,
        CompactSmg, Carbine, HeavyPistol, LessLethal, Kick, Wedge, Lockpick, Medkit, Radio, CameraAlert,
        Console, Alarm, RoomTone, Wind, Gasp, UiHover, UiSelect, Unlock, LightPlace, TargetHit, Ability,
    }

    // Placeholder sound effects synthesized from simple waveforms at startup,
    // so the project ships with no audio files. Swap in real clips later by
    // replacing entries in the returned array.
    public static class SoundLibrary
    {
        const int SampleRate = 22050;
        const float Tau = Mathf.PI * 2f;
        static readonly System.Random rng = new System.Random(11);

        public static AudioClip[] Build()
        {
            var clips = new AudioClip[System.Enum.GetValues(typeof(Sound)).Length];
            clips[(int)Sound.Pistol] = Gunshot("Pistol", 18f, 120f, 0.6f, 0.4f);
            clips[(int)Sound.Smg] = Gunshot("SMG", 24f, 140f, 0.7f, 0.3f);
            clips[(int)Sound.CompactSmg] = Gunshot("Compact SMG", 28f, 160f, 0.75f, 0.25f);
            clips[(int)Sound.Rifle] = Gunshot("Rifle", 13f, 80f, 0.5f, 0.5f);
            clips[(int)Sound.Carbine] = Gunshot("Carbine", 10f, 70f, 0.55f, 0.6f);
            clips[(int)Sound.Shotgun] = Gunshot("Shotgun", 8f, 55f, 0.35f, 0.7f);
            clips[(int)Sound.HeavyPistol] = Gunshot("Heavy Pistol", 11f, 65f, 0.45f, 0.55f);
            clips[(int)Sound.LessLethal] = Make("Less Lethal", 0.35f, t => (Noise() * 0.4f + Mathf.Sin(Tau * 90f * t)) * Mathf.Exp(-t * 18f));
            clips[(int)Sound.EnemyShot] = Gunshot("Enemy Shot", 15f, 100f, 0.55f, 0.45f);
            clips[(int)Sound.Reload] = Make("Reload", 0.5f, t => Click(t, 0f) + Click(t, 0.32f));
            clips[(int)Sound.Empty] = Make("Empty", 0.08f, t => Click(t, 0f));
            clips[(int)Sound.Footstep] = Thud("Footstep", 0.12f, 45f, 80f);
            clips[(int)Sound.DoorOpen] = Make("Door", 0.45f, t => Mathf.Sin(Tau * (260f + 90f * Mathf.Sin(t * 30f)) * t) * Mathf.Sin(Mathf.PI * t / 0.45f) * 0.5f + Noise() * 0.1f * Mathf.Exp(-t * 8f));
            clips[(int)Sound.DoorLocked] = Make("Locked", 0.3f, t => Click(t, 0f) + Click(t, 0.1f) + Click(t, 0.2f));
            clips[(int)Sound.Kick] = Thud("Kick", 0.35f, 12f, 55f);
            clips[(int)Sound.Wedge] = Make("Wedge", 0.2f, t => Click(t, 0f) * 0.6f + Mathf.Sin(Tau * 180f * t) * Mathf.Exp(-t * 30f));
            clips[(int)Sound.Lockpick] = Make("Lockpick", 0.6f, t => Click(t, 0f) * 0.4f + Click(t, 0.2f) * 0.4f + Click(t, 0.41f) * 0.4f);
            clips[(int)Sound.Breach] = Bang("Breach", 0.9f, 7f, 0.2f, 50f);
            clips[(int)Sound.Flashbang] = Bang("Flashbang", 1.4f, 3.5f, 0.45f, 45f);
            clips[(int)Sound.Smoke] = Hiss("Smoke", 1.4f);
            clips[(int)Sound.Throw] = Hiss("Throw", 0.25f);
            clips[(int)Sound.Medkit] = Hiss("Medkit", 0.5f);
            clips[(int)Sound.Detect] = Make("Detect", 0.26f, t => Mathf.Sign(Mathf.Sin(Tau * (t < 0.12f ? 880f : 660f) * t)) * 0.3f * Mathf.Exp(-(t % 0.13f) * 12f));
            clips[(int)Sound.Hit] = Make("Hit", 0.06f, t => Mathf.Sin(Tau * 1700f * t) * Mathf.Exp(-t * 55f));
            clips[(int)Sound.TargetHit] = Make("Target", 0.12f, t => Mathf.Sin(Tau * 900f * t) * Mathf.Exp(-t * 30f) + Click(t, 0f) * 0.4f);
            clips[(int)Sound.Hurt] = Make("Hurt", 0.3f, t => Mathf.Sin(Tau * (140f - 200f * t) * t) * Mathf.Exp(-t * 10f) + Noise() * 0.3f * Mathf.Exp(-t * 30f));
            clips[(int)Sound.Shout] = Radio("Shout", 0.32f, 1200f);
            clips[(int)Sound.Radio] = Radio("Radio", 0.22f, 1500f);
            clips[(int)Sound.Rescue] = Make("Rescue", 0.25f, t => Mathf.Sin(Tau * (600f + 1200f * t) * t) * Mathf.Exp(-t * 6f));
            clips[(int)Sound.Unlock] = Make("Unlock", 0.3f, t => Mathf.Sin(Tau * (700f + 900f * t) * t) * Mathf.Exp(-t * 8f) + Click(t, 0f) * 0.3f);
            clips[(int)Sound.Click] = Make("Click", 0.05f, t => Click(t, 0f));
            clips[(int)Sound.UiHover] = Make("Hover", 0.04f, t => Mathf.Sin(Tau * 2200f * t) * Mathf.Exp(-t * 90f) * 0.4f);
            clips[(int)Sound.UiSelect] = Make("Select", 0.12f, t => Mathf.Sin(Tau * (900f + 600f * t) * t) * Mathf.Exp(-t * 25f));
            clips[(int)Sound.CameraAlert] = Make("Camera", 0.3f, t => Mathf.Sign(Mathf.Sin(Tau * 1400f * t)) * 0.25f * (Mathf.Repeat(t, 0.1f) < 0.05f ? 1f : 0f));
            clips[(int)Sound.Console] = Make("Console", 0.25f, t => Mathf.Sin(Tau * (t < 0.08f ? 1000f : 1500f) * t) * 0.5f * Mathf.Exp(-(t % 0.08f) * 20f));
            clips[(int)Sound.LightPlace] = Make("Light", 0.15f, t => Click(t, 0f) + Mathf.Sin(Tau * 600f * t) * Mathf.Exp(-t * 20f) * 0.3f);
            clips[(int)Sound.Ability] = Make("Ability", 0.4f, t => Mathf.Sin(Tau * (400f + 800f * t) * t) * Mathf.Sin(Mathf.PI * t / 0.4f));
            clips[(int)Sound.Gasp] = Make("Gasp", 0.35f, t => (Noise() * 0.5f + Mathf.Sin(Tau * (500f - 300f * t) * t) * 0.3f) * Mathf.Sin(Mathf.PI * t / 0.35f));
            clips[(int)Sound.Complete] = Tones("Complete", new[] { 523.3f, 659.3f, 784f, 1046.5f }, 0.14f);
            clips[(int)Sound.Fail] = Tones("Fail", new[] { 392f, 311.1f, 233.1f }, 0.28f);
            // Loops
            clips[(int)Sound.Alarm] = Make("Alarm", 1.2f, t => Mathf.Sign(Mathf.Sin(Tau * (t < 0.6f ? 760f : 620f) * t)) * 0.25f);
            clips[(int)Sound.RoomTone] = Drone("Room Tone", 4f, 0.04f, 55f);
            clips[(int)Sound.Wind] = Drone("Wind", 4f, 0.12f, 0f);
            return clips;
        }

        static float Click(float t, float at)
        {
            float local = t - at;
            if (local < 0f) return 0f;
            return Noise() * Mathf.Exp(-local * 180f) + Mathf.Sin(Tau * 2400f * local) * Mathf.Exp(-local * 120f) * 0.5f;
        }

        static AudioClip Gunshot(string name, float decay, float thump, float brightness, float seconds)
        {
            float filtered = 0f;
            return Make(name, seconds, t =>
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

        static AudioClip Thud(string name, float seconds, float decay, float pitch)
        {
            float filtered = 0f;
            return Make(name, seconds, t =>
            {
                filtered = Mathf.Lerp(filtered, Noise(), 0.15f);
                return (filtered + Mathf.Sin(Tau * pitch * t) * 0.6f) * Mathf.Exp(-t * decay);
            });
        }

        static AudioClip Hiss(string name, float seconds)
        {
            float filtered = 0f;
            return Make(name, seconds, t =>
            {
                float n = Noise();
                filtered = Mathf.Lerp(filtered, n, 0.3f);
                return (n - filtered) * Mathf.Sin(Mathf.PI * t / seconds);
            });
        }

        static AudioClip Radio(string name, float seconds, float tone)
        {
            return Make(name, seconds, t => t < 0.1f
                ? Mathf.Sign(Mathf.Sin(Tau * tone * t)) * 0.35f + Noise() * 0.15f
                : Noise() * 0.45f * Mathf.Exp(-(t - 0.1f) * 22f));
        }

        // A soft, loopable background bed: filtered noise plus an optional hum.
        static AudioClip Drone(string name, float seconds, float smoothing, float hum)
        {
            float filtered = 0f;
            return Make(name, seconds, t =>
            {
                filtered = Mathf.Lerp(filtered, Noise(), smoothing);
                float edge = Mathf.Clamp01(Mathf.Min(t, seconds - t) * 4f); // fade the ends so the loop seam is quiet
                return (filtered + (hum > 0f ? Mathf.Sin(Tau * hum * t) * 0.3f : 0f)) * edge;
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
