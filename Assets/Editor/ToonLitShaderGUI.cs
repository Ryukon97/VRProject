using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace VRProject.EditorTools
{
    /// <summary>
    /// VRProject/ToonLit 머티리얼 인스펙터.
    ///
    /// 프로퍼티가 41개라 기본 인스펙터로는 훑기 어렵다. 섹션으로 접고,
    /// 꺼져 있는 기능의 하위 항목은 숨기고, 토글과 셰이더 키워드를 동기화한다.
    ///
    /// 키워드 동기화가 중요한 이유: _AlphaClip 같은 float만 바꾸고
    /// _ALPHATEST_ON 키워드를 안 켜면 값은 1인데 알파 클립이 동작하지 않는다.
    /// 머티리얼 인스펙터에서 직접 만질 때 이 함정에 빠지기 쉽다.
    /// </summary>
    public class ToonLitShaderGUI : ShaderGUI
    {
        private MaterialProperty[] props;
        private MaterialEditor editor;

        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            editor = materialEditor;
            props = properties;

            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField("VRProject · ToonLit", EditorStyles.boldLabel);

            Section("base", "기본", () =>
            {
                Show("_BaseMap");
                Show("_BaseColor");
                Show("_AlphaClip");
                if (On("_AlphaClip")) Show("_Cutoff");
            });

            Section("ramp", "툰 램프", () =>
            {
                Show("_ShadowThreshold");
                Show("_ShadowFeather");
                Show("_ShadowTint");
                Show("_Flatten");
                Show("_ReceiveShadowStrength");
                Hint("그림자에 검정을 곱하지 않고 채도를 유지한 색을 곱하는 것이 핵심이다.\n" +
                     "Flatten은 얼굴에서 높게(0.8), 의상에서 낮게(0.2) 쓴다.");
            });

            Section("second", "3단 명암", () =>
            {
                Show("_SecondStep");
                if (On("_SecondStep"))
                {
                    Show("_ShadowThreshold2");
                    Show("_ShadowTint2");
                    Hint("두 번째 경계는 첫 번째보다 낮아야 의미가 있다.\n" +
                         "머리카락·깃털처럼 굴곡이 많은 곳에서 효과가 크다.");
                }
            });

            Section("charlight", "캐릭터 전용 광원", () =>
            {
                Show("_CharacterLight");
                if (On("_CharacterLight"))
                {
                    Show("_CharacterLightDir");
                    Show("_CharacterLightColor");
                    Hint("씬의 태양광 대신 이 방향으로 캐릭터를 칠한다.\n" +
                         "CharacterToonSettings가 붙어 있으면 방향과 색을 매 프레임 덮어쓴다.");
                }
            });

            Section("ambient", "환경광", () =>
            {
                Show("_AmbientStrength");
                Show("_AmbientFlatten");
                Show("_EnvironmentInfluence");
                Hint("리얼한 느낌의 상당 부분이 환경광에서 온다. 툰으로 갈수록 낮춘다.");
            });

            Section("rimspec", "림 · 스페큘러", () =>
            {
                Show("_RimColor");
                Show("_RimPower");
                Show("_RimIntensity");
                Show("_RimLightAlign");
                EditorGUILayout.Space(4);
                Show("_SpecColor2");
                Show("_SpecPower");
                Show("_SpecIntensity");
                Show("_SpecFeather");
            });

            Section("emission", "이미션", () =>
            {
                Show("_EmissionColor");
                Show("_EmissionMap");
            });

            Section("outline", "아웃라인", () =>
            {
                Show("_OutlineEnabled");
                if (On("_OutlineEnabled"))
                {
                    Show("_OutlineColor");
                    Show("_OutlineTintByAlbedo");
                    Show("_OutlineWidth");
                    Show("_OutlineMaxWidth");
                    Show("_OutlineZOffset");
                    Hint("폭은 화면 기준으로 일정하게 유지된다. VR에서 가까이 가도 굵어지지 않는다.\n" +
                         "드로우콜이 2배가 되므로 배경에는 끄는 편이 좋다.");
                }
            });

            Section("depth", "깊이 오프셋", () =>
            {
                Show("_ZOffset");
                Hint("깊이만 카메라 쪽으로 당긴다. 실제 위치는 그대로다.\n" +
                     "얼굴 위에 얹힌 표정 파츠의 Z-파이팅을 막는 용도인데,\n" +
                     "투명 데칼 모드에서는 깊이 경쟁 자체가 없어 보통 0으로 둔다.");
            });

            Section("surface", "서피스 · 렌더링", () =>
            {
                EditorGUI.BeginChangeCheck();
                Show("_Surface");
                if (EditorGUI.EndChangeCheck()) ApplySurfaceMode();

                using (new EditorGUI.DisabledScope(true))
                {
                    Show("_SrcBlend");
                    Show("_DstBlend");
                }

                Show("_Cull");
                Show("_ZWrite");

                Hint("Transparent Decal로 바꾸면 블렌드·ZWrite·렌더 큐가 자동으로 맞춰진다.\n" +
                     "얼굴 데칼(눈·눈썹·볼)은 이 모드여야 Z-파이팅이 없다.");
            });

            EditorGUILayout.Space(6);
            editor.RenderQueueField();
            editor.EnableInstancingField();
            editor.DoubleSidedGIField();

            SyncKeywords();
        }

        /// <summary>새 셰이더로 갈아끼울 때도 키워드가 맞도록 한다.</summary>
        public override void AssignNewShaderToMaterial(Material material, Shader oldShader, Shader newShader)
        {
            base.AssignNewShaderToMaterial(material, oldShader, newShader);
            if (material == null) return;
            SyncKeywords(material);
            ApplySurfaceMode(material);
        }

        // ── UI 헬퍼 ─────────────────────────────────────────────────

        private void Section(string key, string title, Action body)
        {
            EditorGUILayout.Space(2);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            string prefKey = "VRProject.ToonLitGUI." + key;
            bool open = EditorPrefs.GetBool(prefKey, true);
            bool now = EditorGUILayout.Foldout(open, title, true, EditorStyles.foldoutHeader);
            if (now != open) EditorPrefs.SetBool(prefKey, now);

            if (now)
            {
                EditorGUI.indentLevel++;
                body();
                EditorGUI.indentLevel--;
                EditorGUILayout.Space(2);
            }

            EditorGUILayout.EndVertical();
        }

        private static void Hint(string text)
        {
            EditorGUILayout.LabelField(text, EditorStyles.wordWrappedMiniLabel);
        }

        private MaterialProperty P(string name) => FindProperty(name, props, false);

        private void Show(string name)
        {
            MaterialProperty p = P(name);
            if (p != null) editor.ShaderProperty(p, p.displayName);
        }

        private bool On(string name)
        {
            MaterialProperty p = P(name);
            return p != null && p.floatValue > 0.5f;
        }

        // ── 상태 동기화 ─────────────────────────────────────────────

        private void SyncKeywords()
        {
            foreach (UnityEngine.Object o in editor.targets)
                if (o is Material m) SyncKeywords(m);
        }

        /// <summary>
        /// 토글 float과 셰이더 키워드를 맞춘다.
        /// 값만 바꾸고 키워드를 안 켜면 기능이 조용히 동작하지 않는다.
        /// </summary>
        private static void SyncKeywords(Material m)
        {
            SetKeyword(m, "_ALPHATEST_ON", Flag(m, "_AlphaClip"));
            SetKeyword(m, "_SECONDSTEP_ON", Flag(m, "_SecondStep"));
            SetKeyword(m, "_CHARACTERLIGHT_ON", Flag(m, "_CharacterLight"));
            SetKeyword(m, "_OUTLINE_ON", Flag(m, "_OutlineEnabled"));
        }

        private void ApplySurfaceMode()
        {
            foreach (UnityEngine.Object o in editor.targets)
                if (o is Material m) ApplySurfaceMode(m);
        }

        /// <summary>
        /// 서피스 모드에 맞춰 블렌드·ZWrite·렌더 큐를 한 번에 맞춘다.
        /// 셋을 따로 만지게 두면 조합이 어긋난 머티리얼이 생긴다.
        /// </summary>
        private static void ApplySurfaceMode(Material m)
        {
            if (!m.HasProperty("_Surface")) return;
            bool transparent = m.GetFloat("_Surface") > 0.5f;

            if (transparent)
            {
                m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_ZWrite", 0f);
                m.renderQueue = (int)RenderQueue.Transparent;

                // 데칼은 그림자를 드리우지 않는다. 얼굴에 눈 모양 그림자가 생긴다.
                m.SetShaderPassEnabled("ShadowCaster", false);
            }
            else
            {
                m.SetFloat("_SrcBlend", (float)BlendMode.One);
                m.SetFloat("_DstBlend", (float)BlendMode.Zero);
                m.SetFloat("_ZWrite", 1f);
                m.renderQueue = -1;
                m.SetShaderPassEnabled("ShadowCaster", true);
            }
        }

        private static bool Flag(Material m, string name) =>
            m.HasProperty(name) && m.GetFloat(name) > 0.5f;

        private static void SetKeyword(Material m, string kw, bool on)
        {
            if (on) m.EnableKeyword(kw);
            else m.DisableKeyword(kw);
        }
    }
}
