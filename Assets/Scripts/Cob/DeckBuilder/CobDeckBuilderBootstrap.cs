using UnityEngine;
using UnityEngine.SceneManagement;

namespace Cob.DeckBuilder
{
    public static class CobDeckBuilderBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void SpawnInDeckBuilderScene()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid()) return;
            if (scene.name == null) return;
            if (scene.name.IndexOf("DeckBuilder", System.StringComparison.OrdinalIgnoreCase) < 0) return;

            if (Object.FindFirstObjectByType<CobDeckBuilderController>() != null) return;

            var go = new GameObject("CoB_DeckBuilder");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<CobDeckBuilderController>();
        }
    }
}

