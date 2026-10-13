using System.Collections.Generic;
using UnityEngine;

namespace Lanternlight
{
    /// <summary>
    /// Spawns shadows according to the current night's recipe. Spawns speed up as
    /// dawn approaches, crawlers sometimes come in packs, the side tends to alternate
    /// (so the player keeps swinging the beam), and surges attack both sides at once.
    /// </summary>
    public class EnemySpawner : MonoBehaviour
    {
        const float SideSwitchChance = 0.6f;
        const float PackSpacing = 0.75f;   // world units between pack members
        const float SurgeWarning = 1.2f;   // seconds of warning before a surge lands

        readonly List<Enemy> active = new List<Enemy>();
        NightDefinition night;
        float spawnTimer;
        int lastSide = 1;
        int nextSurge;
        float surgeCountdown = -1f;

        public IReadOnlyList<Enemy> Active => active;

        GameManager Game => GameManager.Instance;

        public void BeginNight(NightDefinition definition)
        {
            ClearAll();
            night = definition;
            spawnTimer = 1f;
            nextSurge = 0;
            surgeCountdown = -1f;
        }

        void Update()
        {
            if (Game.State != GameState.Playing || night == null)
            {
                return;
            }
            float dt = Time.deltaTime;
            float progress = Game.DawnProgress;
            bool quietTime = Game.NightTime > night.Duration - Tuning.SpawnQuietBeforeDawn;
            if (quietTime)
            {
                return;
            }

            UpdateSurges(dt, progress);

            spawnTimer -= dt;
            if (spawnTimer <= 0f)
            {
                float interval = Mathf.Lerp(night.SpawnIntervalStart, night.SpawnIntervalEnd, progress);
                spawnTimer = interval * Random.Range(0.8f, 1.2f);
                SpawnWave(PickSide());
            }
        }

        void UpdateSurges(float dt, float progress)
        {
            if (surgeCountdown < 0f && nextSurge < night.Surges.Length && progress >= night.Surges[nextSurge])
            {
                // Warn the player first, then hit both sides at once.
                surgeCountdown = SurgeWarning;
                nextSurge++;
                Game.Popups.Show(new Vector2(0f, 0.6f), "SURGE! BOTH SIDES!", new Color(1f, 0.45f, 0.45f), 1f, 2f);
                Game.Audio.Play(Sfx.BruteRoar, 0.6f, 0.8f);
            }
            if (surgeCountdown >= 0f)
            {
                surgeCountdown -= dt;
                if (surgeCountdown < 0f)
                {
                    SpawnPack(EnemyKind.Crawler, -1, 3);
                    SpawnPack(EnemyKind.Crawler, 1, 3);
                    spawnTimer = Mathf.Max(spawnTimer, 2.5f); // breathing room after a surge
                }
            }
        }

        int PickSide()
        {
            if (Random.value < SideSwitchChance)
            {
                lastSide = -lastSide;
            }
            return lastSide;
        }

        void SpawnWave(int side)
        {
            EnemyKind kind = night.PickKind(Random.value);
            if (kind == EnemyKind.Crawler && Random.value < night.PackChance)
            {
                SpawnPack(kind, side, Random.Range(2, night.PackSizeMax + 1));
            }
            else
            {
                Spawn(kind, side, 0);
            }
        }

        void SpawnPack(EnemyKind kind, int side, int count)
        {
            for (int i = 0; i < count; i++)
            {
                Spawn(kind, side, i);
            }
        }

        /// <param name="slot">Position within a pack: later members start further out to sea.</param>
        public Enemy Spawn(EnemyKind kind, int side, int slot)
        {
            Vector2 position;
            if (kind == EnemyKind.Wisp)
            {
                position = new Vector2(side * Layout.AirSpawnX, Random.Range(0.3f, 4.6f));
            }
            else
            {
                float x = Random.Range(Layout.GroundSpawnMinX, Layout.GroundSpawnMaxX - 0.6f) + slot * PackSpacing;
                position = new Vector2(side * x, Layout.WaterSurfaceY);
            }

            var go = new GameObject(kind.ToString());
            go.transform.SetParent(transform, false);
            Enemy enemy = AddBehaviour(go, kind);
            enemy.Init(kind, side, position);
            active.Add(enemy);
            return enemy;
        }

        static Enemy AddBehaviour(GameObject go, EnemyKind kind)
        {
            switch (kind)
            {
                case EnemyKind.Skitter: return go.AddComponent<Skitter>();
                case EnemyKind.Brute: return go.AddComponent<Brute>();
                case EnemyKind.Wisp: return go.AddComponent<Wisp>();
                default: return go.AddComponent<Crawler>();
            }
        }

        /// <summary>Stops tracking a shadow (it destroys itself after its death animation).</summary>
        public void Remove(Enemy enemy)
        {
            active.Remove(enemy);
        }

        public void ClearAll()
        {
            foreach (Enemy e in active)
            {
                Destroy(e.gameObject);
            }
            active.Clear();
        }

        public void MeltAll()
        {
            foreach (Enemy e in active)
            {
                e.MeltAtDawn();
            }
            active.Clear();
        }

        public void FlareAll(float damage)
        {
            // Copy, because killed shadows remove themselves from the list.
            foreach (Enemy e in new List<Enemy>(active))
            {
                e.TakeFlareHit(damage);
            }
        }

        /// <summary>How many ground shadows are within <paramref name="distance"/> of the tower.</summary>
        public int CountNearTower(float distance)
        {
            int count = 0;
            foreach (Enemy e in active)
            {
                if (e.IsAlive && e.Kind != EnemyKind.Wisp && Mathf.Abs(e.Centre.x) < distance)
                {
                    count++;
                }
            }
            return count;
        }

        /// <summary>0..1: how close the shadows are to the lighthouse, for the music.</summary>
        public float ThreatLevel()
        {
            float threat = 0f;
            foreach (Enemy e in active)
            {
                float distance = e.Kind == EnemyKind.Wisp
                    ? Vector2.Distance(e.Centre, Layout.LampPosition)
                    : Mathf.Abs(e.Centre.x);
                threat += Mathf.Clamp01(1f - (distance - 1f) / 8f) * (e.Kind == EnemyKind.Brute ? 2f : 1f);
            }
            return Mathf.Clamp01(threat * 0.3f);
        }
    }
}
