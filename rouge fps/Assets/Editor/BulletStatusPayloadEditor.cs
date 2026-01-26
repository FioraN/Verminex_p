using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(BulletStatusPayload))]
public class BulletStatusPayloadEditor : Editor
{
    private SerializedProperty _entries;

    private void OnEnable()
    {
        _entries = serializedObject.FindProperty("entries");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        if (_entries == null)
        {
            EditorGUILayout.HelpBox("Property 'entries' not found. Check BulletStatusPayload field name.", MessageType.Error);
            serializedObject.ApplyModifiedProperties();
            return;
        }

        // 画 Size（一定会出现）
        EditorGUILayout.LabelField("Status Entries applied on hit", EditorStyles.boldLabel);
        EditorGUILayout.Space(4);

        int newSize = EditorGUILayout.IntField("Size", _entries.arraySize);
        if (newSize < 0) newSize = 0;
        if (newSize != _entries.arraySize)
            _entries.arraySize = newSize;

        EditorGUILayout.Space(6);

        // 画每个 Element（每个都有一套按 type 显示的字段）
        for (int i = 0; i < _entries.arraySize; i++)
        {
            SerializedProperty entry = _entries.GetArrayElementAtIndex(i);
            DrawEntry(entry, i);

            EditorGUILayout.Space(6);
        }

        // 增删按钮（不依赖展开三角）
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Add"))
                _entries.arraySize += 1;

            EditorGUI.BeginDisabledGroup(_entries.arraySize == 0);
            if (GUILayout.Button("Remove Last"))
                _entries.arraySize -= 1;
            EditorGUI.EndDisabledGroup();
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawEntry(SerializedProperty entry, int index)
    {
        if (entry == null) return;

        SerializedProperty typeProp = entry.FindPropertyRelative("type");
        SerializedProperty stacksProp = entry.FindPropertyRelative("stacksToAdd");
        SerializedProperty durationProp = entry.FindPropertyRelative("duration");

        SerializedProperty tickIntervalProp = entry.FindPropertyRelative("tickInterval");
        SerializedProperty burnDmgProp = entry.FindPropertyRelative("burnDamagePerTickPerStack");

        SerializedProperty slowProp = entry.FindPropertyRelative("slowPerStack");
        SerializedProperty weakenProp = entry.FindPropertyRelative("weakenPerStack");

        SerializedProperty shockDmgProp = entry.FindPropertyRelative("shockChainDamagePerStack");
        SerializedProperty shockRadiusProp = entry.FindPropertyRelative("shockChainRadius");
        SerializedProperty shockMaxProp = entry.FindPropertyRelative("shockMaxChains");

        using (new EditorGUILayout.VerticalScope("box"))
        {
            EditorGUILayout.LabelField($"Entry {index}", EditorStyles.boldLabel);

            EditorGUILayout.PropertyField(typeProp);
            EditorGUILayout.PropertyField(stacksProp);
            EditorGUILayout.PropertyField(durationProp);

            EditorGUILayout.Space(4);

            StatusType t = (StatusType)typeProp.enumValueIndex;

            switch (t)
            {
                case StatusType.Burn:
                    EditorGUILayout.LabelField("Burn Params", EditorStyles.miniBoldLabel);
                    EditorGUILayout.PropertyField(tickIntervalProp, new GUIContent("Tick Interval"));
                    EditorGUILayout.PropertyField(burnDmgProp, new GUIContent("Damage Per Tick Per Stack"));
                    break;

                case StatusType.Slow:
                    EditorGUILayout.LabelField("Slow Params", EditorStyles.miniBoldLabel);
                    EditorGUILayout.PropertyField(slowProp, new GUIContent("Slow Per Stack"));
                    break;

                case StatusType.Poison:
                    EditorGUILayout.LabelField("Poison (Weaken) Params", EditorStyles.miniBoldLabel);
                    EditorGUILayout.PropertyField(weakenProp, new GUIContent("Weaken Per Stack"));
                    break;

                case StatusType.Shock:
                    EditorGUILayout.LabelField("Shock-A Params", EditorStyles.miniBoldLabel);
                    EditorGUILayout.PropertyField(shockDmgProp, new GUIContent("Chain Damage Per Stack"));
                    EditorGUILayout.PropertyField(shockRadiusProp, new GUIContent("Chain Radius"));
                    EditorGUILayout.PropertyField(shockMaxProp, new GUIContent("Max Chains"));
                    break;
            }
        }
    }
}
