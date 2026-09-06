using System.Collections.Generic;
using UnityEngine;

namespace VRProject.Character
{
    /// <summary>
    /// 머리카락·코트처럼 애니메이션이 건드리지 않는 본을 관성과 중력으로 흔든다.
    ///
    /// 체인의 '뿌리'만 지정하면 자식을 따라 내려가며 마디를 스스로 찾는다.
    /// 이 모델은 흔들 본이 45개쯤 되는데, 마디마다 컴포넌트를 붙이는 방식이면
    /// 손이 너무 많이 간다.
    ///
    /// 계산은 베를레(Verlet) 방식이다. 마디 끝점의 위치를 직접 적분하고,
    /// 그 결과를 바라보도록 본을 회전시킨다. 각도를 직접 적분하는 것보다
    /// 길이 고정과 각도 제한을 걸기 쉽다.
    ///
    /// 실행 순서가 중요하다.
    ///   Animator(포즈) → CharacterGaze(0, 목·머리) → TwistBoneFollower(100, 팔)
    ///   → 이 컴포넌트(200, 머리카락)
    /// 머리카락은 Head의 자식이라, 고개가 돌아간 뒤에 계산해야 따라 돈다.
    /// LateUpdate끼리의 순서는 지정하지 않으면 보장되지 않으므로 맨 뒤로 못박는다.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(200)]
    [AddComponentMenu("VRProject/Spring Bones")]
    public class SpringBones : MonoBehaviour
    {
        /// <summary>
        /// 머리카락이 파고들지 못하게 막는 구.
        ///
        /// 실제 메시가 아니라 구로 대신한다. 옷 메시와 정확히 판정하려면 비싸고,
        /// 어깨·가슴처럼 둥근 부위는 구 몇 개로 충분히 비슷해진다.
        /// 기준 본에 붙어 있으므로 캐릭터가 움직이면 같이 따라간다.
        /// </summary>
        [System.Serializable]
        public class 충돌구
        {
            [Tooltip("이 구가 따라다닐 본. 보통 Chest나 Left/Right shoulder.")]
            public Transform 기준;

            [Tooltip("기준 본에서의 로컬 오프셋.")]
            public Vector3 오프셋;

            [Tooltip("반지름(기준 본의 로컬 단위). 월드 크기는 본 스케일에 맞춰 환산된다.")]
            public float 반지름 = 0.1f;

            [Tooltip("이 구가 속한 머리카락 체인의 뿌리. 비어 있으면 몸통 충돌구다.\n\n" +
                     "같은 체인의 마디는 이 구를 무시한다. 자기가 자기 구를 밀어내면 " +
                     "본이 스스로를 튕겨내며 발산한다.")]
            public Transform 소속체인;

            public bool 유효 => 기준 != null && 반지름 > 0f;

            public Vector3 중심 => 기준.TransformPoint(오프셋);

            /// <summary>월드 기준 반지름. 이 모델처럼 루트가 0.1배로 줄어 있어도 맞게 나온다.</summary>
            public float 월드반지름
            {
                get
                {
                    Vector3 s = 기준.lossyScale;
                    return 반지름 * Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));
                }
            }
        }

        /// <summary>시뮬레이션하는 마디 하나.</summary>
        private class 마디
        {
            public Transform 본;
            public Transform 체인뿌리;   // 자기 체인의 충돌구를 걸러내는 데 쓴다
            public Quaternion 기본회전;   // 애니메이션이 안 건드리므로 이게 '자연스러운' 자세다
            public Vector3 로컬방향;      // 본 기준으로 끝점이 어느 쪽인지
            public float 길이;
            public Vector3 끝점;          // 월드. 이게 실제로 적분되는 값
            public Vector3 이전끝점;
        }

        [Header("체인")]
        [Tooltip("흔들 체인의 첫 마디. 자식은 자동으로 따라간다.\n" +
                 "컨텍스트 메뉴의 '머리카락 체인 자동 할당'을 쓰면 편하다.")]
        [SerializeField] private Transform[] 체인뿌리;

        [Tooltip("한 체인에서 따라갈 최대 마디 수. 가지가 갈라지면 첫 자식만 따라간다.")]
        [Range(1, 16)][SerializeField] private int 최대마디 = 8;

        [Header("물리")]
        [Tooltip("항상 걸리는 힘(m/s²). Y를 음수로 두면 아래로 처진다.\n" +
                 "실제 중력(-9.8)은 짧은 머리카락엔 너무 세다. -2 안팎이 자연스럽다.")]
        [SerializeField] private Vector3 중력 = new Vector3(0f, -2f, 0f);

        [Tooltip("제자리로 돌아가려는 힘. 높을수록 뻣뻣하다.\n" +
                 "0에 가까우면 흐물거리고, 1이면 거의 안 흔들린다.")]
        [Range(0f, 1f)][SerializeField] private float 강성 = 0.12f;

        [Tooltip("관성이 사라지는 정도. 높을수록 빨리 멈춘다.\n" +
                 "낮추면 오래 출렁이는데, 너무 낮으면 진동이 안 잦아든다.")]
        [Range(0f, 1f)][SerializeField] private float 감쇠 = 0.18f;

        [Tooltip("제자리에서 이만큼(도) 이상 벌어지지 않는다.\n" +
                 "고개를 빨리 돌릴 때 머리카락이 얼굴을 뚫고 나가는 것을 막는다.")]
        [Range(0f, 90f)][SerializeField] private float 최대각도 = 40f;

        [Header("충돌 — 어깨·가슴 위에 얹히게 한다")]
        [Tooltip("머리카락 끝이 파고들지 못하게 막는 구(球)들.\n" +
                 "컨텍스트 메뉴의 '충돌구 자동 배치'로 시작점을 잡을 수 있다.")]
        [SerializeField] private List<충돌구> 충돌구들 = new List<충돌구>();

        [Header("안정성")]
        [Tooltip("한 프레임에 캐릭터가 이만큼(m) 넘게 움직이면 순간이동으로 보고\n" +
                 "머리카락을 제자리에 다시 붙인다.\n\n" +
                 "VR 텔레포트나 씬 시작 순간에 머리카락이 뒤로 길게 날리는 것을 막는다.")]
        [Range(0.05f, 5f)][SerializeField] private float 순간이동_기준 = 0.4f;

        [Tooltip("프레임이 튀었을 때 쓸 시간 상한(초).\n" +
                 "에디터에서 잠깐 멈췄다 재개하면 dt가 커져서 머리카락이 폭발한다.")]
        [Range(0.01f, 0.1f)][SerializeField] private float 최대_시간간격 = 0.033f;

        private readonly List<마디> 마디들 = new List<마디>();
        private Vector3 이전위치;
        private bool 준비됨;

        private void OnEnable()
        {
            만들기();
            제자리로();
        }

        private void OnDisable() => 준비됨 = false;

        /// <summary>
        /// 체인 뿌리에서 자식을 따라 내려가며 마디를 만든다.
        ///
        /// 끝 마디(자식 없음)도 흔들어야 머리카락 끝이 살아난다. 자식이 없으면
        /// 바로 앞 마디와 같은 방향·길이로 가상의 끝점을 만들어 쓴다.
        /// 본의 로컬 축이 모델마다 달라서, 축을 짐작하는 것보다 이쪽이 안전하다.
        /// </summary>
        private void 만들기()
        {
            마디들.Clear();
            준비됨 = false;

            if (체인뿌리 == null || 체인뿌리.Length == 0)
            {
                Debug.LogWarning($"[SpringBones] {name}: 체인 뿌리가 비어 있다. " +
                                 "컨텍스트 메뉴의 '머리카락 체인 자동 할당'을 쓸 것.", this);
                return;
            }

            foreach (Transform 뿌리 in 체인뿌리)
            {
                if (뿌리 == null) continue;

                Transform 현재 = 뿌리;
                마디 앞마디 = null;

                for (int i = 0; i < 최대마디 && 현재 != null; i++)
                {
                    Transform 자식 = 현재.childCount > 0 ? 현재.GetChild(0) : null;

                    Vector3 로컬방향;
                    float 길이;

                    if (자식 != null)
                    {
                        Vector3 로컬 = 현재.InverseTransformPoint(자식.position);
                        길이 = 로컬.magnitude;
                        if (길이 < 1e-5f) break;              // 겹쳐 있는 본은 흔들 수 없다
                        로컬방향 = 로컬 / 길이;
                    }
                    else
                    {
                        if (앞마디 == null) break;            // 마디가 하나뿐이면 흔들 것이 없다
                        로컬방향 = 앞마디.로컬방향;            // 앞 마디를 그대로 이어 쓴다
                        길이 = 앞마디.길이;
                    }

                    var m = new 마디
                    {
                        본 = 현재,
                        체인뿌리 = 뿌리,
                        기본회전 = 현재.localRotation,
                        로컬방향 = 로컬방향,
                        길이 = 길이,
                    };
                    m.끝점 = 현재.TransformPoint(로컬방향 * 길이);
                    m.이전끝점 = m.끝점;

                    마디들.Add(m);
                    앞마디 = m;
                    현재 = 자식;
                }
            }

            준비됨 = 마디들.Count > 0;
            이전위치 = transform.position;
        }

        /// <summary>관성을 버리고 머리카락을 지금 자세에 딱 붙인다.</summary>
        public void 제자리로()
        {
            if (!준비됨) return;

            foreach (마디 m in 마디들)
            {
                m.본.localRotation = m.기본회전;
                m.끝점 = m.본.TransformPoint(m.로컬방향 * m.길이);
                m.이전끝점 = m.끝점;
            }
            이전위치 = transform.position;
        }

        private void LateUpdate()
        {
            if (!준비됨) return;

            // 순간이동(텔레포트, 씬 시작)에는 시뮬레이션을 리셋한다.
            // 안 그러면 머리카락이 이전 자리에 남아 길게 늘어졌다가 따라온다.
            float 이동량 = Vector3.Distance(transform.position, 이전위치);
            이전위치 = transform.position;
            if (이동량 > 순간이동_기준)
            {
                제자리로();
                return;
            }

            float dt = Mathf.Min(Time.deltaTime, 최대_시간간격);
            if (dt <= 0f) return;

            Vector3 중력항 = 중력 * (dt * dt);

            // 부모부터 순서대로 푼다. 마디들은 만들 때 이미 뿌리→끝 순서로 담겼고,
            // 앞 마디를 확정한 뒤에 뒤 마디를 풀어야 체인이 딸려온다.
            foreach (마디 m in 마디들)
            {
                // 애니메이션이 이 본을 건드리지 않으므로, 기본 자세가 곧 '돌아갈 자리'다.
                // 매 프레임 되돌려놓고 계산해야 우리가 준 회전이 누적되지 않는다.
                m.본.localRotation = m.기본회전;

                Vector3 뿌리 = m.본.position;
                Vector3 자연끝점 = m.본.TransformPoint(m.로컬방향 * m.길이);

                Vector3 관성 = (m.끝점 - m.이전끝점) * (1f - 감쇠);
                Vector3 다음 = m.끝점 + 관성 + 중력항 + (자연끝점 - m.끝점) * 강성;

                // 길이는 늘어나면 안 된다. 방향만 살리고 길이는 되돌린다.
                Vector3 방향 = 다음 - 뿌리;
                if (방향.sqrMagnitude < 1e-8f) 방향 = 자연끝점 - 뿌리;
                다음 = 뿌리 + 방향.normalized * m.길이;

                다음 = 각도제한(뿌리, 자연끝점, 다음);

                // 충돌은 맨 마지막에 민다.
                //
                // 밀어내면 뿌리에서의 거리가 조금 어긋나는데, 본은 길이를 쓰지 않고
                // '방향'만 써서 회전시키므로(아래 FromToRotation) 겉보기에는 문제가 없다.
                // 순서를 앞에 두면 길이 보정이 다시 안쪽으로 밀어 넣어서 파고든다.
                다음 = 밀어내기(다음, m.체인뿌리);

                m.이전끝점 = m.끝점;
                m.끝점 = 다음;

                // 본이 끝점을 바라보게 돌린다.
                Vector3 현재방향 = 자연끝점 - 뿌리;
                Vector3 목표방향 = m.끝점 - 뿌리;
                if (현재방향.sqrMagnitude > 1e-8f && 목표방향.sqrMagnitude > 1e-8f)
                {
                    m.본.rotation = Quaternion.FromToRotation(현재방향, 목표방향) * m.본.rotation;
                }
            }
        }

        /// <summary>
        /// 제자리에서 최대각도를 넘지 않게 끝점을 되돌린다.
        ///
        /// 이게 없으면 고개를 빨리 돌릴 때 머리카락이 관성으로 크게 뒤처져
        /// 얼굴이나 어깨를 뚫고 들어간다. 충돌 판정 없이 뚫림을 막는 가장 싼 방법이다.
        /// </summary>
        private Vector3 각도제한(Vector3 뿌리, Vector3 자연끝점, Vector3 목표)
        {
            Vector3 기준 = 자연끝점 - 뿌리;
            Vector3 현재 = 목표 - 뿌리;

            if (기준.sqrMagnitude < 1e-8f || 현재.sqrMagnitude < 1e-8f) return 목표;
            if (Vector3.Angle(기준, 현재) <= 최대각도) return 목표;

            Vector3 제한 = Vector3.RotateTowards(
                기준.normalized, 현재.normalized, 최대각도 * Mathf.Deg2Rad, 0f);

            return 뿌리 + 제한 * 현재.magnitude;
        }

        /// <summary>
        /// 충돌구 안으로 들어간 끝점을 표면으로 밀어낸다.
        ///
        /// 이게 있어야 머리카락이 어깨와 옷 '위에 얹힌' 것처럼 보인다.
        /// 각도 제한만으로는 파고드는 것을 막지 못한다. 각도는 제자리에서
        /// 얼마나 벌어졌는지만 볼 뿐, 몸이 어디 있는지는 모르기 때문이다.
        /// </summary>
        private Vector3 밀어내기(Vector3 끝점, Transform 내체인)
        {
            if (충돌구들 == null) return 끝점;

            foreach (충돌구 c in 충돌구들)
            {
                if (c == null || !c.유효) continue;

                // 자기 체인에 붙은 구는 건너뛴다.
                // 자기가 자기를 밀어내면 매 프레임 튕겨나가며 발산한다.
                if (c.소속체인 != null && c.소속체인 == 내체인) continue;

                Vector3 중심 = c.중심;
                float r = c.월드반지름;

                Vector3 d = 끝점 - 중심;
                float 거리 = d.magnitude;

                if (거리 >= r) continue;

                // 정확히 중심에 겹치면 방향을 알 수 없다. 위로 밀어 낸다.
                끝점 = 거리 > 1e-6f ? 중심 + d * (r / 거리) : 중심 + Vector3.up * r;
            }
            return 끝점;
        }

        // ── 셋업 도우미 ─────────────────────────────────────────────

        /// <summary>
        /// Head 아래에서 Hair로 시작하는 본을 찾아 체인 뿌리로 넣는다.
        ///
        /// 마디가 하나뿐인 것(정수리 한 칸짜리 등)은 흔들어도 티가 안 나고
        /// 계산만 늘어나므로 건너뛴다.
        /// </summary>
        [ContextMenu("머리카락 체인 자동 할당")]
        public void 머리카락_자동할당() => 자동할당("Hair", 2);

        /// <summary>코트까지 흔든다. 마디가 28개 늘어나므로 VR에서는 프레임을 보고 결정할 것.</summary>
        [ContextMenu("머리카락 + 코트 자동 할당")]
        public void 코트포함_자동할당() => 자동할당(null, 2);

        private void 자동할당(string 접두사, int 최소마디)
        {
            var 뿌리들 = new List<Transform>();
            var 이름들 = new List<string>();

            foreach (Transform t in GetComponentsInChildren<Transform>(true))
            {
                if (!후보인가(t.name, 접두사)) continue;

                // 부모도 후보면 이건 체인의 중간이다. 뿌리만 담는다.
                if (t.parent != null && 후보인가(t.parent.name, 접두사)) continue;

                if (마디수(t) < 최소마디) continue;

                뿌리들.Add(t);
                이름들.Add($"{t.name}({마디수(t)}마디)");
            }

            if (뿌리들.Count == 0)
            {
                Debug.LogWarning($"[SpringBones] {name}: 흔들 만한 체인을 찾지 못했다. " +
                                 "본 이름을 확인할 것.", this);
                return;
            }

            체인뿌리 = 뿌리들.ToArray();
            만들기();
            제자리로();

            Debug.Log($"[SpringBones] 체인 {뿌리들.Count}개 / 마디 {마디들.Count}개를 잡았다.\n  " +
                      string.Join("\n  ", 이름들), this);
        }

        /// <summary>
        /// 머리·어깨·가슴에 충돌구를 얹는다.
        ///
        /// 크기는 목~머리 사이 거리를 자로 삼아 잡는다. 이 모델은 루트가 0.1배로
        /// 줄어 있어서 고정값을 쓰면 전부 어긋난다. 뼈 사이 거리로 재면 스케일과
        /// 무관하게 대략 맞는 값이 나온다.
        ///
        /// 어디까지나 시작점이다. 씬뷰의 하늘색 구를 보면서 다듬을 것.
        /// </summary>
        [ContextMenu("충돌구 자동 배치")]
        public void 충돌구_자동배치()
        {
            Transform 목 = 본찾기("Neck", "首");
            Transform 머리 = 본찾기("Head", "頭");
            Transform 가슴 = 본찾기("Chest", "上半身2", "上半身");
            Transform 어깨L = 본찾기("Left shoulder", "左肩", "Shoulder_L");
            Transform 어깨R = 본찾기("Right shoulder", "右肩", "Shoulder_R");

            if (목 == null || 머리 == null)
            {
                Debug.LogWarning($"[SpringBones] {name}: Neck/Head 본을 찾지 못해 크기를 잴 수 없다. " +
                                 "충돌구를 직접 넣을 것.", this);
                return;
            }

            // 목~머리 거리를 1자로 삼는다.
            float 자 = Vector3.Distance(목.position, 머리.position);
            if (자 < 1e-6f) return;

            충돌구들 ??= new List<충돌구>();

            // 몸통 것만 갈아끼운다. 머리카락 체인에 붙여둔 구는 건드리지 않는다.
            충돌구들.RemoveAll(c => c == null || c.소속체인 == null);

            // 머리: 두피에 파묻히지 않게. 머리 본 위쪽에 크게 하나.
            더하기(머리, 자 * 1.0f, new Vector3(0f, 자 * 0.6f, 0f), null);

            // 가슴: 앞머리·옆머리가 얹히는 몸통.
            if (가슴 != null) 더하기(가슴, 자 * 1.5f, new Vector3(0f, 자 * 0.5f, 0f), null);

            // 어깨: 옆머리와 뒷머리가 실제로 '안착'하는 지점.
            if (어깨L != null) 더하기(어깨L, 자 * 0.8f, Vector3.zero, null);
            if (어깨R != null) 더하기(어깨R, 자 * 0.8f, Vector3.zero, null);

            Debug.Log($"[SpringBones] 몸통 충돌구를 배치했다 (기준 길이 {자:F3}m). " +
                      $"총 {충돌구들.Count}개. 씬뷰에서 하늘색 구를 보며 다듬을 것.", this);
        }

        /// <summary>
        /// 머리카락 체인의 마디마다 구를 얹어 굵기를 준다.
        ///
        /// 몸통 구만으로는 부족하다. 그건 어깨·가슴 같은 몇 군데만 막아줄 뿐이라,
        /// 옆머리처럼 긴 다발 사이로 다른 머리카락이 그대로 통과한다.
        /// 다발 자체에 굵기를 주면 서로 비집고 들어가지 않는다.
        ///
        /// 자기 체인의 구는 무시하도록 소속을 적어둔다. 안 적으면 각 마디가
        /// 자기 몸통을 밀어내며 발산한다.
        /// </summary>
        [ContextMenu("머리카락 충돌구 자동 배치")]
        public void 머리카락_충돌구_자동배치()
        {
            if (체인뿌리 == null || 체인뿌리.Length == 0)
            {
                Debug.LogWarning($"[SpringBones] {name}: 체인 뿌리가 비어 있다. " +
                                 "'머리카락 체인 자동 할당'을 먼저 실행할 것.", this);
                return;
            }

            충돌구들 ??= new List<충돌구>();

            // 머리카락 것만 갈아끼운다. 몸통 구는 그대로 둔다.
            충돌구들.RemoveAll(c => c == null || c.소속체인 != null);

            int 추가 = 0;

            foreach (Transform 뿌리 in 체인뿌리)
            {
                if (뿌리 == null) continue;

                Transform 현재 = 뿌리;
                for (int i = 0; i < 최대마디 && 현재 != null; i++)
                {
                    Transform 자식 = 현재.childCount > 0 ? 현재.GetChild(0) : null;
                    if (자식 == null) break;   // 끝 마디는 뒤쪽 마디의 구가 이미 덮는다

                    float 길이 = Vector3.Distance(현재.position, 자식.position);
                    if (길이 > 1e-5f)
                    {
                        // 마디 한가운데에, 마디 길이의 절반보다 조금 작게.
                        // 이보다 크면 이웃 다발끼리 항상 겹쳐 머리카락이 부풀어 보인다.
                        Vector3 중점 = (현재.position + 자식.position) * 0.5f;
                        더하기(현재, 길이 * 0.4f, 중점 - 현재.position, 뿌리);
                        추가++;
                    }
                    현재 = 자식;
                }
            }

            Debug.Log($"[SpringBones] 머리카락 충돌구 {추가}개를 배치했다. " +
                      $"총 {충돌구들.Count}개.\n" +
                      "굵어 보이면 각 구의 반지름을 줄이고, 아직 뚫리면 키울 것.", this);
        }

        /// <summary>월드 크기로 받은 반지름·오프셋을 기준 본의 로컬 단위로 환산해 담는다.</summary>
        private void 더하기(Transform 기준, float 월드반지름, Vector3 월드오프셋, Transform 소속체인)
        {
            Vector3 s = 기준.lossyScale;
            float 배율 = Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));
            if (배율 < 1e-6f) return;

            충돌구들.Add(new 충돌구
            {
                기준 = 기준,
                반지름 = 월드반지름 / 배율,
                오프셋 = 기준.InverseTransformVector(월드오프셋),
                소속체인 = 소속체인,
            });
        }

        /// <summary>이름으로 본을 찾는다. 같은 이름이 여럿이면 Armature 아래를 우선한다.</summary>
        private Transform 본찾기(params string[] 후보들)
        {
            Transform[] 전부 = GetComponentsInChildren<Transform>(true);

            foreach (string 이름 in 후보들)
            {
                Transform 예비 = null;
                foreach (Transform t in 전부)
                {
                    if (t.name != 이름) continue;
                    for (Transform p = t; p != null; p = p.parent)
                        if (p.name == "Armature") return t;
                    예비 ??= t;
                }
                if (예비 != null) return 예비;
            }
            return null;
        }

        private static bool 후보인가(string 이름, string 접두사)
        {
            if (접두사 != null) return 이름.StartsWith(접두사);

            // 접두사를 안 주면 MMD 모델의 흔들 본 계열을 통째로 본다.
            return 이름.StartsWith("Hair") || 이름.StartsWith("Coat")
                || 이름.StartsWith("ACoat") || 이름.StartsWith("Skirt")
                || 이름.StartsWith("Tbow") || 이름.StartsWith("Collar");
        }

        private int 마디수(Transform 뿌리)
        {
            int n = 0;
            for (Transform t = 뿌리; t != null && n < 최대마디; t = t.childCount > 0 ? t.GetChild(0) : null)
                n++;
            return n;
        }

        private void OnValidate()
        {
            // 인스펙터에서 체인을 바꾸면 다시 만들어야 한다.
            if (Application.isPlaying && isActiveAndEnabled) { 만들기(); 제자리로(); }
        }

        private void OnDrawGizmosSelected()
        {
            // 충돌구를 먼저 그린다. 머리카락이 어디에 얹히는지 눈으로 맞추는 용도다.
            if (충돌구들 != null)
            {
                foreach (충돌구 c in 충돌구들)
                {
                    if (c == null || !c.유효) continue;

                    // 몸통은 하늘색, 머리카락 다발은 노란색으로 구분해 그린다.
                    Gizmos.color = c.소속체인 == null
                        ? new Color(0.3f, 0.8f, 1f, 0.5f)
                        : new Color(1f, 0.9f, 0.3f, 0.35f);

                    Gizmos.DrawWireSphere(c.중심, c.월드반지름);
                }
            }

            if (체인뿌리 == null) return;

            foreach (Transform 뿌리 in 체인뿌리)
            {
                if (뿌리 == null) continue;

                Transform t = 뿌리;
                for (int i = 0; i < 최대마디 && t != null; i++)
                {
                    Transform 자식 = t.childCount > 0 ? t.GetChild(0) : null;

                    Gizmos.color = new Color(1f, 0.4f, 0.7f, 0.9f);
                    Gizmos.DrawWireSphere(t.position, 0.008f);

                    if (자식 != null)
                    {
                        Gizmos.color = new Color(1f, 0.4f, 0.7f, 0.5f);
                        Gizmos.DrawLine(t.position, 자식.position);
                    }
                    t = 자식;
                }
            }
        }
    }
}
