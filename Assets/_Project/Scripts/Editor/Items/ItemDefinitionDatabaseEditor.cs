using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace VRGame.Items.Editor
{
    [CustomEditor(typeof(ItemDefinitionDatabase))]
    public sealed class ItemDefinitionDatabaseEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            ItemDefinitionDatabase database = (ItemDefinitionDatabase)target;

            EditorGUILayout.Space();
            if (GUILayout.Button("Rebuild Lookup Cache"))
            {
                database.RebuildLookup();
                EditorUtility.SetDirty(database);
            }

            IReadOnlyList<ItemDefinitionValidationIssue> issues = database.ValidateDefinitions();
            ItemDefinitionEditorUtility.DrawValidationIssues(issues);
        }
    }
}
