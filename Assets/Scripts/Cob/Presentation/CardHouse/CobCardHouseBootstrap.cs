using UnityEngine;

namespace Cob.Presentation.CardHouse
{
    public static class CobCardHouseBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void SpawnIfSceneLooksLikeCardHouse()
        {
            if (FindFirstObjectOfTypeCompat<CobCardHouseDemoController>() != null) return;

            // Only auto-spawn if CardHouse groups are present.
            var groupRegistry = FindFirstObjectOfTypeCompat<global::CardHouse.GroupRegistry>();
            if (groupRegistry == null) return;

            var go = new GameObject("CoB_CardHouse_Demo");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<CobCardHouseDemoController>();
        }

        private static T FindFirstObjectOfTypeCompat<T>() where T : Object
        {
#if UNITY_2023_1_OR_NEWER
            return Object.FindFirstObjectByType<T>();
#else
            return Object.FindObjectOfType<T>();
#endif
        }
    }
}

