using System;
using System.IO;
using Cob.Data;
using UnityEditor;
using UnityEngine;

namespace Cob.Editor
{
    public static class CobCardDatabaseImporter
    {
        private const string DefaultResourcesAssetPath = "Assets/Resources/CoB/CobCardDatabase.asset";

        // Default location if you want to keep the JSON inside this CardHouse project.
        private const string DefaultStreamingJsonPath = "Assets/StreamingAssets/CoB/unified_cards_cob_3.8.json";

        // If you still have the original web repo, we can import directly from there.
        private const string OriginalRepoJsonPath = @"C:\Users\lev\Documents\GitHub\Cob-Game-Clean-v.1\public\data\unified_cards_cob_3.8.json";

        [MenuItem("CoB/Import/Card database JSON → ScriptableObject")]
        public static void Import()
        {
            var jsonPath = FindBestDefaultJsonPath();
            if (string.IsNullOrWhiteSpace(jsonPath))
            {
                jsonPath = EditorUtility.OpenFilePanel(
                    title: "Select unified_cards*.json",
                    directory: "",
                    extension: "json"
                );
            }
            else
            {
                // Let user confirm/override if they want
                if (!EditorUtility.DisplayDialog(
                        "CoB Import",
                        $"Found JSON at:\n{jsonPath}\n\nImport it?",
                        "Import",
                        "Choose another file..."
                    ))
                {
                    jsonPath = EditorUtility.OpenFilePanel(
                        title: "Select unified_cards*.json",
                        directory: "",
                        extension: "json"
                    );
                }
            }

            if (string.IsNullOrWhiteSpace(jsonPath))
            {
                return;
            }

            if (!File.Exists(jsonPath))
            {
                EditorUtility.DisplayDialog("CoB Import", $"File not found:\n{jsonPath}", "OK");
                return;
            }

            string json;
            try
            {
                json = File.ReadAllText(jsonPath);
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog("CoB Import", $"Failed to read file:\n{e.Message}", "OK");
                return;
            }

            CobCardDatabaseRoot root;
            try
            {
                root = JsonUtility.FromJson<CobCardDatabaseRoot>(json);
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog("CoB Import", $"Failed to parse JSON:\n{e.Message}", "OK");
                return;
            }

            if (root == null)
            {
                EditorUtility.DisplayDialog("CoB Import", "JSON parsed to null (unexpected).", "OK");
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(DefaultResourcesAssetPath) ?? "Assets");

            var existing = AssetDatabase.LoadAssetAtPath<CobCardDatabase>(DefaultResourcesAssetPath);
            CobCardDatabase db;
            if (existing != null)
            {
                db = existing;
            }
            else
            {
                db = ScriptableObject.CreateInstance<CobCardDatabase>();
                AssetDatabase.CreateAsset(db, DefaultResourcesAssetPath);
            }

            db.ReplaceFrom(root);
            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var counts =
                $"disciple={db.disciple.Count}, starters={db.starters.Count}, attire={db.attire.Count}, monsters={db.monsters.Count}, " +
                $"consumables={db.consumables.Count}, events={db.events.Count}, total={db.All.Count}";

            EditorUtility.DisplayDialog("CoB Import", $"Imported OK.\n\n{counts}\n\nSaved: {DefaultResourcesAssetPath}", "OK");
            Selection.activeObject = db;
        }

        private static string FindBestDefaultJsonPath()
        {
            if (File.Exists(DefaultStreamingJsonPath)) return DefaultStreamingJsonPath;
            if (File.Exists(OriginalRepoJsonPath)) return OriginalRepoJsonPath;
            return null;
        }
    }
}

