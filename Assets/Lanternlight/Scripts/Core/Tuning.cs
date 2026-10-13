namespace Lanternlight
{
    /// <summary>
    /// Every gameplay number in one place so balancing never means hunting through scripts.
    /// </summary>
    public static class Tuning
    {
        // --- Beam -----------------------------------------------------------
        public const float BeamRange = 11f;              // world units from the lamp
        public const float BeamHalfAngle = 14f;          // degrees, before upgrades
        public const float BeamBurnPerSecond = 1f;       // damage per second to a lit shadow
        public const float BeamTurnSharpness = 28f;      // higher = beam snaps to the mouse faster
        public const float LitSlowdown = 0.5f;           // shadows move at this fraction of speed while lit

        // --- Oil ------------------------------------------------------------
        public const float OilCapacity = 100f;
        public const float OilDrainPerSecond = 11f;      // under 8 s of continuous light from a full tank
        public const float OilRegenPerSecond = 3f;       // the wick slowly refills the tank while the beam rests
        public const float OilRelightMinimum = 6f;       // after running dry, need this much to relight
        public const float LowOilFraction = 0.2f;

        // --- Barrels --------------------------------------------------------
        public const float OilPerBarrel = 32f;
        public const float BarrelPullSeconds = 0.8f;     // beam time needed to haul a barrel in
        public const float BarrelPullDecay = 0.6f;       // pull progress lost per second when not lit
        public const float BarrelDriftSpeed = 0.75f;

        // --- Lighthouse -----------------------------------------------------
        public const int StartingFlames = 3;
        public const int DawnFlameRestore = 1;           // dawn relights one flame

        // --- Flare ----------------------------------------------------------
        public const float FlareDamage = 2.5f;           // kills everything except a healthy Brute

        // --- Score ----------------------------------------------------------
        public const float StreakWindow = 2.5f;          // seconds between kills to keep a streak
        public const float StreakStep = 0.25f;           // extra multiplier per kill in a streak
        public const float StreakMaxMultiplier = 3f;
        public const int BarrelScore = 25;
        public const int NightClearBonus = 100;          // multiplied by the night number
        public const int FlameBonus = 50;                // per flame still burning at dawn

        // --- Pacing ---------------------------------------------------------
        public const float NightIntroSeconds = 2.8f;
        public const float DawnLingerSeconds = 3f;
        public const float SpawnQuietBeforeDawn = 3f;    // no new shadows in the last few seconds
        public const float InputLockSeconds = 0.6f;      // ignore clicks right after a screen appears
    }
}
