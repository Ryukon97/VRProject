using System;
using System.Collections.Generic;
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

            [Tooltip("아웃라인 색.\n\n" +
                     "검정은 부위가 서로 다른 색일 때 이물감이 든다.\n" +
                     "베이스 색을 어둡게 하고 채도를 살린 색이 애니메이션 관례에 맞다.\n" +
                     "예: 분홍 머리 → 진한 자주, 흰 셔츠 → 진한 남보라")]
            public Color 아웃라인색 = new Color(0.16f, 0.17f, 0.32f);

            [Tooltip("아웃라인 색을 베이스 텍스처 쪽으로 당기는 정도.\n\n" +
                     "0이면 위 색이 그대로 나온다.\n" +
                     "올릴수록 텍스처 색에 물들어 부위마다 선 색이 자연스럽게 갈린다.\n" +
                     "한 머티리얼 안에 색이 여러 개일 때 특히 유용하다.")]
            [Range(0f, 1f)] public float 아웃라인_알베도혼합 = 0.35f;

            [Tooltip("환경광(스카이박스/SH)이 미치는 영향.\n" +
                     "리얼한 느낌의 상당 부분이 여기서 온다. 툰으로 갈수록 낮춘다.")]
            [Range(0f, 1f)] public float 환경광영향 = 0.2f;

            [Tooltip("씬의 드리운 그림자를 받는 정도. 낮추면 그림자가 옅어진다.")]
            [Range(0f, 1f)] public float 그림자받기 = 0.5f;

            [Tooltip("데칼 불투명도. '표정_투명데칼'이 켜진 머티리얼에서만 효과가 있다.\n\n" +
                     "얼굴그늘(顔かげ)·눈물·세로줄이 너무 진하면 여기를 내린다.\n" +
                     "단, 같은 머티리얼을 쓰는 파츠가 함께 흐려진다.")]
            [Range(0f, 1f)] public float 불투명도 = 1f;

            [Space(6)]
            [Header("그림자 단계 나누기")]
            [Tooltip("명암을 2단이 아니라 3단으로 나눈다.\n\n" +
                     "밝은 면 / 그림자 / 더 깊은 그림자로 갈라져서\n" +
                     "머리카락·깃털처럼 굴곡이 많은 곳의 면이 또렷하게 나뉜다.\n" +
                     "얼굴은 보통 끄는 편이 낫다.")]
            public bool 삼단명암 = false;

            [Tooltip("두 번째 경계 위치. '그림자_임계값'보다 낮아야 의미가 있다.\n" +
                     "이 값 아래로 어두운 부분이 '깊은 그림자색'으로 한 번 더 눌린다.")]
            [Range(0f, 1f)] public float 깊은그림자_임계값 = 0.28f;

            [Tooltip("가장 어두운 단계의 색. 그림자색보다 조금 어둡고 채도가 있어야\n" +
                     "단계가 살아난다. 검정에 가까워지면 툰 느낌이 죽는다.")]
            public Color 깊은그림자색 = new Color(0.55f, 0.53f, 0.72f);
        }

        /// <summary>
        /// 얼굴 영역 전용. 얼굴 피부와 표정 파츠(눈·눈썹·입)를 한 그룹으로 묶되,
        /// 둘 사이의 겹침만 따로 조절한다.
        ///
        /// 셰이딩 값은 공유하고, 아래 세 항목만 두 레이어에 다르게 적용된다.
        /// 그래서 표정이 겹칠 때 한 곳만 보면 된다.
        /// </summary>
        [Serializable]
        public class ExpressionShading : PartShading
        {
            [Space(8)]
            [Header("레이어 겹침")]
            [Tooltip("표정 파츠(눈·눈썹·입)를 얼굴보다 앞으로 당긴다.\n\n" +
                     "표정 파츠는 얼굴 표면에 얹힌 평면이라 깊이가 거의 같다.\n" +
                     "그대로 두면 Z-파이팅으로 지글거리거나 얼굴에 파묻힌다.\n" +
                     "깊이만 바꾸므로 옆에서 봐도 얼굴에서 떠 보이지 않는다.\n\n" +
                     "겹치면 올리고, 옆에서 볼 때 얼굴을 뚫고 나오면 내린다. 0.004~0.008이 보통.")]
            [Range(0f, 0.02f)] public float 표정_띄우기 = 0.004f;

            [Tooltip("얼굴 아웃라인 헐을 뒤로 민다.\n\n" +
                     "아웃라인은 얼굴 메시를 바깥으로 부풀리기 때문에\n" +
                     "그 위에 얹힌 표정 파츠를 덮어버린다.\n" +
                     "눈이 흐릿하게 가려지면 이 값을 올린다.")]
            [Range(0f, 0.02f)] public float 얼굴아웃라인_밀기 = 0.006f;

            [Tooltip("표정 파츠에는 아웃라인을 그리지 않는다.\n\n" +
                     "눈 텍스처에 이미 선이 그려져 있고, 얼굴 위에 얹힌 평면이라\n" +
                     "인버티드 헐 아웃라인이 지저분하게 튄다. 켜두는 것을 권장.")]
            public bool 표정_아웃라인끄기 = true;

            [Space(4)]
            [Tooltip("표정 파츠를 투명 데칼로 그린다. ★ 겹침의 근본 해결책 ★\n\n" +
                     "이 모델의 표정 파츠(눈·눈썹·볼 붉힘·눈물·세로줄)는 얼굴 표면과\n" +
                     "같은 평면에 놓인 데칼이라, 불투명으로 그리면 깊이 경쟁에서 Z-파이팅이 난다.\n" +
                     "원본은 투명 오버레이로 불투명을 다 그린 뒤 덧그리는 방식이었다.\n\n" +
                     "켜면 깊이 경쟁 자체가 사라지므로 '표정_띄우기'가 불필요해진다.\n" +
                     "끄면 예전처럼 불투명 + 깊이 오프셋으로 동작한다.")]
            public bool 표정_투명데칼 = true;
        }

        // ────────────────────────────────────────────────────────────
        [Header("부위별 셰이딩")]
        [Tooltip("얼굴 전체 — 피부(Face_D)와 표정 파츠(Morph_parts_D)를 함께 관리한다.\n\n" +
                 "셰이딩 값은 두 레이어가 공유하고, 겹침만 아래 '레이어 겹침'에서 따로 잡는다.\n" +
                 "표정이 겹치거나 지글거릴 때 이 그룹 하나만 보면 된다.")]
        public ExpressionShading 표정 = new ExpressionShading
        {
            명암평탄화 = 0.80f, 그림자_임계값 = 0.52f, 그림자_부드러움 = 0.10f,
            그림자색 = new Color(0.93f, 0.84f, 0.85f),
            하이라이트 = 0.02f, 림라이트 = 0.10f, 아웃라인두께 = 0.9f,
            아웃라인색 = new Color(0.46f, 0.27f, 0.30f), 아웃라인_알베도혼합 = 0.40f,
            환경광영향 = 0.15f, 그림자받기 = 0.35f,
            표정_띄우기 = 0.004f,
            얼굴아웃라인_밀기 = 0.006f,
            표정_아웃라인끄기 = true,
        };

        [Tooltip("볼 붉힘(照れ) 전용 머티리얼.\n\n" +
                 "이 모델은 슬롯 1(눈)과 슬롯 7(볼 붉힘)이 원래 같은 머티리얼을 공유해서\n" +
                 "따로 조절할 수 없었다. 'Tools ▸ Toon ▸ Split Duplicate Materials'로\n" +
                 "슬롯 7을 복제해 분리한 뒤, 그 머티리얼을 여기 넣으면 아래 값이 적용된다.\n\n" +
                 "비워두면 볼도 표정 그룹 값을 그대로 따른다.")]
        public Material 볼붉힘_머티리얼;

        [Tooltip("볼 붉힘만의 셰이딩. '볼붉힘_머티리얼'이 지정됐을 때만 쓰인다.\n\n" +
                 "볼은 눈보다 붉기를 낮추거나, 얼굴 조명을 조금 받게 두면 자연스럽다.")]
        public ExpressionShading 볼_붉힘 = new ExpressionShading
        {
            명암평탄화 = 0.85f, 그림자_임계값 = 0.52f, 그림자_부드러움 = 0.12f,
            그림자색 = new Color(0.96f, 0.88f, 0.89f),
            하이라이트 = 0f, 림라이트 = 0f, 아웃라인두께 = 0f,
            환경광영향 = 0.10f, 그림자받기 = 0.20f,
            표정_띄우기 = 0f,
            얼굴아웃라인_밀기 = 0f,
            표정_아웃라인끄기 = true,
            표정_투명데칼 = true,
        };

        [Tooltip("Hair_D 텍스처를 쓰는 머티리얼 + Feather")]
        public PartShading 머리카락 = new PartShading
        {
            명암평탄화 = 0.28f, 그림자_임계값 = 0.50f, 그림자_부드러움 = 0.04f,
            그림자색 = new Color(0.75f, 0.72f, 0.88f),
            하이라이트 = 0.22f, 림라이트 = 0.22f, 아웃라인두께 = 1.3f,
            아웃라인색 = new Color(0.42f, 0.20f, 0.30f), 아웃라인_알베도혼합 = 0.55f,
            환경광영향 = 0.20f, 그림자받기 = 0.5f,
            // 머리카락·깃털은 굴곡이 많아 3단으로 나눌 때 가장 효과가 크다.
            삼단명암 = true, 깊은그림자_임계값 = 0.28f,
            깊은그림자색 = new Color(0.56f, 0.52f, 0.74f),
        };

        [Tooltip("Body_D / Coat_D 텍스처 + Hand, High_heels")]
        public PartShading 의상_피부 = new PartShading
        {
            명암평탄화 = 0.18f, 그림자_임계값 = 0.50f, 그림자_부드러움 = 0.05f,
            그림자색 = new Color(0.71f, 0.70f, 0.87f),
            하이라이트 = 0.05f, 림라이트 = 0.16f, 아웃라인두께 = 1.4f,
            환경광영향 = 0.20f, 그림자받기 = 0.5f,
        };

        [Tooltip("코트와 깃털 트림. 이 모델은 슬롯 5에서 둘이 같은 머티리얼(Caot_D)을\n" +
                 "공유하므로 함께 조절된다.\n\n" +
                 "'머티리얼 수동 분류'에 Caot_D를 이 그룹으로 지정해서 쓴다.\n" +
                 "지정하지 않으면 이름이 coat에 걸려 의상_피부로 간다.")]
        public PartShading 깃털_장식 = new PartShading
        {
            // 코트는 면적이 큰 옷이라 너무 평평하면 형태가 죽는다.
            // 깃털의 보송한 느낌은 평탄화보다 넓은 경계(부드러움)로 잡는 편이 낫다.
            명암평탄화 = 0.22f, 그림자_임계값 = 0.50f, 그림자_부드러움 = 0.11f,
            그림자색 = new Color(0.70f, 0.68f, 0.85f),
            하이라이트 = 0.03f,   // 깃털에 하이라이트 밴드가 뜨면 기름져 보인다
            아웃라인색 = new Color(0.30f, 0.22f, 0.40f), 아웃라인_알베도혼합 = 0.50f,
            림라이트 = 0.22f,     // 대신 림으로 실루엣을 살린다
            아웃라인두께 = 1.2f,
            환경광영향 = 0.20f, 그림자받기 = 0.5f,
        };

        [Tooltip("Halo, Horn_Meltal 등 금속·장식")]
        public PartShading 금속_장식 = new PartShading
        {
            명암평탄화 = 0.10f, 그림자_임계값 = 0.48f, 그림자_부드러움 = 0.03f,
            그림자색 = new Color(0.62f, 0.63f, 0.80f),
            하이라이트 = 0.45f, 림라이트 = 0.30f, 아웃라인두께 = 1.0f,
            아웃라인색 = new Color(0.34f, 0.25f, 0.14f), 아웃라인_알베도혼합 = 0.45f,
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
        /// <summary>수동 분류에서 고를 수 있는 그룹.</summary>
        public enum PartGroup { 표정, 볼붉힘, 머리카락, 깃털_장식, 의상_피부, 금속_장식 }

        [Serializable]
        public class MaterialOverride
        {
            public Material 머티리얼;
            public PartGroup 그룹 = PartGroup.머리카락;
        }

        [Header("머티리얼 수동 분류")]
        [Tooltip("이름·텍스처로 자동 판별한 결과를 덮어쓴다. 여기 있는 것이 최우선이다.\n\n" +
                 "예: Feather는 이름 때문에 머리카락으로 가는데, 여기서 '깃털_장식'으로\n" +
                 "지정하면 하이라이트를 따로 낮출 수 있다.")]
        public List<MaterialOverride> 머티리얼_수동분류 = new List<MaterialOverride>();

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
            m.SetFloat("_EnvironmentInfluence", p.환경광영향);
            m.SetFloat("_ReceiveShadowStrength", p.그림자받기);

            // 데칼 모드에서만 의미가 있다. 불투명 모드는 Blend가 One/Zero라 알파를 무시한다.
            if (m.HasProperty("_BaseColor"))
            {
                Color bc = m.GetColor("_BaseColor");
                bc.a = p.불투명도;
                m.SetColor("_BaseColor", bc);
            }

            m.SetFloat("_ShadowThreshold2", p.깊은그림자_임계값);
            m.SetColor("_ShadowTint2", p.깊은그림자색);
            m.SetFloat("_SecondStep", p.삼단명암 ? 1f : 0f);
            SetKeyword(m, "_SECONDSTEP_ON", p.삼단명암);

            float outlineWidth = p.아웃라인두께;
            float zOffset = 0f;
            float outlineZOffset = 0f;

            // 표정 그룹만 두 레이어를 다르게 처리한다.
            // 같은 값을 주면 둘이 함께 움직여 겹침이 그대로 남는다.
            if (p is ExpressionShading exp)
            {
                if (IsExpressionLayer(m))
                {
                    if (exp.표정_투명데칼)
                    {
                        // 깊이 경쟁에서 빼낸다. 오프셋으로 이기려 들 필요가 없어진다.
                        SetDecalMode(m);
                    }
                    else
                    {
                        SetOpaqueMode(m);
                        zOffset = exp.표정_띄우기;   // 불투명일 때만 의미가 있다
                    }

                    if (exp.표정_아웃라인끄기) outlineWidth = 0f;
                }
                else
                {
                    // 얼굴 피부: 아웃라인 헐만 뒤로 밀어 표정을 덮지 않게 한다.
                    SetOpaqueMode(m);
                    outlineZOffset = exp.얼굴아웃라인_밀기;
                }
            }
            else
            {
                SetOpaqueMode(m);
            }

            m.SetFloat("_ZOffset", zOffset);
            m.SetFloat("_OutlineZOffset", outlineZOffset);
            m.SetFloat("_OutlineWidth", outlineWidth);
            m.SetColor("_OutlineColor", p.아웃라인색);
            m.SetFloat("_OutlineTintByAlbedo", p.아웃라인_알베도혼합);

            bool outline = outlineWidth > 0f;
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
        /// 투명 데칼 모드. 불투명을 전부 그린 뒤 알파 블렌딩으로 덧그린다.
        /// 깊이를 쓰지 않으므로 얼굴과 같은 평면에 있어도 Z-파이팅이 없다.
        /// </summary>
        private static void SetDecalMode(Material m)
        {
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_ZOffset", 0f);

            // 알파 블렌딩이 처리하므로 클립은 끈다. 켜두면 경계가 계단처럼 딱딱해진다.
            m.SetFloat("_AlphaClip", 0f);
            m.DisableKeyword("_ALPHATEST_ON");

            // URP Lit 시절 잔재. 이 셰이더는 읽지 않지만 남아 있으면 혼란스럽다.
            m.DisableKeyword("_ALPHAPREMULTIPLY_ON");

            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

            // 데칼이 그림자를 드리우면 얼굴에 눈 모양 그림자가 생긴다.
            m.SetShaderPassEnabled("ShadowCaster", false);
        }

        /// <summary>불투명 모드. 알파 클립 여부는 머티리얼이 가진 값을 그대로 존중한다.</summary>
        private static void SetOpaqueMode(Material m)
        {
            m.SetFloat("_Surface", 0f);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.Zero);
            m.SetFloat("_ZWrite", 1f);
            m.DisableKeyword("_ALPHAPREMULTIPLY_ON");

            // URP Lit에서 넘어올 때 _AlphaClip 값은 따라오지만 키워드가 빠질 수 있다.
            // 그러면 머리카락·헤일로의 투명 부분이 통째로 불투명하게 나온다.
            bool clip = m.HasProperty("_AlphaClip") && m.GetFloat("_AlphaClip") > 0.5f;
            SetKeyword(m, "_ALPHATEST_ON", clip);

            m.renderQueue = -1;   // 셰이더 기본 큐(Geometry)로 되돌린다
            m.SetShaderPassEnabled("ShadowCaster", true);
        }

        /// <summary>
        /// 부위 판별. Aru_Real2는 머티리얼 이름이 부위와 안 맞아서
        /// (Face_Mouth, Horn_Meltal, High_heels…) 텍스처 이름을 1차 기준으로 쓴다.
        /// </summary>
        private PartShading PickPart(Material m)
        {
            // 수동 분류가 가장 먼저다. 자동 판별이 틀렸을 때 여기서 바로잡는다.
            if (머티리얼_수동분류 != null)
            {
                foreach (MaterialOverride o in 머티리얼_수동분류)
                {
                    if (o?.머티리얼 != null && o.머티리얼 == m) return ShadingOf(o.그룹);
                }
            }

            // 볼 붉힘은 머티리얼을 직접 지정받는다. 분리 후에도 이름이 원본과 비슷해서
            // 문자열로 구분하려 들면 눈과 헷갈린다.
            if (볼붉힘_머티리얼 != null && m == 볼붉힘_머티리얼) return 볼_붉힘;

            Texture tex = m.HasProperty("_BaseMap") ? m.GetTexture("_BaseMap") : null;
            if (tex != null)
            {
                PartShading byTex = Classify(tex.name);
                if (byTex != null) return byTex;
            }
            return Classify(m.name) ?? 의상_피부;
        }

        /// <summary>
        /// 각 머티리얼이 어느 그룹으로 분류됐는지 Console에 출력한다.
        /// 수동 분류를 넣은 뒤 의도대로 걸렸는지 확인할 때 쓴다.
        /// </summary>
        [ContextMenu("분류 결과 확인")]
        public void LogClassification()
        {
            CollectRenderers();
            if (렌더러 == null) return;

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"[CharacterToonSettings] {name} — 부위 분류");

            foreach (Renderer r in 렌더러)
            {
                if (r == null) continue;
                Material[] mats = r.sharedMaterials;

                for (int i = 0; i < mats.Length; i++)
                {
                    Material m = mats[i];
                    if (m == null) { sb.AppendLine($"   슬롯[{i}] (없음)"); continue; }

                    Texture t = m.HasProperty("_BaseMap") ? m.GetTexture("_BaseMap") : null;
                    sb.AppendLine($"   슬롯[{i}] {m.name,-22} → {GroupNameOf(PickPart(m)),-10} " +
                                  $"(텍스처 {(t != null ? t.name : "없음")})");
                }
            }

            Debug.Log(sb.ToString(), this);
        }

        private string GroupNameOf(PartShading p)
        {
            if (ReferenceEquals(p, 표정)) return "표정";
            if (ReferenceEquals(p, 볼_붉힘)) return "볼붉힘";
            if (ReferenceEquals(p, 머리카락)) return "머리카락";
            if (ReferenceEquals(p, 깃털_장식)) return "깃털_장식";
            if (ReferenceEquals(p, 금속_장식)) return "금속_장식";
            if (ReferenceEquals(p, 의상_피부)) return "의상_피부";
            return "?";
        }

        private PartShading ShadingOf(PartGroup g)
        {
            switch (g)
            {
                case PartGroup.표정:     return 표정;
                case PartGroup.볼붉힘:   return 볼_붉힘;
                case PartGroup.머리카락: return 머리카락;
                case PartGroup.깃털_장식: return 깃털_장식;
                case PartGroup.금속_장식: return 금속_장식;
                default:                 return 의상_피부;
            }
        }

        private PartShading Classify(string raw)
        {
            string n = raw.ToLowerInvariant();

            // 얼굴 피부(Face_Mouth)와 표정 파츠(Morph_parts)를 한 그룹으로 받는다.
            if (IsFaceRegion(n)) return 표정;

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

        /// <summary>얼굴 영역(피부 + 표정 파츠)인지.</summary>
        private static bool IsFaceRegion(string n)
        {
            return n.Contains("face") || n.Contains("mouth") || n.Contains("顔") ||
                   IsExpressionLayer(n);
        }

        /// <summary>
        /// 얼굴 위에 얹힌 표정 파츠(눈·눈썹·입)인지.
        /// 얼굴 피부와 구분해야 둘 사이의 겹침을 잡을 수 있다.
        /// </summary>
        private static bool IsExpressionLayer(string n)
        {
            return n.Contains("morph") || n.Contains("eye") || n.Contains("iris") ||
                   n.Contains("pupil") || n.Contains("hitomi") || n.Contains("brow") ||
                   n.Contains("lash") || n.Contains("目");
        }

        /// <summary>머티리얼이 표정 파츠 레이어인지. 텍스처 이름을 우선한다.</summary>
        private static bool IsExpressionLayer(Material m)
        {
            Texture tex = m.HasProperty("_BaseMap") ? m.GetTexture("_BaseMap") : null;
            if (tex != null && IsExpressionLayer(tex.name.ToLowerInvariant())) return true;
            return IsExpressionLayer(m.name.ToLowerInvariant());
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
