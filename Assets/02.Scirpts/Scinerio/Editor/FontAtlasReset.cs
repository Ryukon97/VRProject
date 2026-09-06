using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace VRProject.EditorTools
{
    /// <summary>
    /// TMP 폰트 에셋의 글리프와 아틀라스를 비우고, 그 결과를 디스크까지 저장한다.
    ///
    /// 인스펙터의 'Clear Dynamic Data'와 하는 일은 같지만 두 가지가 다르다.
    ///   · 실행 뒤 AssetDatabase.SaveAssets()까지 불러 디스크에 확실히 쓴다.
    ///     메뉴만 눌러서는 에셋이 더티 상태로만 남아, 프로젝트를 저장하지 않으면
    ///     파일이 그대로다. 그러면 비운 줄 알았는데 아무것도 안 바뀐 것처럼 보인다.
    ///   · 전후 상태를 찍어줘서 실제로 반영됐는지 눈으로 확인할 수 있다.
    ///
    /// ClearFontAssetData(false)는 내부적으로 이렇게 동작한다.
    ///   1번 이후의 아틀라스 텍스처를 전부 파괴 → 배열을 1개로 줄임
    ///   → 남은 0번을 m_AtlasWidth×m_AtlasHeight로 재초기화 → 인덱스를 0으로
    /// 그래서 크기가 0×0으로 망가진 아틀라스도 같이 정리된다.
    /// </summary>
    public static class FontAtlasReset
    {
        private const string 기본경로 = "Assets/05Prefabs/경기천년제목OTF_Medium SDF.asset";

        [MenuItem("Tools/VRProject/폰트 아틀라스 비우기")]
        public static void 비우기()
        {
            TMP_FontAsset 폰트 = 대상찾기();
            if (폰트 == null)
            {
                Debug.LogError("[FontAtlasReset] 폰트 에셋을 찾지 못했다. " +
                               $"프로젝트 창에서 TMP 폰트 에셋을 선택하거나 {기본경로}가 있는지 확인할 것.");
                return;
            }

            string 이전 = 상태(폰트);

            Undo.RecordObject(폰트, "폰트 아틀라스 비우기");

            // false = 아틀라스 크기는 유지하고 내용만 비운다.
            // true를 주면 1×1로 줄어들어서 다시 구울 때 자리가 없다.
            폰트.ClearFontAssetData(false);

            EditorUtility.SetDirty(폰트);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[FontAtlasReset] {폰트.name}\n" +
                      $"── 이전 ──\n{이전}\n" +
                      $"── 이후 ──\n{상태(폰트)}\n" +
                      "Dynamic 모드이므로 필요한 글자는 다시 만날 때 새로 구워진다.", 폰트);
        }

        /// <summary>선택한 폰트 에셋을 우선 쓰고, 없으면 기본 경로를 연다.</summary>
        private static TMP_FontAsset 대상찾기()
        {
            if (Selection.activeObject is TMP_FontAsset 선택) return 선택;
            return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(기본경로);
        }

        private static string 상태(TMP_FontAsset f)
        {
            var sb = new StringBuilder();

            sb.AppendLine($"  글리프 {f.glyphTable.Count}개 / 문자 {f.characterTable.Count}개");

            // m_AtlasTextureIndex는 공개 프로퍼티가 없다. 지금 어느 아틀라스에
            // 굽고 있는지는 진단에 꼭 필요하므로 직렬화 필드로 직접 읽는다.
            int 인덱스 = new SerializedObject(f).FindProperty("m_AtlasTextureIndex").intValue;

            sb.AppendLine($"  아틀라스 설정 {f.atlasWidth}×{f.atlasHeight}, 현재 인덱스 {인덱스}");

            if (f.atlasTextures == null)
            {
                sb.AppendLine("  아틀라스 텍스처: 없음");
                return sb.ToString().TrimEnd();
            }

            sb.Append($"  아틀라스 텍스처 {f.atlasTextures.Length}장:");
            for (int i = 0; i < f.atlasTextures.Length; i++)
            {
                Texture2D t = f.atlasTextures[i];

                // 크기가 0인 텍스처가 섞여 있는데 거기에 글자를 구우면
                // 아무것도 그려지지 않아 전부 두부(□)로 나온다.
                string 표기 = t == null ? "null" : $"{t.width}×{t.height}";
                if (t != null && (t.width == 0 || t.height == 0)) 표기 += " ⚠비어있음";

                sb.Append($"  [{i}] {표기}");
            }

            return sb.ToString();
        }
    }
}
