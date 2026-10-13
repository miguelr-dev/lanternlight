using UnityEngine;

namespace Lanternlight
{
    /// <summary>
    /// Starts the game automatically when any scene is played, so there is nothing
    /// to wire up by hand: open the project, press Play. Everything else is built
    /// in code by GameManager.
    /// </summary>
    public static class Bootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void StartGame()
        {
            if (GameManager.Instance == null)
            {
                new GameObject("Lanternlight").AddComponent<GameManager>();
            }
        }
    }
}
