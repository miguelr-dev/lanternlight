using UnityEngine;

namespace Lanternlight
{
    /// <summary>
    /// The game's music is driven entirely by play, instead of a background track:
    ///  * a low drone fades in as shadows get close to the lighthouse,
    ///  * a heartbeat thumps when only one flame is left (faster when shadows are near),
    ///  * a bell melody climbs one note each time the night is 10% closer to dawn,
    ///  * every kill in a burn streak plucks the next note up a pentatonic scale.
    /// </summary>
    public class MusicDirector : MonoBehaviour
    {
        const float DroneMaxVolume = 0.5f;
        const float WaveVolume = 0.45f;
        const float WindVolume = 0.25f;

        float threat;
        float heartbeatTimer;
        int chimesPlayed;

        GameManager Game => GameManager.Instance;

        /// <summary>Called at the start of each night.</summary>
        public void ResetNight()
        {
            chimesPlayed = 0;
            heartbeatTimer = 0f;
        }

        /// <summary>Kill number <paramref name="streakIndex"/> in a streak plays the next note up the scale.</summary>
        public void PlayStreakNote(int streakIndex, Vector2 where)
        {
            int note = Mathf.Clamp(streakIndex, 0, Sfx.NoteCount - 1);
            Game.Audio.Play(Sfx.NotePrefix + note, 0.75f, 1f, where.x / Layout.HalfWidth * 0.6f);
        }

        /// <summary>The final chime, played as the sun comes up.</summary>
        public void PlayDawnChime()
        {
            Game.Audio.Play(Sfx.ChimePrefix + (Sfx.ChimeCount - 1), 0.8f);
        }

        void Update()
        {
            AudioManager audio = Game.Audio;
            bool playing = Game.State == GameState.Playing;

            // Ambience is always there, a little louder on the quiet title screen.
            audio.SetLoop(Sfx.Waves, Game.State == GameState.Title ? WaveVolume * 1.3f : WaveVolume, 0.5f);
            audio.SetLoop(Sfx.Wind, WindVolume, 0.5f);

            // Drone follows how threatened the lighthouse is.
            float targetThreat = playing ? Game.Enemies.ThreatLevel() : 0f;
            threat = Mathf.MoveTowards(threat, targetThreat, Time.deltaTime * 0.8f);
            audio.SetLoop(Sfx.Drone, threat * DroneMaxVolume, 1.5f);

            if (!playing)
            {
                return;
            }

            // Dawn melody: one chime per 10% of the night.
            int due = Mathf.FloorToInt(Game.DawnProgress * Sfx.ChimeCount);
            if (due > chimesPlayed && chimesPlayed < Sfx.ChimeCount - 1)
            {
                Game.Audio.Play(Sfx.ChimePrefix + chimesPlayed, 0.45f);
                chimesPlayed++;
            }

            // Heartbeat on the last flame.
            if (Game.Run.Flames == 1)
            {
                heartbeatTimer -= Time.deltaTime;
                if (heartbeatTimer <= 0f)
                {
                    audio.Play(Sfx.Heartbeat, 0.8f);
                    heartbeatTimer = Mathf.Lerp(1.1f, 0.55f, threat);
                }
            }
        }
    }
}
