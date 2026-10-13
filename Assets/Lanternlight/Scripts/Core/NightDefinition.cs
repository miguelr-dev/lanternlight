namespace Lanternlight
{
    public enum EnemyKind
    {
        Crawler,
        Skitter,
        Brute,
        Wisp,
    }

    /// <summary>
    /// The recipe for one night: how long it lasts, how fast shadows arrive,
    /// which kinds show up, and when the big two-sided surges hit.
    /// Nights are pure data so adding or rebalancing one never touches game logic.
    /// </summary>
    public class NightDefinition
    {
        public string Hint;
        public float Duration;              // seconds until dawn
        public float SpawnIntervalStart;    // seconds between spawns at dusk...
        public float SpawnIntervalEnd;      // ...and just before dawn
        public float PackChance;            // chance a crawler spawn is a whole pack
        public int PackSizeMax;
        public float BarrelInterval;        // seconds between oil barrels
        public float[] Surges;              // night progress (0-1) at which both sides attack at once

        // Relative spawn weights per kind.
        public float Crawler;
        public float Skitter;
        public float Brute;
        public float Wisp;

        public EnemyKind PickKind(float roll)
        {
            float total = Crawler + Skitter + Brute + Wisp;
            float r = roll * total;
            if ((r -= Crawler) < 0f) return EnemyKind.Crawler;
            if ((r -= Skitter) < 0f) return EnemyKind.Skitter;
            if ((r -= Brute) < 0f) return EnemyKind.Brute;
            return EnemyKind.Wisp;
        }

        /// <summary>The five nights of the game, easiest first.</summary>
        public static readonly NightDefinition[] All =
        {
            new NightDefinition
            {
                Hint = "SHADOWS CRAWL FROM THE SEA. BURN THEM.",
                Duration = 65f, SpawnIntervalStart = 2.6f, SpawnIntervalEnd = 1.6f,
                PackChance = 0.15f, PackSizeMax = 2, BarrelInterval = 7f, Surges = new float[0],
                Crawler = 1f,
            },
            new NightDefinition
            {
                Hint = "SKITTERS ARE FAST. KEEP THEM IN THE LIGHT.",
                Duration = 75f, SpawnIntervalStart = 2.2f, SpawnIntervalEnd = 1.25f,
                PackChance = 0.25f, PackSizeMax = 3, BarrelInterval = 7.5f, Surges = new[] { 0.7f },
                Crawler = 0.7f, Skitter = 0.3f,
            },
            new NightDefinition
            {
                Hint = "BRUTES NEED A LONG BURN. THEY TAKE TWO FLAMES.",
                Duration = 80f, SpawnIntervalStart = 2f, SpawnIntervalEnd = 1.1f,
                PackChance = 0.3f, PackSizeMax = 3, BarrelInterval = 7.5f, Surges = new[] { 0.55f },
                Crawler = 0.55f, Skitter = 0.3f, Brute = 0.15f,
            },
            new NightDefinition
            {
                Hint = "WISPS FLY FOR THE LAMP ITSELF. AIM HIGH.",
                Duration = 85f, SpawnIntervalStart = 1.8f, SpawnIntervalEnd = 0.95f,
                PackChance = 0.3f, PackSizeMax = 4, BarrelInterval = 8f, Surges = new[] { 0.4f, 0.8f },
                Crawler = 0.45f, Skitter = 0.25f, Brute = 0.12f, Wisp = 0.18f,
            },
            new NightDefinition
            {
                Hint = "THE LONGEST NIGHT. HOLD ON UNTIL DAWN.",
                Duration = 95f, SpawnIntervalStart = 1.6f, SpawnIntervalEnd = 0.8f,
                PackChance = 0.35f, PackSizeMax = 4, BarrelInterval = 8f, Surges = new[] { 0.3f, 0.6f, 0.85f },
                Crawler = 0.4f, Skitter = 0.25f, Brute = 0.15f, Wisp = 0.2f,
            },
        };
    }
}
