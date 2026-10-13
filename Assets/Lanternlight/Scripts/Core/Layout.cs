using UnityEngine;

namespace Lanternlight
{
    /// <summary>
    /// Fixed world-space layout of the single game screen.
    /// The art is drawn at 16 pixels per unit on a 320 x 180 pixel canvas,
    /// so the visible world is 20 x 11.25 units centred on the origin.
    /// Numbers here must match Tools/generate_art.py.
    /// </summary>
    public static class Layout
    {
        public const float PixelsPerUnit = 16f;
        public const float HalfWidth = 10f;
        public const float HalfHeight = 5.625f;

        // Sea and island.
        public const float WaterSurfaceY = -3.2f;
        public const float SeaFrontTopY = -3.15f;
        public const float SeaBackTopY = -2.3125f;
        public const float IslandCentreY = -2.625f;

        // Lighthouse.
        public const float LighthouseBaseY = -1.6f;
        public const float TowerHalfWidth = 0.94f;
        public static readonly Vector2 LampPosition = new Vector2(0f, 2.68f);

        // Where things enter the screen.
        public const float GroundSpawnMinX = 8.2f;
        public const float GroundSpawnMaxX = 9.6f;
        public const float AirSpawnX = 10.8f;
        public const float BarrelSpawnX = 10.8f;
        public const float BarrelLaneTopY = -3.9f;
        public const float BarrelLaneBottomY = -4.7f;

        /// <summary>
        /// Height of the rock surface at world x. Flat around the lighthouse,
        /// then sloping down into the sea. Mirrors ground_y() in the art generator.
        /// </summary>
        public static float GroundY(float x)
        {
            float ax = Mathf.Abs(x);
            if (ax <= 1.2f)
            {
                return LighthouseBaseY;
            }
            float t = Mathf.Min((ax - 1.2f) / 6.4f, 1f);
            return LighthouseBaseY - 1.8f * Mathf.Pow(t, 1.35f);
        }

        /// <summary>
        /// Where a ground creature's feet are at x: on the rocks, or wading at the
        /// water's surface once the rocks dip below the sea.
        /// </summary>
        public static float WalkY(float x)
        {
            return Mathf.Max(GroundY(x), WaterSurfaceY - 0.08f);
        }
    }

    /// <summary>Sprite sorting orders, back to front.</summary>
    public static class SortOrder
    {
        public const int Sky = -100;
        public const int SkyDawn = -99;
        public const int Stars = -95;
        public const int Clouds = -90;
        public const int SeaBack = -80;
        public const int Halo = -70;
        public const int Island = -60;
        public const int Lighthouse = -50;
        public const int LampGlow = -49;
        public const int Enemies = 0;
        public const int EnemyGlow = 1;
        public const int AirEnemies = 5;
        public const int SeaFront = 20;
        public const int Barrels = 21;
        public const int Beam = 22;
        public const int Particles = 30;
        public const int Popups = 40;
        public const int Flash = 90;
        public const int Hud = 100;
        public const int Overlay = 200;
    }
}
