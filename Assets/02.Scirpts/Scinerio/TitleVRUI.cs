using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace VRProject.Flow
{
    /// <summary>
    /// 타이틀 화면을 VR에서 쓸 수 있게 만든다.
    ///
    /// 평면 게임의 타이틀은 Screen Space - Overlay 캔버스에 그리는데,
    /// 그건 HMD에 아예 렌더링되지 않는다. 헤드셋을 쓰면 아무것도 안 보인다.
    /// 그래서 캔버스를 월드 공간으로 옮겨 플레이어 눈앞에 띄운다.
    ///
    /// 캔버스에 붙여서 쓴다. [ExecuteAlways]라 편집 중에도 거리와 높이를 만지면
    /// 씬 뷰에서 바로 움직인다. 헤드셋을 쓰기 전에 눈으로 맞출 수 있다.
    ///
    /// 입력도 같이 갈아끼운다. 기본 GraphicRaycaster는 마우스만 받으므로
    /// 컨트롤러 레이로는 버튼을 누를 수 없다. 재생할 때 자동으로 바꾼다.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Canvas))]
    [AddComponentMenu("VRProject/Title VR UI")]
    public class TitleVRUI : MonoBehaviour
    {
        [Header("XR 리그")]
        [Tooltip("씬에 XR Origin이 없을 때 재생 시점에 만들 프리팹.\n\n" +
                 "타이틀 씬에는 보통 XR Origin을 안 두는데, 그러면 머리 추적도 " +
                 "컨트롤러도 없어서 버튼을 누를 수가 없다.\n" +
                 "이미 씬에 있으면 이 칸은 무시된다.")]
        [SerializeField] private GameObject XR리그프리팹;

        [Header("배치")]
        [Tooltip("비워두면 Camera.main을 쓴다.")]
        [SerializeField] private Transform 기준카메라;

        [Tooltip("눈에서 타이틀까지의 거리(m).\n\n" +
                 "대화창(1.6m)보다 멀리 두는 편이 시원하다. 2~3m가 무난하다.")]
        [Range(0.6f, 6f)][SerializeField] private float 거리 = 2.2f;

        [Tooltip("시선 높이 기준 위아래 오프셋(m). 0이면 정면.")]
        [Range(-1.5f, 1.5f)][SerializeField] private float 높이오프셋 = 0f;

        [Tooltip("캔버스 픽셀 1000이 몇 미터인지. 0.0015면 1000px = 1.5m.")]
        [Range(0.0002f, 0.01f)][SerializeField] private float 월드스케일 = 0.0015f;

        [Tooltip("항상 플레이어를 정면으로 바라보게 한다.")]
        [SerializeField] private bool 빌보드 = true;

        [Tooltip("고개를 돌리면 타이틀이 따라온다.\n\n" +
                 "끄면 처음 위치에 고정되어 둘러볼 수 있다. 타이틀은 끄는 편이 자연스럽다.\n" +
                 "켜면 어디를 봐도 메뉴가 보이지만 답답하게 느껴질 수 있다.")]
        [SerializeField] private bool 따라오기 = false;

        [Tooltip("따라올 때의 속도. 낮을수록 느긋하다.")]
        [Range(0.5f, 12f)][SerializeField] private float 따라오는속도 = 4f;

        [Header("입력")]
        [Tooltip("재생할 때 VR 컨트롤러로 UI를 누를 수 있게 자동으로 바꾼다.\n\n" +
                 "· 캔버스에 TrackedDeviceGraphicRaycaster 추가\n" +
                 "· EventSystem의 입력 모듈을 XRUIInputModule로 교체\n" +
                 "둘 다 있어야 레이가 버튼에 닿는다. 한쪽만 하면 여전히 안 눌린다.")]
        [SerializeField] private bool VR입력_자동설정 = true;

        private Canvas canvas;

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
            캔버스맞추기();

            if (Application.isPlaying)
            {
                XR리그확보();
                if (VR입력_자동설정) VR입력맞추기();
                StartCoroutine(추적적용후재배치());
            }

            제자리로();
        }

        private System.Collections.IEnumerator 추적적용후재배치()
        {
            // Quest의 실제 HMD 위치와 회전은 씬 시작 직후 여러 프레임에 걸쳐
            // 안정된다. 두 프레임만 기다리면 초기 자세를 기준으로 메뉴가 아래나
            // 옆에 남을 수 있으므로, 짧은 준비 시간 동안 계속 시선 중앙에 맞춘다.
            float 끝나는시간 = Time.unscaledTime + 0.75f;
            while (Time.unscaledTime < 끝나는시간)
            {
                제자리로();
                yield return null;
            }

            제자리로();
        }

        /// <summary>
        /// 캔버스를 월드 공간으로 돌린다.
        ///
        /// 편집 중에도 해두는 이유는, Overlay인 채로는 씬 뷰에서 위치를 맞출 수
        /// 없기 때문이다. 화면 전체에 붙어 있어서 거리 개념이 아예 없다.
        /// </summary>
        private void 캔버스맞추기()
        {
            if (canvas == null) return;

            canvas.renderMode = RenderMode.WorldSpace;
            if (Camera.main != null) canvas.worldCamera = Camera.main;

            var rt = canvas.transform as RectTransform;
            if (rt == null) return;

            // Overlay였다면 크기가 화면 해상도로 잡혀 있다. 월드로 오면 그게 미터가 되어
            // 어처구니없이 커지므로, 픽셀 크기를 정해두고 스케일로 줄인다.
            if (rt.sizeDelta.x < 100f) rt.sizeDelta = new Vector2(1200f, 800f);
            rt.localScale = Vector3.one * 월드스케일;
        }

        /// <summary>씬에 XR Origin이 없으면 프리팹으로 하나 만든다.</summary>
        private void XR리그확보()
        {
            Unity.XR.CoreUtils.XROrigin origin =
                FindAnyObjectByType<Unity.XR.CoreUtils.XROrigin>();

            if (origin == null && XR리그프리팹 == null)
            {
                Debug.LogWarning(
                    "[TitleVRUI] 씬에 XR Origin이 없다. 머리 추적과 컨트롤러가 없어서 " +
                    "버튼을 누를 수 없다.\n" +
                    "'XR 리그 프리팹' 칸에 XR Origin 프리팹을 넣거나, " +
                    "씬에 직접 배치할 것.", this);
                return;
            }

            if (origin == null)
            {
                GameObject rig = Instantiate(XR리그프리팹);
                rig.name = XR리그프리팹.name;
                rig.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                origin = rig.GetComponent<Unity.XR.CoreUtils.XROrigin>();
                Debug.Log($"[TitleVRUI] 씬에 XR Origin이 없어 '{rig.name}'을 만들었다.", rig);
            }

            타이틀리그고정(origin);

            Camera xrCamera = origin != null ? origin.Camera : null;
            if (xrCamera != null)
            {
                기준카메라 = xrCamera.transform;
                canvas.worldCamera = xrCamera;
                xrCamera.clearFlags = CameraClearFlags.SolidColor;
                xrCamera.backgroundColor = Color.black;

                // 씬에 XR 리그를 직접 둔 경우 Camera.main이 어느 카메라를 먼저
                // 반환할지는 보장되지 않는다. XR 카메라 이외의 기존 카메라를 모두
                // 꺼서 두 카메라와 두 AudioListener가 동시에 동작하지 않게 한다.
                foreach (Camera sceneCamera in FindObjectsByType<Camera>(FindObjectsSortMode.None))
                {
                    if (sceneCamera == xrCamera) continue;
                    if (sceneCamera.transform.IsChildOf(origin.transform)) continue;
                    sceneCamera.gameObject.SetActive(false);
                }
            }
        }

        private static void 타이틀리그고정(Unity.XR.CoreUtils.XROrigin origin)
        {
            if (origin == null) return;

            origin.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            foreach (MonoBehaviour component in origin.GetComponentsInChildren<MonoBehaviour>(true))
            {
                string typeName = component.GetType().Name;
                if (typeName.Contains("MoveProvider") ||
                    typeName.Contains("TurnProvider") ||
                    typeName.Contains("TeleportationProvider") ||
                    typeName.Contains("GravityProvider") ||
                    typeName.Contains("CharacterControllerDriver"))
                {
                    component.enabled = false;
                }
            }

            CharacterController controller = origin.GetComponent<CharacterController>();
            if (controller != null) controller.enabled = false;
        }

        /// <summary>
        /// UI 입력을 VR용으로 바꾼다.
        ///
        /// 두 군데를 다 건드려야 한다. 캔버스만 바꾸면 레이는 닿는데 이벤트가 안 가고,
        /// EventSystem만 바꾸면 보낼 대상을 못 찾는다. 한쪽만 해두면
        /// "레이는 보이는데 눌리지 않는" 상태가 되어 원인을 찾기 어렵다.
        /// </summary>
        private void VR입력맞추기()
        {
            if (GetComponent<TrackedDeviceGraphicRaycaster>() == null)
            {
                gameObject.AddComponent<TrackedDeviceGraphicRaycaster>();
            }

            // 기본 GraphicRaycaster는 남겨둔다. 마우스와 컨트롤러는 서로 다른
            // 이벤트를 타서 겹치지 않고, 에디터에서 헤드셋 없이 눌러볼 수 있다.

            EventSystem es = FindAnyObjectByType<EventSystem>();
            if (es == null)
            {
                Debug.LogWarning("[TitleVRUI] 씬에 EventSystem이 없어 UI 입력이 동작하지 않는다.", this);
                return;
            }

            if (es.GetComponent<XRUIInputModule>() != null) return;

            // 입력 모듈은 EventSystem 하나당 하나만 동작한다. 더하는 게 아니라 갈아끼운다.
            var 기존 = es.GetComponent<BaseInputModule>();
            if (기존 != null) Destroy(기존);

            es.gameObject.AddComponent<XRUIInputModule>();
        }

        private void LateUpdate()
        {
            Transform cam = Cam;
            if (cam == null) return;

            Vector3 목표 = 목표위치(cam);

            if (따라오기 && Application.isPlaying)
            {
                float t = 1f - Mathf.Exp(-따라오는속도 * Time.unscaledDeltaTime);
                transform.position = Vector3.Lerp(transform.position, 목표, t);
            }
            else if (!Application.isPlaying)
            {
                // 편집 중에는 즉시 반영해야 슬라이더를 만질 때 눈으로 확인할 수 있다.
                transform.position = 목표;
            }

            if (빌보드) 정면보기(cam);
        }

        private Vector3 목표위치(Transform cam)
        {
            // 타이틀은 시작할 때 사용자가 실제로 보는 정면에 있어야 한다.
            // 수평 방향만 사용하면 HMD가 조금만 위아래로 기울어도 화면 중심에서
            // 크게 벗어나므로 카메라의 실제 forward를 그대로 사용한다.
            return cam.position + cam.forward.normalized * 거리 + Vector3.up * 높이오프셋;
        }

        private void 정면보기(Transform cam)
        {
            Vector3 dir = transform.position - cam.position;
            if (dir.sqrMagnitude < 1e-6f) return;

            transform.rotation = Quaternion.LookRotation(dir.normalized, cam.up);
        }

        /// <summary>보간 없이 즉시 제자리로. 씬이 막 시작했을 때 쓴다.</summary>
        public void 제자리로()
        {
            Transform cam = Cam;
            if (cam == null) return;

            transform.position = 목표위치(cam);
            if (빌보드) 정면보기(cam);
        }

        private void OnValidate()
        {
            canvas = GetComponent<Canvas>();
            캔버스맞추기();
            if (!Application.isPlaying) 제자리로();
        }

        private void OnDrawGizmosSelected()
        {
            Transform cam = Cam;
            if (cam == null) return;

            Gizmos.color = new Color(1f, 0.8f, 0.3f, 0.8f);
            Gizmos.DrawLine(cam.position, transform.position);
            Gizmos.DrawWireSphere(transform.position, 0.06f);
        }
    }
}
