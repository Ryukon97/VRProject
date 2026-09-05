using UnityEngine;

namespace VRProject.Character
{
    /// <summary>
    /// 캐릭터가 플레이어를 걸어서 따라온다.
    ///
    /// 범위 판정은 인스펙터에 꽂아준 Collider로 한다.
    /// 타깃이 콜라이더 밖에 있으면 걸어오고, 안으로 들어오면 멈춰서 대기한다.
    ///
    /// 물리 트리거 이벤트(OnTriggerEnter/Exit)를 쓰지 않고 매 프레임
    /// Collider.ClosestPoint로 직접 판정한다. 이유는 두 가지다.
    ///   1. VR은 텔레포트로 순간이동한다. 트리거 이벤트는 이때 씹히는 경우가 있고,
    ///      Exit를 한 번 놓치면 캐릭터가 영영 멈춰 서 있게 된다.
    ///   2. ClosestPoint는 월드 스케일을 알아서 반영한다. 이 프리팹처럼
    ///      루트가 0.1배로 줄어 있어도 반지름을 따로 환산할 필요가 없다.
    ///
    /// 이동은 스크립트가 직접 한다(루트 모션 사용 안 함).
    /// 애니메이터에는 걷는 중인지만 bool로 알려준다.
    ///
    /// <see cref="CharacterGaze"/>와 같이 붙여도 안전하다. 이쪽은 Update에서
    /// 루트를 움직이고, Gaze는 LateUpdate에서 본만 건드리므로 서로 덮어쓰지 않는다.
    /// </summary>
    [DisallowMultipleComponent]
    public class CharacterFollow : MonoBehaviour
    {
        [Header("타깃")]
        [Tooltip("따라갈 대상. 비워두면 Camera.main(= XR Origin의 Main Camera)을 자동으로 찾는다.")]
        [SerializeField] private Transform target;

        [Header("멈춤 범위")]
        [Tooltip("이 콜라이더 안에 타깃이 들어오면 멈춘다.\n\n" +
                 "반드시 Is Trigger를 켤 것. 꺼두면 솔리드 벽이 되어 플레이어가 " +
                 "애초에 범위 안으로 들어올 수 없다.")]
        [SerializeField] private Collider rangeCollider;

        [Tooltip("멈춘 뒤 다시 걷기 시작하려면 콜라이더 표면에서 이만큼(m) 더 벗어나야 한다.\n\n" +
                 "0이면 경계선 위에서 걷기/멈춤이 매 프레임 뒤집히며 발이 덜덜 떨린다.")]
        [SerializeField] private float exitBuffer = 0.35f;

        [Header("이동")]
        [Tooltip("걷는 속도(m/s). 애니메이션 보폭과 어긋나면 발이 미끄러져 보이므로 같이 맞출 것.")]
        [SerializeField] private float moveSpeed = 1.2f;

        [Tooltip("몸이 도는 속도(도/초).")]
        [SerializeField] private float turnSpeed = 240f;

        [Tooltip("가감속(m/s^2). 낮을수록 뭉근하게 출발하고 멈춘다.")]
        [SerializeField] private float acceleration = 4f;

        [Tooltip("모델의 정면이 +Z가 아닐 때 보정하는 각도(도).\n\n" +
                 "뒷걸음질로 따라오면 180을 넣는다. MMD 변환 모델은 종종 이렇다.")]
        [SerializeField] private float forwardOffset;

        [Header("멈춰 있을 때")]
        [Tooltip("멈춘 상태에서도 몸을 돌려 타깃을 향하게 한다.\n\n" +
                 "끄면 고개만 돌아간다(CharacterGaze 담당). 플레이어가 등 뒤로 " +
                 "돌아가면 시선 가동범위를 넘겨 앞을 보게 되므로 켜두는 편이 자연스럽다.")]
        [SerializeField] private bool turnWhenIdle = true;

        [Tooltip("멈춘 상태에서 몸을 돌리는 속도(도/초). 걸을 때보다 느긋해야 자연스럽다.")]
        [SerializeField] private float idleTurnSpeed = 90f;

        [Tooltip("이 각도(도) 이상 틀어졌을 때만 몸을 돌린다.\n\n" +
                 "0으로 두면 플레이어가 조금만 움직여도 계속 미세하게 꼼지락거린다.")]
        [SerializeField] private float idleTurnThreshold = 45f;

        [Header("지면")]
        [Tooltip("바닥에 붙여서 걷게 한다. 평지라면 꺼두는 편이 안전하다.")]
        [SerializeField] private bool stickToGround;

        [SerializeField] private LayerMask groundMask = ~0;

        [Tooltip("발 위 이 높이(m)에서 아래로 레이를 쏜다. 경사나 턱의 높이보다 커야 한다.")]
        [SerializeField] private float groundProbeHeight = 1.5f;

        // 걷는 중인지 알려줄 bool 파라미터.
        //
        // 인스펙터에 내놓지 않는다. 애니메이터 그래프는 FollowAnimatorSetup이 만들고
        // 이름도 거기서 정한다. 양쪽에서 따로 적게 두면 한 글자만 어긋나도
        // 이동은 되는데 애니메이션만 조용히 안 나오는 상태가 된다.
        // 이름을 바꿀 일이 있으면 FollowAnimatorSetup.WalkParameter와 함께 고칠 것.
        private const string WalkParameter = "IsWalking";

        // 애니메이터도 인스펙터에 내놓지 않는다.
        //
        // 예전에는 필드로 열어뒀는데, 프로젝트 창의 FBX를 끌어다 놓는 실수가 나왔다.
        // 그러면 씬의 캐릭터가 아니라 에셋 파일 안의 Animator에 SetBool을 걸게 되어
        // 이동은 멀쩡한데 애니메이션만 안 나온다. 에러도 안 난다.
        // 자기 계층에서 찾는 것 말고 정답이 없는 값이라 아예 손댈 수 없게 막았다.
        private Animator animator;

        // 런타임 상태
        private bool isFollowing;
        private float currentSpeed;
        private int walkHash;
        private bool hasWalkParam;

        /// <summary>지금 따라가는 중인지. 대사 시스템에서 "도착했을 때" 조건으로 쓸 수 있다.</summary>
        public bool IsFollowing => isFollowing;

        /// <summary>따라갈 대상을 런타임에 교체한다.</summary>
        public void SetTarget(Transform newTarget) => target = newTarget;

        /// <summary>대화 중 등, 잠시 따라오지 못하게 막을 때 쓴다.</summary>
        public bool Paused { get; set; }

        private void Awake()
        {
            animator = GetComponentInChildren<Animator>();
            CacheParameters();

            if (rangeCollider == null)
            {
                Debug.LogWarning($"[CharacterFollow] {name}: Range Collider가 비어 있다. " +
                                 "멈출 범위를 판정할 수 없어 동작하지 않는다.", this);
            }

            // 스크립트가 위치를 직접 쓰므로 루트 모션이 켜져 있으면 서로 밀어내며 떤다.
            if (animator != null && animator.applyRootMotion)
            {
                Debug.LogWarning($"[CharacterFollow] {name}: Animator의 Apply Root Motion이 켜져 있다. " +
                                 "이 스크립트가 이동을 담당하므로 꺼야 한다. 자동으로 끈다.", this);
                animator.applyRootMotion = false;
            }
        }

        /// <summary>
        /// 파라미터 이름을 해시로 바꿔두고, 컨트롤러에 실제로 있는지도 확인한다.
        ///
        /// 없는 파라미터에 SetBool을 부르면 Unity가 매 프레임 경고를 뱉어 콘솔이 잠긴다.
        /// 여기서 한 번만 확인하고 이후로는 건너뛴다.
        /// </summary>
        private void CacheParameters()
        {
            walkHash = Animator.StringToHash(WalkParameter);
            hasWalkParam = false;

            if (animator == null)
            {
                Debug.LogWarning($"[CharacterFollow] {name}: 자기 계층에서 Animator를 찾지 못했다. " +
                                 "이동은 되지만 애니메이션이 나오지 않는다.", this);
                return;
            }

            if (animator.runtimeAnimatorController == null)
            {
                Debug.LogWarning($"[CharacterFollow] {name}: Animator에 컨트롤러가 물려 있지 않다.", this);
                return;
            }

            foreach (AnimatorControllerParameter p in animator.parameters)
            {
                if (p.type != AnimatorControllerParameterType.Bool) continue;
                if (p.name != WalkParameter) continue;

                hasWalkParam = true;
                break;
            }

            if (!hasWalkParam)
            {
                Debug.LogWarning(
                    $"[CharacterFollow] {name}: 애니메이터에 {WalkParameter} bool 파라미터가 없다. " +
                    "이동은 되지만 걷는 애니메이션이 나오지 않는다. " +
                    "메뉴 [Tools > VRProject > 따라오기 애니메이터 설정]으로 만들 수 있다.", this);
            }
        }

        private void Update()
        {
            if (!ResolveTarget())
            {
                isFollowing = false;
                Decelerate();
                return;
            }

            UpdateFollowState();

            if (isFollowing)
            {
                MoveTowardsTarget();
            }
            else
            {
                Decelerate();
                if (turnWhenIdle) TurnIdle();
            }

            PushToAnimator();
        }

        /// <summary>타깃이 없으면 Camera.main을 늦게라도 찾아본다. XR Origin은 씬 로드 후에 붙는 경우가 있다.</summary>
        private bool ResolveTarget()
        {
            if (target != null) return true;
            if (Camera.main != null) target = Camera.main.transform;
            return target != null;
        }

        /// <summary>
        /// 걸을지 멈출지 정한다. 히스테리시스가 핵심이다.
        ///
        /// 들어올 때와 나갈 때의 기준선을 다르게 둔다. 하나의 선으로 판정하면
        /// 플레이어가 경계에 서 있을 때 걷기/멈춤이 매 프레임 뒤집히며 떨린다.
        ///   - 걷는 중 → 콜라이더 안에 들어오는 순간 멈춘다.
        ///   - 멈춘 중 → 표면에서 exitBuffer만큼 더 벌어져야 다시 걷는다.
        /// </summary>
        private void UpdateFollowState()
        {
            if (Paused || rangeCollider == null)
            {
                isFollowing = false;
                return;
            }

            // ClosestPoint는 점이 콜라이더 내부에 있으면 그 점을 그대로 돌려준다.
            // 따라서 거리가 0이면 안, 그보다 크면 표면까지의 거리다.
            Vector3 targetPos = target.position;
            float surfaceDistance = Vector3.Distance(rangeCollider.ClosestPoint(targetPos), targetPos);

            if (isFollowing)
            {
                if (surfaceDistance <= 0.0001f) isFollowing = false;
            }
            else
            {
                if (surfaceDistance > Mathf.Max(0f, exitBuffer)) isFollowing = true;
            }
        }

        private void MoveTowardsTarget()
        {
            Vector3 toTarget = target.position - transform.position;
            toTarget.y = 0f;   // 고개는 Gaze가 든다. 몸은 수평으로만 돈다.

            if (toTarget.sqrMagnitude < 1e-6f) return;
            Vector3 direction = toTarget.normalized;

            Face(direction, turnSpeed);

            // 몸이 아직 돌아가는 중이면 천천히, 정면으로 서면 최고 속도로.
            // 이렇게 해야 제자리에서 돌 때 옆으로 미끄러지지 않는다.
            float alignment = Mathf.Clamp01(Vector3.Dot(Forward(), direction));

            currentSpeed = Mathf.MoveTowards(currentSpeed, moveSpeed * alignment,
                                             acceleration * Time.deltaTime);

            Vector3 next = transform.position + Forward() * (currentSpeed * Time.deltaTime);
            transform.position = stickToGround ? SnapToGround(next) : next;
        }

        private void Decelerate()
        {
            currentSpeed = Mathf.MoveTowards(currentSpeed, 0f, acceleration * Time.deltaTime);
        }

        /// <summary>
        /// 멈춘 채로 몸만 돌린다.
        ///
        /// 문턱값을 넘었을 때만 돈다. 플레이어가 눈앞에서 조금씩 움직일 때마다
        /// 몸이 따라 꼼지락거리면 오히려 부자연스럽다.
        /// </summary>
        private void TurnIdle()
        {
            Vector3 toTarget = target.position - transform.position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude < 1e-6f) return;

            Vector3 direction = toTarget.normalized;
            if (Vector3.Angle(Forward(), direction) < idleTurnThreshold) return;

            Face(direction, idleTurnSpeed);
        }

        /// <summary>
        /// 모델 정면 보정을 반영한 실제 정면 방향.
        ///
        /// 캐릭터가 기울어져 있어도 결과가 흔들리지 않도록 transform.up이 아니라
        /// 월드 up을 축으로 쓴다. 이동은 어차피 수평으로만 하므로 이쪽이 맞다.
        /// </summary>
        private Vector3 Forward()
        {
            return Mathf.Approximately(forwardOffset, 0f)
                ? transform.forward
                : Quaternion.AngleAxis(forwardOffset, Vector3.up) * transform.forward;
        }

        /// <summary>보정 각도를 되돌려 루트에 적용한다. 결과적으로 Forward()가 direction을 향한다.</summary>
        private void Face(Vector3 direction, float degreesPerSecond)
        {
            Quaternion look = Quaternion.LookRotation(direction, Vector3.up) *
                              Quaternion.AngleAxis(-forwardOffset, Vector3.up);

            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, look, degreesPerSecond * Time.deltaTime);
        }

        private Vector3 SnapToGround(Vector3 position)
        {
            Vector3 origin = position + Vector3.up * groundProbeHeight;
            float length = groundProbeHeight * 2f;

            // 자기 자신의 범위 콜라이더를 바닥으로 착각하지 않도록 트리거는 무시한다.
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, length,
                                groundMask, QueryTriggerInteraction.Ignore))
            {
                position.y = hit.point.y;
            }
            return position;
        }

        private void PushToAnimator()
        {
            if (hasWalkParam) animator.SetBool(walkHash, isFollowing);
        }

        private void OnValidate()
        {
            if (rangeCollider != null && !rangeCollider.isTrigger)
            {
                Debug.LogWarning(
                    $"[CharacterFollow] {name}: Range Collider({rangeCollider.GetType().Name})의 " +
                    "Is Trigger가 꺼져 있다. 이대로면 솔리드 충돌체라 플레이어가 범위 안으로 " +
                    "들어올 수 없어 영원히 멈추지 않는다. Is Trigger를 켤 것.", this);
            }

            exitBuffer = Mathf.Max(0f, exitBuffer);
            moveSpeed = Mathf.Max(0f, moveSpeed);
            idleTurnThreshold = Mathf.Clamp(idleTurnThreshold, 0f, 180f);
        }

        private void OnDrawGizmosSelected()
        {
            if (rangeCollider != null)
            {
                Gizmos.color = isFollowing ? new Color(1f, 0.6f, 0.1f) : Color.green;
                Bounds b = rangeCollider.bounds;
                Gizmos.DrawWireCube(b.center, b.size);
            }

            if (target != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(transform.position, target.position);
            }

            // 보정을 반영한 정면. 뒷걸음질할 때 Forward Offset을 맞추는 데 쓴다.
            Gizmos.color = Color.blue;
            Gizmos.DrawRay(transform.position + Vector3.up * 0.1f, Forward());
        }
    }
}
