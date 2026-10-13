using UnityEngine;

namespace Lanternlight
{
    /// <summary>A full-screen colour flash (flare, lighthouse hit) that fades out.</summary>
    public class ScreenFlash : MonoBehaviour
    {
        SpriteRenderer quad;
        Color color;
        float duration;
        float timeLeft;

        void Awake()
        {
            quad = Art.CreateRect("Flash", transform, Vector2.zero, new Vector2(40f, 30f), Color.clear, SortOrder.Flash);
        }

        public void Trigger(Color flashColor, float seconds)
        {
            color = flashColor;
            duration = seconds;
            timeLeft = seconds;
        }

        void Update()
        {
            timeLeft = Mathf.Max(0f, timeLeft - Time.unscaledDeltaTime);
            Color c = color;
            c.a *= duration > 0f ? timeLeft / duration : 0f;
            quad.color = c;
        }
    }
}
