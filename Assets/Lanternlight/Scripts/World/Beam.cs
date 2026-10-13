using System.Collections.Generic;
using UnityEngine;

namespace Lanternlight
{
    /// <summary>
    /// The player's only tool. Follows the mouse around the lamp, burns shadows
    /// caught inside its cone, pulls in oil barrels, and drains oil while lit.
    /// On the title screen it sweeps the sky on its own like a real lighthouse.
    /// </summary>
    public class Beam : MonoBehaviour
    {
        const float FadeSpeed = 12f;          // how fast the cone's alpha follows on/off
        const float HumVolume = 0.32f;
        const float BurnLoopVolume = 0.45f;
        const float EmberInterval = 0.25f;    // seconds between embers thrown off the lamp

        Transform pivot;
        SpriteRenderer cone;
        float visibleAlpha;
        float emberTimer;
        bool dryLocked;        // ran out of oil; can't relight until refuelled a little
        bool lowOilWarned;
        readonly List<Enemy> targets = new List<Enemy>();
        readonly List<Barrel> barrelTargets = new List<Barrel>();

        public float Oil { get; private set; }
        public float OilCapacity { get; private set; } = Tuning.OilCapacity;
        public float OilFraction => OilCapacity > 0f ? Oil / OilCapacity : 0f;
        public bool IsOn { get; private set; }
        public float AngleDegrees { get; private set; } = 90f;
        public bool IsDry => dryLocked;
        public Vector2 Origin => Layout.LampPosition;
        public Vector2 Direction => new Vector2(Mathf.Cos(AngleDegrees * Mathf.Deg2Rad), Mathf.Sin(AngleDegrees * Mathf.Deg2Rad));

        GameManager Game => GameManager.Instance;

        /// <summary>Half the cone's opening angle, including the Wide Beam upgrade.</summary>
        public float HalfAngle
        {
            get
            {
                float widen = Game.Run != null ? Game.Run.BeamWidthMultiplier : 1f;
                return Mathf.Atan(Mathf.Tan(Tuning.BeamHalfAngle * Mathf.Deg2Rad) * widen) * Mathf.Rad2Deg;
            }
        }

        void Awake()
        {
            pivot = new GameObject("Beam pivot").transform;
            pivot.SetParent(transform, false);
            pivot.localPosition = Layout.LampPosition;
            // beam.png is drawn at 32 px per unit so its soft gradient stays smooth.
            cone = Art.CreateSprite("Cone", pivot, Art.Get("beam", Art.LeftCentre, 32f), SortOrder.Beam);
            cone.color = new Color(1f, 1f, 1f, 0f);
            Oil = OilCapacity;
        }

        /// <summary>Called at the start of every night: full tank, beam off.</summary>
        public void ResetForNight(float capacity)
        {
            OilCapacity = capacity;
            Oil = capacity;
            dryLocked = false;
            lowOilWarned = false;
            IsOn = false;
        }

        public void AddOil(float amount)
        {
            Oil = Mathf.Min(OilCapacity, Oil + amount);
            if (Oil >= Tuning.OilRelightMinimum)
            {
                dryLocked = false;
            }
            if (OilFraction > Tuning.LowOilFraction + 0.1f)
            {
                lowOilWarned = false;
            }
        }

        public void ForceOff()
        {
            IsOn = false;
        }

        /// <summary>
        /// Is a circle (a shadow or barrel) inside the cone? Compares the angle to the
        /// target with the cone's half-angle, widened by the target's own size.
        /// </summary>
        public bool Contains(Vector2 point, float radius)
        {
            Vector2 toTarget = point - Origin;
            float distance = toTarget.magnitude;
            if (distance > Tuning.BeamRange + radius)
            {
                return false;
            }
            if (distance < 0.01f)
            {
                return true;
            }
            float angleToTarget = Vector2.Angle(Direction, toTarget);
            float allowance = HalfAngle + Mathf.Atan2(radius, distance) * Mathf.Rad2Deg;
            return angleToTarget <= allowance;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f)
            {
                return; // paused
            }
            bool titleSweep = Game.State == GameState.Title;
            bool playing = Game.State == GameState.Playing;

            Aim(dt, titleSweep);
            UpdateOnOff(titleSweep, playing);

            if (IsOn && playing)
            {
                DrainOil(dt);
                BurnWhatIsLit(dt);
            }
            else
            {
                if (playing)
                {
                    AddOil(Tuning.OilRegenPerSecond * dt); // resting the beam lets the tank recover a little
                }
                Game.Audio.SetLoop(Sfx.BurnLoop, 0f, 4f);
            }
            UpdateVisuals(dt);
        }

        void Aim(float dt, bool titleSweep)
        {
            float target;
            if (titleSweep)
            {
                target = 90f + 70f * Mathf.Sin(Time.time * 0.45f);  // slow sweep across the sky
            }
            else
            {
                Vector2 toMouse = GameInput.MouseWorld(Game.Cam) - Origin;
                if (toMouse.sqrMagnitude < 0.01f)
                {
                    return;
                }
                target = Mathf.Atan2(toMouse.y, toMouse.x) * Mathf.Rad2Deg;
            }
            // Exponential smoothing: responsive, but the beam has a little weight to it.
            AngleDegrees = Mathf.LerpAngle(AngleDegrees, target, 1f - Mathf.Exp(-Tuning.BeamTurnSharpness * dt));
            pivot.localRotation = Quaternion.Euler(0f, 0f, AngleDegrees);
        }

        void UpdateOnOff(bool titleSweep, bool playing)
        {
            bool wants = titleSweep || (playing && GameInput.BeamHeld);
            if (playing && dryLocked)
            {
                // Clicking with an empty tank: the lamp only sputters.
                if (GameInput.ClickPressed)
                {
                    Game.Audio.Play(Sfx.OilEmpty, 0.5f);
                    Game.Hud.FlashOilGauge();
                }
                wants = false;
            }

            if (wants && !IsOn)
            {
                IsOn = true;
                if (playing)
                {
                    Game.Audio.Play(Sfx.BeamOn, 0.6f, Random.Range(0.95f, 1.05f));
                }
            }
            else if (!wants && IsOn)
            {
                IsOn = false;
                if (playing)
                {
                    Game.Audio.Play(Sfx.BeamOff, 0.45f, Random.Range(0.95f, 1.05f));
                }
            }

            // The electric hum, which wavers as the oil runs low.
            float hum = IsOn && playing ? HumVolume : 0f;
            Game.Audio.SetLoop(Sfx.BeamHum, hum, 6f);
            Game.Audio.SetLoopPitch(Sfx.BeamHum, OilFraction < Tuning.LowOilFraction ? 0.9f + 0.1f * Mathf.Sin(Time.time * 20f) : 1f);
        }

        void DrainOil(float dt)
        {
            Oil = Mathf.Max(0f, Oil - Tuning.OilDrainPerSecond * Game.Run.DrainMultiplier * dt);

            if (!lowOilWarned && OilFraction < Tuning.LowOilFraction)
            {
                lowOilWarned = true;
                Game.Audio.Play(Sfx.OilLow, 0.7f);
            }
            if (Oil <= 0f)
            {
                IsOn = false;
                dryLocked = true;
                Game.Audio.Play(Sfx.OilEmpty, 0.9f);
                Game.Particles.Emit(Origin, 8, ParticleStyle.ShadowPuff);
                Game.Popups.Show(Origin + Vector2.up * 1.3f, "OUT OF OIL!", new Color(1f, 0.5f, 0.3f));
            }
        }

        void BurnWhatIsLit(float dt)
        {
            float damage = Tuning.BeamBurnPerSecond * Game.Run.BurnMultiplier * dt;
            bool anyBurning = false;

            // Copy first: shadows remove themselves from the spawner's list when they die.
            targets.Clear();
            targets.AddRange(Game.Enemies.Active);
            foreach (Enemy e in targets)
            {
                if (e.IsAlive && Contains(e.Centre, e.Radius))
                {
                    e.Burn(damage);
                    anyBurning = true;
                }
            }

            barrelTargets.Clear();
            barrelTargets.AddRange(Game.Barrels.Active);
            foreach (Barrel b in barrelTargets)
            {
                if (b.CanBePulled && Contains(b.Centre, b.Radius))
                {
                    b.Pull(dt);
                }
            }

            Game.Audio.SetLoop(Sfx.BurnLoop, anyBurning ? BurnLoopVolume : 0f, anyBurning ? 10f : 4f);

            // A few embers fly from the lamp while it blazes.
            emberTimer -= dt;
            if (emberTimer <= 0f)
            {
                emberTimer = EmberInterval;
                Game.Particles.Emit(Origin + Direction * 0.4f, 1, ParticleStyle.Ember);
            }
        }

        void UpdateVisuals(float dt)
        {
            float target = IsOn ? 1f : 0f;
            if (Game.State == GameState.Title)
            {
                target = 0.7f;
            }
            else if (IsOn && OilFraction < Tuning.LowOilFraction)
            {
                // Flicker when the tank is nearly dry.
                target *= 0.55f + 0.45f * Mathf.PerlinNoise(Time.time * 18f, 0.3f);
            }
            visibleAlpha = Mathf.MoveTowards(visibleAlpha, target, dt * FadeSpeed);
            cone.color = new Color(1f, 1f, 1f, visibleAlpha);

            float widen = Game.Run != null ? Game.Run.BeamWidthMultiplier : 1f;
            cone.transform.localScale = new Vector3(1f, widen, 1f);
        }
    }
}
