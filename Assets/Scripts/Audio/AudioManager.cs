using UnityEngine;

namespace Swat
{
    public enum SoundCategory { Effects, Voice, Ambience, Interface, Weapons, Music }
    public enum Surface { Concrete, Carpet, Tile, Metal, Grass, Asphalt, Wood, Glass }

    // Plays every sound through a fixed set of reusable AudioSources (no
    // objects created per sound, so there can never be hundreds playing at
    // once). Six categories (effects, weapons, voice/radio, environment,
    // interface, music) each have a volume and mute in Settings. The listener
    // follows the player; a reverb zone on the listener switches between
    // indoor and outdoor acoustics. Pausing the game pauses world audio while
    // menus and music keep playing.
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        const int WorldVoices = 20;
        const int VoiceChannels = 2;
        const int UiChannels = 3;

        AudioClip[] clips;
        AudioSource[] world, voice, ui;
        AudioSource ambience, alarm, music, hum;
        int nextWorld, nextVoice, nextUi;
        readonly float[] volumes = new float[6];
        Transform listener, listenerTarget;
        AudioReverbZone reverb;
        Sound currentAmbience = Sound.Click, currentMusic = Sound.Click;
        float alarmTarget, musicFade = 1f, humTarget;

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
            foreach (var source in ui) source.ignoreListenerPause = true;
            ambience = Make2D(1)[0];
            ambience.loop = true;
            alarm = Make2D(1)[0];
            alarm.loop = true;
            alarm.clip = clips[(int)Sound.Alarm];
            music = Make2D(1)[0];
            music.loop = true;
            music.ignoreListenerPause = true;
            hum = Make2D(1)[0];
            hum.loop = true;
            hum.clip = clips[(int)Sound.Hum];

            foreach (var other in FindObjectsByType<AudioListener>(FindObjectsSortMode.None)) other.enabled = false;
            listener = new GameObject("Audio Listener").transform;
            listener.SetParent(transform, false);
            listener.gameObject.AddComponent<AudioListener>();
            reverb = listener.gameObject.AddComponent<AudioReverbZone>();
            reverb.minDistance = 500f;
            reverb.maxDistance = 600f;
            reverb.reverbPreset = AudioReverbPreset.Off;
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
                sources[i].bypassReverbZones = true;
            }
            return sources;
        }

        static float Level(float volume, bool mute, float master)
        {
            return mute ? 0f : master * volume;
        }

        public void ApplyVolumes()
        {
            var s = SaveManager.Settings;
            volumes[(int)SoundCategory.Effects] = Level(s.effectsVolume, s.muteEffects, s.masterVolume);
            volumes[(int)SoundCategory.Voice] = Level(s.voiceVolume, s.muteVoice, s.masterVolume);
            volumes[(int)SoundCategory.Ambience] = Level(s.ambienceVolume, s.muteAmbience, s.masterVolume);
            volumes[(int)SoundCategory.Interface] = Level(s.interfaceVolume, s.muteInterface, s.masterVolume);
            volumes[(int)SoundCategory.Weapons] = Level(s.weaponsVolume, s.muteWeapons, s.masterVolume);
            volumes[(int)SoundCategory.Music] = Level(s.musicVolume, s.muteMusic, s.masterVolume);
            ambience.volume = 0.35f * volumes[(int)SoundCategory.Ambience];
        }

        public float VolumeOf(SoundCategory category) { return volumes[(int)category]; }

        public void FollowWithListener(Transform target)
        {
            listenerTarget = target;
        }

        public void ListenFrom(Vector3 position)
        {
            listenerTarget = null;
            listener.position = position;
        }

        // Paused game: world sounds stop where they are and resume exactly from there.
        public static void SetPaused(bool paused)
        {
            if (AudioListener.pause != paused) AudioListener.pause = paused;
        }

        void LateUpdate()
        {
            if (listenerTarget != null) listener.SetPositionAndRotation(listenerTarget.position + Vector3.up, Quaternion.identity);
            float dt = Time.unscaledDeltaTime;
            float targetVolume = alarmTarget * 0.25f * volumes[(int)SoundCategory.Effects];
            alarm.volume = Mathf.MoveTowards(alarm.volume, targetVolume, dt);
            if (alarm.volume <= 0.001f && alarm.isPlaying && alarmTarget <= 0f) alarm.Stop();

            musicFade = Mathf.MoveTowards(musicFade, 1f, dt * 0.5f);
            music.volume = 0.3f * musicFade * volumes[(int)SoundCategory.Music];
            hum.volume = Mathf.MoveTowards(hum.volume, humTarget * 0.08f * volumes[(int)SoundCategory.Ambience], dt * 0.5f);
            if (hum.volume <= 0.001f && hum.isPlaying && humTarget <= 0f) hum.Stop();
        }

        public static void Play(Sound sound, Vector3 position, float volume = 1f, float pitch = 1f)
        {
            Play(sound, position, volume, pitch, SoundCategory.Effects);
        }

        public static void Play(Sound sound, Vector3 position, float volume, float pitch, SoundCategory category)
        {
            if (Instance == null) return;
            var i = Instance;
            var source = i.world[i.nextWorld];
            i.nextWorld = (i.nextWorld + 1) % WorldVoices;
            source.transform.position = position;
            source.clip = i.clips[(int)sound];
            source.volume = volume * i.volumes[(int)category];
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

        // A short placeholder "voice" burst for a radio line (no recorded dialogue in the project).
        public static void RadioVoice(int speaker)
        {
            var sound = speaker % 3 == 0 ? Sound.RadioVoice1 : speaker % 3 == 1 ? Sound.RadioVoice2 : Sound.RadioVoice3;
            Play2D(sound, 0.35f, 0.9f + (speaker % 4) * 0.07f, SoundCategory.Voice);
        }

        public static Sound StepFor(Surface surface)
        {
            switch (surface)
            {
                case Surface.Carpet: return Sound.StepCarpet;
                case Surface.Tile: return Sound.StepTile;
                case Surface.Metal: return Sound.StepMetal;
                case Surface.Grass: return Sound.StepGrass;
                case Surface.Asphalt: return Sound.StepAsphalt;
                case Surface.Wood: return Sound.StepCarpet;
                default: return Sound.StepConcrete;
            }
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

        // Fluorescent hum layered on the ambience in lit interiors.
        public void SetHum(bool on)
        {
            humTarget = on ? 1f : 0f;
            if (on && !hum.isPlaying) hum.Play();
        }

        public void SetMusic(Sound track)
        {
            if (track == currentMusic) return;
            currentMusic = track;
            if (track == Sound.Click)
            {
                music.Stop();
                return;
            }
            music.clip = clips[(int)track];
            music.Play();
            musicFade = 0f;
        }

        // Indoor rooms get a short room reverb, big halls a larger one, outdoors none.
        public void SetReverb(AudioReverbPreset preset)
        {
            if (QualityManager.Instance != null && (int)QualityManager.Instance.Tier == 0) preset = AudioReverbPreset.Off;
            if (reverb.reverbPreset != preset) reverb.reverbPreset = preset;
        }

        public void SetAlarm(bool on)
        {
            alarmTarget = on ? 1f : 0f;
            if (on && !alarm.isPlaying) alarm.Play();
        }
    }
}
