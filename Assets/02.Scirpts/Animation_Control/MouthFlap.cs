using System;
using System.Collections.Generic;
using UnityEngine;

namespace VRProject.Character
{
    /// <summary>
    /// 말하는 동안 입을 뻐끔거리게 한다.
    ///
    /// 음성 파형을 분석하는 진짜 립싱크가 아니라, 정해둔 입모양을 순서대로
    /// 돌리는 방식이다. 대사가 텍스트뿐인 비주얼노벨에서는 이걸로 충분하고
    /// 오디오 없이도 동작한다.
    ///
    /// 기본 순서는 あ(100) → お(50) → え(100)이다. 크게 벌리고, 살짝 오므리고,
    /// 다시 옆으로 벌리는 모양이라 한 바퀴 돌면 말하는 것처럼 보인다.
    ///
    /// <see cref="FacialExpression"/>과의 조율:
    ///   재생하는 동안 표정 컴포넌트의 '립싱크사용중'을 켜서 입 모프를 넘겨받는다.
    ///   넘겨받지 않으면 둘이 같은 모프를 매 프레임 덮어써서 입이 떨린다.
    ///   끝나면 입을 0으로 닫고 다시 돌려준다.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("VRProject/Mouth Flap")]
    public class MouthFlap : MonoBehaviour
    {
        [Serializable]
        public class 단계
        {
            [Tooltip("블렌드셰이프 이름. MMD 표준 모음(あ い う え お)을 쓴다.")]
            public string 모프 = "あ";

            [Range(0f, 100f)] public float 가중치 = 100f;
        }

        [Header("대상")]
        [Tooltip("블렌드셰이프를 가진 렌더러. 비워두면 자식에서 찾는다.")]
        [SerializeField] private SkinnedMeshRenderer 렌더러;

        [Tooltip("입을 넘겨받을 표정 컴포넌트. 비워두면 자신과 자식에서 찾는다.")]
        [SerializeField] private FacialExpression 표정;

        [Header("입모양 순서")]
        [Tooltip("이 순서대로 반복한다. 위에서부터 하나씩.")]
        [SerializeField]
        private List<단계> 순서 = new List<단계>
        {
            new 단계 { 모프 = "あ", 가중치 = 100f },
            new 단계 { 모프 = "お", 가중치 = 50f },
            new 단계 { 모프 = "え", 가중치 = 100f },
        };

        [Header("속도")]
        [Tooltip("입모양 하나를 유지하는 시간(초). 짧을수록 빠르게 재잘거린다.")]
        [Range(0.03f, 0.5f)][SerializeField] private float 단계시간 = 0.12f;

        [Tooltip("입이 벌어지고 닫히는 속도(초당 가중치).\n" +
                 "낮추면 뭉근하게 움직이고, 높이면 딱딱 끊어진다.")]
        [Range(100f, 2000f)][SerializeField] private float 전환속도 = 700f;

        [Tooltip("아무리 길어도 이 시간(초)이 지나면 멈춘다.\n" +
                 "대사가 길거나 멈춤 신호를 놓쳐도 입이 영영 움직이는 일이 없게 한다.")]
        [Range(0.5f, 30f)][SerializeField] private float 최대재생시간 = 5f;

        [Header("진단")]
        [Tooltip("입이 안 움직여 보일 때 켠다.\n" +
                 "내가 쓴 값과 렌더러에 실제로 남은 값을 비교해서, 다른 컴포넌트가 " +
                 "덮어쓰고 있는지 콘솔에 알려준다. 평소에는 꺼둘 것.")]
        [SerializeField] private bool 진단로그 = false;

        // 런타임 상태
        private int[] 모프인덱스;      // 순서와 짝을 이루는 블렌드셰이프 인덱스
        private float[] 현재가중치;
        private int 단계번호;
        private float 단계경과;
        private float 전체경과;
        private bool 재생중;
        private bool 닫는중;           // 멈춘 뒤 입을 0으로 되돌리는 동안

        /// <summary>지금 입이 움직이고 있는지.</summary>
        public bool 재생중인가 => 재생중;

        private void Awake() => Resolve();

        /// <summary>
        /// 입모양 재생을 시작한다. 이미 재생 중이면 처음부터 다시 시작한다.
        ///
        /// 대사 한 줄마다 부르는 것을 전제로 하므로, 다시 부르면 타이머가 초기화되어
        /// 앞 대사에서 흘러온 재생시간이 다음 대사를 갉아먹지 않는다.
        /// </summary>
        public void 재생시작()
        {
            Resolve();
            if (모프인덱스 == null || 모프인덱스.Length == 0) return;

            단계번호 = 0;
            단계경과 = 0f;
            전체경과 = 0f;
            닫는중 = false;
            재생중 = true;

            // 표정에게서 입을 넘겨받는다.
            if (표정 != null) 표정.립싱크사용중 = true;

            if (진단로그) 상태찍기("재생시작 직후");
        }

        /// <summary>
        /// 입모양 재생을 멈춘다. 곧바로 닫지 않고 부드럽게 0으로 되돌린 뒤
        /// 표정에게 입을 돌려준다. 즉시 끊으면 입이 벌어진 채로 툭 굳는다.
        /// </summary>
        public void 재생중지()
        {
            if (!재생중 && !닫는중) return;

            재생중 = false;
            닫는중 = true;
        }

        /// <summary>
        /// 순서에 적힌 모프 이름이 이 모델에 실제로 있는지 확인한다.
        ///
        /// 이름이 하나라도 틀리면 그 단계는 통째로 건너뛰어 입이 어색하게 움직인다.
        /// 셋업 도구가 붙인 직후에 이걸로 검사한다.
        /// </summary>
        public List<string> 없는모프찾기()
        {
            var 없음 = new List<string>();
            Resolve();

            if (렌더러 == null || 렌더러.sharedMesh == null)
            {
                없음.Add("(블렌드셰이프를 가진 SkinnedMeshRenderer를 찾지 못함)");
                return 없음;
            }

            if (순서 == null) return 없음;

            foreach (단계 s in 순서)
            {
                if (s == null || string.IsNullOrEmpty(s.모프)) continue;
                if (렌더러.sharedMesh.GetBlendShapeIndex(s.모프) < 0) 없음.Add(s.모프);
            }
            return 없음;
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

            if (표정 == null) 표정 = GetComponentInChildren<FacialExpression>(true);

            if (렌더러 == null || 렌더러.sharedMesh == null || 순서 == null) return;
            if (모프인덱스 != null && 모프인덱스.Length == 순서.Count) return;

            모프인덱스 = new int[순서.Count];
            현재가중치 = new float[순서.Count];

            for (int i = 0; i < 순서.Count; i++)
            {
                string 이름 = 순서[i]?.모프;
                모프인덱스[i] = string.IsNullOrEmpty(이름)
                    ? -1
                    : 렌더러.sharedMesh.GetBlendShapeIndex(이름);

                if (모프인덱스[i] < 0 && !string.IsNullOrEmpty(이름))
                {
                    Debug.LogWarning($"[MouthFlap] '{이름}' 블렌드셰이프를 찾지 못했다. " +
                                     "이 모델의 모프 이름을 확인할 것.", this);
                }
            }
        }

        private void LateUpdate()
        {
            if (!재생중 && !닫는중) return;
            if (모프인덱스 == null || 렌더러 == null) return;

            // 쓰기 전에, 지난 프레임에 쓴 값이 그대로 남아 있는지 본다.
            if (진단로그) 덮어쓰기검사();

            if (재생중)
            {
                전체경과 += Time.deltaTime;
                if (전체경과 >= 최대재생시간)
                {
                    재생중지();
                }
                else
                {
                    단계경과 += Time.deltaTime;
                    if (단계경과 >= 단계시간)
                    {
                        단계경과 -= 단계시간;
                        단계번호 = (단계번호 + 1) % 모프인덱스.Length;   // 순환

                        // 단계가 넘어갈 때만 찍는다. 매 프레임 찍으면 콘솔이 잠긴다.
                        if (진단로그) 상태찍기($"단계 {단계번호} (경과 {전체경과:F2}초)");
                    }
                }
            }

            바르기();
        }

        /// <summary>
        /// 지금 단계의 모프만 목표치로 올리고 나머지는 0으로 내린다.
        /// 전부 0에 닿으면 닫기가 끝난 것이므로 표정에게 입을 돌려준다.
        /// </summary>
        private void 바르기()
        {
            float 걸음 = 전환속도 * Time.deltaTime;
            bool 전부닫힘 = true;

            for (int i = 0; i < 모프인덱스.Length; i++)
            {
                int idx = 모프인덱스[i];
                if (idx < 0) continue;

                float 목표 = (재생중 && i == 단계번호) ? 순서[i].가중치 : 0f;

                현재가중치[i] = Mathf.MoveTowards(현재가중치[i], 목표, 걸음);
                렌더러.SetBlendShapeWeight(idx, 현재가중치[i]);

                if (현재가중치[i] > 0.01f) 전부닫힘 = false;
            }

            if (닫는중 && 전부닫힘)
            {
                닫는중 = false;
                if (표정 != null) 표정.립싱크사용중 = false;
            }
        }

        /// <summary>
        /// 지난 프레임에 내가 쓴 값이 렌더러에 그대로 남아 있는지 확인한다.
        ///
        /// 값이 달라져 있으면 다른 컴포넌트가 같은 모프를 덮어쓰고 있다는 뜻이다.
        /// 블렌드셰이프에는 Z축이나 그리는 순서 같은 개념이 없어서, 입이 안 보이는
        /// 원인은 결국 "누가 마지막에 썼는가" 하나뿐이다. 그걸 여기서 직접 잰다.
        /// </summary>
        private void 덮어쓰기검사()
        {
            for (int i = 0; i < 모프인덱스.Length; i++)
            {
                int idx = 모프인덱스[i];
                if (idx < 0) continue;

                float 실제 = 렌더러.GetBlendShapeWeight(idx);
                if (Mathf.Abs(실제 - 현재가중치[i]) <= 0.5f) continue;

                Debug.LogWarning(
                    $"[MouthFlap] '{순서[i].모프}'를 누가 덮어썼다. " +
                    $"내가 쓴 값 {현재가중치[i]:F1} → 지금 {실제:F1}. " +
                    "같은 모프를 만지는 다른 컴포넌트를 찾을 것.", this);
            }
        }

        /// <summary>지금 상태를 한 번에 찍는다. 무엇이 비어 있는지 눈으로 확인할 때 쓴다.</summary>
        [ContextMenu("입모양 상태 찍기")]
        public void 상태찍기() => 상태찍기("수동 확인");

        private void 상태찍기(string 시점)
        {
            Resolve();

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"[MouthFlap] {시점}");
            sb.AppendLine($"  렌더러: {(렌더러 == null ? "없음" : 렌더러.name)}" +
                          $"  메시: {(렌더러 == null || 렌더러.sharedMesh == null ? "없음" : 렌더러.sharedMesh.name)}");
            sb.AppendLine($"  표정: {(표정 == null ? "없음" : 표정.name)}" +
                          $"  립싱크사용중: {(표정 == null ? "-" : 표정.립싱크사용중.ToString())}");
            sb.AppendLine($"  재생중: {재생중}  닫는중: {닫는중}  단계번호: {단계번호}");

            if (모프인덱스 == null)
            {
                sb.AppendLine("  ※ 모프 인덱스가 비어 있다. 렌더러나 순서를 확인할 것.");
            }
            else
            {
                for (int i = 0; i < 모프인덱스.Length; i++)
                {
                    int idx = 모프인덱스[i];
                    string 실제 = (idx >= 0 && 렌더러 != null)
                        ? 렌더러.GetBlendShapeWeight(idx).ToString("F1")
                        : "-";
                    sb.AppendLine($"  [{i}] {순서[i].모프}  블렌드셰이프인덱스={idx}" +
                                  $"  내가쓴값={현재가중치[i]:F1}  렌더러실제값={실제}");
                }
            }

            Debug.Log(sb.ToString(), this);
        }

        private void OnDisable()
        {
            // 비활성화될 때 입을 벌린 채로 남겨두지 않는다.
            if (모프인덱스 != null && 렌더러 != null)
            {
                for (int i = 0; i < 모프인덱스.Length; i++)
                {
                    if (모프인덱스[i] < 0) continue;
                    현재가중치[i] = 0f;
                    렌더러.SetBlendShapeWeight(모프인덱스[i], 0f);
                }
            }

            재생중 = false;
            닫는중 = false;
            if (표정 != null) 표정.립싱크사용중 = false;
        }

        private void OnValidate()
        {
            // 순서를 인스펙터에서 늘리거나 줄이면 인덱스를 다시 찾아야 한다.
            모프인덱스 = null;
        }

        [ContextMenu("입모양 5초 시험 재생")]
        private void 시험재생() => 재생시작();
    }
}
