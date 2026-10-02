using UnityEngine;

namespace Swat
{
    public enum Sound
    {
        Pistol, Smg, Rifle, Shotgun, EnemyShot, Reload, Empty, Footstep, DoorOpen, DoorLocked,
        Breach, Flashbang, Smoke, Throw, Detect, Hit, Hurt, Shout, Rescue, Click, Complete, Fail,
    }

    // Placeholder sound effects are synthesized at startup (no audio files
    // needed) and played through a small pool of reusable AudioSources, so
    // playing a sound never creates or destroys objects.
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        const int SampleRate = 22050;
        const float Tau = Mathf.PI * 2f;
        const int WorldVoices = 14;
        const int UiVoices = 4;

        AudioClip[] clips;
        AudioSource[] worldSources;
        AudioSource[] uiSources;
        int nextWorld, nextUi;
        Transform listener;
        Transform listenerTarget;
        readonly System.Random rng = new System.Random(11);

        void Awake()
        {
            Instance = this;
            BuildClips();

            worldSources = new AudioSource[WorldVoices];
            for (int i = 0; i < WorldVoices; i++)
            {
                var source = new GameObject("Voice " + i).AddComponent<AudioSource>();
                source.transform.SetParent(transform, false);
                source.playOnAwake = false;
                source.spatialBlend = 1f;
                source.rolloffMode = AudioRolloffMode.Linear;
                source.minDistance = 3f;
                source.maxDistance = 45f;
                source.dopplerLevel = 0f;
                worldSources[i] = source;
            }
            uiSources = new AudioSource[UiVoices];
            for (int i = 0; i < UiVoices; i++)
            {
                var source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0f;
                uiSources[i] = source;
            }

            // Top-down games sound best when you "hear" from the player, not the camera.
            foreach (var other in FindObjectsByType<AudioListener>(FindObjectsSortMode.None)) other.enabled = false;
            listener = new GameObject("Audio Listener").transform;
            listener.SetParent(transform, false);
            listener.gameObject.AddComponent<AudioListener>();
        }

        public void FollowWithListener(Transform target)
        {
            listenerTarget = target;
        }

        void LateUpdate()
        {
            if (listenerTarget != null) listener.SetPositionAndRotation(listenerTarget.position + Vector3.up, Quaternion.identity);
        }

        public static void Play(Sound sound, Vector3 position, float volume = 1f, float pitch = 1f)
        {
            if (Instance == null) return;
            var source = Instance.worldSources[Instance.nextWorld];
            Instance.nextWorld = (Instance.nextWorld + 1) % WorldVoices;
            source.transform.position = position;
            source.clip = Instance.clips[(int)sound];
            source.volume = volume;
            source.pitch = pitch;
            source.Play();
        }

        public static void Play2D(Sound sound, float volume = 1f, float pitch = 1f)
        {
            if (Instance == null) return;
            var source = Instance.uiSources[Instance.nextUi];
            Instance.nextUi = (Instance.nextUi + 1) % UiVoices;
            source.clip = Instance.clips[(int)sound];
            source.volume = volume;
            source.pitch = pitch;
            source.Play();
        }

        void BuildClips()
        {
            clips = new AudioClip[System.Enum.GetValues(typeof(Sound)).Length];
            clips[(int)Sound.Pistol] = Gunshot("Pistol", 18f, 120f, 0.6f, 0.4f);
            clips[(int)Sound.Smg] = Gunshot("SMG", 24f, 140f, 0.7f, 0.3f);
            clips[(int)Sound.Rifle] = Gunshot("Rifle", 13f, 80f, 0.5f, 0.5f);
            clips[(int)Sound.Shotgun] = Gunshot("Shotgun", 8f, 55f, 0.35f, 0.7f);
            clips[(int)Sound.EnemyShot] = Gunshot("Enemy Shot", 15f, 100f, 0.55f, 0.45f);
            clips[(int)Sound.Reload] = Make("Reload", 0.5f, t => Click(t, 0f) + Click(t, 0.32f));
            clips[(int)Sound.Empty] = Make("Empty", 0.08f, t => Click(t, 0f));
            clips[(int)Sound.Footstep] = Thud("Footstep", 0.12f, 45f, 80f);
            clips[(int)Sound.DoorOpen] = Make("Door", 0.45f, t => Mathf.Sin(Tau * (260f + 90f * Mathf.Sin(t * 30f)) * t) * Mathf.Sin(Mathf.PI * t / 0.45f) * 0.5f + Noise() * 0.1f * Mathf.Exp(-t * 8f));
            clips[(int)Sound.DoorLocked] = Make("Locked", 0.3f, t => Click(t, 0f) + Click(t, 0.1f) + Click(t, 0.2f));
            clips[(int)Sound.Breach] = Bang("Breach", 0.9f, 7f, 0.2f, 50f);
            clips[(int)Sound.Flashbang] = Bang("Flashbang", 1.4f, 3.5f, 0.45f, 45f);
            clips[(int)Sound.Smoke] = Hiss("Smoke", 1.4f);
            clips[(int)Sound.Throw] = Hiss("Throw", 0.25f);
            clips[(int)Sound.Detect] = Make("Detect", 0.26f, t => Mathf.Sign(Mathf.Sin(Tau * (t < 0.12f ? 880f : 660f) * t)) * 0.3f * Mathf.Exp(-(t % 0.13f) * 12f));
            clips[(int)Sound.Hit] = Make("Hit", 0.06f, t => Mathf.Sin(Tau * 1700f * t) * Mathf.Exp(-t * 55f));
            clips[(int)Sound.Hurt] = Make("Hurt", 0.3f, t => Mathf.Sin(Tau * (140f - 200f * t) * t) * Mathf.Exp(-t * 10f) + Noise() * 0.3f * Mathf.Exp(-t * 30f));
            clips[(int)Sound.Shout] = Make("Shout", 0.32f, t => t < 0.12f
                ? Mathf.Sign(Mathf.Sin(Tau * 1200f * t)) * 0.35f + Noise() * 0.15f
                : Noise() * 0.5f * Mathf.Exp(-(t - 0.12f) * 20f));
            clips[(int)Sound.Rescue] = Make("Rescue", 0.25f, t => Mathf.Sin(Tau * (600f + 1200f * t) * t) * Mathf.Exp(-t * 6f));
            clips[(int)Sound.Click] = Make("Click", 0.05f, t => Click(t, 0f));
            clips[(int)Sound.Complete] = Tones("Complete", new[] { 523.3f, 659.3f, 784f, 1046.5f }, 0.14f);
            clips[(int)Sound.Fail] = Tones("Fail", new[] { 392f, 311.1f, 233.1f }, 0.28f);
        }

        float Click(float t, float at)
        {
            float local = t - at;
            if (local < 0f) return 0f;
            return Noise() * Mathf.Exp(-local * 180f) + Mathf.Sin(Tau * 2400f * local) * Mathf.Exp(-local * 120f) * 0.5f;
        }

        AudioClip Gunshot(string name, float decay, float thump, float brightness, float seconds)
        {
            float filtered = 0f;
            return Make(name, seconds, t =>
            {
                filtered = Mathf.Lerp(filtered, Noise(), brightness);
                return filtered * Mathf.Exp(-t * decay) + Mathf.Sin(Tau * thump * t) * Mathf.Exp(-t * 28f) * 0.8f;
            });
        }

        AudioClip Bang(string name, float seconds, float decay, float brightness, float thump)
        {
            float filtered = 0f;
            return Make(name, seconds, t =>
            {
                filtered = Mathf.Lerp(filtered, Noise(), brightness);
                return filtered * Mathf.Exp(-t * decay) + Mathf.Sin(Tau * thump * t) * Mathf.Exp(-t * 8f);
            });
        }

        AudioClip Thud(string name, float seconds, float decay, float pitch)
        {
            float filtered = 0f;
            return Make(name, seconds, t =>
            {
                filtered = Mathf.Lerp(filtered, Noise(), 0.15f);
                return (filtered + Mathf.Sin(Tau * pitch * t) * 0.6f) * Mathf.Exp(-t * decay);
            });
        }

        AudioClip Hiss(string name, float seconds)
        {
            float filtered = 0f;
            return Make(name, seconds, t =>
            {
                float n = Noise();
                filtered = Mathf.Lerp(filtered, n, 0.3f);
                return (n - filtered) * Mathf.Sin(Mathf.PI * t / seconds);
            });
        }

        AudioClip Tones(string name, float[] notes, float noteLength)
        {
            return Make(name, notes.Length * noteLength + 0.4f, t =>
            {
                int i = Mathf.Min((int)(t / noteLength), notes.Length - 1);
                float local = t - i * noteLength;
                return (Mathf.Sin(Tau * notes[i] * t) + 0.3f * Mathf.Sin(Tau * notes[i] * 2f * t)) * Mathf.Exp(-local * 5f);
            });
        }

        AudioClip Make(string name, float seconds, System.Func<float, float> wave)
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

        float Noise()
        {
            return (float)(rng.NextDouble() * 2.0 - 1.0);
        }
    }
}
