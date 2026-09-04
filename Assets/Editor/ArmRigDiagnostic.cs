using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace VRProject.EditorTools
{
    /// <summary>
    /// 팔 리깅을 뜯어본다.
    ///
    /// A포즈 모델에 T포즈 기준 모션(Mixamo)을 리타게팅하면 팔이 어긋난다.
    /// 다만 어긋남의 원인이 포즈 하나뿐인지, 리깅 구조에도 문제가 있는지
    /// 눈으로는 구분이 안 된다. 이 도구가 넷을 나눠서 알려준다.
    ///
    ///   1) 바인드 포즈의 팔 각도  — 모델이 실제로 A포즈인가
    ///   2) 아바타 포즈의 팔 각도  — Enforce T-Pose가 먹었는가
    ///   3) 팔 본 계보            — 중간에 매핑 안 된 본이 끼어 있는가
    ///   4) 매핑 안 된 본의 스킨 웨이트 — 그 본이 살을 실제로 끌고 있는가
    ///
    /// 메뉴: Tools ▸ Toon ▸ Animation ▸ 팔 리깅 진단
    /// </summary>
    public static class ArmRigDiagnostic
    {
        [MenuItem("Tools/Toon/Animation/팔 리깅 진단")]
        private static void Run()
        {
            string path = ResolvePath();
            if (string.IsNullOrEmpty(path))
            {
                EditorUtility.DisplayDialog("선택 없음",
                    "FBX 또는 씬의 캐릭터를 선택한 뒤 실행하세요.", "확인");
                return;
            }

            var imp = AssetImporter.GetAtPath(path) as ModelImporter;
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (imp == null || prefab == null)
            {
                EditorUtility.DisplayDialog("실패", "모델을 읽지 못했습니다.", "확인");
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine($"╔══ 팔 리깅 진단: {Path.GetFileName(path)}");

            // 휴머노이드 슬롯 → 실제 본 이름
            var map = new Dictionary<string, string>();
            var mappedBones = new HashSet<string>();
            foreach (HumanBone hb in imp.humanDescription.human)
            {
                if (string.IsNullOrEmpty(hb.humanName) || string.IsNullOrEmpty(hb.boneName)) continue;
                map[hb.humanName] = hb.boneName;
                mappedBones.Add(hb.boneName);
            }

            var pose = new Dictionary<string, SkeletonBone>();
            foreach (SkeletonBone b in imp.humanDescription.skeleton)
                if (!string.IsNullOrEmpty(b.name)) pose[b.name] = b;

            var byName = new Dictionary<string, Transform>();
            foreach (Transform t in prefab.GetComponentsInChildren<Transform>(true))
                if (!byName.ContainsKey(t.name)) byName[t.name] = t;

            foreach (string side in new[] { "Left", "Right" })
            {
                sb.AppendLine($"\n── {side} 팔 ──");

                if (!map.TryGetValue(side + "UpperArm", out string upperName) ||
                    !map.TryGetValue(side + "Hand", out string handName) ||
                    !byName.TryGetValue(upperName, out Transform upper) ||
                    !byName.TryGetValue(handName, out Transform hand))
                {
                    sb.AppendLine("   매핑이 없어 진단 불가");
                    continue;
                }

                // 1) 바인드 포즈 각도 — 모델 원본이 A포즈인지
                float bind = Droop(WorldPos(upper, null), WorldPos(hand, null));

                // 2) 아바타 포즈 각도 — Enforce T-Pose가 반영됐는지
                float avatar = Droop(WorldPos(upper, pose), WorldPos(hand, pose));

                sb.AppendLine($"   바인드 포즈 하향각 : {bind,6:F1}°   ({Verdict(bind)})");
                sb.AppendLine($"   아바타 포즈 하향각 : {avatar,6:F1}°   ({Verdict(avatar)})");

                if (Mathf.Abs(bind - avatar) < 3f)
                    sb.AppendLine("   → 두 값이 같다. Enforce T-Pose가 반영되지 않았다.");
                else
                    sb.AppendLine($"   → T포즈 보정이 {Mathf.Abs(bind - avatar):F1}° 적용돼 있다.");

                // 3) 계보에 낀 본
                sb.AppendLine("   계보 (위팔 → 손):");
                var chain = new List<Transform>();
                for (Transform c = hand; c != null && c != upper.parent; c = c.parent) chain.Add(c);
                chain.Reverse();

                foreach (Transform c in chain)
                {
                    bool isMapped = mappedBones.Contains(c.name);
                    sb.AppendLine($"      {(isMapped ? "●" : "○")} {c.name}" +
                                  (isMapped ? "" : "   ← 휴머노이드에 매핑 안 됨"));
                }
            }

            // 4) 매핑 안 된 팔 본이 살을 얼마나 끌고 있는지
            sb.AppendLine("\n── 매핑 안 된 본의 스킨 웨이트 ──");
            ReportUnmappedWeights(prefab, mappedBones, sb);

            // 5) 손가락 매핑. MMD의 엄지는 마디 수가 Unity와 안 맞아 밀리기 쉽다.
            sb.AppendLine("\n── 손가락 매핑 ──");
            ReportFingers(map, sb);

            // 6) 어깨 아래 실제 계층. 트위스트 본이 팔 체인 밖으로 갈라져 나갔는지 본다.
            sb.AppendLine("\n── 팔 계층 트리 (● 매핑됨 / ○ 안 됨, 괄호는 스킨 웨이트) ──");
            ReportArmTree(prefab, map, mappedBones, sb);

            sb.AppendLine("\n" +
                "해석:\n" +
                "  · 아바타 하향각이 15° 미만이면 T포즈 보정이 먹은 것이다.\n" +
                "  · 30°를 넘으면 Configure → Pose ▾ → Enforce T-Pose 가 필요하다.\n" +
                "  · ○ 표시된 본이 웨이트를 많이 들고 있으면, 포즈를 고쳐도\n" +
                "    그 부위는 애니메이션을 따라가지 않는다(Blender에서 웨이트 이전 필요).");

            Debug.Log(sb.ToString());
        }

        /// <summary>
        /// 어깨 아래 계층을 그대로 그린다.
        ///
        /// 팔 체인이 한 줄로 이어지는지, 아니면 중간에 다른 가지가 갈라져 나가는지가
        /// 눈으로 보인다. 갈라진 가지에 있는 본은 팔 회전을 상속받지 못한다.
        /// </summary>
        private static void ReportArmTree(GameObject prefab, Dictionary<string, string> map,
                                          HashSet<string> mapped, StringBuilder sb)
        {
            Dictionary<string, float> weights = BoneWeightRatios(prefab);

            var byName = new Dictionary<string, Transform>();
            foreach (Transform t in prefab.GetComponentsInChildren<Transform>(true))
                if (!byName.ContainsKey(t.name)) byName[t.name] = t;

            foreach (string side in new[] { "Left", "Right" })
            {
                Transform root = null;

                // 어깨가 매핑돼 있으면 거기서, 없으면 위팔에서 시작한다.
                if (map.TryGetValue(side + "Shoulder", out string sh) && byName.TryGetValue(sh, out Transform st))
                    root = st;
                else if (map.TryGetValue(side + "UpperArm", out string ua) && byName.TryGetValue(ua, out Transform ut))
                    root = ut;

                if (root == null) { sb.AppendLine($"   [{side}] 시작 본을 찾지 못했다"); continue; }

                sb.AppendLine($"   [{side}]");
                PrintTree(root, mapped, weights, sb, 2, 0);
            }
        }

        private static void PrintTree(Transform t, HashSet<string> mapped,
                                      Dictionary<string, float> weights,
                                      StringBuilder sb, int indent, int depth)
        {
            // 손가락 마디까지 다 찍으면 너무 길어진다. 손목 아래는 접는다.
            if (depth > 6) return;

            bool isMapped = mapped.Contains(t.name);
            weights.TryGetValue(t.name, out float w);

            string pad = new string(' ', indent * 3);
            string mark = isMapped ? "●" : "○";
            string weightText = w > 0.0001f ? $"  ({w * 100f:F2}%)" : "";
            string warn = (!isMapped && w > 0.0001f) ? "   ← 매핑 안 됐는데 살을 끈다" : "";

            sb.AppendLine($"{pad}{mark} {t.name}{weightText}{warn}");

            // 손가락은 이름만 요약한다.
            int fingerChildren = 0;
            foreach (Transform c in t)
            {
                string n = c.name.ToLowerInvariant();
                if (n.Contains("finger") || n.Contains("thumb")) { fingerChildren++; continue; }
                PrintTree(c, mapped, weights, sb, indent + 1, depth + 1);
            }

            if (fingerChildren > 0)
                sb.AppendLine($"{pad}   … 손가락 가지 {fingerChildren}개 (생략)");
        }

        /// <summary>본 이름 → 전체 대비 스킨 웨이트 비율.</summary>
        private static Dictionary<string, float> BoneWeightRatios(GameObject prefab)
        {
            var result = new Dictionary<string, float>();

            var smr = prefab.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (smr == null || smr.sharedMesh == null || smr.bones == null) return result;

            BoneWeight[] bw;
            try { bw = smr.sharedMesh.boneWeights; }
            catch { return result; }
            if (bw == null || bw.Length == 0) return result;

            Transform[] bones = smr.bones;
            var total = new float[bones.Length];

            foreach (BoneWeight w in bw)
            {
                if (w.boneIndex0 >= 0 && w.boneIndex0 < bones.Length) total[w.boneIndex0] += w.weight0;
                if (w.boneIndex1 >= 0 && w.boneIndex1 < bones.Length) total[w.boneIndex1] += w.weight1;
                if (w.boneIndex2 >= 0 && w.boneIndex2 < bones.Length) total[w.boneIndex2] += w.weight2;
                if (w.boneIndex3 >= 0 && w.boneIndex3 < bones.Length) total[w.boneIndex3] += w.weight3;
            }

            float sum = 0f;
            foreach (float f in total) sum += f;
            if (sum <= 0f) return result;

            for (int i = 0; i < bones.Length; i++)
                if (bones[i] != null && !result.ContainsKey(bones[i].name))
                    result[bones[i].name] = total[i] / sum;

            return result;
        }

        /// <summary>
        /// 손가락 슬롯이 어느 본에 걸렸는지 그대로 보여준다.
        ///
        /// MMD의 엄지는 親指０(중수골) / 親指１ / 親指２ 3개인데 Unity는
        /// Proximal / Intermediate / Distal 3개다. 이름은 3:3으로 맞지만
        /// 가리키는 마디가 한 칸씩 다를 수 있어서, 엄지만 어긋나는 일이 흔하다.
        /// </summary>
        private static void ReportFingers(Dictionary<string, string> map, StringBuilder sb)
        {
            string[] fingers = { "Thumb", "Index", "Middle", "Ring", "Little" };
            string[] joints = { "Proximal", "Intermediate", "Distal" };

            foreach (string side in new[] { "Left", "Right" })
            {
                var lines = new List<string>();

                foreach (string f in fingers)
                {
                    var parts = new List<string>();
                    foreach (string j in joints)
                    {
                        // Unity의 휴머노이드 이름은 몸통이 "LeftUpperArm"(붙여쓰기)인데
                        // 손가락만 "Left Thumb Proximal"(띄어쓰기)이다. 둘 다 시도한다.
                        string bone = Lookup(map, $"{side} {f} {j}") ?? Lookup(map, side + f + j);
                        parts.Add(bone ?? "—");
                    }
                    lines.Add($"      {f,-7} {string.Join("  →  ", parts)}");
                }

                sb.AppendLine($"   [{side}]");
                foreach (string l in lines) sb.AppendLine(l);
            }

            sb.AppendLine("   엄지가 한 마디 밀려 있으면 Configure의 손가락 탭에서 직접 옮긴다.");
        }

        private static string Lookup(Dictionary<string, string> map, string key)
        {
            return map.TryGetValue(key, out string v) && !string.IsNullOrEmpty(v) ? v : null;
        }

        /// <summary>수평 대비 아래로 처진 각도.</summary>
        private static float Droop(Vector3 from, Vector3 to)
        {
            Vector3 d = to - from;
            if (d.sqrMagnitude < 1e-10f) return 0f;
            d.Normalize();
            return Mathf.Atan2(-d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg;
        }

        private static string Verdict(float droop)
        {
            if (droop < 15f) return "T포즈";
            if (droop < 30f) return "약간 기울음";
            return "A포즈";
        }

        /// <summary>pose가 null이면 프리팹의 바인드 포즈를, 있으면 아바타 스켈레톤 포즈를 쓴다.</summary>
        private static Vector3 WorldPos(Transform t, Dictionary<string, SkeletonBone> pose)
        {
            var chain = new List<Transform>();
            for (Transform c = t; c != null; c = c.parent) chain.Add(c);
            chain.Reverse();

            Vector3 p = Vector3.zero;
            Quaternion r = Quaternion.identity;
            Vector3 s = Vector3.one;

            foreach (Transform c in chain)
            {
                Vector3 lp = c.localPosition;
                Quaternion lr = c.localRotation;
                Vector3 ls = c.localScale;

                if (pose != null && pose.TryGetValue(c.name, out SkeletonBone b))
                {
                    lp = b.position; lr = b.rotation; ls = b.scale;
                }

                p += r * Vector3.Scale(lp, s);
                r *= lr;
                s = Vector3.Scale(s, ls);
            }
            return p;
        }

        /// <summary>
        /// 팔 계보에서 매핑 안 된 본이 실제로 정점을 끌고 있는지 센다.
        /// 끌고 있다면 포즈를 고쳐도 그 부위는 안 따라온다.
        /// </summary>
        private static void ReportUnmappedWeights(GameObject prefab, HashSet<string> mapped, StringBuilder sb)
        {
            var smr = prefab.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (smr == null || smr.sharedMesh == null || smr.bones == null)
            {
                sb.AppendLine("   SkinnedMeshRenderer를 찾지 못했다.");
                return;
            }

            BoneWeight[] weights;
            try { weights = smr.sharedMesh.boneWeights; }
            catch (System.Exception e)
            {
                sb.AppendLine($"   메시 웨이트를 읽지 못했다 ({e.GetType().Name}). " +
                              "FBX의 Read/Write를 켜면 읽을 수 있다.");
                return;
            }

            if (weights == null || weights.Length == 0)
            {
                sb.AppendLine("   웨이트 데이터가 비어 있다 (Read/Write 꺼짐).");
                return;
            }

            Transform[] bones = smr.bones;
            var total = new float[bones.Length];

            foreach (BoneWeight w in weights)
            {
                if (w.boneIndex0 >= 0 && w.boneIndex0 < bones.Length) total[w.boneIndex0] += w.weight0;
                if (w.boneIndex1 >= 0 && w.boneIndex1 < bones.Length) total[w.boneIndex1] += w.weight1;
                if (w.boneIndex2 >= 0 && w.boneIndex2 < bones.Length) total[w.boneIndex2] += w.weight2;
                if (w.boneIndex3 >= 0 && w.boneIndex3 < bones.Length) total[w.boneIndex3] += w.weight3;
            }

            float sum = 0f;
            foreach (float f in total) sum += f;
            if (sum <= 0f) { sb.AppendLine("   웨이트 합이 0이다."); return; }

            bool found = false;
            for (int i = 0; i < bones.Length; i++)
            {
                if (bones[i] == null) continue;
                string n = bones[i].name;

                bool armRelated = n.ToLowerInvariant().Contains("arm") ||
                                  n.ToLowerInvariant().Contains("elbow") ||
                                  n.ToLowerInvariant().Contains("wrist") ||
                                  n.ToLowerInvariant().Contains("twist") ||
                                  n.ToLowerInvariant().Contains("shoulder");

                if (!armRelated || mapped.Contains(n)) continue;
                if (total[i] <= 0.0001f) continue;

                sb.AppendLine($"   ○ {n,-16} 웨이트 {total[i] / sum * 100f:F2}% " +
                              "← 매핑 안 됐는데 살을 끌고 있다");
                found = true;
            }

            if (!found) sb.AppendLine("   매핑 안 된 팔 본이 끄는 웨이트 없음 ✓");
        }

        private static string ResolvePath()
        {
            foreach (Object o in Selection.objects)
            {
                string p = AssetDatabase.GetAssetPath(o);
                if (!string.IsNullOrEmpty(p) && AssetImporter.GetAtPath(p) is ModelImporter) return p;

                if (o is GameObject go)
                {
                    var smr = go.GetComponentInChildren<SkinnedMeshRenderer>(true);
                    if (smr != null && smr.sharedMesh != null)
                        return AssetDatabase.GetAssetPath(smr.sharedMesh);
                }
            }
            return null;
        }
    }
}
