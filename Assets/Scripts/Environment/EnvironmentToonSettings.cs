using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace VRProject.Environment
{
    /// <summary>
    /// 배경(City_All)의 툰 셰이딩과 씬 조명을 인스펙터에서 조절한다.
    ///
    /// 핵심 인식: "주간이라 리얼해 보이는" 게 아니다.
    /// 참고 이미지도 한낮 야외지만 툰으로 읽힌다. 차이는 시간대가 아니라
    ///   (1) 디렉셔널 라이트가 하나뿐이고 그림자가 매우 옅다
    ///   (2) 환경 반사가 표면에 잡히지 않는다
    ///   (3) 대비가 낮고 검정이 들려 있다 (하이키)
    ///   (4) 원경이 푸르게 날아간다 (공기원근)
    /// 이 컴포넌트가 그 넷을 담당한다.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("VRProject/Environment Toon Settings")]
    public class EnvironmentToonSettings : MonoBehaviour
    {
        // ── 배경 셰이딩 ─────────────────────────────────────────────
        [Header("배경 셰이딩")]
        [Tooltip("배경 머티리얼에도 값을 적용할지.")]
        public bool 셰이딩적용 = true;

        [Tooltip("URP Lit 머티리얼을 만나면 자동으로 ToonLit으로 바꾼다.")]
        public bool 셰이더_자동교체 = true;

        [Tooltip("명암을 평평하게 만드는 정도. 배경은 캐릭터보다 높게 둬야\n" +
                 "캐릭터가 앞으로 나온다. 참고 이미지의 건물에도 진한 명암이 없다.")]
        [Range(0f, 1f)] public float 명암평탄화 = 0.45f;

        [Tooltip("밝은 면과 그림자 면이 갈리는 지점.")]
        [Range(0f, 1f)] public float 그림자_임계값 = 0.46f;

        [Tooltip("경계의 부드러움. 배경은 캐릭터보다 넓게 둬서 단계가 덜 도드라지게 한다.")]
        [Range(0.001f, 0.4f)] public float 그림자_부드러움 = 0.14f;

        [Tooltip("그림자 색. 검정이 아니라 채도를 유지한 색을 곱하는 게 핵심.")]
        public Color 그림자색 = new Color(0.66f, 0.71f, 0.86f);

        [Tooltip("하이라이트 세기. 배경 반사가 강하면 즉시 리얼해진다.")]
        [Range(0f, 1f)] public float 하이라이트 = 0.03f;

        [Tooltip("환경광(스카이박스/SH)이 미치는 영향. 배경은 캐릭터보다 높게 남긴다.")]
        [Range(0f, 1f)] public float 환경광영향 = 0.6f;

        [Tooltip("드리운 그림자를 받는 정도.")]
        [Range(0f, 1f)] public float 그림자받기 = 0.45f;

        [Tooltip("배경 아웃라인. 켜면 드로우콜이 2배가 되고 화면이 지저분해진다.\n" +
                 "0을 권장.")]
        [Range(0f, 8f)] public float 아웃라인두께 = 0f;

        // ── 키 라이트 ───────────────────────────────────────────────
        [Header("키 라이트")]
        [Tooltip("비워두면 씬에서 가장 밝은 디렉셔널 라이트를 자동으로 쓴다.")]
        public Light 키라이트;

        [Tooltip("나머지 디렉셔널 라이트를 끈다.\n" +
                 "URP는 하나만 메인으로 쓰고 나머지는 추가 광원으로 밝기를 그대로 더하기 때문에,\n" +
                 "여러 개가 켜져 있으면 화면이 날아간다.")]
        public bool 다른_디렉셔널라이트_끄기 = true;

        [Tooltip("적용할지. 끄면 라이트를 건드리지 않는다.")]
        public bool 라이트적용 = true;

        public Color 라이트색 = new Color(1f, 0.957f, 0.902f);

        [Range(0f, 3f)] public float 라이트세기 = 1.0f;

        [Tooltip("그림자 진하기. 참고 이미지의 건물에는 형태가 겨우 분리될 정도의\n" +
                 "옅은 그림자만 있다. 1.0으로 두면 무슨 셰이더를 써도 리얼해 보인다.")]
        [Range(0f, 1f)] public float 그림자진하기 = 0.35f;

        [Tooltip("빛이 내려오는 각도. 양수가 위에서 아래로.")]
        [Range(0f, 89f)] public float 라이트_상하 = 50f;

        [Range(-180f, 180f)] public float 라이트_좌우 = -35f;

        // ── 환경광 ──────────────────────────────────────────────────
        [Header("환경광")]
        [Tooltip("적용할지. 씬 전역 설정이라 되돌리려면 Lighting 창에서 직접 바꿔야 한다.")]
        public bool 환경광적용 = true;

        [Tooltip("Skybox 앰비언트는 방향성이 강하고 밝아 툰 명암을 지운다.\n" +
                 "Gradient로 바꿔 통제된 양만 암부에 넣는다.")]
        public Color 하늘색 = new Color(0.60f, 0.68f, 0.82f);
        public Color 중간색 = new Color(0.52f, 0.56f, 0.65f);
        public Color 바닥색 = new Color(0.42f, 0.43f, 0.47f);

        [Range(0f, 3f)] public float 환경광세기 = 1.0f;

        [Tooltip("환경 반사. 툰 표면에 반사가 잡히면 즉시 리얼해진다. 0 권장.")]
        [Range(0f, 1f)] public float 반사강도 = 0f;

        // ── 안개 (공기원근) ─────────────────────────────────────────
        [Header("안개 — 공기원근")]
        [Tooltip("주간 야외에서는 안개를 끄는 게 아니라 써야 한다.\n" +
                 "참고 이미지의 원경 빌딩이 푸르게 날아간 것이 공기원근이고,\n" +
                 "이게 배경을 평면화해서 캐릭터를 앞으로 끌어낸다.")]
        public bool 안개사용 = true;

        public Color 안개색 = new Color(0.72f, 0.82f, 0.94f);

        [Tooltip("이 거리부터 안개가 시작된다.")]
        public float 안개_시작 = 25f;

        [Tooltip("이 거리에서 완전히 안개색이 된다.")]
        public float 안개_끝 = 260f;

        // ── 포스트 프로세싱 ─────────────────────────────────────────
        [Header("포스트 프로세싱")]
        [Tooltip("Global Volume의 프로파일. 비워두면 씬에서 자동으로 찾는다.\n" +
                 "없으면 아래 컨텍스트 메뉴로 만들 수 있다.")]
        public VolumeProfile 볼륨프로파일;

        [Tooltip("적용할지.")]
        public bool 포스트적용 = true;

        [Tooltip("전체 밝기. 순백으로 잘리면 낮춘다.")]
        [Range(-3f, 3f)] public float 노출 = -0.15f;

        [Tooltip("음수로 갈수록 검정이 들리고 대비가 낮아진다.\n" +
                 "하이키가 애니메이션풍의 큰 축이다.")]
        [Range(-100f, 100f)] public float 대비 = -14f;

        [Range(-100f, 100f)] public float 채도 = 10f;

        [Tooltip("그림자에 섞을 색. 청색 쪽으로 밀면 애니메이션처럼 보인다.")]
        public Color 그림자_색보정 = new Color(0.94f, 0.98f, 1.12f);

        [Tooltip("그림자를 얼마나 들어올릴지.")]
        [Range(0f, 0.3f)] public float 그림자_리프트 = 0.06f;

        [Tooltip("블룸이 걸리기 시작하는 밝기. VR은 스테레오라 눈부심이 증폭된다.")]
        [Range(0f, 3f)] public float 블룸_임계값 = 1.15f;

        [Range(0f, 2f)] public float 블룸_세기 = 0.35f;

        // ────────────────────────────────────────────────────────────
        private Renderer[] cached;

        private void OnEnable() => ApplyAll();

        private void OnValidate() => ApplyAll();

        [ContextMenu("전체 다시 적용")]
        public void ApplyAll()
        {
            ApplyShading();
            ApplyLight();
            ApplyAmbient();
            ApplyFog();
            ApplyPost();
        }

        // ── 셰이딩 ──────────────────────────────────────────────────
        private void ApplyShading()
        {
            if (!셰이딩적용) return;

            if (cached == null || cached.Length == 0)
                cached = GetComponentsInChildren<Renderer>(true);

            Shader toon = Shader.Find("VRProject/ToonLit");
            if (toon == null) return;

            foreach (Renderer r in cached)
            {
                if (r == null) continue;

                foreach (Material m in r.sharedMaterials)
                {
                    if (m == null) continue;

                    if (셰이더_자동교체 && m.shader != toon)
                    {
                        Texture baseMap = m.HasProperty("_BaseMap") ? m.GetTexture("_BaseMap")
                                        : (m.HasProperty("_MainTex") ? m.GetTexture("_MainTex") : null);
                        Color baseColor = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor")
                                        : (m.HasProperty("_Color") ? m.GetColor("_Color") : Color.white);
                        m.shader = toon;
                        if (baseMap != null) m.SetTexture("_BaseMap", baseMap);
                        m.SetColor("_BaseColor", baseColor);
                    }

                    if (m.shader != toon) continue;

                    m.SetFloat("_Flatten", 명암평탄화);
                    m.SetFloat("_ShadowThreshold", 그림자_임계값);
                    m.SetFloat("_ShadowFeather", 그림자_부드러움);
                    m.SetColor("_ShadowTint", 그림자색);
                    m.SetFloat("_SpecIntensity", 하이라이트);
                    m.SetFloat("_EnvironmentInfluence", 환경광영향);
                    m.SetFloat("_ReceiveShadowStrength", 그림자받기);
                    m.SetFloat("_OutlineWidth", 아웃라인두께);

                    bool outline = 아웃라인두께 > 0f;
                    m.SetFloat("_OutlineEnabled", outline ? 1f : 0f);
                    if (outline) m.EnableKeyword("_OUTLINE_ON");
                    else m.DisableKeyword("_OUTLINE_ON");

                    // 배경은 씬의 실제 태양광을 받는다. 캐릭터 전용 광원은 쓰지 않는다.
                    m.SetFloat("_CharacterLight", 0f);
                    m.DisableKeyword("_CHARACTERLIGHT_ON");
                }

                r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            }
        }

        // ── 라이트 ──────────────────────────────────────────────────
        private void ApplyLight()
        {
            if (!라이트적용) return;

            Light key = 키라이트 != null ? 키라이트 : FindBrightestDirectional();
            if (key == null) return;

            if (다른_디렉셔널라이트_끄기)
            {
                foreach (Light l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                {
                    if (l == key) continue;
                    if (l.type != LightType.Directional) continue;
                    if (l.enabled) l.enabled = false;
                }
            }

            key.enabled = true;
            key.color = 라이트색;
            key.intensity = 라이트세기;
            key.shadows = LightShadows.Soft;
            key.shadowStrength = 그림자진하기;

            // 얼굴·머리카락의 지저분한 셀프 섀도를 막는다.
            key.shadowBias = 0.08f;
            key.shadowNormalBias = 0.45f;

            key.transform.rotation = Quaternion.Euler(라이트_상하, 라이트_좌우, 0f);
        }

        private Light FindBrightestDirectional()
        {
            Light best = null;
            foreach (Light l in FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (l.type != LightType.Directional) continue;
                if (best == null || l.intensity > best.intensity) best = l;
            }
            return best;
        }

        // ── 환경광 ──────────────────────────────────────────────────
        private void ApplyAmbient()
        {
            if (!환경광적용) return;

            RenderSettings.ambientMode = AmbientMode.Trilight;   // Gradient
            RenderSettings.ambientSkyColor = 하늘색;
            RenderSettings.ambientEquatorColor = 중간색;
            RenderSettings.ambientGroundColor = 바닥색;
            RenderSettings.ambientIntensity = 환경광세기;

            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.customReflectionTexture = null;
            RenderSettings.reflectionIntensity = 반사강도;
        }

        // ── 안개 ────────────────────────────────────────────────────
        private void ApplyFog()
        {
            RenderSettings.fog = 안개사용;
            if (!안개사용) return;

            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = 안개색;
            RenderSettings.fogStartDistance = 안개_시작;
            RenderSettings.fogEndDistance = Mathf.Max(안개_끝, 안개_시작 + 1f);
        }

        // ── 포스트 ──────────────────────────────────────────────────
        private void ApplyPost()
        {
            if (!포스트적용) return;

            VolumeProfile profile = 볼륨프로파일;
            if (profile == null)
            {
                Volume v = FindFirstObjectByType<Volume>();
                if (v != null) profile = v.sharedProfile;
            }
            if (profile == null) return;

            // ACES는 대비를 세게 걸어 툰 색을 눌러버린다. Neutral이 맞다.
            if (profile.TryGet(out Tonemapping tm))
            {
                tm.active = true;
                tm.mode.overrideState = true;
                tm.mode.value = TonemappingMode.Neutral;
            }

            if (profile.TryGet(out ColorAdjustments ca))
            {
                ca.active = true;
                ca.postExposure.overrideState = true; ca.postExposure.value = 노출;
                ca.contrast.overrideState = true;     ca.contrast.value = 대비;
                ca.saturation.overrideState = true;   ca.saturation.value = 채도;
            }

            if (profile.TryGet(out ShadowsMidtonesHighlights smh))
            {
                smh.active = true;
                smh.shadows.overrideState = true;
                smh.shadows.value = new Vector4(그림자_색보정.r, 그림자_색보정.g,
                                                그림자_색보정.b, 그림자_리프트);
            }

            if (profile.TryGet(out Bloom bloom))
            {
                bloom.active = true;
                bloom.threshold.overrideState = true; bloom.threshold.value = 블룸_임계값;
                bloom.intensity.overrideState = true; bloom.intensity.value = 블룸_세기;
                bloom.scatter.overrideState = true;   bloom.scatter.value = 0.65f;
            }

            // VR에서는 반드시 꺼야 한다.
            // DoF는 플레이어가 어디에 초점을 맞출지 알 수 없어 눈이 싸우고,
            // 모션블러는 멀미로 직결된다.
            if (profile.TryGet(out DepthOfField dof)) dof.active = false;
            if (profile.TryGet(out MotionBlur mb)) mb.active = false;
        }

#if UNITY_EDITOR
        [ContextMenu("Global Volume + 프로파일 만들기")]
        private void CreateVolume()
        {
            Volume v = FindFirstObjectByType<Volume>();
            if (v == null)
            {
                var go = new GameObject("Global Volume (Toon)");
                UnityEditor.Undo.RegisterCreatedObjectUndo(go, "Create Toon Volume");
                v = go.AddComponent<Volume>();
                v.isGlobal = true;
                v.priority = 0f;
            }

            if (v.sharedProfile == null)
            {
                const string dir = "Assets/Settings";
                if (!System.IO.Directory.Exists(dir)) System.IO.Directory.CreateDirectory(dir);

                string path = UnityEditor.AssetDatabase.GenerateUniqueAssetPath(
                    $"{dir}/ToonVolumeProfile.asset");
                var p = ScriptableObject.CreateInstance<VolumeProfile>();
                UnityEditor.AssetDatabase.CreateAsset(p, path);

                // 이 컴포넌트가 조절할 항목들을 미리 넣어둔다.
                p.Add<Tonemapping>(true);
                p.Add<ColorAdjustments>(true);
                p.Add<ShadowsMidtonesHighlights>(true);
                p.Add<Bloom>(true);

                UnityEditor.EditorUtility.SetDirty(p);
                UnityEditor.AssetDatabase.SaveAssets();
                v.sharedProfile = p;
            }

            볼륨프로파일 = v.sharedProfile;
            ApplyPost();
            Debug.Log($"[EnvironmentToonSettings] Volume 준비 완료: {볼륨프로파일.name}", this);
        }
#endif
    }
}
