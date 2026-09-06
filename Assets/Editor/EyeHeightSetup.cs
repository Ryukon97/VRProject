using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using VRProject.Character;

namespace VRProject.EditorTools
{
    /// <summary>
    /// 아루의 눈높이를 재서 XR Origin에 그대로 넣는다.
    ///
    /// 눈높이는 월드 Y로 적을 수 있는 값이 아니다. Camera Y Offset은 XR Origin의
    /// 원점에서 위로 얼마인지를 뜻하므로, '아루의 발밑에서 눈까지'라는 상대 높이라야 한다.
    /// 그래서 눈 본의 월드 Y에서 발밑 Y를 빼서 구한다.
    ///
    /// 발밑은 스킨 메시의 월드 AABB 바닥으로 잡는다. 루트 오브젝트의 위치는
    /// 모델마다 발밑이 아닐 수 있어서(이 프리팹도 Y가 0.261만큼 떠 있다) 믿을 수 없다.
    ///
    /// 값을 넣는 김에 눈높이를 어긋나게 만드는 설정도 같이 바로잡는다.
    ///   · 비균등 스케일  → 고개 움직임이 늘어나 보이고 CharacterController가 깨진다
    ///   · Floor 모드     → Camera Y Offset을 아예 무시한다
    ///   · 리그의 Y 위치  → 눈높이에 그대로 더해진다
    /// </summary>
    public static class EyeHeightSetup
    {
        [MenuItem("Tools/VRProject/아루 눈높이 재기")]
        public static void 재기() => 실행(적용: false);

        [MenuItem("Tools/VRProject/아루 눈높이로 XR Origin 맞추기")]
        public static void 맞추기() => 실행(적용: true);

        private static void 실행(bool 적용)
        {
            FacialExpression 캐릭터 = Object.FindFirstObjectByType<FacialExpression>();
            if (캐릭터 == null)
            {
                Debug.LogError("[EyeHeightSetup] 씬에서 캐릭터(FacialExpression)를 찾지 못했다.");
                return;
            }

            if (!눈높이재기(캐릭터, out float 눈Y, out float 발밑Y, out string 근거))
            {
                return;
            }

            float 눈높이 = 눈Y - 발밑Y;

            Debug.Log($"[EyeHeightSetup] {캐릭터.name}\n" +
                      $"  눈 월드 Y   : {눈Y:F3}\n" +
                      $"  발밑 월드 Y : {발밑Y:F3}\n" +
                      $"  → 눈높이    : {눈높이:F3} m   ({근거})", 캐릭터);

            if (!적용) return;

            XROrigin origin = Object.FindFirstObjectByType<XROrigin>();
            if (origin == null)
            {
                Debug.LogError("[EyeHeightSetup] 씬에서 XR Origin을 찾지 못했다.");
                return;
            }

            Undo.RecordObject(origin, "눈높이 맞추기");
            Undo.RecordObject(origin.transform, "눈높이 맞추기");

            // 비균등 스케일은 트래킹 공간을 축마다 다르게 늘린다.
            // 좌우로 1m 움직였는데 게임에서 2m 가는 식이라 멀미가 난다.
            Vector3 이전스케일 = origin.transform.localScale;
            origin.transform.localScale = Vector3.one;

            // 리그의 Y는 눈높이에 그대로 더해진다. 아루가 선 바닥에 맞춘다.
            Vector3 위치 = origin.transform.position;
            float 이전Y = 위치.y;
            위치.y = 발밑Y;
            origin.transform.position = 위치;

            // Floor 모드는 Camera Y Offset을 무시하고 실제 키를 그대로 쓴다.
            // 연출 높이를 정하려면 Device 모드여야 한다.
            XROrigin.TrackingOriginMode 이전모드 = origin.RequestedTrackingOriginMode;
            origin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Device;
            origin.CameraYOffset = 눈높이;

            EditorUtility.SetDirty(origin);
            EditorUtility.SetDirty(origin.transform);
            EditorSceneManager.MarkSceneDirty(origin.gameObject.scene);

            Debug.Log($"[EyeHeightSetup] XR Origin을 맞췄다.\n" +
                      $"  스케일        : {이전스케일} → (1, 1, 1)\n" +
                      $"  위치 Y        : {이전Y:F3} → {발밑Y:F3}\n" +
                      $"  트래킹 모드   : {이전모드} → Device\n" +
                      $"  Camera Y Offset: {눈높이:F3}\n\n" +
                      "Character Controller의 Height/Center는 XRBodyTransformer가 " +
                      "런타임에 덮어쓰므로 손대지 않아도 된다.", origin);
        }

        /// <summary>
        /// 눈 본의 월드 Y와 발밑 Y를 잰다.
        ///
        /// 발밑은 스킨 메시의 월드 AABB 바닥을 쓴다. 루트 위치는 모델마다
        /// 발밑이 아니라 허리나 원점일 수 있어서 기준으로 삼을 수 없다.
        /// </summary>
        private static bool 눈높이재기(Component 캐릭터, out float 눈Y, out float 발밑Y, out string 근거)
        {
            눈Y = 0f; 발밑Y = 0f; 근거 = "";

            Transform 눈L = 본찾기(캐릭터, "Eye_L", "左目", "EyeLeft");
            Transform 눈R = 본찾기(캐릭터, "Eye_R", "右目", "EyeRight");
            Transform 머리 = 본찾기(캐릭터, "Head", "頭");

            if (눈L != null && 눈R != null)
            {
                눈Y = (눈L.position.y + 눈R.position.y) * 0.5f;
                근거 = "Eye_L/Eye_R 중점";
            }
            else if (눈L != null || 눈R != null)
            {
                눈Y = (눈L != null ? 눈L : 눈R).position.y;
                근거 = "눈 본 한쪽";
            }
            else if (머리 != null)
            {
                눈Y = 머리.position.y;
                근거 = "눈 본이 없어 Head 본으로 대체(실제 눈보다 조금 낮다)";
            }
            else
            {
                Debug.LogError("[EyeHeightSetup] Eye_L/Eye_R/Head 본을 모두 찾지 못했다.");
                return false;
            }

            var smr = 캐릭터.GetComponentInChildren<SkinnedMeshRenderer>();
            if (smr == null)
            {
                Debug.LogError("[EyeHeightSetup] SkinnedMeshRenderer를 찾지 못해 발밑을 잴 수 없다.");
                return false;
            }

            발밑Y = smr.bounds.min.y;
            return true;
        }

        private static Transform 본찾기(Component 뿌리, params string[] 후보들)
        {
            Transform[] 전부 = 뿌리.GetComponentsInChildren<Transform>(true);

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
    }
}
