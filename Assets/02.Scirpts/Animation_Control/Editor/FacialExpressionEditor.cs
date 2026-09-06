using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using VRProject.Character;

namespace VRProject.EditorTools
{
    /// <summary>
    /// 표정을 버튼으로 재생하고, 믹서 슬라이더로 새 조합을 직접 만든다.
    /// 인덱스를 세어가며 맞추는 것보다 훨씬 빠르다.
    /// </summary>
    [CustomEditor(typeof(FacialExpression))]
    public class FacialExpressionEditor : Editor
    {
        private const int ButtonsPerRow = 3;

        private string 저장이름 = "새 표정";
        private float 저장_전환시간 = 0.15f;

        // 그룹별 접힘 상태. 눈·눈썹·입은 기본으로 펼쳐둔다.
        private static readonly Dictionary<FacialExpression.MorphGroup, bool> Folded =
            new Dictionary<FacialExpression.MorphGroup, bool>
            {
                { FacialExpression.MorphGroup.눈썹, true },
                { FacialExpression.MorphGroup.눈, true },
                { FacialExpression.MorphGroup.입, true },
                { FacialExpression.MorphGroup.기타, false },
            };

        public override void OnInspectorGUI()
        {
            var fx = (FacialExpression)target;

            DrawPresetButtons(fx);
            EditorGUILayout.Space(6);
            DrawMixer(fx);

            EditorGUILayout.Space(8);
            DrawDefaultInspector();
        }

        // ── 프리셋 재생 ─────────────────────────────────────────────
        private void DrawPresetButtons(FacialExpression fx)
        {
            EditorGUILayout.LabelField("표정 미리보기", EditorStyles.boldLabel);

            if (fx.믹서모드)
            {
                EditorGUILayout.HelpBox(
                    "믹서 모드가 켜져 있어 프리셋 대신 믹서 값이 적용됩니다.\n" +
                    "프리셋을 확인하려면 믹서 모드를 끄세요.", MessageType.Info);
            }

            if (fx.Count == 0)
            {
                EditorGUILayout.HelpBox("표정 목록이 비어 있습니다.", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField("현재", fx.CurrentName);

            // 목록을 그리는 도중에 지우면 레이아웃이 깨진다. 인덱스만 받아두고
            // 루프가 끝난 뒤에 처리한다.
            int 삭제요청 = -1;

            for (int i = 0; i < fx.Count; i += ButtonsPerRow)
            {
                EditorGUILayout.BeginHorizontal();
                for (int k = 0; k < ButtonsPerRow && i + k < fx.Count; k++)
                {
                    int index = i + k;
                    string label = fx.표정목록[index]?.이름 ?? $"[{index}]";

                    GUI.backgroundColor = (label == fx.CurrentName)
                        ? new Color(0.6f, 0.8f, 1f) : Color.white;

                    if (GUILayout.Button(label, GUILayout.Height(24)))
                    {
                        Undo.RecordObject(fx, "Play expression");
                        fx.Play(index);
                        EditorUtility.SetDirty(fx);
                    }

                    GUI.backgroundColor = new Color(1f, 0.65f, 0.65f);
                    if (GUILayout.Button(new GUIContent("✕", $"'{label}' 삭제 (Ctrl+Z로 되돌릴 수 있음)"),
                                         GUILayout.Width(22), GUILayout.Height(24)))
                    {
                        삭제요청 = index;
                    }
                }
                GUI.backgroundColor = Color.white;
                EditorGUILayout.EndHorizontal();
            }

            if (삭제요청 >= 0)
            {
                string name = fx.표정목록[삭제요청]?.이름 ?? $"[{삭제요청}]";

                // 리스트를 바꾸기 전에 기록해야 Undo가 성립한다.
                Undo.RecordObject(fx, $"Delete expression '{name}'");
                fx.RemoveExpression(삭제요청);
                EditorUtility.SetDirty(fx);

                Debug.Log($"[FacialExpression] '{name}' 삭제. Ctrl+Z로 되돌릴 수 있다.", fx);
                GUIUtility.ExitGUI();   // 이번 프레임 레이아웃을 여기서 끊는다
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("부가 효과 모두 끄기"))
            {
                Undo.RecordObject(fx, "Clear extras");
                fx.SetExtras(0f, 0f, 0f, 0f);
                EditorUtility.SetDirty(fx);
            }
            if (GUILayout.Button("빠진 기본 표정 추가"))
            {
                Undo.RecordObject(fx, "Add missing expressions");
                if (fx.AddMissingDefaults() == 0)
                    Debug.Log("[FacialExpression] 추가할 표정이 없다. 이미 전부 있다.", fx);
                EditorUtility.SetDirty(fx);
            }
            EditorGUILayout.EndHorizontal();
        }

        // ── 믹서 ────────────────────────────────────────────────────
        private void DrawMixer(FacialExpression fx)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUI.BeginChangeCheck();
            bool on = EditorGUILayout.ToggleLeft("믹서 모드 — 슬라이더로 직접 만들기", fx.믹서모드,
                                                 EditorStyles.boldLabel);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(fx, "Toggle mixer");
                fx.믹서모드 = on;
                EditorUtility.SetDirty(fx);
            }

            if (!fx.믹서모드)
            {
                EditorGUILayout.LabelField("켜면 눈·눈썹·입 슬라이더가 나타납니다.",
                                           EditorStyles.miniLabel);
                EditorGUILayout.EndVertical();
                return;
            }

            SkinnedMeshRenderer smr = fx.TargetRenderer;
            if (smr == null || smr.sharedMesh == null)
            {
                EditorGUILayout.HelpBox("블렌드셰이프를 가진 렌더러를 찾지 못했습니다.",
                                        MessageType.Warning);
                EditorGUILayout.EndVertical();
                return;
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("현재 표정 불러오기"))
            {
                Undo.RecordObject(fx, "Load mixer");
                fx.LoadMixerFromCurrent();
                EditorUtility.SetDirty(fx);
            }
            if (GUILayout.Button("모두 0으로"))
            {
                Undo.RecordObject(fx, "Clear mixer");
                fx.ClearMixer();
                EditorUtility.SetDirty(fx);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);
            DrawGroup(fx, smr.sharedMesh, FacialExpression.MorphGroup.눈썹);
            DrawGroup(fx, smr.sharedMesh, FacialExpression.MorphGroup.눈);
            DrawGroup(fx, smr.sharedMesh, FacialExpression.MorphGroup.입);
            DrawGroup(fx, smr.sharedMesh, FacialExpression.MorphGroup.기타);

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("표정으로 저장", EditorStyles.boldLabel);
            저장이름 = EditorGUILayout.TextField("이름", 저장이름);
            저장_전환시간 = EditorGUILayout.Slider("전환시간", 저장_전환시간, 0f, 1f);

            GUI.backgroundColor = new Color(0.7f, 1f, 0.7f);
            if (GUILayout.Button("이 조합을 표정으로 저장", GUILayout.Height(26)))
            {
                if (string.IsNullOrWhiteSpace(저장이름))
                {
                    Debug.LogWarning("[FacialExpression] 표정 이름을 입력하세요.", fx);
                }
                else
                {
                    Undo.RecordObject(fx, "Save expression");
                    int idx = fx.SaveMixerAsExpression(저장이름.Trim(), 저장_전환시간);
                    fx.믹서모드 = false;          // 저장했으면 프리셋으로 확인하게 넘긴다
                    fx.Play(idx);
                    EditorUtility.SetDirty(fx);
                    Debug.Log($"[FacialExpression] '{저장이름}' 저장 완료 (인덱스 {idx}).", fx);
                }
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.LabelField("같은 이름이 있으면 덮어씁니다. 0인 모프는 저장되지 않습니다.",
                                       EditorStyles.miniLabel);

            EditorGUILayout.EndVertical();
        }

        private void DrawGroup(FacialExpression fx, Mesh mesh, FacialExpression.MorphGroup group)
        {
            // 이 그룹에 해당하는 모프를 먼저 모은다. 없으면 헤더도 그리지 않는다.
            var names = new List<string>();
            for (int i = 0; i < mesh.blendShapeCount; i++)
            {
                string n = mesh.GetBlendShapeName(i);
                if (FacialExpression.GroupOf(n) == group) names.Add(n);
            }
            if (names.Count == 0) return;

            Folded[group] = EditorGUILayout.Foldout(Folded[group], $"{group}  ({names.Count})", true);
            if (!Folded[group]) return;

            EditorGUI.indentLevel++;
            foreach (string n in names)
            {
                float cur = fx.GetMixer(n);

                EditorGUI.BeginChangeCheck();
                float next = EditorGUILayout.Slider(n, cur, 0f, 100f);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(fx, "Mix morph");
                    fx.SetMixer(n, next);
                    EditorUtility.SetDirty(fx);
                }
            }
            EditorGUI.indentLevel--;
        }
    }
}
