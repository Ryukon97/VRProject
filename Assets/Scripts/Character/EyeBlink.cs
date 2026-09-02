using System.Collections.Generic;
using UnityEngine;

namespace VRProject.Character
{
    /// <summary>
    /// 블렌드셰이프 기반 눈 깜빡임.
    ///
    /// MMD 변환 모델의 표준 모프 이름(まばたき)을 우선 찾고, 없으면 후보 목록을 훑는다.
    /// 이름을 못 찾으면 인스펙터에서 인덱스를 직접 지정할 수 있다.
    ///
    /// 깜빡임이 없으면 캐릭터는 즉시 인형처럼 보인다. 시선 추적보다 비용이 낮으면서
    /// 체감 효과는 비슷하게 크다.
    /// </summary>
    [DisallowMultipleComponent]
    public class EyeBlink : MonoBehaviour
    {
        [Header("대상")]
        [Tooltip("얼굴 블렌드셰이프를 가진 렌더러. 비워두면 자식에서 まばたき를 가진 것을 찾는다.")]
        [SerializeField] private SkinnedMeshRenderer faceRenderer;

        [Tooltip("깜빡임 블렌드셰이프 이름. 비워두면 아래 후보 목록으로 찾는다.")]
        [SerializeField] private string blendShapeName = "まばたき";

        [Tooltip("이름으로 못 찾을 때 쓸 인덱스. -1이면 사용하지 않음.")]
        [SerializeField] private int blendShapeIndexOverride = -1;

        [Header("간격 (초)")]
        [SerializeField] private float minInterval = 2.8f;
        [SerializeField] private float maxInterval = 6.5f;

        [Header("동작 (초)")]
        [Tooltip("눈을 감는 데 걸리는 시간. 사람은 감는 게 뜨는 것보다 빠르다.")]
        [SerializeField] private float closeDuration = 0.055f;
        [Tooltip("감은 상태를 유지하는 시간.")]
        [SerializeField] private float holdDuration = 0.035f;
        [Tooltip("눈을 뜨는 데 걸리는 시간.")]
        [SerializeField] private float openDuration = 0.11f;

        [Header("변주")]
        [Tooltip("두 번 연속 깜빡일 확률. 실제 사람에게서 흔히 나타난다.")]
        [Range(0f, 1f)][SerializeField] private float doubleBlinkChance = 0.18f;

        [Tooltip("완전히 감기지 않는 얕은 깜빡임의 확률.")]
        [Range(0f, 1f)][SerializeField] private float halfBlinkChance = 0.12f;

        [Tooltip("최대 가중치. 100이 완전히 감은 상태다.")]
        [Range(0f, 100f)][SerializeField] private float fullWeight = 100f;

        // MMD / VRM / 일반 FBX에서 흔한 이름들. 위에서부터 찾는다.
        private static readonly string[] NameCandidates =
        {
            "まばたき", "まばたき2", "Blink", "blink", "eye_close", "EyeBlink",
            "Fcl_EYE_Close", "vrc.blink_left", "A_Blink", "Wink",
        };

        private int shapeIndex = -1;
        private float timer;
        private bool suppressed;
        private float externalWeight = -1f;   // 0 이상이면 외부(표정 시스템)가 값을 점유한 상태

        /// <summary>표정 전환이나 컷신 중에 깜빡임을 멈춘다.</summary>
        public bool Suppressed
        {
            get => suppressed;
            set => suppressed = value;
        }

        /// <summary>깜빡임이 실제로 붙었는지. 셋업 검증용.</summary>
        public bool IsReady => faceRenderer != null && shapeIndex >= 0;

        /// <summary>
        /// 표정 시스템이 눈 가중치를 직접 잡아야 할 때 쓴다(예: 웃으며 눈 감기).
        /// -1을 넘기면 다시 깜빡임에 제어권을 돌려준다.
        /// </summary>
        public void OverrideWeight(float weight)
        {
            externalWeight = weight;
            if (weight >= 0f && IsReady)
                faceRenderer.SetBlendShapeWeight(shapeIndex, Mathf.Clamp(weight, 0f, 100f));
        }

        /// <summary>지금 즉시 한 번 깜빡인다. 놀람 연출 등에 쓴다.</summary>
        public void BlinkNow()
        {
            if (!IsReady) return;
            StopAllCoroutines();
            StartCoroutine(BlinkRoutine(false));
            ScheduleNext();
        }

        private void Awake()
        {
            Resolve();
            ScheduleNext();
        }

        private void Resolve()
        {
            if (faceRenderer == null)
                faceRenderer = FindRendererWithBlink();

            if (faceRenderer == null || faceRenderer.sharedMesh == null)
            {
                Debug.LogWarning($"[EyeBlink] {name}: 블렌드셰이프를 가진 SkinnedMeshRenderer를 찾지 못했다.", this);
                return;
            }

            shapeIndex = ResolveIndex(faceRenderer);

            if (shapeIndex < 0)
            {
                Debug.LogWarning($"[EyeBlink] {name}: 깜빡임 블렌드셰이프를 찾지 못했다. " +
                                 "컨텍스트 메뉴의 'Log BlendShape Names'로 이름을 확인할 것.", this);
            }
        }

        private int ResolveIndex(SkinnedMeshRenderer smr)
        {
            Mesh mesh = smr.sharedMesh;

            if (!string.IsNullOrEmpty(blendShapeName))
            {
                int i = mesh.GetBlendShapeIndex(blendShapeName);
                if (i >= 0) return i;
            }

            foreach (string candidate in NameCandidates)
            {
                int i = mesh.GetBlendShapeIndex(candidate);
                if (i >= 0) return i;
            }

            // 부분 일치로 한 번 더 시도한다. FBX 변환기가 접두사를 붙이는 경우가 있다.
            for (int i = 0; i < mesh.blendShapeCount; i++)
            {
                string n = mesh.GetBlendShapeName(i);
                if (n.Contains("まばたき") || n.Contains("Blink") || n.Contains("blink"))
                    return i;
            }

            if (blendShapeIndexOverride >= 0 && blendShapeIndexOverride < mesh.blendShapeCount)
                return blendShapeIndexOverride;

            return -1;
        }

        private SkinnedMeshRenderer FindRendererWithBlink()
        {
            var renderers = GetComponentsInChildren<SkinnedMeshRenderer>(true);
            SkinnedMeshRenderer fallback = null;

            foreach (var r in renderers)
            {
                if (r.sharedMesh == null || r.sharedMesh.blendShapeCount == 0) continue;
                fallback ??= r;
                if (ResolveIndex(r) >= 0) return r;
            }
            return fallback;
        }

        private void ScheduleNext()
        {
            timer = Random.Range(minInterval, maxInterval);
        }

        private void Update()
        {
            if (!IsReady || suppressed || externalWeight >= 0f) return;

            timer -= Time.deltaTime;
            if (timer > 0f) return;

            StartCoroutine(BlinkRoutine(Random.value < doubleBlinkChance));
            ScheduleNext();
        }

        private System.Collections.IEnumerator BlinkRoutine(bool doubled)
        {
            int repeats = doubled ? 2 : 1;

            for (int n = 0; n < repeats; n++)
            {
                // 두 번째 깜빡임은 살짝 얕게. 그래야 기계적으로 안 보인다.
                float peak = fullWeight;
                if (n > 0) peak *= 0.82f;
                else if (Random.value < halfBlinkChance) peak *= Random.Range(0.55f, 0.75f);

                yield return Ramp(0f, peak, closeDuration);

                if (holdDuration > 0f)
                    yield return new WaitForSeconds(holdDuration);

                yield return Ramp(peak, 0f, openDuration);

                if (n < repeats - 1)
                    yield return new WaitForSeconds(Random.Range(0.07f, 0.13f));
            }

            SetWeight(0f);
        }

        private System.Collections.IEnumerator Ramp(float from, float to, float duration)
        {
            if (duration <= 0f)
            {
                SetWeight(to);
                yield break;
            }

            float t = 0f;
            while (t < duration)
            {
                // 외부에서 제어권을 가져가면 즉시 중단한다.
                if (suppressed || externalWeight >= 0f) yield break;

                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                SetWeight(Mathf.Lerp(from, to, k * k * (3f - 2f * k))); // smoothstep
                yield return null;
            }
            SetWeight(to);
        }

        private void SetWeight(float w)
        {
            if (!IsReady) return;
            faceRenderer.SetBlendShapeWeight(shapeIndex, Mathf.Clamp(w, 0f, 100f));
        }

        [ContextMenu("Log BlendShape Names")]
        private void LogBlendShapeNames()
        {
            var renderers = GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var sb = new System.Text.StringBuilder();

            foreach (var r in renderers)
            {
                if (r.sharedMesh == null) continue;
                int count = r.sharedMesh.blendShapeCount;
                if (count == 0) continue;

                sb.AppendLine($"── {r.name} ({count}개)");
                for (int i = 0; i < count; i++)
                    sb.AppendLine($"   [{i}] {r.sharedMesh.GetBlendShapeName(i)}");
            }

            Debug.Log(sb.Length > 0 ? sb.ToString() : $"[EyeBlink] {name}: 블렌드셰이프가 없다.", this);
        }
    }
}
