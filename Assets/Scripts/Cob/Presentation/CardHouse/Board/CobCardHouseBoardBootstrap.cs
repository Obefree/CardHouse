using UnityEngine;
using UnityEngine.SceneManagement;

namespace Cob.Presentation.CardHouse.Board
{
    public static class CobCardHouseBoardBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void SpawnInCardTableScene()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid()) return;
            var name = scene.name ?? string.Empty;

            // Use a clean scene as base.
            if (name.IndexOf("CardTable", System.StringComparison.OrdinalIgnoreCase) < 0 &&
                name.IndexOf("CoB", System.StringComparison.OrdinalIgnoreCase) < 0)
            {
                return;
            }

            if (FindFirstObjectOfTypeCompat<CobCardHouseBoardController>() != null) return;

            var go = new GameObject("CoB_Board");
            go.AddComponent<CobCardHouseBoardController>();
        }

        private static T FindFirstObjectOfTypeCompat<T>() where T : UnityEngine.Object
        {
#if UNITY_2023_1_OR_NEWER
            return UnityEngine.Object.FindFirstObjectByType<T>();
#else
            return UnityEngine.Object.FindObjectOfType<T>();
#endif
        }
    }
}

