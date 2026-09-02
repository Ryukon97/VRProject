using System.Text;
using UnityEditor;
using UnityEngine;

namespace VRProject.EditorTools
{
    /// <summary>
    /// 선택한 모델의 실제 구조를 덤프한다.
    /// FBX 바이너리를 뜯어 추측하는 것보다 Unity가 임포트한 결과가 항상 정확하다.
    ///
    /// 메뉴: Tools ▸ Toon ▸ Inspect Model Structure
    /// </summary>
    public static class ModelInspector
    {
        [MenuItem("Tools/Toon/Inspect Model Structure")]
        private static void Inspect()
        {
            var go = Selection.activeGameObject;
            if (go == null)
            {
                EditorUtility.DisplayDialog("선택 없음", "Hierarchy에서 모델을 선택하세요.", "확인");
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine($"╔══ {go.name} 구조 ══");

            DumpRenderers(go, sb);
            DumpBones(go, sb);
            DumpBlendShapes(go, sb);

            string text = sb.ToString();
            Debug.Log(text, go);

            string path = $"Assets/{go.name}_structure.txt";
            System.IO.File.WriteAllText(path, text, Encoding.UTF8);
            AssetDatabase.Refresh();
            Debug.Log($"[ModelInspector] 파일로도 저장: {path}");
        }

        private static void DumpRenderers(GameObject go, StringBuilder sb)
        {
            var renderers = go.GetComponentsInChildren<Renderer>(true);
            sb.AppendLine($"\n── 렌더러 {renderers.Length}개 ──");

            foreach (var r in renderers)
            {
                sb.AppendLine($"  ▸ {r.name}  [{r.GetType().Name}]");
                sb.AppendLine($"      Reflection Probes: {r.probeAnchor}  " +
                              $"LightProbe: {r.lightProbeUsage}  ReflProbe: {r.reflectionProbeUsage}");

                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    Material m = mats[i];
                    if (m == null)
                    {
                        sb.AppendLine($"      [{i}] (없음)");
                        continue;
                    }

                    string tex = "-";
                    if (m.HasProperty("_BaseMap") && m.GetTexture("_BaseMap") != null)
                        tex = m.GetTexture("_BaseMap").name;
                    else if (m.HasProperty("_MainTex") && m.GetTexture("_MainTex") != null)
                        tex = m.GetTexture("_MainTex").name;

                    sb.AppendLine($"      [{i}] {m.name,-24} 셰이더: {m.shader.name,-38} 텍스처: {tex}");
                }
            }
        }

        private static void DumpBones(GameObject go, StringBuilder sb)
        {
            var smr = go.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (smr == null || smr.bones == null || smr.bones.Length == 0)
            {
                sb.AppendLine("\n── 본 없음 ──");
                return;
            }

            sb.AppendLine($"\n── 본 {smr.bones.Length}개 (루트: {(smr.rootBone != null ? smr.rootBone.name : "?")}) ──");

            // 시선 추적에 필요한 본만 추려서 먼저 보여준다.
            string[] wanted =
            {
                "Hips", "Spine", "Chest", "UpperChest", "Neck", "Head",
                "Eye_L", "Eye_R", "Eyeball_L", "Eyeball_R",
            };

            sb.AppendLine("  ▸ 시선 추적용 후보:");
            foreach (string w in wanted)
            {
                foreach (var b in smr.bones)
                {
                    if (b != null && b.name == w)
                    {
                        sb.AppendLine($"      ✓ {w}   (경로: {GetPath(b, go.transform)})");
                        break;
                    }
                }
            }

            sb.AppendLine("  ▸ 전체 목록:");
            foreach (var b in smr.bones)
                if (b != null) sb.AppendLine($"      {b.name}");
        }

        private static void DumpBlendShapes(GameObject go, StringBuilder sb)
        {
            foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (smr.sharedMesh == null) continue;
                int count = smr.sharedMesh.blendShapeCount;
                if (count == 0) continue;

                sb.AppendLine($"\n── 블렌드셰이프: {smr.name} ({count}개) ──");
                for (int i = 0; i < count; i++)
                    sb.AppendLine($"      [{i}] {smr.sharedMesh.GetBlendShapeName(i)}");
            }
        }

        private static string GetPath(Transform t, Transform root)
        {
            if (t == root) return t.name;
            var sb = new StringBuilder(t.name);
            Transform cur = t.parent;
            while (cur != null && cur != root)
            {
                sb.Insert(0, cur.name + "/");
                cur = cur.parent;
            }
            return sb.ToString();
        }
    }
}
