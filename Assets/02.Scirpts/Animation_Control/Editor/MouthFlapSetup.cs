using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using VRProject.Character;

namespace VRProject.EditorTools
{
    /// <summary>
    /// 캐릭터에 MouthFlap을 붙이고 ChatManager와 이어준다.
    ///
    /// 컴포넌트를 손으로 붙이는 단계를 남겨두면 빠뜨리기 쉽다. 게다가 빠뜨렸을 때
    /// 증상이 "대사의 입모양 체크를 켰는데 입이 안 움직인다"뿐이라, 어디를
    /// 봐야 하는지 알기 어렵다.
    ///
    /// 붙이는 위치는 FacialExpression과 같은 오브젝트다. 둘이 같은 렌더러를
    /// 찾아야 입 모프를 주고받을 수 있기 때문이다. 각자 다른 오브젝트에 있으면
    /// 서로 다른 SkinnedMeshRenderer를 잡아 입이 넘어가지 않는다.
    /// </summary>
    public static class MouthFlapSetup
    {
        [MenuItem("Tools/VRProject/입모양 컴포넌트 설정")]
        public static void Setup()
        {
            // 표정 컴포넌트가 곧 "얼굴을 가진 캐릭터"의 표식이다.
            FacialExpression face = Object.FindFirstObjectByType<FacialExpression>();
            if (face == null)
            {
                Debug.LogError("[MouthFlapSetup] 열려 있는 씬에서 FacialExpression을 찾지 못했다. " +
                               "캐릭터가 있는 씬을 열고 다시 실행할 것.");
                return;
            }

            GameObject host = face.gameObject;

            MouthFlap flap = host.GetComponent<MouthFlap>();
            if (flap == null)
            {
                flap = Undo.AddComponent<MouthFlap>(host);
                Debug.Log($"[MouthFlapSetup] {host.name}에 MouthFlap을 추가했다.", flap);
            }
            else
            {
                Debug.Log($"[MouthFlapSetup] {host.name}에 이미 MouthFlap이 있다.", flap);
            }

            VerifyMorphs(flap);
            LinkToChatManager(flap);

            EditorUtility.SetDirty(flap);
            EditorSceneManager.MarkSceneDirty(host.scene);
        }

        /// <summary>
        /// 입모양 순서에 적힌 모프가 이 모델에 실제로 있는지 확인한다.
        ///
        /// 이름이 틀리면 그 단계는 통째로 건너뛰어서, 입이 아예 안 움직이거나
        /// 한 모양만 반복한다. 붙인 직후에 걸러내는 편이 낫다.
        /// </summary>
        private static void VerifyMorphs(MouthFlap flap)
        {
            List<string> missing = flap.없는모프찾기();

            if (missing.Count == 0)
            {
                Debug.Log("[MouthFlapSetup] 입모양 모프를 모두 찾았다. (あ / お / え)", flap);
                return;
            }

            Debug.LogWarning(
                $"[MouthFlapSetup] 이 모델에 없는 모프가 있다: {string.Join(", ", missing)}\n" +
                "MouthFlap의 '입모양 순서'에서 이름을 이 모델의 것으로 고칠 것. " +
                "SkinnedMeshRenderer의 BlendShapes 목록에서 실제 이름을 확인할 수 있다.", flap);
        }

        /// <summary>
        /// ChatManager의 입모양 칸을 채운다.
        ///
        /// 비워두면 OnEnable에서 씬을 뒤져 찾긴 하지만, 인스펙터에 박아두면
        /// 무엇이 물려 있는지 눈으로 확인할 수 있다.
        /// </summary>
        private static void LinkToChatManager(MouthFlap flap)
        {
            ChatManager chat = Object.FindFirstObjectByType<ChatManager>();
            if (chat == null)
            {
                Debug.LogWarning("[MouthFlapSetup] 씬에서 ChatManager를 찾지 못해 연결을 건너뛴다.");
                return;
            }

            bool changed = false;

            if (chat.입모양 != flap)
            {
                Undo.RecordObject(chat, "입모양 연결");
                chat.입모양 = flap;
                changed = true;
            }

            // 표정도 같이 채워둔다. 어차피 같은 캐릭터의 것이다.
            var face = flap.GetComponent<FacialExpression>();
            if (face != null && chat.표정 != face)
            {
                Undo.RecordObject(chat, "표정 연결");
                chat.표정 = face;
                changed = true;
            }

            if (!changed)
            {
                Debug.Log("[MouthFlapSetup] ChatManager는 이미 연결되어 있다.", chat);
                return;
            }

            EditorUtility.SetDirty(chat);
            Debug.Log($"[MouthFlapSetup] ChatManager에 표정/입모양을 연결했다.", chat);
        }
    }
}
