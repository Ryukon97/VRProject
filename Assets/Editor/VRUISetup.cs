using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;
using VRProject.Dialogue;

namespace VRProject.EditorTools
{
    /// <summary>
    /// 월드 공간 UI를 VR 컨트롤러 레이로 누를 수 있게 만든다.
    ///
    /// UGUI의 기본 조합은 마우스 전용이라 헤드셋에서는 아무것도 눌리지 않는다.
    /// 두 군데를 바꿔야 하는데, 한쪽만 하면 여전히 안 눌려서 원인을 찾기 어렵다.
    ///
    ///   1. 캔버스   GraphicRaycaster        → 마우스 포인터만 받는다
    ///               TrackedDeviceGraphicRaycaster 를 더해야 XR 레이를 받는다
    ///
    ///   2. 이벤트   InputSystemUIInputModule → 컨트롤러 레이를 UI로 보내지 않는다
    ///      시스템   XRUIInputModule 로 바꿔야 한다
    ///
    /// 캔버스가 World Space인지, worldCamera가 채워져 있는지도 함께 본다.
    /// worldCamera가 비어 있으면 레이 판정 좌표가 어긋나 버튼이 엉뚱한 곳에서 눌린다.
    /// </summary>
    public static class VRUISetup
    {
        [MenuItem("Tools/VRProject/VR UI 상호작용 설정")]
        public static void Setup()
        {
            VRDialogueUI dialogueUI = Object.FindFirstObjectByType<VRDialogueUI>();
            if (dialogueUI == null)
            {
                Debug.LogError("[VRUISetup] 열려 있는 씬에서 VRDialogueUI를 찾지 못했다. " +
                               "대화 캔버스가 있는 씬을 열고 다시 실행할 것.");
                return;
            }

            bool changed = false;
            changed |= SetupCanvas(dialogueUI.GetComponent<Canvas>());
            changed |= SetupEventSystem();

            if (!changed)
            {
                Debug.Log("[VRUISetup] 이미 VR 상호작용이 가능한 상태다. 바꾼 것이 없다.");
                return;
            }

            EditorSceneManager.MarkSceneDirty(dialogueUI.gameObject.scene);
            Debug.Log("[VRUISetup] 완료. 이제 컨트롤러 레이로 선택지를 누를 수 있다.");
        }

        private static bool SetupCanvas(Canvas canvas)
        {
            if (canvas == null)
            {
                Debug.LogError("[VRUISetup] VRDialogueUI에 Canvas가 없다.");
                return false;
            }

            bool changed = false;
            GameObject go = canvas.gameObject;

            // Screen Space - Overlay는 HMD에 아예 렌더링되지 않는다.
            if (canvas.renderMode != RenderMode.WorldSpace)
            {
                Undo.RecordObject(canvas, "VR UI 설정");
                canvas.renderMode = RenderMode.WorldSpace;
                EditorUtility.SetDirty(canvas);
                changed = true;
                Debug.Log("[VRUISetup] 캔버스를 World Space로 바꿨다.", canvas);
            }

            // worldCamera가 비어 있으면 레이 판정 좌표가 어긋난다.
            if (canvas.worldCamera == null && Camera.main != null)
            {
                Undo.RecordObject(canvas, "VR UI 설정");
                canvas.worldCamera = Camera.main;
                EditorUtility.SetDirty(canvas);
                changed = true;
                Debug.Log($"[VRUISetup] 캔버스의 Event Camera를 {Camera.main.name}으로 채웠다.", canvas);
            }

            if (go.GetComponent<TrackedDeviceGraphicRaycaster>() == null)
            {
                Undo.AddComponent<TrackedDeviceGraphicRaycaster>(go);
                changed = true;
                Debug.Log("[VRUISetup] 캔버스에 TrackedDeviceGraphicRaycaster를 추가했다.", go);
            }

            // 기본 GraphicRaycaster는 일부러 남겨둔다.
            // 마우스 포인터와 XR 레이는 서로 다른 이벤트를 타므로 중복으로 눌리지 않고,
            // 이게 있어야 헤드셋 없이 에디터에서 마우스로 시험해볼 수 있다.
            if (go.GetComponent<GraphicRaycaster>() == null)
            {
                Debug.Log("[VRUISetup] GraphicRaycaster가 없다. " +
                          "에디터에서 마우스로 시험하려면 추가해 둘 것.", go);
            }

            return changed;
        }

        /// <summary>
        /// EventSystem의 입력 모듈을 XRUIInputModule로 바꾼다.
        ///
        /// 입력 모듈은 한 EventSystem에 하나만 동작하므로, 더하는 게 아니라 갈아끼워야 한다.
        /// XRUIInputModule도 마우스·터치를 처리하니 에디터 테스트가 막히지는 않는다.
        /// </summary>
        private static bool SetupEventSystem()
        {
            EventSystem eventSystem = Object.FindFirstObjectByType<EventSystem>();
            if (eventSystem == null)
            {
                Debug.LogError("[VRUISetup] 씬에 EventSystem이 없다. UI 입력이 아예 동작하지 않는다.");
                return false;
            }

            GameObject go = eventSystem.gameObject;

            if (go.GetComponent<XRUIInputModule>() != null)
            {
                return false;
            }

            var oldModule = go.GetComponent<InputSystemUIInputModule>();
            if (oldModule != null)
            {
                Undo.DestroyObjectImmediate(oldModule);
                Debug.Log("[VRUISetup] InputSystemUIInputModule을 제거했다.", go);
            }

            var standalone = go.GetComponent<StandaloneInputModule>();
            if (standalone != null)
            {
                Undo.DestroyObjectImmediate(standalone);
                Debug.Log("[VRUISetup] StandaloneInputModule을 제거했다.", go);
            }

            Undo.AddComponent<XRUIInputModule>(go);
            Debug.Log("[VRUISetup] EventSystem에 XRUIInputModule을 추가했다.", go);

            return true;
        }
    }
}
