using System.Text;
using UnityEditor;
using UnityEngine;

namespace VRProject.EditorTools
{
    /// <summary>
    /// 선택한 오브젝트의 본 계층을 들여쓰기로 찍는다.
    ///
    /// 기존 ModelInspector는 SkinnedMeshRenderer.bones 배열을 그대로 나열한다.
    /// 그건 스키닝용 순서라서 부모-자식 관계가 드러나지 않는다. 흔들 본을 고를 때는
    /// "어디서 시작해 몇 마디로 이어지는가"가 중요한데, 그건 계층으로 봐야 안다.
    /// </summary>
    public static class BoneHierarchyDump
    {
        [MenuItem("Tools/VRProject/본 계층 찍기")]
        public static void Dump()
        {
            GameObject go = Selection.activeGameObject;
            if (go == null)
            {
                Debug.LogError("[BoneHierarchyDump] 하이어라키에서 캐릭터를 선택하고 실행할 것.");
                return;
            }

            // 스킨 루트가 있으면 거기서, 없으면 선택한 것에서 시작한다.
            Transform root = go.transform;
            var smr = go.GetComponentInChildren<SkinnedMeshRenderer>();
            if (smr != null && smr.rootBone != null) root = smr.rootBone;

            var sb = new StringBuilder();
            sb.AppendLine($"╔══ {go.name} 본 계층 (루트: {root.name}) ══");
            Walk(root, 0, sb);

            string path = $"Assets/{go.name}_bones.txt";
            System.IO.File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            AssetDatabase.Refresh();

            Debug.Log($"[BoneHierarchyDump] {path} 에 저장했다.\n\n{sb}");
        }

        private static void Walk(Transform t, int depth, StringBuilder sb)
        {
            // 자식이 하나뿐인 마디가 이어지면 흔들 수 있는 체인이라는 뜻이다.
            // 표시에 자식 수를 같이 적어 한눈에 보이게 한다.
            string 표시 = t.childCount == 0 ? "·" : (t.childCount == 1 ? "│" : "┬");

            sb.AppendLine($"{new string(' ', depth * 2)}{표시} {t.name}" +
                          (t.childCount > 1 ? $"   (가지 {t.childCount})" : ""));

            for (int i = 0; i < t.childCount; i++)
            {
                Walk(t.GetChild(i), depth + 1, sb);
            }
        }
    }
}
