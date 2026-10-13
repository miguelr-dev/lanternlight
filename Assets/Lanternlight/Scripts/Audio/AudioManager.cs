using System.Collections.Generic;
using UnityEngine;

namespace Lanternlight
{
    /// <summary>Clip names in Resources/Audio, so typos are caught by the compiler.</summary>
    public static class Sfx
    {
        public const string BeamOn = "beam_on";
        public const string BeamOff = "beam_off";
        public const string BeamHum = "beam_hum";
        public const string BurnLoop = "burn_loop";
        public const string EnemyDie = "enemy_die";
        public const string SpawnSplash = "spawn_splash";
        public const string SkitterChitter = "skitter_chitter";
        public const string BruteRoar = "brute_roar";
        public const string WispWhisper = "wisp_whisper";
        public const string BarrelCollect = "barrel_collect";
        public const string OilEmpty = "oil_empty";
        public const string OilLow = "oil_low";
        public const string FlameLost = "flame_lost";
        public const string Flare = "flare";
        public const string Heartbeat = "heartbeat";
        public const string UiClick = "ui_click";
        public const string UiConfirm = "ui_confirm";
        public const string NightStart = "night_start";
        public const string Dawn = "dawn";
        public const string GameOver = "game_over";
        public const string Victory = "victory";
        public const string Drone = "drone";
        public const string Waves = "waves";
        public const string Wind = "wind";
        public const string NotePrefix = "note_";     // note_0 .. note_7
        public const string ChimePrefix = "chime_";   // chime_0 .. chime_9
        public const int NoteCount = 8;
        public const int ChimeCount = 10;
    }

    /// <summary>
    /// Plays one-shot sounds on a small pool of voices (with pitch variation and
    /// stereo panning by screen position) and manages looping layers that fade
    /// smoothly to whatever volume the game asks for.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        const int VoiceCount = 24;
        const float MasterVolume = 0.9f;

        class Loop
        {
            public AudioSource Source;
            public float Target;
            public float FadeSpeed;
        }

        readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        readonly Dictionary<string, Loop> loops = new Dictionary<string, Loop>();
        readonly Dictionary<string, float> lastPlayed = new Dictionary<string, float>();
        AudioSource[] voices;
        int nextVoice;

        void Awake()
        {
            voices = new AudioSource[VoiceCount];
            for (int i = 0; i < VoiceCount; i++)
            {
                voices[i] = gameObject.AddComponent<AudioSource>();
                voices[i].playOnAwake = false;
            }
        }

        public AudioClip Clip(string name)
        {
            if (!clips.TryGetValue(name, out AudioClip clip))
            {
                clip = Resources.Load<AudioClip>("Audio/" + name);
                if (clip == null)
                {
                    Debug.LogError($"Lanternlight: missing sound 'Resources/Audio/{name}.wav'");
                }
                clips[name] = clip;
            }
            return clip;
        }

        /// <summary>Plays a one-shot sound.</summary>
        /// <param name="pan">-1 = left speaker, 1 = right speaker.</param>
        public void Play(string name, float volume = 1f, float pitch = 1f, float pan = 0f)
        {
            AudioClip clip = Clip(name);
            if (clip == null)
            {
                return;
            }
            AudioSource voice = voices[nextVoice];
            nextVoice = (nextVoice + 1) % voices.Length;
            voice.clip = clip;
            voice.volume = volume * MasterVolume;
            voice.pitch = pitch;
            voice.panStereo = Mathf.Clamp(pan, -1f, 1f);
            voice.loop = false;
            voice.Play();
            lastPlayed[name] = Time.unscaledTime;
        }

        /// <summary>Plays a sound panned to where it happened, with slight random pitch so repeats don't sound robotic.</summary>
        public void PlayAt(string name, Vector2 worldPosition, float volume = 1f, float pitchJitter = 0.08f)
        {
            float pan = worldPosition.x / Layout.HalfWidth * 0.8f;
            Play(name, volume, 1f + Random.Range(-pitchJitter, pitchJitter), pan);
        }

        /// <summary>Same as PlayAt but skipped if the sound already played very recently (stops pile-ups).</summary>
        public void PlayThrottled(string name, Vector2 worldPosition, float minInterval, float volume = 1f)
        {
            if (lastPlayed.TryGetValue(name, out float last) && Time.unscaledTime - last < minInterval)
            {
                return;
            }
            PlayAt(name, worldPosition, volume);
        }

        /// <summary>Sets the volume a looping layer should fade to (0 silences it).</summary>
        public void SetLoop(string name, float volume, float fadeSpeed = 3f)
        {
            if (!loops.TryGetValue(name, out Loop loop))
            {
                AudioClip clip = Clip(name);
                if (clip == null)
                {
                    return;
                }
                var source = gameObject.AddComponent<AudioSource>();
                source.clip = clip;
                source.loop = true;
                source.playOnAwake = false;
                source.volume = 0f;
                source.Play();
                loop = new Loop { Source = source };
                loops[name] = loop;
            }
            loop.Target = volume;
            loop.FadeSpeed = fadeSpeed;
        }

        /// <summary>Changes a loop's pitch (the beam hum wavers when oil runs low).</summary>
        public void SetLoopPitch(string name, float pitch)
        {
            if (loops.TryGetValue(name, out Loop loop))
            {
                loop.Source.pitch = pitch;
            }
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            foreach (Loop loop in loops.Values)
            {
                float target = loop.Target * MasterVolume;
                loop.Source.volume = Mathf.MoveTowards(loop.Source.volume, target, loop.FadeSpeed * dt);
            }
        }
    }
}
