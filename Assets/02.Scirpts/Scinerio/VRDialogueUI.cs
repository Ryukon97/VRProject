using UnityEngine;
using UnityEngine.UI;

namespace VRProject.Dialogue
{
    /// <summary>
    /// 대화 UI를 플레이어 눈앞의 월드 공간에 띄운다.
    ///
    /// VR에서 UI를 머리에 완전히 고정하면(HUD) 시야에 못이 박힌 것처럼 붙어 다녀
    /// 멀미와 눈 피로를 유발한다. 그래서 여기서는 '느슨한 추종'을 쓴다 —
    /// 고개를 조금 돌리는 동안에는 UI가 월드에 가만히 있고,
    /// 데드존을 벗어날 만큼 돌아섰을 때만 부드럽게 따라온다.
    ///
    /// Screen Space - Overlay 캔버스는 HMD에 아예 렌더링되지 않으므로
    /// 반드시 World Space여야 한다. 컨텍스트 메뉴의 '캔버스 VR 설정'이 그걸 잡아준다.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Canvas))]
    [AddComponentMenu("VRProject/VR Dialogue UI")]
    public class VRDialogueUI : MonoBehaviour
    {
        public enum 배치모드
        {
            플레이어_앞,   // 카메라 기준. 고개를 돌리면 따라온다
            화자_옆,       // 지정한 화자 옆에 고정. 플레이어를 향해서만 회전한다
        }

        // 거리의 허용 범위. Range 속성과 런타임 클램프가 같은 값을 보도록 상수로 둔다.
        // 캐릭터가 코앞에 설 수 있으므로 예전(0.6)보다 더 당길 수 있게 열어두었다.
        private const float 최소거리 = 0.3f;
        private const float 최대거리 = 5f;

        [Header("배치")]
        [Tooltip("비워두면 Camera.main(XR Origin의 Main Camera)을 쓴다.")]
        [SerializeField] private Transform 기준카메라;

        [SerializeField] private 배치모드 모드 = 배치모드.플레이어_앞;

        [Tooltip("화자_옆 모드에서 UI를 붙일 대상. 보통 캐릭터의 가슴 높이 빈 오브젝트.")]
        [SerializeField] private Transform 화자앵커;

        [Tooltip("눈에서 UI까지의 거리(m).\n\n" +
                 "1m보다 가까우면 초점을 맞추느라 눈이 피로해진다.\n" +
                 "3m를 넘으면 글씨가 작아 읽기 어렵다. 1.5~2.0이 편하다.\n\n" +
                 "대화 상대가 코앞에 서 있으면 UI가 캐릭터에 가려진다.\n" +
                 "그럴 때는 캐릭터보다 앞으로 당겨야 하므로 1.0 안팎까지 내린다.")]
        [Range(최소거리, 최대거리)][SerializeField] private float 거리 = 1.6f;

        [Tooltip("시선 높이 기준 위아래 오프셋(m).\n" +
                 "음수면 시선보다 아래. 정면을 가리지 않아 대화 상대를 보면서 읽을 수 있다.")]
        [Range(-1f, 1f)][SerializeField] private float 높이오프셋 = -0.25f;

        [Header("따라오기")]
        [Tooltip("이 각도 안에서 고개를 움직이는 동안에는 UI가 제자리에 머문다.\n" +
                 "0으로 두면 머리에 붙어 다녀 멀미가 난다. 15~25도를 권장.")]
        [Range(0f, 60f)][SerializeField] private float 데드존각도 = 20f;

        [Tooltip("데드존을 벗어났을 때 따라오는 속도. 낮을수록 느긋하다.")]
        [Range(0.5f, 12f)][SerializeField] private float 따라오는속도 = 3f;

        [Tooltip("켜면 좌우 회전만 따라간다. 고개를 위아래로 끄덕여도 UI가 출렁이지 않는다.")]
        [SerializeField] private bool 수평만_따라가기 = true;

        [Tooltip("목표와 이만큼 이상 벌어지면 보간하지 않고 즉시 옮긴다(m).\n\n" +
                 "플레이 시작 순간 XR 트래킹이 카메라를 바닥에서 머리 높이로 올리는데,\n" +
                 "그때 UI가 천천히 날아오면 어색하다.")]
        [Range(0.5f, 5f)][SerializeField] private float 순간이동_기준 = 1.2f;

        [Header("표시")]
        [Tooltip("항상 플레이어를 정면으로 바라보게 한다.")]
        [SerializeField] private bool 빌보드 = true;

        [Tooltip("캔버스 픽셀 1000이 몇 미터인지. 0.001이면 1000px = 1m.")]
        [SerializeField] private float 월드스케일 = 0.001f;

        private Canvas canvas;
        private bool 따라가는중;

        /// <summary>
        /// 눈에서 UI까지의 거리(m).
        ///
        /// 런타임에 바꾸면 다음 LateUpdate부터 반영된다. 설정 메뉴의 슬라이더나
        /// 컨트롤러 조이스틱에 물려 쓰라고 열어두었다.
        /// </summary>
        public float 거리설정
        {
            get => 거리;
            set => 거리 = Mathf.Clamp(value, 최소거리, 최대거리);
        }

        /// <summary>
        /// 지금 거리에서 delta(m)만큼 밀거나 당긴다.
        ///
        /// 조이스틱 축에 물릴 때는 Time.deltaTime을 곱해서 넘길 것.
        /// 예: ui.거리밀기(stick.y * 0.5f * Time.deltaTime)
        /// </summary>
        public void 거리밀기(float delta) => 거리설정 = 거리 + delta;

        private Transform Cam
        {
            get
            {
                if (기준카메라 != null) return 기준카메라;
                return Camera.main != null ? Camera.main.transform : null;
            }
        }

        private void OnEnable()
        {
            canvas = GetComponent<Canvas>();
            SnapToTarget();
        }

        private void LateUpdate()
        {
            Transform cam = Cam;
            if (cam == null) return;

            Vector3 목표위치 = ComputeTargetPosition(cam);

            if (모드 == 배치모드.화자_옆)
            {
                // 화자 옆은 월드 고정이다. 위치는 그대로 두고 회전만 맞춘다.
                transform.position = 목표위치;
            }
            else
            {
                UpdateLazyFollow(cam, 목표위치);
            }

            if (빌보드) FacePlayer(cam);
        }

        /// <summary>
        /// 느슨한 추종. 데드존 안에서는 멈춰 있다가, 벗어나면 따라붙기 시작하고
        /// 정면에 다시 들어오면 멈춘다. 히스테리시스를 둬서 경계에서 떨지 않게 한다.
        /// </summary>
        private void UpdateLazyFollow(Transform cam, Vector3 목표위치)
        {
            Vector3 toUI = transform.position - cam.position;
            Vector3 forward = cam.forward;

            if (수평만_따라가기)
            {
                toUI.y = 0f;
                forward.y = 0f;
            }

            float 각도 = (toUI.sqrMagnitude < 1e-6f || forward.sqrMagnitude < 1e-6f)
                ? 999f : Vector3.Angle(toUI, forward);

            // 각도만 보면 안 된다.
            //
            // 플레이를 누르면 XR 트래킹이 카메라를 바닥에서 실제 머리 높이로 끌어올리는데,
            // 그건 수직 이동이라 수평 각도가 거의 변하지 않는다. 각도만 판정하면
            // UI가 바닥 근처에 놓인 채 영원히 따라오지 않는다.
            // 앞뒤로 걸어갈 때도 마찬가지다.
            Vector3 목표에서 = 목표위치 - transform.position;
            float 위치오차 = 목표에서.magnitude;

            const float 위치허용치 = 0.35f;   // 이만큼 벌어지면 따라간다(m)

            bool 벗어남 = 각도 > 데드존각도 || 위치오차 > 위치허용치;
            bool 충분히가까움 = 각도 < 데드존각도 * 0.4f && 위치오차 < 위치허용치 * 0.4f;

            if (벗어남) 따라가는중 = true;
            else if (충분히가까움) 따라가는중 = false;

            // 너무 멀면 미끄러지듯 따라오는 게 오히려 어색하다. 즉시 옮긴다.
            // 플레이 시작 순간 트래킹이 카메라를 머리 높이로 올릴 때가 이 경우다.
            if (위치오차 > 순간이동_기준)
            {
                transform.position = 목표위치;
                따라가는중 = false;
                return;
            }

            if (!따라가는중 && Application.isPlaying) return;

            float t = Application.isPlaying
                ? 1f - Mathf.Exp(-따라오는속도 * Time.deltaTime)   // 프레임레이트 독립
                : 1f;                                              // 편집 중에는 즉시

            transform.position = Vector3.Lerp(transform.position, 목표위치, t);
        }

        private Vector3 ComputeTargetPosition(Transform cam)
        {
            if (모드 == 배치모드.화자_옆 && 화자앵커 != null)
                return 화자앵커.position;

            Vector3 forward = cam.forward;
            if (수평만_따라가기)
            {
                forward.y = 0f;
                if (forward.sqrMagnitude < 1e-6f) forward = cam.forward;
                forward.Normalize();
            }

            return cam.position + forward * 거리 + Vector3.up * 높이오프셋;
        }

        /// <summary>캔버스가 플레이어를 향하게 한다. 기울지 않도록 월드 업을 쓴다.</summary>
        private void FacePlayer(Transform cam)
        {
            Vector3 dir = transform.position - cam.position;
            if (수평만_따라가기) dir.y = 0f;
            if (dir.sqrMagnitude < 1e-6f) return;

            transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
        }

        // 편집 중에 거리를 눈으로 맞추기 위한 단축 메뉴.
        // 인스펙터 슬라이더를 조금씩 끄는 것보다 빠르고, 값이 기억에 남는다.

        /// <summary>캐릭터가 코앞에 설 때. 캐릭터보다 앞으로 나와 가려지지 않는다.</summary>
        [ContextMenu("거리 · 가깝게 (1.0m)")]
        private void 거리_가깝게() => 거리적용(1.0f);

        [ContextMenu("거리 · 보통 (1.6m)")]
        private void 거리_보통() => 거리적용(1.6f);

        [ContextMenu("거리 · 멀게 (2.4m)")]
        private void 거리_멀게() => 거리적용(2.4f);

        private void 거리적용(float 값)
        {
            거리설정 = 값;
            SnapToTarget();
            Debug.Log($"[VRDialogueUI] 거리를 {거리:F2}m로 맞췄다.", this);
        }

        /// <summary>보간 없이 즉시 목표 위치로 옮긴다. 대화 시작 시점에 쓴다.</summary>
        public void SnapToTarget()
        {
            Transform cam = Cam;
            if (cam == null) return;

            transform.position = ComputeTargetPosition(cam);
            따라가는중 = false;
            if (빌보드) FacePlayer(cam);
        }

        /// <summary>
        /// 캔버스를 VR용으로 설정한다.
        ///
        /// Screen Space - Overlay는 HMD에 렌더링되지 않는다. 이걸 안 바꾸면
        /// 모니터에만 보이고 헤드셋에서는 아무것도 안 나온다.
        /// </summary>
        [ContextMenu("캔버스 VR 설정")]
        public void SetupCanvasForVR()
        {
            canvas = GetComponent<Canvas>();
            if (canvas == null) return;

            canvas.renderMode = RenderMode.WorldSpace;

            Camera cam = Camera.main;
            if (cam != null) canvas.worldCamera = cam;

            var rt = canvas.transform as RectTransform;
            if (rt != null)
            {
                // 픽셀 크기를 유지한 채 월드 스케일만 줄인다.
                // 1000px 폭이면 월드에서 1m가 된다(월드스케일 0.001 기준).
                if (rt.sizeDelta.x < 100f) rt.sizeDelta = new Vector2(1000f, 320f);
                rt.localScale = Vector3.one * 월드스케일;
            }

            // XRI의 레이·시선으로 버튼을 누르려면 기본 GraphicRaycaster가 아니라
            // TrackedDeviceGraphicRaycaster가 필요하다.
            var 기본레이캐스터 = GetComponent<GraphicRaycaster>();
            bool xr레이캐스터있음 = false;
            foreach (var c in GetComponents<Component>())
            {
                if (c != null && c.GetType().Name == "TrackedDeviceGraphicRaycaster")
                {
                    xr레이캐스터있음 = true;
                    break;
                }
            }

            string 경고 = xr레이캐스터있음
                ? ""
                : "\n※ 선택지를 누르려면 이 캔버스에 TrackedDeviceGraphicRaycaster를 추가하세요 " +
                  "(Add Component에서 검색). 기본 GraphicRaycaster는 VR 레이를 못 받습니다.";

            SnapToTarget();

            Debug.Log($"[VRDialogueUI] 캔버스를 World Space로 설정했다. " +
                      $"카메라={(cam != null ? cam.name : "없음")}, 스케일={월드스케일}{경고}", this);
        }

        private void OnValidate()
        {
            if (!Application.isPlaying) SnapToTarget();
        }

        private void OnDrawGizmosSelected()
        {
            Transform cam = Cam;
            if (cam == null) return;

            Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.8f);
            Gizmos.DrawLine(cam.position, transform.position);
            Gizmos.DrawWireSphere(transform.position, 0.05f);

            // 데드존 시각화
            Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.25f);
            Vector3 f = cam.forward;
            if (수평만_따라가기) { f.y = 0f; f.Normalize(); }
            Quaternion l = Quaternion.AngleAxis(-데드존각도, Vector3.up);
            Quaternion r = Quaternion.AngleAxis(데드존각도, Vector3.up);
            Gizmos.DrawLine(cam.position, cam.position + (l * f) * 거리);
            Gizmos.DrawLine(cam.position, cam.position + (r * f) * 거리);
        }
    }
}
