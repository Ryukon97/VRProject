using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using VRProject.Character;

namespace VRProject.EditorTools
{
    /// <summary>
    /// 한 렌더러에서 여러 슬롯이 같은 머티리얼 에셋을 공유할 때, 두 번째부터를
    /// 복제본으로 갈아끼워 슬롯별로 따로 설정할 수 있게 한다.
    ///
    /// Aru_Real2가 정확히 이 경우다.
    ///   슬롯 1 = Morph_parts (눈·눈썹·입)
    ///   슬롯 7 = tere (照れ = 볼 붉힘)
    /// 둘 다 Morph_Parts_d 하나를 쓰고 있어서 눈과 볼을 따로 만질 수 없다.
    ///
    /// 메뉴: Tools ▸ Toon ▸ Split Duplicate Materials
    /// </summary>
    public static class DuplicateMaterialSplitter
    {
        [MenuItem("Tools/Toon/Split Duplicate Materials")]
        private static void Split()
        {
            GameObject go = Selection.activeGameObject;
            if (go == null)
            {
                EditorUtility.DisplayDialog("선택 없음",
                    "Hierarchy에서 모델(Aru_Real2)을 선택한 뒤 실행하세요.", "확인");
                return;
            }

            var log = new StringBuilder();
            var created = new List<Material>();
            int splits = 0;

            foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
            {
                Material[] mats = r.sharedMaterials;
                var firstSlot = new Dictionary<Material, int>();
                bool changed = false;

                for (int i = 0; i < mats.Length; i++)
                {
                    Material m = mats[i];
                    if (m == null) continue;

                    if (!firstSlot.ContainsKey(m))
                    {
                        firstSlot[m] = i;
                        continue;
                    }

                    Material copy = Duplicate(m, i);
                    if (copy == null)
                    {
                        log.AppendLine($"  ✗ 슬롯[{i}] {m.name} — 복제 실패(에셋 경로 없음)");
                        continue;
                    }

                    log.AppendLine($"  ✓ 슬롯[{i}] {m.name} → {copy.name}   " +
                                   $"(원본은 슬롯[{firstSlot[m]}]에 그대로 유지)");
                    mats[i] = copy;
                    created.Add(copy);
                    changed = true;
                    splits++;
                }

                if (!changed) continue;

                Undo.RecordObject(r, "Split duplicate materials");
                r.sharedMaterials = mats;
                EditorUtility.SetDirty(r);

                // 프리팹 인스턴스면 오버라이드로 기록해야 남는다.
                if (PrefabUtility.IsPartOfPrefabInstance(r))
                    PrefabUtility.RecordPrefabInstancePropertyModifications(r);
            }

            if (splits == 0)
            {
                EditorUtility.DisplayDialog("분리할 것 없음",
                    "중복으로 할당된 머티리얼이 없습니다.\n" +
                    "이미 분리했거나, 다른 오브젝트를 선택하셨을 수 있습니다.", "확인");
                return;
            }

            AssetDatabase.SaveAssets();

            string wired = TryWireToComponent(go, created);
            if (!string.IsNullOrEmpty(wired)) log.AppendLine(wired);

            string msg = $"머티리얼 {splits}개를 분리했습니다.\n\n{log}";
            Debug.Log($"[DuplicateMaterialSplitter]\n{msg}", go);
            EditorUtility.DisplayDialog("머티리얼 분리 완료", msg, "확인");
        }

        private static Material Duplicate(Material src, int slot)
        {
            string path = AssetDatabase.GetAssetPath(src);
            if (string.IsNullOrEmpty(path)) return null;   // 인스턴스 머티리얼은 복제 대상 아님

            string dir = Path.GetDirectoryName(path);
            string newPath = AssetDatabase.GenerateUniqueAssetPath(
                $"{dir}/{src.name}_slot{slot}.mat");

            if (!AssetDatabase.CopyAsset(path, newPath)) return null;

            AssetDatabase.ImportAsset(newPath);
            return AssetDatabase.LoadAssetAtPath<Material>(newPath);
        }

        /// <summary>
        /// 분리된 머티리얼을 CharacterToonSettings의 볼 붉힘 슬롯에 자동으로 물린다.
        /// 이 모델에서는 중복된 표정 머티리얼의 두 번째가 tere(볼 붉힘)이다.
        /// 다른 부위였다면 인스펙터에서 다시 지정하면 된다.
        /// </summary>
        private static string TryWireToComponent(GameObject go, List<Material> created)
        {
            if (created.Count != 1) return null;

            var settings = go.GetComponentInChildren<CharacterToonSettings>(true);
            if (settings == null) return null;
            if (settings.볼붉힘_머티리얼 != null) return null;   // 이미 지정돼 있으면 두지 않는다

            Undo.RecordObject(settings, "Wire blush material");
            settings.볼붉힘_머티리얼 = created[0];
            EditorUtility.SetDirty(settings);

            return $"\n  → CharacterToonSettings의 '볼붉힘_머티리얼'에 {created[0].name}을 물렸습니다.";
        }
    }
}
