using UnityEngine;
using UnityEngine.Rendering;

namespace VRProject
{
    /// <summary>
    /// ToonLit ↔ URP Lit 왕복 전환.
    ///
    /// 비포/애프터 비교용이다. 셰이더 이름을 서로 맞춰뒀기 때문에
    /// (_BaseMap, _BaseColor, _Cutoff, _AlphaClip, _Surface …)
    /// 왕복해도 기본 텍스처와 투명 설정이 유지된다.
    /// </summary>
    public static class ToonMaterialUtil
    {
        public const string ToonShaderName = "VRProject/ToonLit";
        public const string UrpLitShaderName = "Universal Render Pipeline/Lit";

        public static Shader ToonShader => Shader.Find(ToonShaderName);
        public static Shader UrpLitShader => Shader.Find(UrpLitShaderName);

        public static bool IsToon(Material m) =>
            m != null && m.shader != null && m.shader.name == ToonShaderName;

        /// <summary>URP Lit → ToonLit. 이미 툰이면 아무것도 하지 않는다.</summary>
        public static void ToToon(Material m)
        {
            Shader toon = ToonShader;
            if (m == null || toon == null || m.shader == toon) return;

            Texture baseMap = GetTex(m, "_BaseMap", "_MainTex");
            Color baseColor = GetCol(m, "_BaseColor", "_Color");

            m.shader = toon;

            if (baseMap != null) m.SetTexture("_BaseMap", baseMap);
            m.SetColor("_BaseColor", baseColor);
        }

        /// <summary>
        /// ToonLit → URP Lit. 투명/알파클립 상태를 복원한다.
        /// URP Lit은 키워드로 서피스 모드를 판단하므로 값만 넣으면 안 되고
        /// 키워드와 렌더 큐까지 같이 맞춰줘야 한다.
        /// </summary>
        public static void ToUrpLit(Material m)
        {
            Shader lit = UrpLitShader;
            if (m == null || lit == null || m.shader == lit) return;

            Texture baseMap = GetTex(m, "_BaseMap", "_MainTex");
            Color baseColor = GetCol(m, "_BaseColor", "_Color");
            bool transparent = m.HasProperty("_Surface") && m.GetFloat("_Surface") > 0.5f;
            bool clip = m.HasProperty("_AlphaClip") && m.GetFloat("_AlphaClip") > 0.5f;
            float cutoff = m.HasProperty("_Cutoff") ? m.GetFloat("_Cutoff") : 0.5f;

            m.shader = lit;

            if (baseMap != null) m.SetTexture("_BaseMap", baseMap);
            m.SetColor("_BaseColor", baseColor);
            m.SetFloat("_Cutoff", cutoff);

            if (transparent)
            {
                m.SetFloat("_Surface", 1f);
                m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_ZWrite", 0f);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.renderQueue = (int)RenderQueue.Transparent;
            }
            else
            {
                m.SetFloat("_Surface", 0f);
                m.SetFloat("_SrcBlend", (float)BlendMode.One);
                m.SetFloat("_DstBlend", (float)BlendMode.Zero);
                m.SetFloat("_ZWrite", 1f);
                m.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.renderQueue = -1;
            }

            m.SetFloat("_AlphaClip", clip ? 1f : 0f);
            if (clip) m.EnableKeyword("_ALPHATEST_ON");
            else m.DisableKeyword("_ALPHATEST_ON");

            // 데칼 모드에서 껐던 것을 되살린다.
            m.SetShaderPassEnabled("ShadowCaster", true);
        }

        private static Texture GetTex(Material m, params string[] names)
        {
            foreach (string n in names)
                if (m.HasProperty(n) && m.GetTexture(n) != null) return m.GetTexture(n);
            return null;
        }

        private static Color GetCol(Material m, params string[] names)
        {
            foreach (string n in names)
                if (m.HasProperty(n)) return m.GetColor(n);
            return Color.white;
        }
    }
}
