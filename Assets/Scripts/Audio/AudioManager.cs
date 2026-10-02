using UnityEngine;

namespace Swat
{
    public enum SoundCategory { Effects, Voice, Ambience, Interface }

    // Plays every sound through a fixed set of reusable AudioSources (no
    // objects created per sound). Volumes are per category and can be muted
    // individually from Settings. The listener follows the player, which
    // sounds right for a top-down game.
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        const int WorldVoices = 16;
        const int VoiceChannels = 2;
        const int UiChannels = 3;

        AudioClip[] clips;
        AudioSource[] world, voice, ui;
        AudioSource ambience, alarm;
        int nextWorld, nextVoice, nextUi;
        readonly float[] volumes = new float[4];
        Transform listener, listenerTarget;
        Sound currentAmbience = Sound.Click;
        float alarmTarget;

        void Awake()
        {
            Instance = this;
            clips = SoundLibrary.Build();
            world = new AudioSource[WorldVoices];
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
                world[i] = source;
            }
            voice = Make2D(VoiceChannels);
            ui = Make2D(UiChannels);
            ambience = Make2D(1)[0];
            ambience.loop = true;
            alarm = Make2D(1)[0];
            alarm.loop = true;
            alarm.clip = clips[(int)Sound.Alarm];

            foreach (var other in FindObjectsByType<AudioListener>(FindObjectsSortMode.None)) other.enabled = false;
            listener = new GameObject("Audio Listener").transform;
            listener.SetParent(transform, false);
            listener.gameObject.AddComponent<AudioListener>();
            ApplyVolumes();
        }

        AudioSource[] Make2D(int count)
        {
            var sources = new AudioSource[count];
            for (int i = 0; i < count; i++)
            {
                sources[i] = gameObject.AddComponent<AudioSource>();
                sources[i].playOnAwake = false;
                sources[i].spatialBlend = 0f;
            }
            return sources;
        }

        public void ApplyVolumes()
        {
            var s = SaveManager.Settings;
            volumes[(int)SoundCategory.Effects] = s.muteEffects ? 0f : s.masterVolume * s.effectsVolume;
            volumes[(int)SoundCategory.Voice] = s.muteVoice ? 0f : s.masterVolume * s.voiceVolume;
            volumes[(int)SoundCategory.Ambience] = s.muteAmbience ? 0f : s.masterVolume * s.ambienceVolume;
            volumes[(int)SoundCategory.Interface] = s.muteInterface ? 0f : s.masterVolume * s.interfaceVolume;
            ambience.volume = 0.35f * volumes[(int)SoundCategory.Ambience];
        }

        public void FollowWithListener(Transform target)
        {
            listenerTarget = target;
        }

        public void ListenFrom(Vector3 position)
        {
            listenerTarget = null;
            listener.position = position;
        }

        void LateUpdate()
        {
            if (listenerTarget != null) listener.SetPositionAndRotation(listenerTarget.position + Vector3.up, Quaternion.identity);
            float targetVolume = alarmTarget * 0.25f * volumes[(int)SoundCategory.Effects];
            alarm.volume = Mathf.MoveTowards(alarm.volume, targetVolume, Time.unscaledDeltaTime);
            if (alarm.volume <= 0.001f && alarm.isPlaying && alarmTarget <= 0f) alarm.Stop();
        }

        public static void Play(Sound sound, Vector3 position, float volume = 1f, float pitch = 1f)
        {
            if (Instance == null) return;
            var i = Instance;
            var source = i.world[i.nextWorld];
            i.nextWorld = (i.nextWorld + 1) % WorldVoices;
            source.transform.position = position;
            source.clip = i.clips[(int)sound];
            source.volume = volume * i.volumes[(int)SoundCategory.Effects];
            source.pitch = pitch;
            source.Play();
        }

        public static void Play2D(Sound sound, float volume = 1f, float pitch = 1f, SoundCategory category = SoundCategory.Effects)
        {
            if (Instance == null) return;
            var i = Instance;
            AudioSource source;
            if (category == SoundCategory.Voice)
            {
                source = i.voice[i.nextVoice];
                i.nextVoice = (i.nextVoice + 1) % VoiceChannels;
            }
            else
            {
                source = i.ui[i.nextUi];
                i.nextUi = (i.nextUi + 1) % UiChannels;
            }
            source.clip = i.clips[(int)sound];
            source.volume = volume * i.volumes[(int)category];
            source.pitch = pitch;
            source.Play();
        }

        public static void Ui(Sound sound, float volume = 0.6f)
        {
            Play2D(sound, volume, 1f, SoundCategory.Interface);
        }

        public static void RadioChirp(float pitch = 1f)
        {
            Play2D(Sound.Radio, 0.45f, pitch, SoundCategory.Voice);
        }

        // Background bed: RoomTone indoors, Wind outdoors, or Click for silence.
        public void SetAmbience(Sound bed)
        {
            if (bed == currentAmbience) return;
            currentAmbience = bed;
            if (bed == Sound.Click)
            {
                ambience.Stop();
                return;
            }
            ambience.clip = clips[(int)bed];
            ambience.Play();
        }

        public void SetAlarm(bool on)
        {
            alarmTarget = on ? 1f : 0f;
            if (on && !alarm.isPlaying) alarm.Play();
        }
    }
}
