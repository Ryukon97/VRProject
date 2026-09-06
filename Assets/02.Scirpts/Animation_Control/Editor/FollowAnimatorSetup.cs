using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace VRProject.EditorTools
{
    /// <summary>
    /// 따라오기에 필요한 애니메이터 상태와 전이를 한 번에 만든다.
    ///
    ///     Standing Idle ──IsWalking──▶ Walking ⟲
    ///           ▲                          │
    ///           └────────!IsWalking────────┘
    ///
    /// Walking은 계속 유지되는 상태이므로 클립이 반복이어야 한다.
    /// Mixamo FBX는 기본이 1회 재생이라, 그대로 두면 한 바퀴 돌고 굳은 채 미끄러진다.
    /// 이 설정은 애니메이터가 아니라 FBX 임포터에 있어서 놓치기 쉽다.
    ///
    /// 손으로 해도 되는 작업이지만, 파라미터 이름을 한 글자만 틀려도
    /// CharacterFollow가 조용히 애니메이션만 안 나오는 상태가 된다.
    /// 이름을 코드 한 곳으로 묶어두려고 도구로 만들었다.
    ///
    /// 여러 번 실행해도 안전하다.
    /// </summary>
    public static class FollowAnimatorSetup
    {
        // CharacterFollow.WalkParameter와 반드시 같아야 한다.
        // 양쪽 다 const이므로, 이름을 바꾼다면 두 파일을 함께 고칠 것.
        private const string WalkParameter = "IsWalking";

        private const string IdleStateName = "Standing Idle";
        private const string WalkStateName = "Walking";

        private const string ControllerPath = "Assets/08animation/Aru_Real2_Controller.controller";

        // 걷기 클립을 갈아끼우려면 이 줄만 바꾸면 된다.
        private const string WalkClipPath = "Assets/08animation/Walking.fbx";

        // 전이 시간(초). 걷기 시작과 멈춤이 뚝 끊기지 않을 만큼만 겹친다.
        private const float BlendDuration = 0.2f;

        [MenuItem("Tools/VRProject/따라오기 애니메이터 설정")]
        public static void Setup()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
            {
                Debug.LogError($"[FollowAnimatorSetup] 컨트롤러를 찾지 못했다: {ControllerPath}");
                return;
            }

            AnimationClip walkClip = LoadClip(WalkClipPath);
            if (walkClip == null)
            {
                Debug.LogError($"[FollowAnimatorSetup] {WalkClipPath} 안에서 애니메이션 클립을 찾지 못했다. " +
                               "FBX의 Rig가 Humanoid인지, Animation 탭에 클립이 있는지 확인할 것.");
                return;
            }

            AnimatorStateMachine machine = controller.layers[0].stateMachine;

            AddParameterIfMissing(controller, WalkParameter, AnimatorControllerParameterType.Bool);

            AnimatorState idle = FindState(machine, IdleStateName);
            if (idle == null)
            {
                Debug.LogError($"[FollowAnimatorSetup] '{IdleStateName}' 상태가 없다. " +
                               "기본 대기 상태의 이름이 바뀌었는지 확인할 것.");
                return;
            }

            Vector3 idlePos = machine.states.First(s => s.state == idle).position;
            AnimatorState walk = FindOrCreateState(machine, WalkStateName,
                                                   idlePos + new Vector3(0f, 90f, 0f));

            // 클립은 매번 다시 물린다. FBX를 새로 임포트하면 참조가 끊어질 수 있다.
            walk.motion = walkClip;

            // 전이는 전부 지우고 새로 깐다.
            //
            // 중간 상태를 지우면 Unity가 그 상태의 전이를 앞 상태로 넘겨버린다.
            // 조건 없이 Exit Time만 달린 전이가 대기 상태에 붙어 있으면
            // IsWalking과 무관하게 제멋대로 걷기 시작하는데, 그래프만 봐서는
            // 알아채기 어렵다. 매번 백지에서 다시 깔아 그런 잔재를 없앤다.
            ClearOutgoingTransitions(idle, walk);

            Conditional(idle, walk, AnimatorConditionMode.If);      // 걷기 시작
            Conditional(walk, idle, AnimatorConditionMode.IfNot);   // 멈춤

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            WarnIfAvatarMissing(WalkClipPath);
            WarnIfNotLooping(WalkClipPath);

            Debug.Log($"[FollowAnimatorSetup] 완료. " +
                      $"'{IdleStateName}' <-> '{WalkStateName}'({walkClip.name}), " +
                      $"파라미터 '{WalkParameter}'.", controller);
        }

        /// <summary>
        /// 걷기 FBX의 리그를 Humanoid로 다시 빌드하고 반복 재생을 켠다.
        ///
        /// Rig가 Humanoid로 "지정"만 되고 아바타가 실제로 빌드되지 않은 상태가 있다.
        /// 메타에는 animationType: 3이 박혀 있는데 human/skeleton은 빈 배열인 경우다.
        /// 이러면 클립에 리타게팅할 머슬 커브가 없어서, 스테이트 전이는 정상인데
        /// 캐릭터가 아무 포즈도 잡지 않는다. 에러가 안 나서 원인 찾기가 고약하다.
        /// </summary>
        [MenuItem("Tools/VRProject/걷기 FBX 리그 복구")]
        public static void RebuildWalkRig()
        {
            var importer = AssetImporter.GetAtPath(WalkClipPath) as ModelImporter;
            if (importer == null)
            {
                Debug.LogError($"[FollowAnimatorSetup] 모델 임포터를 찾지 못했다: {WalkClipPath}");
                return;
            }

            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.autoGenerateAvatarMappingIfUnspecified = true;
            importer.importAnimation = true;
            importer.SaveAndReimport();

            // 반복 설정은 리그를 잡은 뒤에 해야 한다.
            // defaultClipAnimations는 현재 임포트 결과에서 읽어오므로,
            // Humanoid로 바뀌기 전에 읽으면 엉뚱한 테이크가 잡힌다.
            ApplyLoop(importer);

            Avatar avatar = FindAvatar(WalkClipPath);
            if (avatar == null || !avatar.isValid || !avatar.isHuman)
            {
                Debug.LogError(
                    $"[FollowAnimatorSetup] 재임포트했지만 아바타가 여전히 유효하지 않다. " +
                    $"(avatar={(avatar == null ? "없음" : avatar.isValid ? "human 아님" : "invalid")})\n" +
                    "FBX를 선택해 Rig 탭 > Configure에서 본 매핑을 직접 확인할 것. " +
                    "Mixamo 파일이면 Skin 포함으로 다시 받는 편이 빠르다.");
                return;
            }

            Debug.Log($"[FollowAnimatorSetup] 리그 복구 완료. 아바타 '{avatar.name}' (human, valid). " +
                      "이어서 [따라오기 애니메이터 설정]을 한 번 더 실행할 것.");
        }

        /// <summary>
        /// FBX 안의 테이크를 반복 재생으로 바꾼다.
        ///
        /// clipAnimations가 비어 있으면 Unity는 기본 테이크를 그대로 쓴다.
        /// 반복을 켜려면 defaultClipAnimations로 그 테이크를 꺼내 loopTime을 세우고
        /// clipAnimations에 되돌려 넣어야 한다.
        /// </summary>
        private static void ApplyLoop(ModelImporter importer)
        {
            ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
            if (clips == null || clips.Length == 0)
            {
                Debug.LogWarning($"[FollowAnimatorSetup] {importer.assetPath}에 테이크가 없어 " +
                                 "반복을 설정하지 못했다.");
                return;
            }

            foreach (ModelImporterClipAnimation clip in clips)
            {
                clip.loopTime = true;

                // 시작 포즈와 끝 포즈를 맞춰 이음매를 줄인다.
                // 이게 꺼져 있으면 한 바퀴 돌 때마다 발이 튄다.
                clip.loopPose = true;
            }

            importer.clipAnimations = clips;
            importer.SaveAndReimport();

            Debug.Log($"[FollowAnimatorSetup] 클립 {clips.Length}개를 반복 재생으로 설정했다.");
        }

        /// <summary>애니메이션 FBX의 아바타가 성한지 확인하고, 아니면 원인을 짚어준다.</summary>
        private static void WarnIfAvatarMissing(string path)
        {
            Avatar avatar = FindAvatar(path);
            if (avatar != null && avatar.isValid && avatar.isHuman) return;

            Debug.LogWarning(
                $"[FollowAnimatorSetup] {path}의 휴머노이드 아바타가 비어 있거나 유효하지 않다.\n" +
                "상태 전이는 되지만 캐릭터가 움직이지 않는다. " +
                "메뉴 [Tools > VRProject > 걷기 FBX 리그 복구]를 실행할 것.");
        }

        /// <summary>
        /// 걷기 클립이 반복으로 잡혀 있는지 본다.
        ///
        /// 어긋나도 에러가 안 나고 "왠지 어색한" 움직임으로만 나타나서
        /// 눈으로 원인을 짚기가 특히 어렵다.
        /// </summary>
        private static void WarnIfNotLooping(string path)
        {
            AnimationClip clip = LoadClip(path);
            if (clip == null || clip.isLooping) return;

            Debug.LogWarning(
                $"[FollowAnimatorSetup] {path}의 Loop Time이 꺼져 있다. " +
                "걷기는 계속 유지되므로 반복이어야 한다. " +
                "지금은 한 바퀴 돌고 굳은 채로 미끄러진다.\n" +
                "메뉴 [Tools > VRProject > 걷기 FBX 리그 복구]를 실행할 것.");
        }

        private static Avatar FindAvatar(string path)
        {
            return AssetDatabase.LoadAllAssetRepresentationsAtPath(path)
                .OfType<Avatar>()
                .FirstOrDefault();
        }

        /// <summary>
        /// FBX 안의 애니메이션 클립을 꺼낸다.
        ///
        /// Mixamo FBX는 클립이 서브 에셋으로 들어 있고 이름이 대개 'mixamo.com'이다.
        /// 에디터가 만드는 __preview__ 클립은 실제 에셋이 아니므로 걸러낸다.
        /// </summary>
        private static AnimationClip LoadClip(string path)
        {
            return AssetDatabase.LoadAllAssetRepresentationsAtPath(path)
                .OfType<AnimationClip>()
                .FirstOrDefault(c => !c.name.StartsWith("__preview__"));
        }

        private static void AddParameterIfMissing(AnimatorController controller, string name,
                                                  AnimatorControllerParameterType type)
        {
            if (controller.parameters.Any(p => p.name == name)) return;

            controller.AddParameter(name, type);
            Debug.Log($"[FollowAnimatorSetup] 파라미터 '{name}'({type})을 추가했다.");
        }

        private static AnimatorState FindState(AnimatorStateMachine machine, string name)
        {
            return machine.states.FirstOrDefault(s => s.state.name == name).state;
        }

        private static AnimatorState FindOrCreateState(AnimatorStateMachine machine,
                                                       string name, Vector3 position)
        {
            AnimatorState state = FindState(machine, name);
            if (state != null) return state;

            Debug.Log($"[FollowAnimatorSetup] '{name}' 상태를 만들었다.");
            return machine.AddState(name, position);
        }

        /// <summary>
        /// 주어진 상태의 나가는 전이를 모두 지운다.
        ///
        /// 이 도구가 그래프의 유일한 출처가 되도록 매번 백지에서 다시 깐다.
        /// 손으로 만져둔 전이가 있었다면 같이 사라지므로, 조정은 이 코드에서 할 것.
        /// </summary>
        private static void ClearOutgoingTransitions(params AnimatorState[] states)
        {
            foreach (AnimatorState state in states)
            {
                if (state == null) continue;
                foreach (AnimatorStateTransition t in state.transitions.ToArray())
                {
                    state.RemoveTransition(t);
                }
            }
        }

        /// <summary>
        /// 파라미터 조건으로만 넘어가는 전이를 만든다.
        ///
        /// hasExitTime을 끄는 것이 중요하다. 켜두면 클립이 한 바퀴 다 돌 때까지
        /// 반응하지 않아서, 플레이어가 범위 안에 들어와도 한동안 계속 걸어온다.
        /// </summary>
        private static void Conditional(AnimatorState from, AnimatorState to,
                                        AnimatorConditionMode mode)
        {
            AnimatorStateTransition t = from.AddTransition(to);
            t.hasExitTime = false;
            t.exitTime = 0f;
            t.duration = BlendDuration;
            t.hasFixedDuration = true;
            t.AddCondition(mode, 0f, WalkParameter);
        }
    }
}
