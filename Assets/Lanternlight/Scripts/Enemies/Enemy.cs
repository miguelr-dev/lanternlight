using UnityEngine;

namespace Lanternlight
{
    /// <summary>
    /// Base class for every shadow creature. Handles the shared life cycle -
    /// rising out of the sea, advancing on the lighthouse, burning in the beam,
    /// and dying - while each subclass decides how it looks and how it moves.
    /// </summary>
    public abstract class Enemy : MonoBehaviour
    {
        static readonly Color BurnColor = new Color(1f, 0.55f, 0.2f);
        static readonly Color DawnColor = new Color(1f, 0.8f, 0.5f);

        protected enum Phase { Emerging, Advancing, Dying }

        // --- Stats, set by each subclass in Configure() ---------------------
        protected float MaxHealth = 1f;
        protected float Speed = 1f;
        protected float LitSpeedFactor = Tuning.LitSlowdown;
        protected float AnimationFps = 8f;
        protected float EmergeSeconds = 0.7f;
        protected bool Flying;
        public int Points { get; protected set; } = 10;
        public int Damage { get; protected set; } = 1;
        public float Radius { get; protected set; } = 0.4f;   // hit size for the beam
        public float Height { get; protected set; } = 0.7f;   // used to find the body's centre

        // --- Runtime state --------------------------------------------------
        public EnemyKind Kind { get; private set; }
        public int Side { get; private set; }                 // -1 came from the left, +1 from the right
        protected Phase CurrentPhase { get; private set; } = Phase.Emerging;
        protected Vector2 Position;                           // feet position (centre for flyers)
        protected float PhaseTime;                            // seconds spent in the current phase
        protected SpriteRenderer Body;

        SpriteRenderer heatGlow;
        Sprite[] frames;
        Sprite[] masks;
        float health;
        float animTime;
        float heat;                 // burn glow, 0..1
        int lastBurnFrame = -10;
        bool killedByDawn;
        Vector2 emergeFrom;

        public bool IsAlive => CurrentPhase != Phase.Dying;
        public bool IsLit => Time.frameCount - lastBurnFrame <= 1;
        public float HealthFraction => Mathf.Clamp01(health / MaxHealth);

        /// <summary>The point the beam aims at: middle of the body.</summary>
        public Vector2 Centre => Flying ? Position : Position + Vector2.up * (Height * 0.5f);

        protected GameManager Game => GameManager.Instance;

        // --- Hooks for subclasses ------------------------------------------
        /// <summary>Set stats and return the sprite name prefix (e.g. "crawler").</summary>
        protected abstract string Configure();

        /// <summary>Move toward the lighthouse. <paramref name="speedScale"/> is lower while lit.</summary>
        protected abstract void Advance(float dt, float speedScale);

        /// <summary>True once the creature is close enough to hurt the lighthouse.</summary>
        protected virtual bool HasReachedLighthouse()
        {
            return Mathf.Abs(Position.x) <= Layout.TowerHalfWidth + Radius * 0.5f;
        }

        /// <summary>Optional extra sound or effect when first appearing.</summary>
        protected virtual void OnSpawned() { }

        // --- Life cycle -----------------------------------------------------
        public void Init(EnemyKind kind, int side, Vector2 spawnPosition)
        {
            Kind = kind;
            Side = side;
            string art = Configure();
            health = MaxHealth;

            frames = Art.Frames(art, 4, Flying ? Art.Centre : Art.BottomCentre);
            masks = new Sprite[frames.Length];
            for (int i = 0; i < frames.Length; i++)
            {
                masks[i] = Art.Get($"{art}_{i}_mask", Flying ? Art.Centre : Art.BottomCentre);
            }
            int order = Flying ? SortOrder.AirEnemies : SortOrder.Enemies;
            Body = Art.CreateSprite("Body", transform, frames[0], order);
            heatGlow = Art.CreateSprite("Heat", transform, masks[0], order + 1);
            heatGlow.color = new Color(BurnColor.r, BurnColor.g, BurnColor.b, 0f);
            Face(side < 0 ? 1f : -1f);
            animTime = Random.value;

            Position = spawnPosition;
            emergeFrom = Flying ? spawnPosition : new Vector2(spawnPosition.x, Layout.WaterSurfaceY - Height - 0.2f);
            if (!Flying)
            {
                Position = emergeFrom;
                Game.Particles.Emit(new Vector2(spawnPosition.x, Layout.WaterSurfaceY), 10, ParticleStyle.Splash);
                Game.Audio.PlayAt(Sfx.SpawnSplash, spawnPosition, 0.45f);
            }
            OnSpawned();
            ApplyTransform();
        }

        /// <summary>Flip the sprite so it faces its direction of travel (art faces right).</summary>
        protected void Face(float directionX)
        {
            bool flip = directionX < 0f;
            Body.flipX = flip;
            heatGlow.flipX = flip;
        }

        protected void Update()
        {
            float dt = Time.deltaTime;
            Animate(dt);
            UpdateHeat(dt);

            if (CurrentPhase == Phase.Dying)
            {
                UpdateDying(dt);
                return;
            }
            // Shadows only move during the night itself (they freeze on the game-over screen).
            if (Game.State != GameState.Playing)
            {
                return;
            }

            PhaseTime += dt;
            if (CurrentPhase == Phase.Emerging)
            {
                Emerge();
            }
            else
            {
                Advance(dt, IsLit ? LitSpeedFactor : 1f);
                if (HasReachedLighthouse())
                {
                    Game.Lighthouse.TakeHit(Damage, Centre);
                    Game.Particles.Emit(Centre, 10, ParticleStyle.ShadowPuff);
                    BeginDying();
                    Game.Enemies.Remove(this);
                    return;
                }
            }
            ApplyTransform();
        }

        void Emerge()
        {
            float t = Mathf.Clamp01(PhaseTime / EmergeSeconds);
            if (Flying)
            {
                Body.color = new Color(1f, 1f, 1f, t);  // flyers fade in instead of rising
            }
            else
            {
                float targetY = Layout.WalkY(emergeFrom.x);
                Position.y = Mathf.Lerp(emergeFrom.y, targetY, 1f - (1f - t) * (1f - t));
            }
            if (t >= 1f)
            {
                CurrentPhase = Phase.Advancing;
                PhaseTime = 0f;
            }
        }

        /// <summary>The beam calls this every frame the creature is inside the cone.</summary>
        public void Burn(float damage)
        {
            if (!IsAlive)
            {
                return;
            }
            lastBurnFrame = Time.frameCount;
            health -= damage;
            if (Random.value < 0.35f)
            {
                Game.Particles.Emit(Centre + Random.insideUnitCircle * Radius * 0.6f, 1, ParticleStyle.Ember);
            }
            if (health <= 0f)
            {
                Die(true);
            }
        }

        /// <summary>Flat damage from a flare.</summary>
        public void TakeFlareHit(float damage)
        {
            if (!IsAlive)
            {
                return;
            }
            health -= damage;
            heat = 1f;
            if (health <= 0f)
            {
                Die(true);
            }
        }

        /// <summary>At sunrise every shadow melts away (no score, golden motes instead of smoke).</summary>
        public void MeltAtDawn()
        {
            if (!IsAlive)
            {
                return;
            }
            killedByDawn = true;
            Game.Particles.Emit(Centre, 8, ParticleStyle.Dawn);
            BeginDying();
        }

        void Die(bool byPlayer)
        {
            Game.Particles.Emit(Centre, 6, ParticleStyle.ShadowPuff);
            Game.Particles.Emit(Centre, 10, ParticleStyle.Ember);
            Game.Audio.PlayAt(Sfx.EnemyDie, Centre, 0.7f);
            BeginDying();
            if (byPlayer)
            {
                Game.OnEnemyKilled(this);
            }
            Game.Enemies.Remove(this);
        }

        void BeginDying()
        {
            CurrentPhase = Phase.Dying;
            PhaseTime = 0f;
        }

        void UpdateDying(float dt)
        {
            const float duration = 0.35f;
            PhaseTime += dt;
            float t = Mathf.Clamp01(PhaseTime / duration);
            // Shrink down into a smudge while glowing out.
            transform.localScale = new Vector3(1f + t * 0.3f, 1f - t, 1f);
            Color tint = killedByDawn ? DawnColor : BurnColor;
            heatGlow.color = new Color(tint.r, tint.g, tint.b, (1f - t) * 0.9f);
            Body.color = new Color(1f, 1f, 1f, 1f - t);
            if (t >= 1f)
            {
                Destroy(gameObject);
            }
        }

        void Animate(float dt)
        {
            animTime += dt * AnimationFps;
            int frame = Mathf.FloorToInt(animTime) % frames.Length;
            Body.sprite = frames[frame];
            heatGlow.sprite = masks[frame];
        }

        void UpdateHeat(float dt)
        {
            if (CurrentPhase == Phase.Dying)
            {
                return;
            }
            // Glows hotter the longer it burns and the more damage it has taken.
            float target = IsLit ? 0.25f + 0.4f * (1f - HealthFraction) : 0f;
            heat = Mathf.MoveTowards(heat, target, dt * (IsLit ? 4f : 2f));
            heatGlow.color = new Color(BurnColor.r, BurnColor.g, BurnColor.b, heat);
            // A burning shadow shudders.
            Body.transform.localPosition = IsLit ? (Vector3)(Random.insideUnitCircle * 0.03f) : Vector3.zero;
            heatGlow.transform.localPosition = Body.transform.localPosition;
        }

        protected void ApplyTransform()
        {
            transform.localPosition = Position;
        }
    }
}
