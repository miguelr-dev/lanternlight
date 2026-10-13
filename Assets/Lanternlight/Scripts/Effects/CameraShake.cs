using UnityEngine;

namespace Lanternlight
{
    /// <summary>
    /// Trauma-based camera shake: hits add trauma, which decays over time.
    /// Shake strength is trauma squared, so small bumps stay subtle and big hits feel big.
    /// </summary>
    public class CameraShake : MonoBehaviour
    {
        const float MaxOffset = 0.35f;
        const float MaxRoll = 1.5f;
        const float DecayPerSecond = 1.6f;

        Camera cam;
        Vector3 restPosition;
        float trauma;

        public void Init(Camera camera)
        {
            cam = camera;
            restPosition = cam.transform.position;
        }

        public void Add(float amount)
        {
            trauma = Mathf.Clamp01(trauma + amount);
        }

        void LateUpdate()
        {
            if (cam == null)
            {
                return;
            }
            trauma = Mathf.Max(0f, trauma - DecayPerSecond * Time.unscaledDeltaTime);
            float shake = trauma * trauma;
            float t = Time.unscaledTime * 25f;
            float x = (Mathf.PerlinNoise(t, 0.1f) * 2f - 1f) * MaxOffset * shake;
            float y = (Mathf.PerlinNoise(0.7f, t) * 2f - 1f) * MaxOffset * shake;
            float roll = (Mathf.PerlinNoise(t, 3.3f) * 2f - 1f) * MaxRoll * shake;
            cam.transform.position = restPosition + new Vector3(x, y, 0f);
            cam.transform.rotation = Quaternion.Euler(0f, 0f, roll);
        }
    }
}
