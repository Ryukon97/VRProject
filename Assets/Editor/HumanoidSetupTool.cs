using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace VRProject.EditorTools
{
    /// <summary>
    /// Mixamo 애니메이션을 MMD 변환 모델에 붙이기 위한 휴머노이드 셋업.
    ///
    /// Mixamo 스켈레톤(mixamorig:Spine1/Spine2)과 이 모델의 스켈레톤(Spine/Chest)은
    /// 이름도 개수도 다르다. 휴머노이드 리타게팅 외에는 재생할 방법이 없어서,
    /// 양쪽 FBX를 모두 Humanoid로 바꾸고 아바타를 통해 연결한다.
    ///
    /// 메뉴: Tools ▸ Toon ▸ Animation
    /// </summary>
    public static class HumanoidSetupTool
    {
        // 휴머노이드가 반드시 요구하는 본. 하나라도 비면 아바타가 무효가 된다.
        private static readonly string[] RequiredBones =
        {
            "Hips", "Spine", "Head",
            "LeftUpperLeg", "LeftLowerLeg", "LeftFoot",
            "RightUpperLeg", "RightLowerLeg", "RightFoot",
            "LeftUpperArm", "LeftLowerArm", "LeftHand",
            "RightUpperArm", "RightLowerArm", "RightHand",
        };

        // ────────────────────────────────────────────────────────────
        [MenuItem("Tools/Toon/Animation/1. 선택 모델을 휴머노이드로 변환")]
        private static void ConvertSelectionToHumanoid()
        {
            var paths = CollectModelPaths();
            if (paths.Count == 0)
            {
                EditorUtility.DisplayDialog("선택 없음",
                    "Project 창에서 FBX를, 또는 Hierarchy에서 모델을 선택한 뒤 실행하세요.", "확인");
                return;
            }

            var log = new StringBuilder("[HumanoidSetup] 휴머노이드 변환\n");

            foreach (string path in paths)
            {
                var imp = AssetImporter.GetAtPath(path) as ModelImporter;
                if (imp == null) { log.AppendLine($"  ✗ {path} — ModelImporter 아님"); continue; }

                imp.animationType = ModelImporterAnimationType.Human;
                imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;

                // 계층이 사라지면 물리본(Hair/Coat)과 CharacterGaze가 참조하는
                // Spine/Chest/Neck/Head에 접근할 수 없게 된다.
                imp.optimizeGameObjects = false;

                imp.SaveAndReimport();
                log.AppendLine($"  ✓ {Path.GetFileName(path)} → Humanoid");

                // 자동 매퍼가 중복된 이름의 본을 잡으면 아바타 생성이 실패한다.
                // 변환 직후에 바로 정리한다.
                RepairAmbiguous(path, log);
            }

            AssetDatabase.Refresh();
            Debug.Log(log.ToString());

            EditorUtility.DisplayDialog("변환 완료",
                log + "\n이어서 '2. 아바타 매핑 검증'을 실행해 결과를 확인하세요.\n" +
                "Configure에서 T포즈(Enforce T-Pose)도 반드시 확인해야 합니다.", "확인");
        }

        // ────────────────────────────────────────────────────────────
        [MenuItem("Tools/Toon/Animation/1-B. 매핑 충돌 수리 (중복 본 이름)")]
        private static void RepairSelection()
        {
            var paths = CollectModelPaths();
            if (paths.Count == 0)
            {
                EditorUtility.DisplayDialog("선택 없음", "FBX 또는 모델을 선택하세요.", "확인");
                return;
            }

            var log = new StringBuilder("[HumanoidSetup] 매핑 충돌 수리\n");
            foreach (string path in paths) RepairAmbiguous(path, log);

            Debug.Log(log.ToString());
            EditorUtility.DisplayDialog("수리 완료", log.ToString(), "확인");
        }

        /// <summary>
        /// 계층에 같은 이름의 트랜스폼이 둘 이상 있으면 Unity는 아바타를 만들지 못한다.
        ///
        /// 이 모델은 FBX 안에 스켈레톤이 두 벌 들어 있다 — 원본 MMD(일본어 이름)와
        /// Blender에서 영문으로 renaming한 것. 물리본(Hair_*, Coat_* …)은 양쪽에서
        /// 이름이 같아서, 자동 매퍼가 그중 하나를 Jaw 같은 슬롯에 잘못 잡으면
        /// "Ambiguous Transform" 오류로 아바타 생성이 통째로 실패한다.
        ///
        /// 필수 본은 두 스켈레톤에서 이름이 달라(Hips vs 腰) 충돌하지 않으므로,
        /// 중복 이름을 가리키는 매핑만 걷어내면 된다.
        /// </summary>
        private static void RepairAmbiguous(string path, StringBuilder log)
        {
            var imp = AssetImporter.GetAtPath(path) as ModelImporter;
            if (imp == null || imp.animationType != ModelImporterAnimationType.Human) return;

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) { log.AppendLine($"  ✗ {Path.GetFileName(path)} — 프리팹 로드 실패"); return; }

            // 계층 전체에서 이름별 개수를 센다.
            var counts = new Dictionary<string, int>();
            foreach (Transform t in prefab.GetComponentsInChildren<Transform>(true))
                counts[t.name] = counts.TryGetValue(t.name, out int c) ? c + 1 : 1;

            HumanDescription hd = imp.humanDescription;
            var kept = new List<HumanBone>();
            var dropped = new List<string>();
            var blocked = new List<string>();

            foreach (HumanBone hb in hd.human)
            {
                bool duplicated = !string.IsNullOrEmpty(hb.boneName)
                                  && counts.TryGetValue(hb.boneName, out int n) && n > 1;

                if (!duplicated) { kept.Add(hb); continue; }

                // 필수 본이 중복 이름이면 걷어내도 아바타가 안 만들어진다.
                // 지우지 말고 문제만 알린다.
                if (System.Array.IndexOf(RequiredBones, hb.humanName) >= 0)
                {
                    kept.Add(hb);
                    blocked.Add($"{hb.humanName} ← {hb.boneName} (중복 {counts[hb.boneName]}개)");
                    continue;
                }

                dropped.Add($"{hb.humanName} ← {hb.boneName} (중복 {counts[hb.boneName]}개)");
            }

            log.AppendLine($"── {Path.GetFileName(path)}");

            if (dropped.Count == 0 && blocked.Count == 0)
            {
                log.AppendLine("   중복 이름을 가리키는 매핑 없음 ✓");
                return;
            }

            foreach (string d in dropped) log.AppendLine($"   제거: {d}");

            foreach (string b in blocked)
                log.AppendLine($"   ⚠ 필수 본이 중복 이름이다 — 수동 처리 필요: {b}");

            if (dropped.Count > 0)
            {
                hd.human = kept.ToArray();
                imp.humanDescription = hd;
                imp.SaveAndReimport();
                log.AppendLine($"   → {dropped.Count}개 제거 후 재임포트했다.");
            }
        }

        // ────────────────────────────────────────────────────────────
        [MenuItem("Tools/Toon/Animation/1-C. 블렌드셰이프 노멀 끄기 (깜빡임 흔들림 해결)")]
        private static void DisableBlendShapeNormals()
        {
            var paths = CollectModelPaths();
            if (paths.Count == 0)
            {
                EditorUtility.DisplayDialog("선택 없음", "FBX 또는 모델을 선택하세요.", "확인");
                return;
            }

            var log = new StringBuilder("[HumanoidSetup] 블렌드셰이프 노멀 끄기\n");

            foreach (string path in paths)
            {
                var imp = AssetImporter.GetAtPath(path) as ModelImporter;
                if (imp == null) continue;

                ModelImporterNormals before = imp.importBlendShapeNormals;

                // Calculate이면 Unity가 블렌드셰이프 프레임마다 노멀을 다시 굽는다.
                // 이 모델은 얼굴·머리카락·옷이 전부 한 메시라, 눈을 깜빡일 때마다
                // 메시 전체의 노멀이 미세하게 흔들리고 툰 셰이딩의 임계값이
                // 그 변화를 명암 경계 이동으로 증폭시킨다.
                //
                // None으로 두면 블렌드셰이프가 위치만 움직이고 노멀은 원본을 유지한다.
                imp.importBlendShapeNormals = ModelImporterNormals.None;
                imp.SaveAndReimport();

                log.AppendLine($"  ✓ {Path.GetFileName(path)}: {before} → None");
            }

            Debug.Log(log.ToString());
            EditorUtility.DisplayDialog("완료",
                log + "\n눈을 깜빡여도 머리카락·옷 셰이딩이 흔들리지 않아야 합니다.", "확인");
        }

        // ────────────────────────────────────────────────────────────
        [MenuItem("Tools/Toon/Animation/2. 아바타 매핑 검증")]
        private static void VerifyAvatar()
        {
            var paths = CollectModelPaths();
            if (paths.Count == 0)
            {
                EditorUtility.DisplayDialog("선택 없음", "FBX 또는 모델을 선택하세요.", "확인");
                return;
            }

            var sb = new StringBuilder();
            foreach (string path in paths) VerifyOne(path, sb);
            Debug.Log(sb.ToString());
        }

        private static void VerifyOne(string path, StringBuilder sb)
        {
            sb.AppendLine($"╔══ 아바타 검증: {Path.GetFileName(path)}");

            var imp = AssetImporter.GetAtPath(path) as ModelImporter;
            if (imp == null) { sb.AppendLine("   ✗ ModelImporter 아님\n"); return; }

            sb.AppendLine($"   Animation Type : {imp.animationType}");
            sb.AppendLine($"   Optimize GO    : {imp.optimizeGameObjects}" +
                          (imp.optimizeGameObjects ? "   ← 켜져 있으면 계층이 사라진다" : ""));

            if (imp.animationType != ModelImporterAnimationType.Human)
            {
                sb.AppendLine("   ✗ Humanoid가 아니다. 1번 메뉴로 먼저 변환할 것.\n");
                return;
            }

            // 생성된 아바타 자체가 유효한지
            Avatar avatar = FindAvatar(path);
            if (avatar == null) sb.AppendLine("   ✗ 아바타를 찾지 못했다.");
            else sb.AppendLine($"   Avatar         : {avatar.name}  isValid={avatar.isValid}  isHuman={avatar.isHuman}");

            // 휴머노이드 슬롯 ↔ 실제 본 매핑
            var map = new Dictionary<string, string>();
            foreach (HumanBone hb in imp.humanDescription.human)
            {
                if (!string.IsNullOrEmpty(hb.humanName)) map[hb.humanName] = hb.boneName;
            }

            sb.AppendLine($"   매핑된 본       : {map.Count}개");
            sb.AppendLine("── 필수 본 ──");

            var missing = new List<string>();
            foreach (string need in RequiredBones)
            {
                if (map.TryGetValue(need, out string bone) && !string.IsNullOrEmpty(bone))
                    sb.AppendLine($"   ✓ {need,-16} ← {bone}");
                else
                {
                    sb.AppendLine($"   ✗ {need,-16} ← (없음)");
                    missing.Add(need);
                }
            }

            // 트위스트 본이 주요 슬롯에 잘못 잡히는 사고가 흔하다.
            // 이 모델에는 zArmTwist_L/R, zHandTwist_L/R 이 있다.
            sb.AppendLine("── 의심되는 매핑 ──");
            bool suspicious = false;
            foreach (KeyValuePair<string, string> kv in map)
            {
                string bone = kv.Value ?? "";
                bool isTwist = bone.ToLowerInvariant().Contains("twist");
                bool isMainSlot = kv.Key.EndsWith("UpperArm") || kv.Key.EndsWith("LowerArm") ||
                                  kv.Key.EndsWith("Hand") || kv.Key.EndsWith("UpperLeg") ||
                                  kv.Key.EndsWith("LowerLeg");

                if (isTwist && isMainSlot)
                {
                    sb.AppendLine($"   ⚠ {kv.Key,-16} ← {bone}   트위스트 본이 주 슬롯에 잡혔다");
                    suspicious = true;
                }
            }
            if (!suspicious) sb.AppendLine("   없음 ✓");

            // 손가락은 선택이지만 있으면 포즈가 훨씬 자연스럽다.
            int fingers = 0;
            foreach (string k in map.Keys)
                if (k.Contains("Thumb") || k.Contains("Index") || k.Contains("Middle") ||
                    k.Contains("Ring") || k.Contains("Little")) fingers++;
            sb.AppendLine($"── 손가락 매핑: {fingers}개 (선택 사항) ──");

            CheckTPose(imp, map, sb);

            sb.AppendLine(missing.Count == 0
                ? "결과: 필수 본이 모두 매핑됐다. 리타게팅 가능.\n"
                : $"결과: 필수 본 {missing.Count}개 누락 — Configure에서 직접 지정해야 한다.\n");
        }

        // ────────────────────────────────────────────────────────────
        [MenuItem("Tools/Toon/Animation/3. Animator 셋업 (씬의 캐릭터 선택)")]
        private static void SetupAnimator()
        {
            GameObject go = Selection.activeGameObject;
            if (go == null || !go.scene.IsValid())
            {
                EditorUtility.DisplayDialog("선택 없음",
                    "Hierarchy에서 캐릭터(Aru_Real2)를 선택한 뒤 실행하세요.", "확인");
                return;
            }

            string modelPath = ResolveModelPath(go);
            if (string.IsNullOrEmpty(modelPath))
            {
                EditorUtility.DisplayDialog("모델을 찾지 못함",
                    "선택한 오브젝트에서 SkinnedMeshRenderer를 찾지 못했습니다.", "확인");
                return;
            }

            Avatar avatar = FindAvatar(modelPath);
            if (avatar == null || !avatar.isHuman)
            {
                EditorUtility.DisplayDialog("아바타 없음",
                    "휴머노이드 아바타가 없습니다. 1번 메뉴로 먼저 변환하세요.", "확인");
                return;
            }

            // 프로젝트의 휴머노이드 애니메이션 클립을 모은다.
            var clips = FindHumanoidClips();
            if (clips.Count == 0)
            {
                EditorUtility.DisplayDialog("클립 없음",
                    "휴머노이드 애니메이션 클립을 찾지 못했습니다.\n" +
                    "Idle.fbx도 1번 메뉴로 Humanoid 변환해야 합니다.", "확인");
                return;
            }

            const string dir = "Assets/animation";
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            string ctrlPath = $"{dir}/{go.name}_Controller.controller";
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ctrlPath);

            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(ctrlPath);
            }

            // 첫 클립을 기본 상태로. 나머지는 상태만 만들어두고 전이는 직접 잇는다.
            AnimatorStateMachine sm = controller.layers[0].stateMachine;
            var existing = new HashSet<string>();
            foreach (ChildAnimatorState s in sm.states) existing.Add(s.state.name);

            AnimatorState first = null;
            foreach (AnimationClip clip in clips)
            {
                if (existing.Contains(clip.name)) continue;
                AnimatorState st = sm.AddState(clip.name);
                st.motion = clip;
                if (first == null) first = st;
            }
            if (first != null && sm.defaultState == null) sm.defaultState = first;

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            Animator animator = go.GetComponent<Animator>();
            if (animator == null)
            {
                animator = Undo.AddComponent<Animator>(go);
            }
            else Undo.RecordObject(animator, "Setup Animator");

            animator.avatar = avatar;
            animator.runtimeAnimatorController = controller;

            // Mixamo의 In-Place 클립은 루트 모션이 없다. 켜두면 제자리에서 미끄러진다.
            animator.applyRootMotion = false;

            // 화면 밖에서도 표정·시선 스크립트가 일관되게 돌도록.
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            EditorUtility.SetDirty(animator);
            if (PrefabUtility.IsPartOfPrefabInstance(animator))
                PrefabUtility.RecordPrefabInstancePropertyModifications(animator);

            var names = new List<string>();
            foreach (AnimationClip c in clips) names.Add(c.name);

            string msg = $"Animator 셋업 완료\n\n" +
                         $"컨트롤러 : {ctrlPath}\n" +
                         $"아바타   : {avatar.name}\n" +
                         $"클립     : {string.Join(", ", names)}\n\n" +
                         $"Root Motion 꺼짐, Culling = AlwaysAnimate";

            Debug.Log($"[HumanoidSetup] {msg}", go);
            EditorUtility.DisplayDialog("완료", msg, "확인");
        }

        /// <summary>
        /// 아바타가 T포즈로 잡혔는지 판정한다.
        ///
        /// Mixamo 모션은 T포즈 기준으로 만들어진다. 아바타가 A포즈로 남아 있으면
        /// 리타게팅한 팔이 어긋난다. 이 모델처럼 MMD 원본은 대부분 A포즈다.
        ///
        /// 위팔에서 손까지의 방향이 수평에 가까우면 T, 아래로 기울면 A로 본다.
        /// </summary>
        private static void CheckTPose(ModelImporter imp, Dictionary<string, string> map, StringBuilder sb)
        {
            sb.AppendLine("── T포즈 판정 ──");

            string path = imp.assetPath;
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) { sb.AppendLine("   프리팹 로드 실패 — 판정 불가"); return; }

            // 아바타가 쓰는 스켈레톤 포즈. Enforce T-Pose는 여기를 바꾼다.
            var pose = new Dictionary<string, SkeletonBone>();
            foreach (SkeletonBone b in imp.humanDescription.skeleton)
                if (!string.IsNullOrEmpty(b.name)) pose[b.name] = b;

            var byName = new Dictionary<string, Transform>();
            foreach (Transform t in prefab.GetComponentsInChildren<Transform>(true))
                if (!byName.ContainsKey(t.name)) byName[t.name] = t;

            if (!TryBone(map, byName, "LeftUpperArm", out Transform upper) ||
                !TryBone(map, byName, "LeftHand", out Transform hand))
            {
                sb.AppendLine("   왼팔 본을 찾지 못해 판정 불가");
                return;
            }

            Vector3 a = PoseWorldPos(upper, pose);
            Vector3 b2 = PoseWorldPos(hand, pose);
            Vector3 dir = b2 - a;

            if (dir.sqrMagnitude < 1e-8f) { sb.AppendLine("   팔 길이가 0 — 판정 불가"); return; }

            dir.Normalize();

            // 수평 성분 대비 아래로 처진 각도.
            float horizontal = new Vector2(dir.x, dir.z).magnitude;
            float droop = Mathf.Atan2(-dir.y, horizontal) * Mathf.Rad2Deg;

            sb.AppendLine($"   위팔→손 하향각: {droop:F1}°");

            if (droop < 15f)
                sb.AppendLine("   ✓ T포즈로 보인다. Mixamo 리타게팅에 적합.");
            else if (droop < 30f)
                sb.AppendLine("   △ 살짝 기울어 있다. 팔이 어색하면 Configure에서 Enforce T-Pose.");
            else
                sb.AppendLine($"   ✗ A포즈다({droop:F0}° 하향). Mixamo 모션이 어긋난다.\n" +
                              "     Configure... → Pose ▾ → Enforce T-Pose → Done → Apply");
        }

        private static bool TryBone(Dictionary<string, string> map, Dictionary<string, Transform> byName,
                                    string humanName, out Transform t)
        {
            t = null;
            return map.TryGetValue(humanName, out string bone)
                   && !string.IsNullOrEmpty(bone)
                   && byName.TryGetValue(bone, out t);
        }

        /// <summary>아바타 스켈레톤 포즈를 누적해 월드 위치를 구한다.</summary>
        private static Vector3 PoseWorldPos(Transform t, Dictionary<string, SkeletonBone> pose)
        {
            var chain = new List<Transform>();
            for (Transform c = t; c != null; c = c.parent) chain.Add(c);
            chain.Reverse();

            Vector3 p = Vector3.zero;
            Quaternion r = Quaternion.identity;
            Vector3 s = Vector3.one;

            foreach (Transform c in chain)
            {
                Vector3 lp; Quaternion lr; Vector3 ls;

                if (pose.TryGetValue(c.name, out SkeletonBone b))
                {
                    lp = b.position; lr = b.rotation; ls = b.scale;
                }
                else
                {
                    lp = c.localPosition; lr = c.localRotation; ls = c.localScale;
                }

                p += r * Vector3.Scale(lp, s);
                r *= lr;
                s = Vector3.Scale(s, ls);
            }

            return p;
        }

        // ────────────────────────────────────────────────────────────
        [MenuItem("Tools/Toon/Animation/4. 선택한 클립을 기본 애니메이션으로")]
        private static void SetDefaultClip()
        {
            AnimationClip clip = PickSelectedClip();
            if (clip == null)
            {
                EditorUtility.DisplayDialog("클립 선택 필요",
                    "Project 창에서 애니메이션 FBX(또는 그 안의 클립)를 선택한 뒤 실행하세요.\n\n" +
                    "FBX가 Humanoid로 변환돼 있어야 클립이 잡힙니다 — " +
                    "안 되면 '1. 휴머노이드로 변환'을 먼저 실행하세요.", "확인");
                return;
            }

            if (!clip.isHumanMotion)
            {
                EditorUtility.DisplayDialog("휴머노이드 클립이 아님",
                    $"'{clip.name}'은 휴머노이드 모션이 아닙니다.\n" +
                    "해당 FBX를 '1. 휴머노이드로 변환'으로 먼저 처리하세요.", "확인");
                return;
            }

            var controllers = new List<AnimatorController>();
            foreach (string guid in AssetDatabase.FindAssets("t:AnimatorController", new[] { "Assets/animation" }))
            {
                var c = AssetDatabase.LoadAssetAtPath<AnimatorController>(AssetDatabase.GUIDToAssetPath(guid));
                if (c != null) controllers.Add(c);
            }

            if (controllers.Count == 0)
            {
                EditorUtility.DisplayDialog("컨트롤러 없음",
                    "Assets/animation에서 AnimatorController를 찾지 못했습니다.", "확인");
                return;
            }

            var log = new StringBuilder($"[HumanoidSetup] 기본 클립 → {clip.name}\n");

            foreach (AnimatorController controller in controllers)
            {
                AnimatorStateMachine sm = controller.layers[0].stateMachine;

                // 같은 클립을 이미 쓰는 상태가 있으면 그것을 기본으로 삼는다.
                AnimatorState found = null;
                foreach (ChildAnimatorState cs in sm.states)
                {
                    if (cs.state != null && cs.state.motion == clip) { found = cs.state; break; }
                }

                if (found == null)
                {
                    // 기본 상태가 하나뿐이면 모션만 갈아끼운다. 상태를 늘리지 않는 편이 깔끔하다.
                    if (sm.states.Length == 1 && sm.states[0].state != null)
                    {
                        found = sm.states[0].state;
                        Undo.RecordObject(found, "Set default clip");
                        found.motion = clip;
                        found.name = clip.name;
                    }
                    else
                    {
                        found = sm.AddState(clip.name);
                        found.motion = clip;
                    }
                }

                sm.defaultState = found;

                EditorUtility.SetDirty(found);
                EditorUtility.SetDirty(controller);
                log.AppendLine($"  ✓ {controller.name}: 기본 상태 '{found.name}'");
            }

            AssetDatabase.SaveAssets();
            Debug.Log(log.ToString());
            EditorUtility.DisplayDialog("완료", log.ToString(), "확인");
        }

        /// <summary>선택에서 휴머노이드 애니메이션 클립을 찾는다. FBX를 골라도 안쪽 클립을 집는다.</summary>
        private static AnimationClip PickSelectedClip()
        {
            foreach (Object o in Selection.objects)
            {
                if (o is AnimationClip direct && !direct.name.StartsWith("__preview__")) return direct;

                string path = AssetDatabase.GetAssetPath(o);
                if (string.IsNullOrEmpty(path)) continue;

                foreach (Object sub in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (sub is AnimationClip c && !c.name.StartsWith("__preview__")) return c;
                }
            }
            return null;
        }

        // ── 헬퍼 ────────────────────────────────────────────────────

        private static List<string> CollectModelPaths()
        {
            var paths = new List<string>();

            foreach (Object o in Selection.objects)
            {
                string p = AssetDatabase.GetAssetPath(o);

                if (!string.IsNullOrEmpty(p) && AssetImporter.GetAtPath(p) is ModelImporter)
                {
                    if (!paths.Contains(p)) paths.Add(p);
                    continue;
                }

                if (o is GameObject go)
                {
                    string mp = ResolveModelPath(go);
                    if (!string.IsNullOrEmpty(mp) && !paths.Contains(mp)) paths.Add(mp);
                }
            }

            return paths;
        }

        /// <summary>메시 에셋 경로로 원본 FBX를 역추적한다. 경로를 박아두는 것보다 정확하다.</summary>
        private static string ResolveModelPath(GameObject go)
        {
            var smr = go.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (smr != null && smr.sharedMesh != null)
                return AssetDatabase.GetAssetPath(smr.sharedMesh);

            var mf = go.GetComponentInChildren<MeshFilter>(true);
            if (mf != null && mf.sharedMesh != null)
                return AssetDatabase.GetAssetPath(mf.sharedMesh);

            return null;
        }

        private static Avatar FindAvatar(string modelPath)
        {
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(modelPath))
                if (o is Avatar a) return a;
            return null;
        }

        private static List<AnimationClip> FindHumanoidClips()
        {
            var result = new List<AnimationClip>();

            foreach (string guid in AssetDatabase.FindAssets("t:AnimationClip", new[] { "Assets/animation" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (o is AnimationClip c && !c.name.StartsWith("__preview__") && c.isHumanMotion)
                        result.Add(c);
                }
            }

            return result;
        }
    }
}
