using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace VRProject.EditorTools
{
    /// <summary>
    /// ToonLit 머티리얼 중 '투명인데 불투명 큐에 있는' 것을 찾아 고친다.
    ///
    /// URP Lit에서 툰으로 셰이더만 갈아끼우면 블렌드 값은 따라오는데
    /// 렌더 큐는 따라오지 않는다. 셰이더의 SubShader 태그가 Geometry(2000)이고
    /// 머티리얼의 Custom Render Queue가 -1(셰이더 기본값)이기 때문이다.
    ///
    /// 그러면 알파 블렌딩을 불투명 큐에서 하게 되는데, 불투명 큐는 앞에서 뒤로
    /// 정렬하고 깊이 기록을 전제로 한다. 깊이를 안 쓰는 투명 물체가 섞이면
    /// 그리는 순서가 카메라 위치에 따라 매 프레임 뒤집혀 번쩍거린다.
    ///
    /// 게다가 RenderType이 Opaque라 URP가 깊이 프리패스(DepthOnly/DepthNormals)에도
    /// 포함시킨다. 본체는 깊이를 안 쓰는데 프리패스에서는 쓰니, 뒤에 있는 것이
    /// 깊이 테스트에서 잘려 검게 나온다. VR은 눈마다 따로 그려서 더 심하게 튄다.
    /// </summary>
    public static class ToonTransparencyRepair
    {
        private const string ShaderPath = "Assets/Shaders/ToonLit.shader";

        // 투명 머티리얼이 참여하면 안 되는 패스들.
        // 전부 ZWrite On으로 고정되어 있어서, 켜두면 유리가 깊이를 써버린다.
        private static readonly string[] 끌패스 = { "ShadowCaster", "DepthOnly", "DepthNormals" };

        [MenuItem("Tools/VRProject/툰 투명 머티리얼 복구")]
        public static void Repair()
        {
            var toon = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            if (toon == null)
            {
                Debug.LogError($"[ToonTransparencyRepair] 셰이더를 찾지 못했다: {ShaderPath}");
                return;
            }

            var 고친것 = new List<string>();
            var 멀쩡한것 = 0;

            foreach (string guid in AssetDatabase.FindAssets("t:Material"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);

                if (m == null || m.shader != toon) continue;
                if (!m.HasProperty("_Surface") || m.GetFloat("_Surface") <= 0.5f) continue;

                if (이미정상(m)) { 멀쩡한것++; continue; }

                Undo.RecordObject(m, "툰 투명 머티리얼 복구");

                // 투명 큐로 옮긴다. 이것 하나가 깜빡임의 핵심 원인이다.
                m.renderQueue = (int)RenderQueue.Transparent;

                // 본체는 깊이를 쓰지 않는다. 블렌드 값(스트레이트/프리멀티플라이)은
                // 원래 것을 존중해 건드리지 않는다. 둘 다 투명으로 유효하다.
                m.SetFloat("_ZWrite", 0f);

                // 외곽선은 뒤집힌 검은 껍질이라, 유리에 붙으면 검은 섬광이 된다.
                if (m.HasProperty("_OutlineEnabled")) m.SetFloat("_OutlineEnabled", 0f);
                m.DisableKeyword("_OUTLINE_ON");
                m.SetShaderPassEnabled("SRPDefaultUnlit", false);

                foreach (string p in 끌패스) m.SetShaderPassEnabled(p, false);

                EditorUtility.SetDirty(m);
                고친것.Add(System.IO.Path.GetFileNameWithoutExtension(path));
            }

            AssetDatabase.SaveAssets();

            if (고친것.Count == 0)
            {
                Debug.Log($"[ToonTransparencyRepair] 고칠 것이 없다. " +
                          $"투명 툰 머티리얼 {멀쩡한것}개는 이미 정상이다.");
                return;
            }

            Debug.Log($"[ToonTransparencyRepair] {고친것.Count}개를 고쳤다 " +
                      $"(이미 정상 {멀쩡한것}개).\n  " + string.Join("\n  ", 고친것));
        }

        /// <summary>렌더 큐가 투명 구간이고 깊이를 안 쓰면 정상으로 본다.</summary>
        private static bool 이미정상(Material m)
        {
            bool 큐정상 = m.renderQueue >= (int)RenderQueue.Transparent;
            bool 깊이정상 = !m.HasProperty("_ZWrite") || m.GetFloat("_ZWrite") < 0.5f;
            bool 외곽선꺼짐 = !m.HasProperty("_OutlineEnabled")
                              || m.GetFloat("_OutlineEnabled") < 0.5f;

            return 큐정상 && 깊이정상 && 외곽선꺼짐;
        }
    }
}
