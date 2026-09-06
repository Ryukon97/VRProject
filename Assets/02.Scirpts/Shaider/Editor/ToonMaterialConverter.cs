using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace VRProject.EditorTools
{
    /// <summary>
    /// 선택한 오브젝트/머티리얼의 셰이더를 VRProject/ToonLit으로 바꾸고
    /// 부위별 프리셋을 넣어준다.
    ///
    /// 머티리얼 이름으로 부위를 추정한다(face_d, Hair_d, Body_d 등 MMD 변환 관례).
    /// 추정이 틀리면 인스펙터에서 _Flatten만 조절하면 된다.
    ///
    /// 메뉴: Tools ▸ Toon ▸ Convert Selection to ToonLit
    /// </summary>
    public static class ToonMaterialConverter
    {
        private const string ToonShaderName = "VRProject/ToonLit";

        // 직전에 적용된 프리셋 이름. 로그 출력용.
        private static string lastPresetLabel = "";

        // 부위별 시작값. 원본 영상 분석의 "얼굴은 명암 경계가 거의 없고,
        // 머리카락은 큰 덩어리 명암, 의상은 청보라 그림자"에 대응한다.
        private struct Preset
        {
            public string Label;
            public float Flatten;
            public float ShadowThreshold;
            public float ShadowFeather;
            public Color ShadowTint;
            public float SpecIntensity;
            public float RimIntensity;
            public float OutlineWidth;

            /// <summary>환경광(SH)이 미치는 영향. 낮출수록 툰에 가깝고, 높을수록 리얼해진다.</summary>
            public float EnvironmentInfluence;

            /// <summary>씬 조명 대신 캐릭터 전용 광원을 쓸지. 캐릭터는 켜고 배경은 끈다.</summary>
            public bool UseCharacterLight;
        }

        private static readonly Preset Face = new Preset
        {
            Label = "얼굴",
            // 얼굴은 코 옆 그림자가 생기면 즉시 사실적으로 보인다. 거의 평평하게 만든다.
            Flatten = 0.80f,
            ShadowThreshold = 0.52f,
            ShadowFeather = 0.10f,
            ShadowTint = new Color(0.93f, 0.84f, 0.85f), // 분홍/적갈 계열
            SpecIntensity = 0.02f,
            RimIntensity = 0.10f,
            OutlineWidth = 0.9f,
            EnvironmentInfluence = 0.15f,
            UseCharacterLight = true,
        };

        private static readonly Preset Hair = new Preset
        {
            Label = "머리카락",
            Flatten = 0.28f,
            ShadowThreshold = 0.50f,
            ShadowFeather = 0.04f,
            ShadowTint = new Color(0.75f, 0.72f, 0.88f),
            SpecIntensity = 0.22f,   // 머리카락만 하이라이트 밴드를 살린다
            RimIntensity = 0.22f,
            OutlineWidth = 1.3f,
            EnvironmentInfluence = 0.2f,
            UseCharacterLight = true,
        };

        private static readonly Preset Cloth = new Preset
        {
            Label = "의상/피부",
            Flatten = 0.18f,
            ShadowThreshold = 0.50f,
            ShadowFeather = 0.05f,
            ShadowTint = new Color(0.71f, 0.70f, 0.87f), // 청보라 계열
            SpecIntensity = 0.05f,
            RimIntensity = 0.16f,
            OutlineWidth = 1.4f,
            EnvironmentInfluence = 0.2f,
            UseCharacterLight = true,
        };

        private static readonly Preset Metal = new Preset
        {
            Label = "금속/장식",
            Flatten = 0.10f,
            ShadowThreshold = 0.48f,
            ShadowFeather = 0.03f,
            ShadowTint = new Color(0.62f, 0.63f, 0.80f),
            SpecIntensity = 0.45f,   // 장식은 하이라이트를 살려야 눈에 띈다
            RimIntensity = 0.30f,
            OutlineWidth = 1.0f,
            EnvironmentInfluence = 0.35f,
            UseCharacterLight = true,
        };

        private static readonly Preset Environment = new Preset
        {
            Label = "배경",
            // 배경은 캐릭터보다 단계를 덜 나눠야 캐릭터가 앞으로 나온다.
            // 참고 이미지의 도시도 건물에 진한 그림자가 거의 없다.
            Flatten = 0.45f,
            ShadowThreshold = 0.46f,
            ShadowFeather = 0.14f,
            ShadowTint = new Color(0.66f, 0.71f, 0.86f),
            SpecIntensity = 0.03f,
            RimIntensity = 0.04f,
            OutlineWidth = 0f,       // 배경 아웃라인은 끈다. 드로우콜이 2배가 된다.
            EnvironmentInfluence = 0.6f,
            UseCharacterLight = false,   // 배경은 실제 태양광을 그대로 받는다
        };

        [MenuItem("Tools/Toon/Convert Selection to ToonLit")]
        private static void ConvertSelection()
        {
            Shader toon = Shader.Find(ToonShaderName);
            if (toon == null)
            {
                EditorUtility.DisplayDialog("ToonLit 없음",
                    $"'{ToonShaderName}' 셰이더를 찾지 못했습니다.\n" +
                    "Assets/Shaders/ToonLit.shader가 컴파일됐는지 확인하세요.", "확인");
                return;
            }

            var materials = CollectMaterials();
            if (materials.Count == 0)
            {
                EditorUtility.DisplayDialog("선택 없음",
                    "GameObject 또는 Material을 선택한 뒤 다시 실행하세요.", "확인");
                return;
            }

            int converted = 0;
            var log = new System.Text.StringBuilder();
            log.AppendLine("[ToonMaterialConverter] 부위 판별 결과 — 틀린 게 있으면 인스펙터에서 고치세요");

            foreach (Material mat in materials)
            {
                if (mat == null) continue;

                Undo.RecordObject(mat, "Convert to ToonLit");
                Convert(mat, toon);
                EditorUtility.SetDirty(mat);
                converted++;

                Texture t = GetTexture(mat, "_BaseMap", "_MainTex");
                log.AppendLine($"   {mat.name,-24} → {lastPresetLabel,-10} " +
                               $"(텍스처: {(t != null ? t.name : "없음")})");
            }

            AssetDatabase.SaveAssets();
            log.AppendLine($"\n총 {converted}개 변환 완료.");
            Debug.Log(log.ToString());
        }

        [MenuItem("Tools/Toon/Convert Selection to ToonLit", true)]
        private static bool ConvertSelectionValidate() => Selection.objects.Length > 0;

        private static List<Material> CollectMaterials()
        {
            var result = new List<Material>();
            var seen = new HashSet<Material>();

            foreach (Object obj in Selection.objects)
            {
                if (obj is Material m)
                {
                    if (seen.Add(m)) result.Add(m);
                    continue;
                }

                if (obj is GameObject go)
                {
                    foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                    {
                        foreach (Material sm in r.sharedMaterials)
                        {
                            if (sm != null && seen.Add(sm)) result.Add(sm);
                        }
                    }
                }
            }

            return result;
        }

        private static void Convert(Material mat, Shader toon)
        {
            // 기존 값을 먼저 빼둔다. 셰이더를 바꾸면 대응 안 되는 프로퍼티는 날아간다.
            Texture baseMap = GetTexture(mat, "_BaseMap", "_MainTex");
            Color baseColor = GetColor(mat, Color.white, "_BaseColor", "_Color");
            Vector2 tiling = mat.HasProperty("_BaseMap") ? mat.GetTextureScale("_BaseMap") : Vector2.one;
            Vector2 offset = mat.HasProperty("_BaseMap") ? mat.GetTextureOffset("_BaseMap") : Vector2.zero;
            float cutoff = mat.HasProperty("_Cutoff") ? mat.GetFloat("_Cutoff") : 0.5f;
            bool wasAlphaClip = mat.HasProperty("_AlphaClip") && mat.GetFloat("_AlphaClip") > 0.5f;
            Texture emissionMap = GetTexture(mat, "_EmissionMap");
            Color emissionColor = mat.HasProperty("_EmissionColor")
                ? mat.GetColor("_EmissionColor") : Color.black;

            mat.shader = toon;

            if (baseMap != null) mat.SetTexture("_BaseMap", baseMap);
            mat.SetColor("_BaseColor", baseColor);
            mat.SetTextureScale("_BaseMap", tiling);
            mat.SetTextureOffset("_BaseMap", offset);
            mat.SetFloat("_Cutoff", cutoff);

            if (emissionMap != null) mat.SetTexture("_EmissionMap", emissionMap);
            mat.SetColor("_EmissionColor", emissionColor);

            Preset p = PickPreset(mat);
            mat.SetFloat("_Flatten", p.Flatten);
            mat.SetFloat("_ShadowThreshold", p.ShadowThreshold);
            mat.SetFloat("_ShadowFeather", p.ShadowFeather);
            mat.SetColor("_ShadowTint", p.ShadowTint);
            mat.SetFloat("_SpecIntensity", p.SpecIntensity);
            mat.SetFloat("_RimIntensity", p.RimIntensity);
            mat.SetFloat("_OutlineWidth", p.OutlineWidth);

            // 환경광 차단과 캐릭터 전용 광원이 "리얼한 느낌"을 걷어내는 두 축이다.
            mat.SetFloat("_EnvironmentInfluence", p.EnvironmentInfluence);
            mat.SetFloat("_CharacterLight", p.UseCharacterLight ? 1f : 0f);
            SetKeyword(mat, "_CHARACTERLIGHT_ON", p.UseCharacterLight);

            lastPresetLabel = p.Label;

            bool outlineOn = p.OutlineWidth > 0f;
            mat.SetFloat("_OutlineEnabled", outlineOn ? 1f : 0f);
            SetKeyword(mat, "_OUTLINE_ON", outlineOn);

            // 알파 클립은 머리카락/눈썹 등 투명 부위에 필요하다.
            bool alphaClip = wasAlphaClip || LooksTransparent(mat.name);
            mat.SetFloat("_AlphaClip", alphaClip ? 1f : 0f);
            SetKeyword(mat, "_ALPHATEST_ON", alphaClip);

            // 얼굴은 양면으로 두면 속눈썹이 겹쳐 보인다. 기본은 뒷면 컬링.
            mat.SetFloat("_Cull", 2f);
            mat.SetFloat("_ZWrite", 1f);
        }

        /// <summary>
        /// 부위를 판별한다.
        ///
        /// Aru_Real2는 머티리얼 이름이 부위와 잘 안 맞는다(Face_Mouth, Horn_Meltal,
        /// High_heels, Morph_parts…). 반면 텍스처 이름은 Body_D / Coat_D / Face_D /
        /// Hair_D / Halo_D / Morph_parts_D로 규칙적이라 이쪽을 1차 기준으로 쓴다.
        /// </summary>
        private static Preset PickPreset(Material mat)
        {
            // 1차: 텍스처 이름
            Texture tex = GetTexture(mat, "_BaseMap", "_MainTex");
            if (tex != null)
            {
                Preset? byTexture = Classify(tex.name);
                if (byTexture.HasValue) return byTexture.Value;
            }

            // 2차: 머티리얼 이름
            Preset? byName = Classify(mat.name);
            if (byName.HasValue) return byName.Value;

            return Environment;
        }

        private static Preset? Classify(string raw)
        {
            string n = raw.ToLowerInvariant();

            // 얼굴 — Morph_parts는 눈/눈썹/입 등 표정 파츠라 얼굴로 묶는다.
            if (n.Contains("face") || n.Contains("morph") || n.Contains("eye") ||
                n.Contains("mouth") || n.Contains("顔") || n.Contains("目"))
                return Face;

            if (n.Contains("hair") || n.Contains("髪") || n.Contains("feather"))
                return Hair;

            // 금속/장식 — Horn_Meltal은 원본의 오타를 그대로 둔 것이므로 둘 다 잡는다.
            if (n.Contains("metal") || n.Contains("meltal") || n.Contains("halo") ||
                n.Contains("horn") || n.Contains("weapon"))
                return Metal;

            if (n.Contains("body") || n.Contains("coat") || n.Contains("caot") ||
                n.Contains("cloth") || n.Contains("skin") || n.Contains("hand") ||
                n.Contains("heel") || n.Contains("shoe") || n.Contains("spa"))
                return Cloth;

            return null;
        }

        private static bool LooksTransparent(string name)
        {
            string n = name.ToLowerInvariant();
            return n.Contains("hair") || n.Contains("eyelash") || n.Contains("morph") ||
                   n.Contains("halo") || n.Contains("spa");
        }

        private static void SetKeyword(Material mat, string keyword, bool enabled)
        {
            if (enabled) mat.EnableKeyword(keyword);
            else mat.DisableKeyword(keyword);
        }

        private static Texture GetTexture(Material mat, params string[] names)
        {
            foreach (string n in names)
                if (mat.HasProperty(n) && mat.GetTexture(n) != null)
                    return mat.GetTexture(n);
            return null;
        }

        private static Color GetColor(Material mat, Color fallback, params string[] names)
        {
            foreach (string n in names)
                if (mat.HasProperty(n))
                    return mat.GetColor(n);
            return fallback;
        }
    }
}
