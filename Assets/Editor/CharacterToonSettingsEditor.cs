using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using VRProject.Character;

namespace VRProject.EditorTools
{
    /// <summary>
    /// 부위 그룹마다 테두리 박스를 둘러 접었다 폈다 하는 영역을 눈으로 구분한다.
    /// 항목이 많아서 박스가 없으면 어느 값이 어느 부위 것인지 헷갈린다.
    ///
    /// 필드 선언 순서는 그대로 유지하고, 그룹만 박스로 감싼다.
    /// </summary>
    [CustomEditor(typeof(CharacterToonSettings))]
    [CanEditMultipleObjects]
    public class CharacterToonSettingsEditor : Editor
    {
        // 박스로 감쌀 그룹 필드 이름. 나머지는 평범하게 그린다.
        private static readonly HashSet<string> GroupFields = new HashSet<string>
        {
            "표정", "볼_붉힘", "머리카락", "깃털_장식", "의상_피부", "금속_장식",
        };

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            SerializedProperty it = serializedObject.GetIterator();
            bool enterChildren = true;

            while (it.NextVisible(enterChildren))
            {
                enterChildren = false;   // 자식은 PropertyField가 알아서 그린다

                if (it.propertyPath == "m_Script")
                {
                    using (new EditorGUI.DisabledScope(true))
                        EditorGUILayout.PropertyField(it, true);
                    continue;
                }

                if (GroupFields.Contains(it.name))
                    DrawBoxed(it);
                else
                    EditorGUILayout.PropertyField(it, true);
            }

            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space(6);
            DrawUtilityButtons();
        }

        /// <summary>그룹 하나를 테두리 박스 안에 그린다.</summary>
        private static void DrawBoxed(SerializedProperty prop)
        {
            EditorGUILayout.Space(2);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // 접혀 있을 때는 헤더만, 펼치면 안쪽에 여백을 조금 준다.
            EditorGUILayout.PropertyField(prop.Copy(), true);

            if (prop.isExpanded) EditorGUILayout.Space(2);

            EditorGUILayout.EndVertical();
        }

        private void DrawUtilityButtons()
        {
            var settings = (CharacterToonSettings)target;

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("셰이딩 다시 적용", GUILayout.Height(22)))
            {
                foreach (Object o in targets)
                    if (o is CharacterToonSettings s) s.ApplyShading();
            }

            if (GUILayout.Button("분류 결과 확인", GUILayout.Height(22)))
            {
                settings.LogClassification();
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField(
                "'분류 결과 확인'은 각 머티리얼이 어느 그룹으로 갔는지 Console에 출력합니다.",
                EditorStyles.miniLabel);
        }
    }
}
