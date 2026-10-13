using System.Collections.Generic;
using UnityEngine;

namespace Lanternlight
{
    public enum GameState
    {
        Title,
        NightIntro,       // "NIGHT 2" card before shadows arrive
        Playing,
        Paused,
        Dawn,             // the sun rises, shadows melt
        ChoosingUpgrade,  // pick a blessing for the next night
        GameOver,
        Victory,
    }

    /// <summary>
    /// Owns the game flow (title -> nights -> dawn -> ... -> victory), the run's
    /// score and streaks, and creates every other system. Other scripts reach shared
    /// systems through GameManager.Instance (e.g. Game.Audio, Game.Beam).
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        const string BestScoreKey = "Lanternlight.BestScore";

        public static GameManager Instance { get; private set; }

        public GameState State { get; private set; }
        public RunState Run { get; private set; }
        public float NightTime { get; private set; }
        public int FlaresLeft { get; private set; }
        public int Streak { get; private set; }

        // Systems.
        public Camera Cam { get; private set; }
        public AudioManager Audio { get; private set; }
        public MusicDirector Music { get; private set; }
        public WorldBuilder World { get; private set; }
        public Lighthouse Lighthouse { get; private set; }
        public Beam Beam { get; private set; }
        public EnemySpawner Enemies { get; private set; }
        public BarrelSpawner Barrels { get; private set; }
        public ParticleFx Particles { get; private set; }
        public FloatingText Popups { get; private set; }
        public CameraShake Shake { get; private set; }
        public ScreenFlash Flash { get; private set; }
        public Hud Hud { get; private set; }
        public ScreenOverlay Overlay { get; private set; }

        RunState nightStartSnapshot;   // restored by "retry night"
        bool flareHintShown;
        float streakTimer;
        float stateTimer;

        public NightDefinition CurrentNight => NightDefinition.All[Run.NightIndex];
        public float DawnProgress => Run == null ? 0f : Mathf.Clamp01(NightTime / CurrentNight.Duration);
        public float StreakMultiplier => Mathf.Min(1f + (Streak - 1) * Tuning.StreakStep, Tuning.StreakMaxMultiplier);
        int BestScore => PlayerPrefs.GetInt(BestScoreKey, 0);

        // --- Setup ----------------------------------------------------------
        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            Application.targetFrameRate = 60;

            SetUpCamera();
            Audio = AddSystem<AudioManager>("Audio");
            Particles = AddSystem<ParticleFx>("Particles");
            Popups = AddSystem<FloatingText>("Popups");
            Flash = AddSystem<ScreenFlash>("Screen flash");
            Shake = AddSystem<CameraShake>("Camera shake");
            Shake.Init(Cam);
            World = AddSystem<WorldBuilder>("World");
            Lighthouse = AddSystem<Lighthouse>("Lighthouse");
            Beam = AddSystem<Beam>("Beam");
            Enemies = AddSystem<EnemySpawner>("Shadows");
            Barrels = AddSystem<BarrelSpawner>("Barrels");
            Hud = AddSystem<Hud>("HUD");
            Overlay = AddSystem<ScreenOverlay>("Screens");
            Music = AddSystem<MusicDirector>("Music");

            EnterTitle();
        }

        T AddSystem<T>(string systemName) where T : Component
        {
            var go = new GameObject(systemName);
            go.transform.SetParent(transform, false);
            return go.AddComponent<T>();
        }

        void SetUpCamera()
        {
            Cam = Camera.main;
            if (Cam == null)
            {
                var go = new GameObject("Main Camera") { tag = "MainCamera" };
                Cam = go.AddComponent<Camera>();
            }
            if (Cam.GetComponent<AudioListener>() == null)
            {
                Cam.gameObject.AddComponent<AudioListener>();
            }
            Cam.orthographic = true;
            Cam.orthographicSize = Layout.HalfHeight;
            Cam.transform.position = new Vector3(0f, 0f, -10f);
            Cam.transform.rotation = Quaternion.identity;
            Cam.clearFlags = CameraClearFlags.SolidColor;
            Cam.backgroundColor = new Color(0.02f, 0.03f, 0.08f);
        }

        /// <summary>
        /// Keeps the whole 20-unit-wide play area visible on narrower windows
        /// (wider ones just show a little extra sky and sea at the sides).
        /// </summary>
        void FitCameraToScreen()
        {
            Cam.orthographicSize = Mathf.Max(Layout.HalfHeight, Layout.HalfWidth / Mathf.Max(Cam.aspect, 0.1f));
        }

        void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
                Time.timeScale = 1f;
                AudioListener.pause = false;
            }
        }

        // --- Per-frame flow -------------------------------------------------
        void Update()
        {
            FitCameraToScreen();
            stateTimer -= Time.unscaledDeltaTime;
            switch (State)
            {
                case GameState.Title:
                    if (Overlay.InputReady && GameInput.AdvancePressed)
                    {
                        Audio.Play(Sfx.UiConfirm, 0.7f);
                        StartRun();
                    }
                    break;

                case GameState.NightIntro:
                    if (stateTimer <= 0f)
                    {
                        State = GameState.Playing;
                        Overlay.Hide();
                    }
                    break;

                case GameState.Playing:
                    UpdatePlaying();
                    break;

                case GameState.Paused:
                    if (GameInput.PausePressed)
                    {
                        SetPaused(false);
                    }
                    else if (GameInput.QuitPressed)
                    {
                        SetPaused(false);
                        EnterTitle();
                    }
                    break;

                case GameState.Dawn:
                    if (stateTimer <= 0f)
                    {
                        if (Run.IsLastNight)
                        {
                            EnterVictory();
                        }
                        else
                        {
                            EnterUpgradeChoice();
                        }
                    }
                    break;

                case GameState.ChoosingUpgrade:
                    if (GameInput.Choice1Pressed) Overlay.PressButton(0);
                    if (GameInput.Choice2Pressed) Overlay.PressButton(1);
                    break;

                case GameState.GameOver:
                    if (Overlay.InputReady && GameInput.AdvancePressed)
                    {
                        Audio.Play(Sfx.UiConfirm, 0.7f);
                        RetryNight();
                    }
                    else if (Overlay.InputReady && GameInput.PausePressed)
                    {
                        EnterTitle();
                    }
                    break;

                case GameState.Victory:
                    if (Overlay.InputReady && GameInput.AdvancePressed)
                    {
                        Audio.Play(Sfx.UiConfirm, 0.7f);
                        EnterTitle();
                    }
                    break;
            }
        }

        void UpdatePlaying()
        {
            if (GameInput.PausePressed)
            {
                SetPaused(true);
                return;
            }

            NightTime += Time.deltaTime;

            // Streaks expire if the player goes too long without a kill.
            if (Streak > 0)
            {
                streakTimer -= Time.deltaTime;
                if (streakTimer <= 0f)
                {
                    Streak = 0;
                }
            }

            if (GameInput.FlarePressed)
            {
                FireFlare();
            }
            else if (!flareHintShown && FlaresLeft > 0 && Enemies.CountNearTower(3f) >= 3)
            {
                // Remind the player the panic button exists when they're being swarmed.
                flareHintShown = true;
                Popups.Show(Layout.LampPosition + Vector2.up * 1.3f, "SPACE - FLARE!", new Color(1f, 0.9f, 0.6f), 1f, 2.5f);
            }

            if (NightTime >= CurrentNight.Duration)
            {
                EnterDawn();
            }
        }

        // --- State transitions ---------------------------------------------
        void EnterTitle()
        {
            State = GameState.Title;
            Run = null;
            NightTime = 0f;
            Streak = 0;
            Enemies.ClearAll();
            Barrels.ClearAll();
            Popups.HideAll();
            Hud.SetVisible(false);
            Overlay.ShowTitle(BestScore);
        }

        void StartRun()
        {
            Run = new RunState();
            BeginNight();
        }

        void BeginNight()
        {
            nightStartSnapshot = Run.Clone();
            NightTime = 0f;
            Streak = 0;
            FlaresLeft = Run.FlaresPerNight;
            flareHintShown = false;

            Enemies.BeginNight(CurrentNight);
            Barrels.BeginNight(CurrentNight);
            Beam.ResetForNight(Run.OilCapacity);
            Music.ResetNight();
            Popups.HideAll();
            Hud.SetVisible(true);

            Audio.Play(Sfx.NightStart, 0.8f);
            Overlay.ShowNightIntro(Run.NightIndex + 1, NightDefinition.All.Length, CurrentNight.Hint);
            State = GameState.NightIntro;
            stateTimer = Tuning.NightIntroSeconds;
        }

        void RetryNight()
        {
            Run = nightStartSnapshot.Clone();
            BeginNight();
        }

        void EnterDawn()
        {
            State = GameState.Dawn;
            stateTimer = Tuning.DawnLingerSeconds;
            Streak = 0;
            Beam.ForceOff();
            Enemies.MeltAll();

            int bonus = Tuning.NightClearBonus * (Run.NightIndex + 1) + Tuning.FlameBonus * Run.Flames;
            Run.Score += bonus;
            bool restored = Run.Flames < Run.MaxFlames;
            Run.Flames = Mathf.Min(Run.MaxFlames, Run.Flames + Tuning.DawnFlameRestore);

            Audio.Play(Sfx.Dawn, 0.8f);
            Music.PlayDawnChime();
            Particles.Emit(Layout.LampPosition, 30, ParticleStyle.Dawn);
            Overlay.ShowDawn(Run.NightIndex + 1, bonus, restored && !Run.IsLastNight);
        }

        void EnterUpgradeChoice()
        {
            State = GameState.ChoosingUpgrade;
            List<UpgradeInfo> choices = Run.RollUpgradeChoices(2);
            if (choices.Count == 0)
            {
                NextNight(); // everything is maxed out
                return;
            }
            Overlay.ShowUpgradeChoice(choices, OnUpgradeChosen);
        }

        void OnUpgradeChosen(UpgradeInfo upgrade)
        {
            if (State != GameState.ChoosingUpgrade)
            {
                return;
            }
            Audio.Play(Sfx.UiConfirm, 0.8f);
            Run.ApplyUpgrade(upgrade.Type);
            NextNight();
        }

        void NextNight()
        {
            Run.NightIndex++;
            BeginNight();
        }

        void EnterGameOver()
        {
            State = GameState.GameOver;
            Beam.ForceOff();
            SaveBestScore(Run.Score);
            Audio.Play(Sfx.GameOver, 0.9f);
            Overlay.ShowGameOver(Run.NightIndex + 1, Run.Score);
        }

        void EnterVictory()
        {
            State = GameState.Victory;
            bool newBest = SaveBestScore(Run.Score);
            Hud.SetVisible(false);
            Audio.Play(Sfx.Victory, 0.9f);
            Particles.Emit(Layout.LampPosition, 60, ParticleStyle.Spark);
            Overlay.ShowVictory(Run.Score, BestScore, newBest);
        }

        void SetPaused(bool paused)
        {
            State = paused ? GameState.Paused : GameState.Playing;
            Time.timeScale = paused ? 0f : 1f;
            AudioListener.pause = paused;
            if (paused)
            {
                Overlay.ShowPause();
            }
            else
            {
                Overlay.Hide();
            }
        }

        /// <returns>True if this is a new best score.</returns>
        bool SaveBestScore(int score)
        {
            if (score <= BestScore)
            {
                return false;
            }
            PlayerPrefs.SetInt(BestScoreKey, score);
            PlayerPrefs.Save();
            return true;
        }

        // --- Events reported by other systems -------------------------------
        /// <summary>A shadow was burned away by the beam or a flare.</summary>
        public void OnEnemyKilled(Enemy enemy)
        {
            Streak = streakTimer > 0f ? Streak + 1 : 1;
            streakTimer = Tuning.StreakWindow;
            int points = Mathf.RoundToInt(enemy.Points * StreakMultiplier);
            Run.Score += points;

            Vector2 at = enemy.Centre + Vector2.up * 0.5f;
            Color color = Streak >= 2 ? new Color(1f, 0.85f, 0.4f) : Color.white;
            Popups.Show(at, "+" + points, color);
            Music.PlayStreakNote(Streak - 1, enemy.Centre);
        }

        public void OnBarrelCollected(Vector2 where)
        {
            Beam.AddOil(Tuning.OilPerBarrel);
            if (Run != null)
            {
                Run.Score += Tuning.BarrelScore;
            }
            Audio.Play(Sfx.BarrelCollect, 0.9f);
            Particles.Emit(where, 18, ParticleStyle.Spark);
            Popups.Show(where + Vector2.up * 0.9f, "+OIL", new Color(1f, 0.8f, 0.35f));
        }

        public void OnLighthouseDamaged()
        {
            Streak = 0;
            if (Run.Flames <= 0 && State == GameState.Playing)
            {
                EnterGameOver();
            }
        }

        void FireFlare()
        {
            if (FlaresLeft <= 0)
            {
                Audio.Play(Sfx.OilEmpty, 0.4f, 1.4f); // dud click
                Popups.Show(Layout.LampPosition + Vector2.up * 1.2f, "NO FLARES LEFT", new Color(0.7f, 0.7f, 0.8f));
                return;
            }
            FlaresLeft--;
            Audio.Play(Sfx.Flare, 1f);
            Flash.Trigger(new Color(1f, 0.97f, 0.85f, 0.85f), 0.7f);
            Shake.Add(0.35f);
            Particles.Emit(Layout.LampPosition, 50, ParticleStyle.Spark);
            Enemies.FlareAll(Tuning.FlareDamage);
        }
    }
}
