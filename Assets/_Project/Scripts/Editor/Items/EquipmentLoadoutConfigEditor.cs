using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace VRGame.Items.Editor
{
    [CustomEditor(typeof(EquipmentLoadoutConfig))]
    public sealed class EquipmentLoadoutConfigEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EquipmentLoadoutConfig config = (EquipmentLoadoutConfig)target;
            IReadOnlyList<EquipmentLoadoutConfigValidationIssue> issues = config.ValidateConfig();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Runtime Slots", EditorStyles.boldLabel);
            List<EquipmentRuntimeSlot> slots = config.BuildRuntimeSlots();
            for (int i = 0; i < slots.Count; i++)
            {
                EquipmentRuntimeSlot slot = slots[i];
                EditorGUILayout.LabelField(slot.SlotId, $"{slot.DisplayName} ({slot.AllowedItemFlags})");
            }

            DrawValidationIssues(issues);
        }

        private static void DrawValidationIssues(IReadOnlyList<EquipmentLoadoutConfigValidationIssue> issues)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Validation", EditorStyles.boldLabel);

            if (issues == null || issues.Count == 0)
            {
                EditorGUILayout.HelpBox("No equipment loadout config validation issues found.", MessageType.Info);
                return;
            }

            for (int i = 0; i < issues.Count; i++)
            {
                EquipmentLoadoutConfigValidationIssue issue = issues[i];
                EditorGUILayout.HelpBox(issue.Message, ToMessageType(issue.Severity));
            }
        }

        private static MessageType ToMessageType(EquipmentLoadoutConfigValidationSeverity severity)
        {
            switch (severity)
            {
                case EquipmentLoadoutConfigValidationSeverity.Error:
                    return MessageType.Error;
                case EquipmentLoadoutConfigValidationSeverity.Warning:
                    return MessageType.Warning;
                case EquipmentLoadoutConfigValidationSeverity.Info:
                default:
                    return MessageType.Info;
            }
        }
    }
}
