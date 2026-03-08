using Cob.Presentation.CardHouse.Board;
using UnityEditor;
using UnityEngine;

namespace Cob.EditorTools
{
    public static class CobBoardSceneTools
    {
        [MenuItem("GameObject/CoB/Create Board Controller (CoB_Board)", priority = 11)]
        private static void CreateBoardController(MenuCommand _)
        {
            var existing = Object.FindFirstObjectByType<CobCardHouseBoardController>();
            if (existing != null)
            {
                Selection.activeObject = existing.gameObject;
                EditorGUIUtility.PingObject(existing.gameObject);
                return;
            }

            var go = new GameObject("CoB_Board");
            Undo.RegisterCreatedObjectUndo(go, "Create CoB_Board");
            var controller = go.AddComponent<CobCardHouseBoardController>();
            Undo.RegisterCreatedObjectUndo(controller, "Add CobCardHouseBoardController");

            Selection.activeObject = go;
            EditorGUIUtility.PingObject(go);
        }

        [MenuItem("Tools/CoB/Select CoB_Board", priority = 11)]
        private static void SelectBoardController()
        {
            var existing = Object.FindFirstObjectByType<CobCardHouseBoardController>();
            if (existing == null)
            {
                EditorUtility.DisplayDialog("CoB", "CobCardHouseBoardController не найден в открытой сцене.\n\nСоздай его: GameObject → CoB → Create Board Controller.", "OK");
                return;
            }

            Selection.activeObject = existing.gameObject;
            EditorGUIUtility.PingObject(existing.gameObject);
        }
    }
}

