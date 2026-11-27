using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(BossAttackLibrary))]
public class BossAttackLibraryEditor : Editor
{
    private SerializedProperty _attacksProperty;

    private void OnEnable()
    {
        _attacksProperty = serializedObject.FindProperty("bossAttacks");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.HelpBox(
            "보스 런타임에서 사용하는 필드만 표시합니다. " +
            "보스 전용 데이터 구조를 사용하며 attackName, startupStartNormalized, comboWindowEndNormalized, rollCancelStartNormalized, dashDuration은 제거됐습니다.",
            MessageType.Info);

        DrawAttackList();

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawAttackList()
    {
        if (_attacksProperty == null)
        {
            EditorGUILayout.HelpBox("attacks 프로퍼티를 찾을 수 없습니다.", MessageType.Error);
            return;
        }

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Boss Attacks", EditorStyles.boldLabel);
        if (GUILayout.Button("Add", GUILayout.Width(70f)))
        {
            int index = _attacksProperty.arraySize;
            _attacksProperty.InsertArrayElementAtIndex(index);
            SerializedProperty newElement = _attacksProperty.GetArrayElementAtIndex(index);
            ResetAttackElement(newElement, index);
            newElement.isExpanded = true;
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(4f);

        for (int i = 0; i < _attacksProperty.arraySize; i++)
        {
            SerializedProperty element = _attacksProperty.GetArrayElementAtIndex(i);
            DrawAttackElement(element, i);
            EditorGUILayout.Space(6f);
        }
    }

    private void DrawAttackElement(SerializedProperty element, int index)
    {
        SerializedProperty attackId = element.FindPropertyRelative("attackId");
        SerializedProperty animatorStateName = element.FindPropertyRelative("animatorStateName");
        string header = BuildHeader(index, attackId, animatorStateName);

        EditorGUILayout.BeginVertical("box");

        EditorGUILayout.BeginHorizontal();
        element.isExpanded = EditorGUILayout.Foldout(element.isExpanded, header, true);
        GUILayout.FlexibleSpace();
        GUI.enabled = index > 0;
        if (GUILayout.Button("Up", GUILayout.Width(40f)))
        {
            _attacksProperty.MoveArrayElement(index, index - 1);
        }

        GUI.enabled = index < _attacksProperty.arraySize - 1;
        if (GUILayout.Button("Down", GUILayout.Width(50f)))
        {
            _attacksProperty.MoveArrayElement(index, index + 1);
        }

        GUI.enabled = true;
        if (GUILayout.Button("Delete", GUILayout.Width(60f)))
        {
            _attacksProperty.DeleteArrayElementAtIndex(index);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
            return;
        }
        EditorGUILayout.EndHorizontal();

        if (element.isExpanded)
        {
            DrawUsedFields(element);
        }

        EditorGUILayout.EndVertical();
    }

    private static string BuildHeader(int index, SerializedProperty attackId, SerializedProperty animatorStateName)
    {
        string id = attackId != null ? attackId.stringValue : string.Empty;
        string animatorState = animatorStateName != null ? animatorStateName.stringValue : string.Empty;

        if (!string.IsNullOrWhiteSpace(id))
        {
            return $"{index + 1}. {id}";
        }

        if (!string.IsNullOrWhiteSpace(animatorState))
        {
            return $"{index + 1}. {animatorState}";
        }

        return $"{index + 1}. New Attack";
    }

    private static void DrawUsedFields(SerializedProperty element)
    {
        DrawProperty(element, "attackId");
        DrawProperty(element, "animatorStateName");
        DrawProperty(element, "playerHitReactionType");

        EditorGUILayout.Space(2f);
        EditorGUILayout.LabelField("Damage", EditorStyles.boldLabel);
        DrawProperty(element, "damage");
        DrawProperty(element, "staminaCost");
        DrawProperty(element, "staminaDamage");
        DrawProperty(element, "groggyDamage");
        DrawProperty(element, "poiseDamage");

        EditorGUILayout.Space(2f);
        EditorGUILayout.LabelField("Timing", EditorStyles.boldLabel);
        DrawProperty(element, "impactNormalized");
        DrawProperty(element, "activeStartNormalized");
        DrawProperty(element, "activeEndNormalized");
        DrawProperty(element, "recoveryEndNormalized");

        EditorGUILayout.Space(2f);
        EditorGUILayout.LabelField("Control", EditorStyles.boldLabel);
        DrawProperty(element, "comboWindowStartNormalized");
        DrawProperty(element, "trackUntilNormalized");
        DrawProperty(element, "facingLockNormalized");

        EditorGUILayout.Space(2f);
        EditorGUILayout.LabelField("Armor", EditorStyles.boldLabel);
        SerializedProperty hasSuperArmor = element.FindPropertyRelative("hasSuperArmor");
        EditorGUILayout.PropertyField(hasSuperArmor);
        if (hasSuperArmor.boolValue)
        {
            DrawProperty(element, "superArmorStartNormalized");
            DrawProperty(element, "superArmorEndNormalized");
        }

        EditorGUILayout.Space(2f);
        EditorGUILayout.LabelField("Motion", EditorStyles.boldLabel);
        DrawProperty(element, "dashDistance");
        DrawProperty(element, "prepareStartSpeed");
        DrawProperty(element, "prepareEndSpeed");
        DrawProperty(element, "impactSpeed");
        DrawProperty(element, "recoverySpeed");
        DrawProperty(element, "postAttackWaitMultiplier");

        EditorGUILayout.Space(2f);
        EditorGUILayout.LabelField("Feedback", EditorStyles.boldLabel);
        DrawProperty(element, "isHeavyAttack");
        DrawProperty(element, "cameraShakeIntensity");
        DrawProperty(element, "cameraShakeDuration");
        DrawProperty(element, "hitStopDuration");
        DrawProperty(element, "hitStopMultiplier");
    }

    private static void DrawProperty(SerializedProperty element, string propertyName)
    {
        SerializedProperty property = element.FindPropertyRelative(propertyName);
        if (property != null)
        {
            EditorGUILayout.PropertyField(property);
        }
    }

    private static void ResetAttackElement(SerializedProperty element, int index)
    {
        SetString(element, "attackId", $"Attack_{index + 1}");
        SetString(element, "animatorStateName", $"Attack_{index + 1}");
        element.FindPropertyRelative("playerHitReactionType").enumValueIndex = 0;

        element.FindPropertyRelative("damage").intValue = 10;
        element.FindPropertyRelative("staminaCost").floatValue = 0f;
        element.FindPropertyRelative("staminaDamage").floatValue = 0f;
        element.FindPropertyRelative("groggyDamage").floatValue = 0f;
        element.FindPropertyRelative("poiseDamage").floatValue = 0f;

        element.FindPropertyRelative("impactNormalized").floatValue = 0.5f;
        element.FindPropertyRelative("activeStartNormalized").floatValue = 0.35f;
        element.FindPropertyRelative("activeEndNormalized").floatValue = 0.5f;
        element.FindPropertyRelative("recoveryEndNormalized").floatValue = 1f;

        element.FindPropertyRelative("comboWindowStartNormalized").floatValue = 0.6f;
        element.FindPropertyRelative("trackUntilNormalized").floatValue = 0.15f;
        element.FindPropertyRelative("facingLockNormalized").floatValue = 0.24f;

        element.FindPropertyRelative("hasSuperArmor").boolValue = false;
        element.FindPropertyRelative("superArmorStartNormalized").floatValue = 0f;
        element.FindPropertyRelative("superArmorEndNormalized").floatValue = 0f;

        element.FindPropertyRelative("dashDistance").floatValue = 0f;
        element.FindPropertyRelative("prepareStartSpeed").floatValue = 0.1f;
        element.FindPropertyRelative("prepareEndSpeed").floatValue = 0.8f;
        element.FindPropertyRelative("impactSpeed").floatValue = 5f;
        element.FindPropertyRelative("recoverySpeed").floatValue = 0.3f;
        element.FindPropertyRelative("postAttackWaitMultiplier").floatValue = 0.15f;

        element.FindPropertyRelative("isHeavyAttack").boolValue = false;
        element.FindPropertyRelative("cameraShakeIntensity").floatValue = 0f;
        element.FindPropertyRelative("cameraShakeDuration").floatValue = 0.08f;
        element.FindPropertyRelative("hitStopDuration").floatValue = 0.12f;
        element.FindPropertyRelative("hitStopMultiplier").floatValue = 1f;
    }

    private static void SetString(SerializedProperty element, string propertyName, string value)
    {
        SerializedProperty property = element.FindPropertyRelative(propertyName);
        if (property != null)
        {
            property.stringValue = value;
        }
    }
}
