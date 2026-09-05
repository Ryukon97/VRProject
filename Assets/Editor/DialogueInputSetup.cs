using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;

namespace VRProject.EditorTools
{
    /// <summary>
    /// 씬의 ChatManager에 대사 넘김 입력(Dialogue/Advance)을 연결한다.
    ///
    /// DialogueInput.inputactions에는 Quest 컨트롤러 A/B(오른손), X/Y(왼손)와
    /// Space가 이미 묶여 있는데, 씬에 꽂지 않으면 아무 의미가 없다.
    /// 그 상태로 헤드셋을 쓰면 마우스도 키보드도 없으니 대사를 넘길 방법이 사라진다.
    ///
    /// InputActionReference는 .inputactions의 서브 에셋이라 프로젝트 창에서
    /// 파일을 펼쳐야 보인다. 그냥 파일째로 끌어다 놓으면 타입이 맞지 않아
    /// 칸에 들어가지 않는데, 이게 놓치기 쉬운 지점이라 도구로 만들었다.
    /// </summary>
    public static class DialogueInputSetup
    {
        private const string InputAssetPath = "Assets/DialogueInput.inputactions";
        private const string MapName = "Dialogue";
        private const string ActionName = "Advance";

        [MenuItem("Tools/VRProject/대사 입력 연결")]
        public static void Setup()
        {
            InputActionReference advance = FindActionReference();
            if (advance == null)
            {
                Debug.LogError(
                    $"[DialogueInputSetup] {InputAssetPath}에서 '{MapName}/{ActionName}' 액션을 찾지 못했다. " +
                    "액션 이름이 바뀌었는지 확인할 것.");
                return;
            }

            ChatManager chat = Object.FindFirstObjectByType<ChatManager>();
            if (chat == null)
            {
                Debug.LogError("[DialogueInputSetup] 열려 있는 씬에서 ChatManager를 찾지 못했다. " +
                               "대화 시스템이 있는 씬을 열고 다시 실행할 것.");
                return;
            }

            if (chat.vrAdvanceAction == advance)
            {
                Debug.Log($"[DialogueInputSetup] {chat.name}에 이미 연결되어 있다. 그대로 둔다.", chat);
                return;
            }

            Undo.RecordObject(chat, "대사 입력 연결");
            chat.vrAdvanceAction = advance;
            EditorUtility.SetDirty(chat);
            EditorSceneManager.MarkSceneDirty(chat.gameObject.scene);

            Debug.Log($"[DialogueInputSetup] 연결 완료. " +
                      $"{chat.name} > VR Advance Action → {MapName}/{ActionName}\n" +
                      "바인딩: 오른손 A/B, 왼손 X/Y, 키보드 Space", chat);
        }

        /// <summary>
        /// .inputactions의 서브 에셋 중에서 원하는 액션의 참조를 찾는다.
        ///
        /// 임포터가 액션 하나당 InputActionReference를 하나씩 만들어 붙인다.
        /// 이름만 비교하면 다른 맵에 같은 이름이 있을 때 엉뚱한 것을 집으므로
        /// 맵 이름까지 함께 본다.
        /// </summary>
        private static InputActionReference FindActionReference()
        {
            return AssetDatabase.LoadAllAssetRepresentationsAtPath(InputAssetPath)
                .OfType<InputActionReference>()
                .FirstOrDefault(r => r.action != null
                                     && r.action.name == ActionName
                                     && r.action.actionMap != null
                                     && r.action.actionMap.name == MapName);
        }
    }
}
