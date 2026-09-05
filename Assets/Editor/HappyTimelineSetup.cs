using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using VRProject.Character;

namespace VRProject.EditorTools
{
    /// <summary>
    /// Happy 동작을 Aru에게 물려 Happy_Aru 타임라인으로 구성한다.
    ///
    /// 두 군데를 건드려야 하는데, 초보자가 흔히 한쪽만 하고 헤맨다.
    ///   1. 타임라인 에셋(.playable) — 어떤 클립을 언제 재생할지
    ///   2. 씬의 PlayableDirector — 그 트랙을 "누가" 연기할지(바인딩)
    ///
    /// 바인딩은 타임라인 에셋이 아니라 씬의 Director에 저장된다. 그래서 에셋만
    /// 만들어두면 Timeline 창에 트랙은 보이는데 재생해도 아무 일이 없다.
    ///
    /// 여러 번 실행해도 안전하다. 트랙과 클립은 매번 새로 깐다.
    /// </summary>
    public static class HappyTimelineSetup
    {
        private const string TimelinePath = "Assets/10TimeLine/Happy_Aru.playable";
        private const string HappyClipPath = "Assets/08animation/Happy.fbx";

        // 트랙 이름. 나중에 트랙을 더 늘릴 때 이걸로 찾아 재사용한다.
        private const string TrackName = "Aru";

        [MenuItem("Tools/VRProject/Happy 타임라인 구성")]
        public static void Setup()
        {
            var timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(TimelinePath);
            if (timeline == null)
            {
                Debug.LogError($"[HappyTimelineSetup] 타임라인을 찾지 못했다: {TimelinePath}");
                return;
            }

            AnimationClip happy = LoadClip(HappyClipPath);
            if (happy == null)
            {
                Debug.LogError($"[HappyTimelineSetup] {HappyClipPath} 안에서 애니메이션 클립을 찾지 못했다. " +
                               "FBX의 Rig가 Humanoid인지 확인할 것.");
                return;
            }

            WarnIfAvatarMissing(HappyClipPath);

            AnimationTrack track = BuildTrack(timeline, happy);

            EditorUtility.SetDirty(timeline);
            AssetDatabase.SaveAssets();

            Debug.Log($"[HappyTimelineSetup] 트랙 구성 완료. " +
                      $"'{TrackName}' 트랙에 '{happy.name}' ({happy.length:F2}초) 배치.", timeline);

            BindInOpenScene(timeline, track);
        }

        /// <summary>
        /// 애니메이션 트랙을 만들고 Happy 클립을 0초에 얹는다.
        ///
        /// 이미 있으면 트랙은 재사용하고 클립만 새로 깐다. 트랙을 지웠다 만들면
        /// 씬 Director에 저장된 바인딩이 끊어져 매번 다시 연결해야 하기 때문이다.
        /// </summary>
        private static AnimationTrack BuildTrack(TimelineAsset timeline, AnimationClip happy)
        {
            AnimationTrack track = timeline.GetOutputTracks()
                .OfType<AnimationTrack>()
                .FirstOrDefault(t => t.name == TrackName);

            if (track == null)
            {
                track = timeline.CreateTrack<AnimationTrack>(null, TrackName);
                Debug.Log($"[HappyTimelineSetup] '{TrackName}' 애니메이션 트랙을 만들었다.");
            }

            foreach (TimelineClip old in track.GetClips().ToArray())
            {
                timeline.DeleteClip(old);
            }

            TimelineClip clip = track.CreateClip(happy);
            clip.start = 0d;
            clip.duration = happy.length;
            clip.displayName = happy.name;

            return track;
        }

        /// <summary>
        /// 열려 있는 씬의 Aru에 PlayableDirector를 붙이고 트랙을 바인딩한다.
        ///
        /// 씬 파일을 직접 쓰지 않고 에디터 API로만 만진다. Undo도 걸어두므로
        /// 결과가 마음에 안 들면 Ctrl+Z로 되돌릴 수 있다.
        /// </summary>
        private static void BindInOpenScene(TimelineAsset timeline, AnimationTrack track)
        {
            CharacterFollow aru = Object.FindFirstObjectByType<CharacterFollow>();
            if (aru == null)
            {
                Debug.LogWarning(
                    "[HappyTimelineSetup] 열려 있는 씬에서 Aru(CharacterFollow가 붙은 오브젝트)를 " +
                    "찾지 못해 바인딩을 건너뛴다.\n" +
                    "Aru가 있는 씬을 열고 다시 실행하거나, Aru에 Playable Director를 직접 붙이고 " +
                    $"Playable에 {TimelinePath}를, '{TrackName}' 트랙에 Aru의 Animator를 넣을 것.");
                return;
            }

            Animator animator = aru.GetComponentInChildren<Animator>();
            if (animator == null)
            {
                Debug.LogError($"[HappyTimelineSetup] {aru.name}의 계층에서 Animator를 찾지 못했다. " +
                               "애니메이션 트랙은 Animator에 바인딩되어야 한다.");
                return;
            }

            PlayableDirector director = aru.GetComponent<PlayableDirector>();
            if (director == null)
            {
                director = Undo.AddComponent<PlayableDirector>(aru.gameObject);
                Debug.Log($"[HappyTimelineSetup] {aru.name}에 Playable Director를 추가했다.");
            }

            Undo.RecordObject(director, "Happy 타임라인 바인딩");

            director.playableAsset = timeline;
            director.SetGenericBinding(track, animator);

            // 기본값은 자동 재생이다. 감정 표현은 대사에 맞춰 불러 쓰는 것이라
            // 씬이 시작하자마자 혼자 웃으면 곤란하다.
            director.playOnAwake = false;

            EditorUtility.SetDirty(director);
            EditorSceneManager.MarkSceneDirty(aru.gameObject.scene);

            Debug.Log($"[HappyTimelineSetup] 바인딩 완료. " +
                      $"{aru.name} > Playable Director > '{TrackName}' 트랙 → {animator.name}.\n" +
                      "Play On Awake는 꺼두었다. 재생하려면 director.Play()를 부를 것.", director);

            WarnAboutActivationTrack(timeline);
        }

        /// <summary>
        /// 기존 Activation Track이 남아 있으면 짚어준다.
        ///
        /// 바인딩이 비어 있으면 아무 일도 안 하지만, 나중에 무심코 Aru를 물리면
        /// 클립 구간(0~5초) 밖에서 캐릭터가 통째로 사라진다. 원인을 찾기 어려운 종류다.
        /// </summary>
        private static void WarnAboutActivationTrack(TimelineAsset timeline)
        {
            bool hasActivation = timeline.GetOutputTracks().OfType<ActivationTrack>().Any();
            if (!hasActivation) return;

            Debug.LogWarning(
                "[HappyTimelineSetup] 타임라인에 Activation Track이 남아 있다. " +
                "지금은 바인딩이 비어 있어 무해하지만, 여기에 Aru를 물리면 " +
                "클립 구간 밖에서 캐릭터가 통째로 비활성화된다. 쓰지 않으면 지우는 편이 좋다.");
        }

        private static void WarnIfAvatarMissing(string path)
        {
            Avatar avatar = AssetDatabase.LoadAllAssetRepresentationsAtPath(path)
                .OfType<Avatar>()
                .FirstOrDefault();

            if (avatar != null && avatar.isValid && avatar.isHuman) return;

            Debug.LogWarning(
                $"[HappyTimelineSetup] {path}의 휴머노이드 아바타가 비어 있거나 유효하지 않다.\n" +
                "타임라인에 클립은 올라가지만 캐릭터가 움직이지 않는다. " +
                "FBX를 선택해 Rig 탭에서 Humanoid / Create From This Model로 Apply할 것.");
        }

        /// <summary>
        /// FBX 안의 애니메이션 클립을 꺼낸다.
        ///
        /// 에디터가 만드는 __preview__ 클립은 실제 에셋이 아니므로 걸러낸다.
        /// </summary>
        private static AnimationClip LoadClip(string path)
        {
            return AssetDatabase.LoadAllAssetRepresentationsAtPath(path)
                .OfType<AnimationClip>()
                .FirstOrDefault(c => !c.name.StartsWith("__preview__"));
        }
    }
}
