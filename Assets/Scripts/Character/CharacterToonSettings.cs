using System;
using UnityEngine;

namespace VRProject.Character
{
    /// <summary>
    /// 캐릭터(Aru_Real2)의 툰 셰이딩과 전용 광원을 인스펙터에서 조절한다.
    ///
    /// 값을 바꾸면 즉시 반영된다(플레이 중이 아니어도).
    /// 머티리얼은 텍스처 이름으로 부위를 판별해 각 그룹의 값을 적용한다.
    ///
    /// 캐릭터 전용 광원이 이 컴포넌트의 핵심이다. 씬의 태양광 대신 캐릭터만의
    /// 고정 광원을 쓰기 때문에, 캐릭터가 어디에 서 있든 해가 어느 방향이든
    /// 얼굴 조명이 흔들리지 않는다. 배경은 실제 태양광을 그대로 받는다.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("VRProject/Character Toon Settings")]
    public class CharacterToonSettings : MonoBehaviour
    {
        private static readonly int LightDirID = Shader.PropertyToID("_CharacterLightDir");
        private static readonly int LightColorID = Shader.PropertyToID("_CharacterLightColor");

        /// <summary>부위 하나의 셰이딩 값.</summary>
        [Serializable]
        public class PartShading
        {
            [Tooltip("이 부위에 값을 적용할지. 끄면 머티리얼의 현재 값을 그대로 둔다.")]
            public bool 적용 = true;

            [Tooltip("명암을 평평하게 만드는 정도. 얼굴은 높게(0.8), 의상은 낮게(0.2).\n" +
                     "코 옆 그림자가 생기면 즉시 사실적으로 보인다.")]
            [Range(0f, 1f)] public float 명암평탄화 = 0.2f;

            [Tooltip("밝은 면과 그림자 면이 갈리는 지점. 0.5가 정확히 명암 경계.")]
            [Range(0f, 1f)] public float 그림자_임계값 = 0.5f;

            [Tooltip("경계가 넘어가는 부드러움. 낮을수록 딱딱한 툰.")]
            [Range(0.001f, 0.4f)] public float 그림자_부드러움 = 0.05f;

            [Tooltip("그림자 색. 검정을 곱하지 않고 채도를 유지한 색을 곱하는 게 핵심.\n" +
                     "피부는 분홍/적갈, 의상·머리카락은 청보라.")]
            public Color 그림자색 = new Color(0.71f, 0.70f, 0.87f);

            [Tooltip("하이라이트 세기. 머리카락만 높이고 나머지는 억제한다.")]
            [Range(0f, 1f)] public float 하이라이트 = 0.05f;

            [Tooltip("실루엣 가장자리 빛. 좁고 약하게.")]
            [Range(0f, 1f)] public float 림라이트 = 0.16f;

            [Tooltip("아웃라인 두께(화면 픽셀 기준). 0이면 아웃라인 없음.")]
            [Range(0f, 8f)] public float 아웃라인두께 = 1.4f;

            [Tooltip("환경광(스카이박스/SH)이 미치는 영향.\n" +
                     "리얼한 느낌의 상당 부분이 여기서 온다. 툰으로 갈수록 낮춘다.")]
            [Range(0f, 1f)] public float 환경광영향 = 0.2f;

            [Tooltip("씬의 드리운 그림자를 받는 정도. 낮추면 그림자가 옅어진다.")]
            [Range(0f, 1f)] public float 그림자받기 = 0.5f;
        }

        // ────────────────────────────────────────────────────────────
        [Header("부위별 셰이딩")]
        [Tooltip("얼굴 피부. Face_D 텍스처를 쓰는 머티리얼 (Face_Mouth)")]
        public PartShading 얼굴 = new PartShading
        {
            명암평탄화 = 0.80f, 그림자_임계값 = 0.52f, 그림자_부드러움 = 0.10f,
            그림자색 = new Color(0.93f, 0.84f, 0.85f),
            하이라이트 = 0.02f, 림라이트 = 0.10f, 아웃라인두께 = 0.9f,
            환경광영향 = 0.15f, 그림자받기 = 0.35f,
        };

        [Tooltip("눈·눈썹·입 파츠. Morph_parts_D 텍스처를 쓰는 머티리얼 (Morph_parts)\n\n" +
                 "눈은 조명을 받으면 안 된다. 어두워지는 순간 캐릭터가 죽은 눈이 된다.\n" +
                 "명암평탄화 1.0 + 환경광영향 0 + 그림자받기 0이면 텍스처 색이 그대로 나온다.\n" +
                 "아웃라인은 0을 권장 — 눈 텍스처에 이미 선이 그려져 있고,\n" +
                 "얼굴 위에 얹힌 평면 지오메트리라 인버티드 헐이 지저분해진다.")]
        public PartShading 눈 = new PartShading
        {
            명암평탄화 = 1.0f,          // 완전 평탄 = 사실상 무조명
            그림자_임계값 = 0.5f,
            그림자_부드러움 = 0.05f,
            그림자색 = new Color(1f, 1f, 1f),   // 그림자에서도 색이 죽지 않게
            하이라이트 = 0f,
            림라이트 = 0f,
            아웃라인두께 = 0f,
            환경광영향 = 0f,
            그림자받기 = 0f,
        };

        [Tooltip("Hair_D 텍스처를 쓰는 머티리얼 + Feather")]
        public PartShading 머리카락 = new PartShading
        {
            명암평탄화 = 0.28f, 그림자_임계값 = 0.50f, 그림자_부드러움 = 0.04f,
            그림자색 = new Color(0.75f, 0.72f, 0.88f),
            하이라이트 = 0.22f, 림라이트 = 0.22f, 아웃라인두께 = 1.3f,
            환경광영향 = 0.20f, 그림자받기 = 0.5f,
        };

        [Tooltip("Body_D / Coat_D 텍스처 + Hand, High_heels")]
        public PartShading 의상_피부 = new PartShading
        {
            명암평탄화 = 0.18f, 그림자_임계값 = 0.50f, 그림자_부드러움 = 0.05f,
            그림자색 = new Color(0.71f, 0.70f, 0.87f),
            하이라이트 = 0.05f, 림라이트 = 0.16f, 아웃라인두께 = 1.4f,
            환경광영향 = 0.20f, 그림자받기 = 0.5f,
        };

        [Tooltip("Halo, Horn_Meltal 등 금속·장식")]
        public PartShading 금속_장식 = new PartShading
        {
            명암평탄화 = 0.10f, 그림자_임계값 = 0.48f, 그림자_부드러움 = 0.03f,
            그림자색 = new Color(0.62f, 0.63f, 0.80f),
            하이라이트 = 0.45f, 림라이트 = 0.30f, 아웃라인두께 = 1.0f,
            환경광영향 = 0.35f, 그림자받기 = 0.5f,
        };

        // ────────────────────────────────────────────────────────────
        [Header("캐릭터 전용 광원")]
        [Tooltip("켜면 씬의 디렉셔널 라이트 대신 아래 광원으로 캐릭터를 칠한다.\n" +
                 "한낮 야외에서도 툰으로 보이게 하는 핵심 장치.")]
        public bool 전용광원사용 = true;

        [Tooltip("좌우 각도. 음수면 화면 왼쪽에서 빛이 온다.")]
        [Range(-180f, 180f)] public float 광원_좌우 = -32f;

        [Tooltip("위아래 각도. 양수면 위에서 내려온다.")]
        [Range(-89f, 89f)] public float 광원_상하 = 38f;

        [Tooltip("1이면 광원이 카메라를 따라 돌아 얼굴이 항상 밝다. 0이면 월드 고정.\n" +
                 "완전히 따라가면 입체감이 사라져 스티커처럼 보인다. 0.6~0.8 권장.")]
        [Range(0f, 1f)] public float 카메라추종 = 0.7f;

        [Tooltip("따라가는 속도. 낮으면 조명이 부드럽게 미끄러진다.")]
        [Range(0.5f, 30f)] public float 추종속도 = 6f;

        [Tooltip("캐릭터에 닿는 빛의 색. 배경 태양광과 달라도 된다 — 오히려 그게 목적이다.")]
        public Color 광원색 = new Color(1f, 0.97f, 0.93f);

        [Tooltip("세기. 1을 넘기면 밝은 면이 날아가기 시작한다.")]
        [Range(0f, 2f)] public float 광원세기 = 1f;

        // ────────────────────────────────────────────────────────────
        [Header("대상")]
        [Tooltip("비워두면 자식의 모든 렌더러를 쓴다.")]
        public Renderer[] 렌더러;

        [Tooltip("기준 카메라. 비워두면 Camera.main (XR Origin의 Main Camera).")]
        public Transform 기준카메라;

        [Tooltip("URP Lit 머티리얼을 만나면 자동으로 ToonLit으로 바꾼다.")]
        public bool 셰이더_자동교체 = true;

        private MaterialPropertyBlock block;
        private Vector3 smoothedDir;
        private bool primed;

        private void OnEnable()
        {
            CollectRenderers();
            primed = false;
            ApplyShading();
            ApplyLight();
        }

        private void LateUpdate() => ApplyLight();

        private void OnValidate()
        {
            CollectRenderers();
            primed = false;
            ApplyShading();
            ApplyLight();
        }

        private void CollectRenderers()
        {
            if (렌더러 == null || 렌더러.Length == 0)
                렌더러 = GetComponentsInChildren<Renderer>(true);
        }

        // ── 셰이딩 적용 ─────────────────────────────────────────────

        /// <summary>인스펙터 값을 머티리얼에 쓴다. 값이 바뀔 때만 호출된다.</summary>
        [ContextMenu("셰이딩 다시 적용")]
        public void ApplyShading()
        {
            if (렌더러 == null) return;
            Shader toon = Shader.Find("VRProject/ToonLit");

            foreach (Renderer r in 렌더러)
            {
                if (r == null) continue;

                foreach (Material m in r.sharedMaterials)
                {
                    if (m == null) continue;

                    if (셰이더_자동교체 && toon != null && m.shader != toon)
                        SwitchToToon(m, toon);

                    if (m.shader == null || m.shader.name != "VRProject/ToonLit") continue;

                    PartShading p = PickPart(m);
                    if (p == null || !p.적용) continue;

                    Write(m, p);
                }

                // 환경 반사가 툰 표면에 잡히면 즉시 리얼해진다.
                r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            }
        }

        private static void SwitchToToon(Material m, Shader toon)
        {
            Texture baseMap = m.HasProperty("_BaseMap") ? m.GetTexture("_BaseMap")
                            : (m.HasProperty("_MainTex") ? m.GetTexture("_MainTex") : null);
            Color baseColor = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor")
                            : (m.HasProperty("_Color") ? m.GetColor("_Color") : Color.white);

            m.shader = toon;
            if (baseMap != null) m.SetTexture("_BaseMap", baseMap);
            m.SetColor("_BaseColor", baseColor);
        }

        private void Write(Material m, PartShading p)
        {
            m.SetFloat("_Flatten", p.명암평탄화);
            m.SetFloat("_ShadowThreshold", p.그림자_임계값);
            m.SetFloat("_ShadowFeather", p.그림자_부드러움);
            m.SetColor("_ShadowTint", p.그림자색);
            m.SetFloat("_SpecIntensity", p.하이라이트);
            m.SetFloat("_RimIntensity", p.림라이트);
            m.SetFloat("_OutlineWidth", p.아웃라인두께);
            m.SetFloat("_EnvironmentInfluence", p.환경광영향);
            m.SetFloat("_ReceiveShadowStrength", p.그림자받기);

            bool outline = p.아웃라인두께 > 0f;
            m.SetFloat("_OutlineEnabled", outline ? 1f : 0f);
            SetKeyword(m, "_OUTLINE_ON", outline);

            m.SetFloat("_CharacterLight", 전용광원사용 ? 1f : 0f);
            SetKeyword(m, "_CHARACTERLIGHT_ON", 전용광원사용);
        }

        private static void SetKeyword(Material m, string kw, bool on)
        {
            if (on) m.EnableKeyword(kw);
            else m.DisableKeyword(kw);
        }

        /// <summary>
        /// 부위 판별. Aru_Real2는 머티리얼 이름이 부위와 안 맞아서
        /// (Face_Mouth, Horn_Meltal, High_heels…) 텍스처 이름을 1차 기준으로 쓴다.
        /// </summary>
        private PartShading PickPart(Material m)
        {
            Texture tex = m.HasProperty("_BaseMap") ? m.GetTexture("_BaseMap") : null;
            if (tex != null)
            {
                PartShading byTex = Classify(tex.name);
                if (byTex != null) return byTex;
            }
            return Classify(m.name) ?? 의상_피부;
        }

        private PartShading Classify(string raw)
        {
            string n = raw.ToLowerInvariant();

            // 눈을 얼굴보다 먼저 본다. Morph_parts가 눈·눈썹·입 파츠다.
            if (n.Contains("morph") || n.Contains("eye") || n.Contains("iris") ||
                n.Contains("pupil") || n.Contains("hitomi") || n.Contains("brow") ||
                n.Contains("lash") || n.Contains("目"))
                return 눈;

            // Face_Mouth는 얼굴 피부다. 위에서 눈이 걸러진 뒤에 잡힌다.
            if (n.Contains("face") || n.Contains("mouth") || n.Contains("顔"))
                return 얼굴;

            if (n.Contains("hair") || n.Contains("feather"))
                return 머리카락;

            if (n.Contains("metal") || n.Contains("meltal") || n.Contains("halo") ||
                n.Contains("horn") || n.Contains("weapon"))
                return 금속_장식;

            if (n.Contains("body") || n.Contains("coat") || n.Contains("caot") ||
                n.Contains("cloth") || n.Contains("skin") || n.Contains("hand") ||
                n.Contains("heel") || n.Contains("shoe"))
                return 의상_피부;

            return null;
        }

        // ── 광원 방향 ───────────────────────────────────────────────

        private void ApplyLight()
        {
            if (렌더러 == null || 렌더러.Length == 0) return;
            block ??= new MaterialPropertyBlock();

            Vector3 dir = ComputeDirection();
            Color col = 광원색 * 광원세기;

            foreach (Renderer r in 렌더러)
            {
                if (r == null) continue;
                r.GetPropertyBlock(block);
                block.SetVector(LightDirID, new Vector4(dir.x, dir.y, dir.z, 0f));
                block.SetColor(LightColorID, col);
                r.SetPropertyBlock(block);
            }
        }

        private Vector3 ComputeDirection()
        {
            Quaternion local = Quaternion.Euler(광원_상하, 광원_좌우, 0f);
            Vector3 worldFixed = -((transform.rotation * local) * Vector3.forward);

            Transform cam = 기준카메라 != null ? 기준카메라
                          : (Camera.main != null ? Camera.main.transform : null);

            Vector3 target = worldFixed;

            if (cam != null && 카메라추종 > 0f)
            {
                // 카메라의 좌우 회전만 쓴다. 상하까지 따라가면 고개를 숙일 때마다
                // 캐릭터 조명이 같이 흔들려 어지럽다.
                Quaternion camYaw = Quaternion.Euler(0f, cam.eulerAngles.y, 0f);
                Vector3 fromCamera = -((camYaw * local) * Vector3.forward);
                target = Vector3.Slerp(worldFixed, fromCamera, 카메라추종).normalized;
            }

            if (!primed)
            {
                smoothedDir = target;
                primed = true;
            }
            else if (Application.isPlaying && 추종속도 > 0f)
            {
                float t = 1f - Mathf.Exp(-추종속도 * Time.deltaTime);
                smoothedDir = Vector3.Slerp(smoothedDir, target, t).normalized;
            }
            else
            {
                smoothedDir = target;
            }

            return smoothedDir;
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 origin = transform.position + Vector3.up * 1.4f;
            Vector3 dir = ComputeDirection();

            Gizmos.color = 광원색;
            Gizmos.DrawLine(origin, origin + dir * 0.8f);
            Gizmos.DrawWireSphere(origin + dir * 0.8f, 0.08f);
        }
    }
}
