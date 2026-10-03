using UnityEngine;

namespace Swat
{
    public enum Sound
    {
        Pistol, Smg, Rifle, Shotgun, EnemyShot, Reload, Empty, Footstep, DoorOpen, DoorLocked,
        Breach, Flashbang, Smoke, Throw, Detect, Hit, Hurt, Shout, Rescue, Click, Complete, Fail,
        CompactSmg, Carbine, HeavyPistol, LessLethal, Kick, Wedge, Lockpick, Medkit, Radio, CameraAlert,
        Console, Alarm, RoomTone, Wind, Gasp, UiHover, UiSelect, Unlock, LightPlace, TargetHit, Ability,
        // Added in the polish update.
        StepConcrete, StepCarpet, StepTile, StepMetal, StepGrass, StepAsphalt,
        DoorClose, DoorOpenMetal, DoorHandle, Shell, RicochetMetal, ImpactGlass,
        RadioVoice1, RadioVoice2, RadioVoice3, RadioOrder, ObjectiveTone, Warning, FlashlightClick,
        MusicMenu, MusicMission, Hum, Equip, WeaponRaise,
        // Added with the arsenal update.
        Pdw, BurstRifle, Marksman, Lmg, AutoShotgun, Pepperball, MachinePistol, Revolver, Zap,
        Pump, MagOut, MagIn, Charge, Kill, Whiz,
        Rotary, Launcher, SpinUp, Burst,
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
            // Gunshots: a sharp transient, a bright crack, a low thump that drops in pitch and a
            // short rumble tail, soft-clipped together so they hit hard without distorting.
            clips[(int)Sound.Pistol] = Shot("Pistol", 0.42f, 34f, 0.62f, 190f, 70f, 26f, 0.9f, 9f, 0.35f);
            clips[(int)Sound.Smg] = Shot("SMG", 0.32f, 40f, 0.7f, 210f, 80f, 30f, 0.75f, 12f, 0.25f);
            clips[(int)Sound.CompactSmg] = Shot("Compact SMG", 0.28f, 46f, 0.76f, 240f, 90f, 34f, 0.7f, 14f, 0.2f);
            clips[(int)Sound.Rifle] = Shot("Rifle", 0.55f, 26f, 0.55f, 150f, 48f, 20f, 1.05f, 6f, 0.45f);
            clips[(int)Sound.Carbine] = Shot("Carbine", 0.65f, 20f, 0.58f, 140f, 44f, 16f, 1.1f, 5f, 0.5f);
            clips[(int)Sound.Shotgun] = Shot("Shotgun", 0.8f, 15f, 0.38f, 120f, 36f, 11f, 1.35f, 4f, 0.65f);
            clips[(int)Sound.HeavyPistol] = Shot("Heavy Pistol", 0.6f, 22f, 0.5f, 140f, 42f, 15f, 1.2f, 5f, 0.5f);
            clips[(int)Sound.LessLethal] = Make("Less Lethal", 0.35f, t => (Noise() * 0.4f + Mathf.Sin(Tau * 90f * t)) * Mathf.Exp(-t * 18f));
            clips[(int)Sound.EnemyShot] = Shot("Enemy Shot", 0.5f, 28f, 0.55f, 170f, 60f, 22f, 0.9f, 7f, 0.4f);
            clips[(int)Sound.Pdw] = Shot("PDW", 0.3f, 42f, 0.72f, 220f, 85f, 32f, 0.7f, 13f, 0.22f);
            clips[(int)Sound.BurstRifle] = Shot("Burst Rifle", 0.5f, 28f, 0.57f, 160f, 52f, 22f, 1f, 7f, 0.4f);
            clips[(int)Sound.Marksman] = Shot("Marksman Rifle", 0.85f, 17f, 0.6f, 130f, 38f, 12f, 1.25f, 3.5f, 0.6f);
            clips[(int)Sound.Lmg] = Shot("Light Machine Gun", 0.45f, 26f, 0.52f, 140f, 46f, 22f, 1.1f, 7f, 0.4f);
            clips[(int)Sound.AutoShotgun] = Shot("Auto Shotgun", 0.6f, 18f, 0.4f, 125f, 38f, 13f, 1.3f, 5f, 0.55f);
            clips[(int)Sound.MachinePistol] = Shot("Machine Pistol", 0.26f, 48f, 0.74f, 250f, 95f, 36f, 0.65f, 15f, 0.18f);
            clips[(int)Sound.Revolver] = Shot("Revolver", 0.8f, 18f, 0.48f, 130f, 38f, 12f, 1.3f, 4f, 0.6f);
            clips[(int)Sound.Pepperball] = Make("Pepperball", 0.18f, t => (Noise() * 0.5f * Mathf.Exp(-t * 60f) + Mathf.Sin(Tau * (320f - 600f * t) * t) * Mathf.Exp(-t * 30f) * 0.6f));
            clips[(int)Sound.Zap] = Make("Zap", 0.45f, t => (Mathf.Sign(Mathf.Sin(Tau * 95f * t)) * 0.4f + Noise() * 0.5f) * (0.6f + 0.4f * Mathf.Sin(Tau * 23f * t)) * Mathf.Min(1f, (0.45f - t) * 10f) + Click(t, 0f));
            clips[(int)Sound.Reload] = Make("Reload", 0.5f, t => Click(t, 0f) + Click(t, 0.32f));
            // Weapon handling: pump or bolt after a shot, magazine out and in, charging handle.
            clips[(int)Sound.Pump] = Make("Pump", 0.3f, t => Slide(t, 0f, 0.08f) + Click(t, 0.08f) * 0.9f + Slide(t, 0.14f, 0.07f) + Click(t, 0.21f));
            clips[(int)Sound.MagOut] = Make("Mag Out", 0.22f, t => Click(t, 0f) * 0.8f + Slide(t, 0.03f, 0.12f) * 0.7f);
            clips[(int)Sound.MagIn] = Make("Mag In", 0.2f, t => Thump(t, 0f, 160f) * 0.8f + Click(t, 0.02f) + Click(t, 0.05f) * 0.5f);
            clips[(int)Sound.Charge] = Make("Charge", 0.28f, t => Slide(t, 0f, 0.1f) + Click(t, 0.1f) * 0.7f + Slide(t, 0.13f, 0.06f) * 0.8f + Click(t, 0.19f) * 1.1f);
            clips[(int)Sound.Kill] = Make("Kill Confirm", 0.22f, t => (Mathf.Sin(Tau * 1320f * t) * Mathf.Exp(-t * 26f) + (t > 0.06f ? Mathf.Sin(Tau * 1760f * t) * Mathf.Exp(-(t - 0.06f) * 22f) : 0f)) * 0.6f + Click(t, 0f) * 0.3f);
            clips[(int)Sound.Whiz] = Whiz("Near Miss", 0.2f);
            clips[(int)Sound.Rotary] = Shot("Rotary Gun", 0.16f, 60f, 0.8f, 260f, 120f, 45f, 0.6f, 20f, 0.15f);
            clips[(int)Sound.Launcher] = Make("Launcher", 0.4f, t => Mathf.Sin(Tau * (180f - 260f * t) * t) * Mathf.Exp(-t * 9f) + Click(t, 0f) * 0.6f + Noise() * 0.25f * Mathf.Exp(-t * 30f));
            clips[(int)Sound.SpinUp] = Make("Spin Up", 0.45f, t => (Mathf.Sin(Tau * (120f + 900f * t) * t) * 0.4f + Noise() * 0.12f) * Mathf.Clamp01(t * 8f) * Mathf.Clamp01((0.45f - t) * 10f));
            clips[(int)Sound.Burst] = Bang("Paint Burst", 0.7f, 6f, 0.3f, 60f);
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

            // Footsteps per surface: same idea (filtered noise thump) with different tone and length.
            clips[(int)Sound.StepConcrete] = Step("Step Concrete", 0.11f, 0.35f, 70f, 40f);
            clips[(int)Sound.StepCarpet] = Step("Step Carpet", 0.09f, 0.08f, 55f, 60f);
            clips[(int)Sound.StepTile] = Step("Step Tile", 0.1f, 0.6f, 110f, 45f);
            clips[(int)Sound.StepMetal] = Make("Step Metal", 0.16f, t => (Mathf.Sin(Tau * 420f * t) * 0.5f + Mathf.Sin(Tau * 610f * t) * 0.3f + Noise() * 0.3f) * Mathf.Exp(-t * 28f));
            clips[(int)Sound.StepGrass] = Hiss("Step Grass", 0.12f);
            clips[(int)Sound.StepAsphalt] = Step("Step Asphalt", 0.1f, 0.25f, 60f, 42f);

            clips[(int)Sound.DoorClose] = Make("Door Close", 0.3f, t => Thump(t, 0.05f, 70f) + Click(t, 0.06f) * 0.6f);
            clips[(int)Sound.DoorOpenMetal] = Make("Metal Door", 0.5f, t => (Mathf.Sin(Tau * (520f + 60f * Mathf.Sin(t * 24f)) * t) * 0.35f + Noise() * 0.12f) * Mathf.Sin(Mathf.PI * t / 0.5f) + Click(t, 0f) * 0.5f);
            clips[(int)Sound.DoorHandle] = Make("Door Handle", 0.12f, t => Click(t, 0f) * 0.7f + Click(t, 0.05f) * 0.5f);
            clips[(int)Sound.Shell] = Make("Shell", 0.18f, t => (Mathf.Sin(Tau * 3200f * t) * 0.5f + Mathf.Sin(Tau * 4700f * t) * 0.3f) * Mathf.Exp(-t * 35f) + (t > 0.07f ? Mathf.Sin(Tau * 3000f * t) * 0.3f * Mathf.Exp(-(t - 0.07f) * 40f) : 0f));
            clips[(int)Sound.RicochetMetal] = Make("Ricochet", 0.25f, t => Mathf.Sin(Tau * (2600f - 3000f * t) * t) * Mathf.Exp(-t * 14f) * 0.6f + Noise() * 0.3f * Mathf.Exp(-t * 60f));
            clips[(int)Sound.ImpactGlass] = Make("Glass", 0.3f, t => (Noise() * 0.6f + Mathf.Sin(Tau * 5200f * t) * 0.3f) * Mathf.Exp(-t * 16f));

            // Radio "voice" placeholders: band-limited noise in syllable-like bursts,
            // clearly a stand-in for recorded voice lines.
            clips[(int)Sound.RadioVoice1] = Babble("Radio Voice 1", 0.5f, 3, 520f);
            clips[(int)Sound.RadioVoice2] = Babble("Radio Voice 2", 0.65f, 4, 440f);
            clips[(int)Sound.RadioVoice3] = Babble("Radio Voice 3", 0.4f, 2, 600f);
            clips[(int)Sound.RadioOrder] = Make("Order", 0.16f, t => Mathf.Sign(Mathf.Sin(Tau * (t < 0.07f ? 1250f : 1600f) * t)) * 0.18f * Mathf.Exp(-(t % 0.08f) * 18f));
            clips[(int)Sound.ObjectiveTone] = Tones("Objective", new[] { 659.3f, 987.8f }, 0.12f);
            clips[(int)Sound.Warning] = Make("Warning", 0.4f, t => Mathf.Sin(Tau * 520f * t) * (Mathf.Repeat(t, 0.2f) < 0.12f ? 0.5f : 0f));
            clips[(int)Sound.FlashlightClick] = Make("Flashlight", 0.06f, t => Click(t, 0f) * 0.7f + Mathf.Sin(Tau * 3000f * t) * Mathf.Exp(-t * 120f) * 0.3f);
            clips[(int)Sound.Equip] = Make("Equip", 0.2f, t => Click(t, 0f) * 0.5f + Click(t, 0.09f) * 0.4f + Noise() * 0.1f * Mathf.Exp(-t * 20f));
            clips[(int)Sound.WeaponRaise] = Hiss("Weapon Raise", 0.18f);
            clips[(int)Sound.Hum] = Drone("Fluorescent Hum", 3f, 0.02f, 120f);

            // Music beds (loops): a calm minor pad for menus and a low pulse for missions.
            clips[(int)Sound.MusicMenu] = Pad("Menu Theme", 16f, new[] { 110f, 87.31f, 98f, 82.41f }, 0f);
            clips[(int)Sound.MusicMission] = Pad("Mission Tension", 16f, new[] { 73.42f, 69.3f, 73.42f, 65.41f }, 2f);
            return clips;
        }

        static float Click(float t, float at)
        {
            float local = t - at;
            if (local < 0f) return 0f;
            return Noise() * Mathf.Exp(-local * 180f) + Mathf.Sin(Tau * 2400f * local) * Mathf.Exp(-local * 120f) * 0.5f;
        }

        // seconds: clip length; crackDecay/brightness: the noise crack; bodyStart/bodyEnd/bodyDecay/bodyLevel:
        // the pitch-dropping thump; tailDecay/tailLevel: the rumble after the shot.
        static AudioClip Shot(string name, float seconds, float crackDecay, float brightness, float bodyStart, float bodyEnd, float bodyDecay, float bodyLevel, float tailDecay, float tailLevel)
        {
            float filtered = 0f, rumble = 0f, phase = 0f;
            return Make(name, seconds, t =>
            {
                float click = t < 0.004f ? Noise() * (1f - t / 0.004f) * 1.3f : 0f;
                filtered = Mathf.Lerp(filtered, Noise(), brightness);
                float crack = filtered * Mathf.Exp(-t * crackDecay);
                float frequency = Mathf.Lerp(bodyEnd, bodyStart, Mathf.Exp(-t * 28f));
                phase += Tau * frequency / SampleRate;
                float body = Mathf.Sin(phase) * Mathf.Exp(-t * bodyDecay) * bodyLevel;
                rumble = Mathf.Lerp(rumble, Noise(), 0.035f);
                float tail = rumble * 4f * Mathf.Exp(-t * tailDecay) * tailLevel * Mathf.Clamp01(t * 40f);
                return SoftClip((click + crack + body + tail) * 1.8f);
            });
        }

        static float SoftClip(float x)
        {
            return x / (1f + Mathf.Abs(x));
        }

        // A short metallic slide (pump, bolt, magazine).
        static float Slide(float t, float at, float length)
        {
            float local = t - at;
            if (local < 0f || local > length) return 0f;
            return Noise() * 0.35f * Mathf.Sin(Mathf.PI * local / length) + Mathf.Sin(Tau * 900f * local) * 0.1f * Mathf.Sin(Mathf.PI * local / length);
        }

        // A bullet passing close by: band-limited noise sweeping down in pitch.
        static AudioClip Whiz(string name, float seconds)
        {
            float filtered = 0f, slow = 0f;
            return Make(name, seconds, t =>
            {
                float n = Noise();
                float k = Mathf.Lerp(0.6f, 0.15f, t / seconds);
                filtered = Mathf.Lerp(filtered, n, k);
                slow = Mathf.Lerp(slow, filtered, 0.25f);
                return (filtered - slow) * Mathf.Sin(Mathf.PI * t / seconds) + Mathf.Sin(Tau * (2400f - 5000f * t) * t) * 0.15f * Mathf.Sin(Mathf.PI * t / seconds);
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

        static float Thump(float t, float at, float pitch)
        {
            float local = t - at;
            if (local < 0f) return 0f;
            return Mathf.Sin(Tau * pitch * local) * Mathf.Exp(-local * 22f);
        }

        static AudioClip Step(string name, float seconds, float brightness, float pitch, float decay)
        {
            float filtered = 0f;
            return Make(name, seconds, t =>
            {
                filtered = Mathf.Lerp(filtered, Noise(), brightness);
                return (filtered * 0.8f + Mathf.Sin(Tau * pitch * t) * 0.5f) * Mathf.Exp(-t * decay);
            });
        }

        static AudioClip Babble(string name, float seconds, int syllables, float formant)
        {
            float filtered = 0f, slow = 0f;
            float syllable = (seconds - 0.12f) / syllables;
            return Make(name, seconds, t =>
            {
                // Radio squelch at the start, then noise shaped into syllables.
                if (t < 0.06f) return Mathf.Sign(Mathf.Sin(Tau * 1500f * t)) * 0.25f;
                float local = t - 0.06f;
                float env = Mathf.Max(0f, Mathf.Sin(Mathf.PI * Mathf.Repeat(local, syllable) / syllable));
                float n = Noise();
                filtered = Mathf.Lerp(filtered, n, 0.35f);
                slow = Mathf.Lerp(slow, filtered, 0.2f);
                float band = filtered - slow;
                float pitch = formant * (1f + 0.15f * Mathf.Sin(local * 9f));
                return (band * 0.7f + Mathf.Sin(Tau * pitch * t) * 0.25f) * env * (local < seconds - 0.1f ? 1f : 0f);
            });
        }

        // Slow evolving chord pad; 'pulse' adds a soft rhythmic throb (beats per second).
        static AudioClip Pad(string name, float seconds, float[] roots, float pulse)
        {
            float chordLength = seconds / roots.Length;
            return Make(name, seconds, t =>
            {
                int i = Mathf.Min((int)(t / chordLength), roots.Length - 1);
                float local = t - i * chordLength;
                float root = roots[i];
                float fade = Mathf.Clamp01(local * 1.5f) * Mathf.Clamp01((chordLength - local) * 1.5f);
                float tone = Mathf.Sin(Tau * root * t) + 0.6f * Mathf.Sin(Tau * root * 1.5f * t) + 0.45f * Mathf.Sin(Tau * root * 2.4f * t)
                    + 0.25f * Mathf.Sin(Tau * root * 4.01f * t) * (0.5f + 0.5f * Mathf.Sin(t * 0.7f));
                float throb = pulse > 0f ? 0.6f + 0.4f * Mathf.Max(0f, Mathf.Sin(Tau * pulse * t)) : 1f;
                float edge = Mathf.Clamp01(Mathf.Min(t, seconds - t) * 2f);
                return tone * fade * throb * edge;
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
