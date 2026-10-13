using System.Collections.Generic;
using UnityEngine;

namespace Lanternlight
{
    /// <summary>Sends oil barrels drifting across the sea at the night's rhythm.</summary>
    public class BarrelSpawner : MonoBehaviour
    {
        readonly List<Barrel> active = new List<Barrel>();
        float timer;
        float interval = 7f;
        bool hintPending;     // first barrel of the first night explains itself

        public IReadOnlyList<Barrel> Active => active;

        GameManager Game => GameManager.Instance;

        public void BeginNight(NightDefinition night)
        {
            ClearAll();
            interval = night.BarrelInterval;
            timer = 2f; // first barrel arrives early so the player learns about oil
            hintPending = Game.Run != null && Game.Run.NightIndex == 0;
        }

        void Update()
        {
            if (Game.State != GameState.Playing)
            {
                return;
            }
            timer -= Time.deltaTime;
            if (timer <= 0f)
            {
                timer = interval * Random.Range(0.8f, 1.2f);
                Spawn();
            }
        }

        void Spawn()
        {
            float side = Random.value < 0.5f ? -1f : 1f;
            var go = new GameObject("Barrel");
            go.transform.SetParent(transform, false);
            var barrel = go.AddComponent<Barrel>();
            barrel.Init(side * Layout.BarrelSpawnX, Random.Range(Layout.BarrelLaneBottomY, Layout.BarrelLaneTopY), -side);
            active.Add(barrel);
            if (hintPending)
            {
                hintPending = false;
                Game.Popups.Show(new Vector2(side * 6f, Layout.BarrelLaneTopY + 1.4f), "OIL! SHINE ON THE BARREL", new Color(1f, 0.8f, 0.35f), 0.5f, 3.5f);
            }
        }

        public void Remove(Barrel barrel)
        {
            active.Remove(barrel);
            Destroy(barrel.gameObject);
        }

        public void ClearAll()
        {
            foreach (Barrel b in active)
            {
                Destroy(b.gameObject);
            }
            active.Clear();
        }
    }
}
