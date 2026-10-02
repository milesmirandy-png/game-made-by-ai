using UnityEngine;

namespace Swat
{
    // Starts the game when you press Play in any scene, so nothing has to be
    // set up in the editor. If a scene already contains a GameManager, that one is used.
    public static class GameBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void CreateGame()
        {
            if (Object.FindAnyObjectByType<GameManager>() != null) return;
            new GameObject("SWAT Game").AddComponent<GameManager>();
        }
    }
}
