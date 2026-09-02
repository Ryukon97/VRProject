using System;
using System.Collections.Generic;
using UnityEngine;

namespace VRProject.Character
{
    /// <summary>
    /// 블렌드셰이프로 표정을 만든다.
    ///
    /// 표정 하나는 모프 여러 개의 조합이다(미소 = にこり 100, 슬픔 = 困る2 + 涙 + うつ線).
    /// 프리셋을 정의해두고 이름이나 인덱스로 재생하면 지정한 시간에 걸쳐 크로스페이드된다.
    ///
    /// EyeBlink와의 조율:
    ///   표정이 눈을 감는 모프(笑い, ウィンク 등)를 쓰면 깜빡임을 자동으로 멈춘다.
    ///   그렇지 않으면 깜빡임은 계속 돌고, 이 컴포넌트는 まばたき를 건드리지 않는다.
    ///
    /// 립싱크와의 조율:
    ///   '립싱크_사용중'을 켜면 あいうえお를 관리 대상에서 빼서 uLipSync 같은 것이
    ///   입을 전담하게 한다.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("VRProject/Facial Expression")]
    public class FacialExpression : MonoBehaviour
    {
        [Serializable]
        public class MorphWeight
        {
            [Tooltip("블렌드셰이프 이름. 이 모델은 MMD 표준 이름을 쓴다(笑い, 困る, 涙 …).")]
            public string 모프 = "";

            [Range(0f, 100f)] public float 가중치 = 100f;
        }

        [Serializable]
        public class Expression
        {
            [Tooltip("표정 이름. Play(\"미소\")처럼 코드에서 부를 때 쓴다.")]
            public string 이름 = "새 표정";

            [Tooltip("이 표정을 구성하는 모프 조합. 여기 없는 모프는 0으로 돌아간다.")]
            public List<MorphWeight> 모프들 = new List<MorphWeight>();

            [Tooltip("이 표정으로 넘어가는 데 걸리는 시간(초). 0이면 즉시.\n" +
                     "즉시 바꾸면 툭툭 끊겨 보인다. 0.12~0.25가 자연스럽다.")]
            [Range(0f, 1f)] public float 전환시간 = 0.15f;

            [Tooltip("이 표정 동안 눈 깜빡임을 멈춘다.\n" +
                     "눈을 감는 모프를 쓰면 자동으로 멈추므로 보통은 건드릴 필요가 없다.")]
            public bool 깜빡임_정지;
        }

        // ────────────────────────────────────────────────────────────
        [Header("대상")]
        [Tooltip("블렌드셰이프를 가진 렌더러. 비워두면 자식에서 찾는다.")]
        [SerializeField] private SkinnedMeshRenderer 렌더러;

        [Header("재생")]
        [Tooltip("현재 표정. 인스펙터의 버튼으로 바꾸거나 코드에서 Play()로 부른다.")]
        [SerializeField] private int 현재표정 = 0;

        [Tooltip("켜면 입 모프(あ い う え お)를 건드리지 않는다.\n" +
                 "uLipSync 등이 입을 전담할 때 켠다.")]
        [SerializeField] private bool 립싱크_사용중 = false;

        [Header("부가 효과 — 표정과 별개로 겹쳐 쓴다")]
        [Tooltip("땀·눈물·세로줄 같은 것을 표정 위에 얹는다. 0이면 꺼짐.")]
        [Range(0f, 100f)] public float 땀 = 0f;
        [Range(0f, 100f)] public float 눈물 = 0f;
        [Range(0f, 100f)] public float 세로줄 = 0f;
        [Range(0f, 100f)] public float 얼굴그늘 = 0f;

        [Header("믹서 — 값을 직접 찾을 때 켠다")]
        [Tooltip("켜면 표정 프리셋 대신 아래 믹서 값이 얼굴에 적용된다.\n" +
                 "인스펙터 위쪽의 슬라이더로 눈·눈썹·입을 직접 맞춘 뒤\n" +
                 "'표정으로 저장'을 누르면 프리셋이 된다.")]
        public bool 믹서모드 = false;

        [Tooltip("믹서에서 조절 중인 모프 값. 보통 직접 건드릴 필요는 없다.")]
        [SerializeField] private List<MorphWeight> 믹서값 = new List<MorphWeight>();

        [Header("표정 목록")]
        [Tooltip("모프 이름은 이 모델에서 실제로 확인한 46개 중에서 골랐다.\n" +
                 "어떤 모프가 무슨 표정인지는 MMD 관례를 따른 추정이므로,\n" +
                 "눈으로 보고 안 맞으면 이름과 가중치를 고치면 된다.")]
        public List<Expression> 표정목록 = DefaultExpressions();

        // 부가 효과에 해당하는 모프 이름 (이 모델에서 확인됨)
        private const string MorphSweat = "汗";
        private const string MorphTear = "涙";
        private const string MorphGloom = "うつ線";
        private const string MorphFaceShade = "顔かげ";
        private const string MorphBlink = "まばたき";

        private static readonly string[] MouthMorphs = { "あ", "い", "う", "え", "お" };

        // 눈을 감는 모프. 이게 들어간 표정에서는 깜빡임을 멈춘다.
        private static readonly string[] EyeClosingMorphs =
        {
            "笑い", "ウィンク", "ウィンク右", "ウィンク２", "ウィンク２右", "にこり", "︿",
        };

        private float[] current;      // 현재 적용 중인 가중치
        private float[] target;       // 목표 가중치
        private bool[] managed;       // 이 컴포넌트가 관리하는 모프인지
        private int blinkIndex = -1;
        private EyeBlink blink;
        private float fadeSpeed = 1f / 0.15f;

        public int Count => 표정목록 != null ? 표정목록.Count : 0;

        public string CurrentName =>
            (표정목록 != null && 현재표정 >= 0 && 현재표정 < 표정목록.Count)
                ? 표정목록[현재표정].이름 : "-";

        // ── 공개 API ────────────────────────────────────────────────

        /// <summary>이름으로 표정을 재생한다. 없으면 아무것도 하지 않고 false를 반환한다.</summary>
        public bool Play(string name)
        {
            if (표정목록 == null) return false;
            for (int i = 0; i < 표정목록.Count; i++)
            {
                if (표정목록[i].이름 == name) { Play(i); return true; }
            }
            Debug.LogWarning($"[FacialExpression] '{name}' 표정을 찾지 못했다.", this);
            return false;
        }

        /// <summary>인덱스로 표정을 재생한다.</summary>
        public void Play(int index)
        {
            if (표정목록 == null || 표정목록.Count == 0) return;
            현재표정 = Mathf.Clamp(index, 0, 표정목록.Count - 1);
            Resolve();
            RebuildTarget();

            // 편집 중에는 크로스페이드를 기다릴 이유가 없다. 버튼을 누르면 바로 보여야 한다.
            if (!Application.isPlaying && current != null && target != null)
            {
                Array.Copy(target, current, target.Length);
                Apply();
            }
        }

        /// <summary>부가 효과를 한 번에 설정한다.</summary>
        public void SetExtras(float sweat, float tear, float gloom, float faceShade)
        {
            땀 = sweat; 눈물 = tear; 세로줄 = gloom; 얼굴그늘 = faceShade;
            RebuildTarget();
        }

        // ── 믹서 API ────────────────────────────────────────────────

        /// <summary>모프를 어느 그룹에 넣어 보여줄지. MMD 관례를 따른다.</summary>
        public enum MorphGroup { 눈썹, 눈, 입, 기타 }

        private static readonly HashSet<string> BrowMorphs = new HashSet<string>
        {
            "真面目", "真面目2", "困る", "困る2", "にこり", "怒り", "上", "下", "嬉しい",
        };

        private static readonly HashSet<string> EyeMorphs = new HashSet<string>
        {
            "まばたき", "笑い", "ウィンク", "ウィンク右", "ウィンク２", "ウィンク２右",
            "びっくり", "睨み", "︿", "目暗", "目小", "目_OFF", "ハイライト_ON",
        };

        private static readonly HashSet<string> MouthGroupMorphs = new HashSet<string>
        {
            "あ", "い", "う", "え", "お", "にやり", "にやり2", "ワ大", "ワ-", "ん", "む",
            "むっ", "口上", "口下", "ヒイィィィ", "笑い口",
        };

        public static MorphGroup GroupOf(string morph)
        {
            if (BrowMorphs.Contains(morph)) return MorphGroup.눈썹;
            if (EyeMorphs.Contains(morph)) return MorphGroup.눈;
            if (MouthGroupMorphs.Contains(morph)) return MorphGroup.입;
            return MorphGroup.기타;
        }

        /// <summary>믹서 UI가 쓰는 렌더러. 모프 이름 목록을 여기서 읽는다.</summary>
        public SkinnedMeshRenderer TargetRenderer { get { Resolve(); return 렌더러; } }

        public float GetMixer(string morph)
        {
            if (믹서값 == null) return 0f;
            foreach (MorphWeight mw in 믹서값)
                if (mw != null && mw.모프 == morph) return mw.가중치;
            return 0f;
        }

        public void SetMixer(string morph, float weight)
        {
            믹서값 ??= new List<MorphWeight>();

            foreach (MorphWeight mw in 믹서값)
            {
                if (mw != null && mw.모프 == morph) { mw.가중치 = weight; RebuildAndApply(); return; }
            }
            믹서값.Add(new MorphWeight { 모프 = morph, 가중치 = weight });
            RebuildAndApply();
        }

        /// <summary>
        /// 표정을 목록에서 지운다.
        ///
        /// 지운 표정만 쓰던 모프는 관리 대상에서 빠지는데, 그대로 두면 마지막 값이
        /// 얼굴에 남는다(웃던 눈이 계속 감겨 있는 식). RebuildTarget이 관리에서
        /// 빠진 모프를 0으로 되돌리므로 여기서는 목록만 건드리면 된다.
        /// </summary>
        public bool RemoveExpression(int index)
        {
            if (표정목록 == null || index < 0 || index >= 표정목록.Count) return false;

            표정목록.RemoveAt(index);

            if (표정목록.Count == 0) 현재표정 = 0;
            else if (현재표정 >= 표정목록.Count) 현재표정 = 표정목록.Count - 1;

            RebuildAndApply();
            return true;
        }

        [ContextMenu("믹서 초기화")]
        public void ClearMixer()
        {
            믹서값?.Clear();
            RebuildAndApply();
        }

        /// <summary>현재 표정의 조합을 믹서로 불러온다. 거기서부터 다듬으면 된다.</summary>
        public void LoadMixerFromCurrent()
        {
            믹서값 ??= new List<MorphWeight>();
            믹서값.Clear();

            Expression cur = CurrentExpression();
            if (cur?.모프들 != null)
            {
                foreach (MorphWeight mw in cur.모프들)
                    if (mw != null) 믹서값.Add(new MorphWeight { 모프 = mw.모프, 가중치 = mw.가중치 });
            }
            RebuildAndApply();
        }

        /// <summary>
        /// 믹서의 현재 조합을 표정으로 저장한다.
        /// 같은 이름이 있으면 덮어쓰고, 없으면 목록 끝에 추가한다.
        /// </summary>
        public int SaveMixerAsExpression(string name, float fade = 0.15f)
        {
            표정목록 ??= new List<Expression>();

            var morphs = new List<MorphWeight>();
            if (믹서값 != null)
            {
                foreach (MorphWeight mw in 믹서값)
                {
                    if (mw == null || string.IsNullOrEmpty(mw.모프)) continue;
                    if (mw.가중치 <= 0.01f) continue;      // 0인 것은 저장하지 않는다
                    morphs.Add(new MorphWeight { 모프 = mw.모프, 가중치 = mw.가중치 });
                }
            }

            for (int i = 0; i < 표정목록.Count; i++)
            {
                if (표정목록[i] != null && 표정목록[i].이름 == name)
                {
                    표정목록[i].모프들 = morphs;
                    표정목록[i].전환시간 = fade;
                    현재표정 = i;
                    RebuildAndApply();
                    return i;
                }
            }

            표정목록.Add(new Expression { 이름 = name, 모프들 = morphs, 전환시간 = fade });
            현재표정 = 표정목록.Count - 1;
            RebuildAndApply();
            return 현재표정;
        }

        private void RebuildAndApply()
        {
            Resolve();
            RebuildTarget();
            if (!Application.isPlaying && current != null && target != null)
            {
                Array.Copy(target, current, target.Length);
                Apply();
            }
        }

        /// <summary>
        /// 기본 목록에는 있는데 현재 목록에 없는 표정만 뒤에 덧붙인다.
        /// 이미 조정해둔 표정은 건드리지 않는다.
        /// </summary>
        [ContextMenu("빠진 기본 표정 추가")]
        public int AddMissingDefaults()
        {
            표정목록 ??= new List<Expression>();

            int added = 0;
            foreach (Expression d in DefaultExpressions())
            {
                bool exists = false;
                foreach (Expression e in 표정목록)
                {
                    if (e != null && e.이름 == d.이름) { exists = true; break; }
                }
                if (exists) continue;

                표정목록.Add(d);
                added++;
            }

            if (added > 0)
            {
                Resolve();
                RebuildTarget();
                Debug.Log($"[FacialExpression] 표정 {added}개를 추가했다.", this);
            }
            return added;
        }

        // ── 내부 ────────────────────────────────────────────────────

        private void OnEnable()
        {
            Resolve();
            RebuildTarget();

            // 편집 중에는 즉시 반영한다. 기다릴 이유가 없다.
            if (!Application.isPlaying && current != null && target != null)
                Array.Copy(target, current, target.Length);

            Apply();
        }

        private void OnValidate()
        {
            Resolve();
            RebuildTarget();
            if (!Application.isPlaying && current != null && target != null)
                Array.Copy(target, current, target.Length);
            Apply();
        }

        private void Resolve()
        {
            if (렌더러 == null)
            {
                foreach (var r in GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    if (r.sharedMesh != null && r.sharedMesh.blendShapeCount > 0) { 렌더러 = r; break; }
                }
            }

            if (렌더러 == null || 렌더러.sharedMesh == null) return;

            int n = 렌더러.sharedMesh.blendShapeCount;
            if (current == null || current.Length != n)
            {
                current = new float[n];
                target = new float[n];
                managed = new bool[n];
            }

            blinkIndex = 렌더러.sharedMesh.GetBlendShapeIndex(MorphBlink);
            if (blink == null) blink = GetComponentInChildren<EyeBlink>(true);
        }

        /// <summary>현재 표정 + 부가 효과로부터 목표 가중치를 다시 만든다.</summary>
        private void RebuildTarget()
        {
            if (렌더러 == null || 렌더러.sharedMesh == null || target == null) return;
            Mesh mesh = 렌더러.sharedMesh;

            // 직전에 관리하던 목록을 기억해둔다. 표정을 지우거나 모프를 빼면
            // 관리에서 빠지는데, 그대로 두면 마지막 가중치가 얼굴에 남는다.
            bool[] wasManaged = (bool[])managed.Clone();

            Array.Clear(target, 0, target.Length);
            Array.Clear(managed, 0, managed.Length);

            // 어떤 프리셋에든 등장하는 모프는 전부 관리 대상이다.
            // 그래야 이전 표정에서 켜졌던 것이 0으로 돌아간다.
            if (표정목록 != null)
            {
                foreach (Expression e in 표정목록)
                {
                    if (e?.모프들 == null) continue;
                    foreach (MorphWeight mw in e.모프들)
                    {
                        int idx = IndexOf(mesh, mw?.모프);
                        if (idx >= 0) managed[idx] = true;
                    }
                }
            }

            MarkManaged(mesh, MorphSweat);
            MarkManaged(mesh, MorphTear);
            MarkManaged(mesh, MorphGloom);
            MarkManaged(mesh, MorphFaceShade);

            // 믹서에서 건드린 모프도 관리 대상이다. 그래야 믹서를 끄거나 값을
            // 0으로 내렸을 때 얼굴에 남아있지 않는다.
            if (믹서값 != null)
            {
                foreach (MorphWeight mw in 믹서값)
                {
                    int idx = IndexOf(mesh, mw?.모프);
                    if (idx >= 0) managed[idx] = true;
                }
            }

            // 립싱크가 입을 잡고 있으면 넘겨준다.
            if (립싱크_사용중)
            {
                foreach (string m in MouthMorphs)
                {
                    int idx = IndexOf(mesh, m);
                    if (idx >= 0) managed[idx] = false;
                }
            }

            // 관리에서 빠진 모프는 0으로 놓아준다.
            // 이게 없으면 표정을 지웠을 때 감긴 눈이 그대로 남는다.
            ReleaseUnmanaged(wasManaged);

            Expression cur = CurrentExpression();

            if (믹서모드)
            {
                // 믹서가 켜져 있으면 프리셋 대신 믹서 값만 얼굴에 간다.
                fadeSpeed = 1000f;   // 편집 중에는 즉시 반영
                if (믹서값 != null)
                {
                    foreach (MorphWeight mw in 믹서값)
                    {
                        int idx = IndexOf(mesh, mw?.모프);
                        if (idx >= 0) target[idx] = mw.가중치;
                    }
                }
                UpdateBlinkSuppressionFromTarget(mesh);
                return;
            }

            if (cur != null)
            {
                fadeSpeed = cur.전환시간 > 0.001f ? 1f / cur.전환시간 : 1000f;

                if (cur.모프들 != null)
                {
                    foreach (MorphWeight mw in cur.모프들)
                    {
                        int idx = IndexOf(mesh, mw?.모프);
                        if (idx >= 0 && managed[idx]) target[idx] = mw.가중치;
                    }
                }
            }

            SetTarget(mesh, MorphSweat, 땀);
            SetTarget(mesh, MorphTear, 눈물);
            SetTarget(mesh, MorphGloom, 세로줄);
            SetTarget(mesh, MorphFaceShade, 얼굴그늘);

            UpdateBlinkSuppression(cur);
        }

        /// <summary>
        /// 직전에는 관리했는데 이제 아닌 모프를 0으로 되돌린다.
        /// 관리 대상에서 빠지면 Apply()가 더 이상 손대지 않으므로,
        /// 여기서 놓아주지 않으면 마지막 값이 얼굴에 그대로 굳는다.
        /// </summary>
        private void ReleaseUnmanaged(bool[] wasManaged)
        {
            if (wasManaged == null || 렌더러 == null) return;

            for (int i = 0; i < managed.Length; i++)
            {
                if (!wasManaged[i] || managed[i]) continue;

                current[i] = 0f;
                target[i] = 0f;

                // まばたき는 EyeBlink가 다시 가져가므로 굳이 건드리지 않는다.
                if (i == blinkIndex && blink != null && blink.isActiveAndEnabled) continue;

                렌더러.SetBlendShapeWeight(i, 0f);
            }
        }

        private void MarkManaged(Mesh mesh, string name)
        {
            int idx = IndexOf(mesh, name);
            if (idx >= 0) managed[idx] = true;
        }

        private void SetTarget(Mesh mesh, string name, float w)
        {
            int idx = IndexOf(mesh, name);
            if (idx >= 0) target[idx] = w;
        }

        private static int IndexOf(Mesh mesh, string name)
        {
            return string.IsNullOrEmpty(name) ? -1 : mesh.GetBlendShapeIndex(name);
        }

        private Expression CurrentExpression()
        {
            if (표정목록 == null || 표정목록.Count == 0) return null;
            현재표정 = Mathf.Clamp(현재표정, 0, 표정목록.Count - 1);
            return 표정목록[현재표정];
        }

        /// <summary>
        /// 표정이 눈을 감는 모프를 쓰면 깜빡임을 멈춘다.
        /// 안 그러면 EyeBlink와 이 컴포넌트가 まばたき를 두고 매 프레임 싸운다.
        /// </summary>
        private void UpdateBlinkSuppression(Expression cur)
        {
            if (blink == null) return;

            bool suppress = cur != null && cur.깜빡임_정지;

            if (!suppress && cur?.모프들 != null)
            {
                foreach (MorphWeight mw in cur.모프들)
                {
                    if (mw == null || mw.가중치 <= 0.01f) continue;
                    if (mw.모프 == MorphBlink) { suppress = true; break; }
                    foreach (string e in EyeClosingMorphs)
                    {
                        if (mw.모프 == e) { suppress = true; break; }
                    }
                    if (suppress) break;
                }
            }

            blink.Suppressed = suppress;
        }

        /// <summary>믹서 모드용. 목표 가중치에서 눈 감는 모프가 켜졌는지 본다.</summary>
        private void UpdateBlinkSuppressionFromTarget(Mesh mesh)
        {
            if (blink == null) return;

            bool suppress = false;
            foreach (string e in EyeClosingMorphs)
            {
                int idx = IndexOf(mesh, e);
                if (idx >= 0 && target[idx] > 0.01f) { suppress = true; break; }
            }

            if (!suppress && blinkIndex >= 0 && target[blinkIndex] > 0.01f) suppress = true;

            blink.Suppressed = suppress;
        }

        private void Update()
        {
            if (current == null || target == null) return;

            if (Application.isPlaying)
            {
                // 전환시간 동안 0→100을 지나가야 하므로 초당 100/전환시간 만큼 움직인다.
                float step = fadeSpeed * 100f * Time.deltaTime;
                for (int i = 0; i < current.Length; i++)
                {
                    if (!managed[i]) continue;
                    current[i] = Mathf.MoveTowards(current[i], target[i], step);
                }
            }

            Apply();
        }

        private void Apply()
        {
            if (렌더러 == null || current == null) return;

            bool blinkActive = blink != null && blink.isActiveAndEnabled && !blink.Suppressed;

            for (int i = 0; i < current.Length; i++)
            {
                if (!managed[i]) continue;

                // 깜빡임이 돌고 있으면 まばたき는 EyeBlink에게 맡긴다.
                if (blinkActive && i == blinkIndex) continue;

                렌더러.SetBlendShapeWeight(i, current[i]);
            }
        }

        // ── 기본 프리셋 ─────────────────────────────────────────────

        /// <summary>
        /// 이 모델에서 실제로 확인한 46개 모프로 구성한 시작점.
        /// 어떤 모프가 무슨 표정인지는 MMD 관례를 따른 추정이므로,
        /// 눈으로 보고 안 맞으면 인스펙터에서 고치면 된다.
        /// </summary>
        private static List<Expression> DefaultExpressions()
        {
            return new List<Expression>
            {
                Make("기본", 0.15f),
                Make("미소", 0.18f, ("にこり", 100f)),
                Make("활짝웃음", 0.12f, ("笑い", 100f), ("ワ大", 70f)),

                // 눈은 아치로 감고 입은 크게 벌린 함박웃음.
                // 笑い가 눈을 감으므로 깜빡임은 자동으로 멈춘다.
                // 입이 덜 벌어지면 ワ大를 올리고, 그래도 아쉬우면 笑い口를 40쯤 얹는다.
                Make("또 아루함", 0.10f, ("笑い", 100f), ("ワ大", 100f)),

                Make("무표정", 0.20f, ("真面目", 100f)),
                Make("곤란", 0.18f, ("困る", 100f), ("汗", 60f)),
                Make("놀람", 0.08f, ("びっくり", 100f), ("ワ大", 80f)),
                Make("화남", 0.12f, ("怒り", 100f), ("むっ", 70f)),
                Make("장난", 0.15f, ("にやり", 100f), ("ウィンク", 100f)),
                Make("슬픔", 0.25f, ("困る2", 100f), ("涙", 100f), ("うつ線", 50f)),
                Make("노려봄", 0.15f, ("睨み", 100f)),
            };
        }

        private static Expression Make(string name, float fade, params (string morph, float w)[] morphs)
        {
            var e = new Expression { 이름 = name, 전환시간 = fade };
            foreach (var (morph, w) in morphs)
                e.모프들.Add(new MorphWeight { 모프 = morph, 가중치 = w });
            return e;
        }
    }
}
