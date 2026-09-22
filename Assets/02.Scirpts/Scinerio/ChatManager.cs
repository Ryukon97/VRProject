using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
//using UnityEngine.UIElements;

public class ChatManager : MonoBehaviour
{
    [Header("Data Source")]
    public DialogueDataSO currentScenario;
    public SoundDataSO bgmSetting;

    [Header("UI References")]
    public TextMeshProUGUI ChatText;
    public TextMeshProUGUI CharacterName;
    public GameObject choicePanel;
    public TextMeshProUGUI[] choiceButtonsText;
    public DialogueEntry currentEntry;


    [Header("UI References")]
    public UnityEngine.UI.Image ChatImage;





    public bool isPausedByMenu = false;
    private int nextIDResult = -1;
    private float 선택입력허용시간;

    [Header("VR: 선택지 위치")]
    [Tooltip("선택지가 열렸을 때 눈에서 떨어질 거리(m). 컨트롤러보다 앞에 놓이도록 1.5m를 권장합니다.")]
    [SerializeField, Range(0.8f, 3f)] private float 선택지거리 = 1.5f;

    [Tooltip("멀어진 선택지가 너무 작아지지 않도록 ChoicePanel만 키우는 배율입니다.")]
    [SerializeField, Range(1f, 3f)] private float 선택지크기배율 = 2f;

    private VRProject.Dialogue.VRDialogueUI 선택지대화UI;
    private float 선택지전거리;
    private Vector3 선택지전크기;
    private bool 선택지배치변경중;

    // ── VR 확장 ─────────────────────────────────────────────────
    [Header("VR: 다음 대사 입력")]
    [Tooltip("Quest 컨트롤러의 A/B/X/Y를 묶은 InputAction.\n" +
             "비워두면 실행 중 A/B/X/Y 입력을 자동으로 만들어 사용한다.")]
    public UnityEngine.InputSystem.InputActionReference vrAdvanceAction;
    private UnityEngine.InputSystem.InputAction 자동VR넘김액션;

    [Header("VR: 선택지 타임라인")]
    [Tooltip("ChoiceData.timeline을 재생할 씬의 PlayableDirector.\n" +
             "비워두면 타임라인을 건너뛰고 기존처럼 바로 이동한다.")]
    public UnityEngine.Playables.PlayableDirector choiceDirector;

    [Tooltip("타임라인 재생 중 대사 진행을 멈출지. 보통 켜둔다.")]
    public bool pauseDuringTimeline = true;

    [Header("보이스")]
    [Tooltip("대사 보이스를 재생할 AudioSource.\n" +
             "비워두면 아래 설정에 맞춰 자동으로 만든다.")]
    public AudioSource voiceSource;

    [Tooltip("보이스를 캐릭터 입 위치에서 3D로 재생한다.\n\n" +
             "끄면 어디를 보든 같은 크기로 들리는 2D가 된다.\n" +
             "나레이션처럼 화자가 화면에 없는 대사가 많으면 끄는 편이 낫다.")]
    public bool voice3D = true;

    [Tooltip("보이스가 나올 위치. 비워두면 캐릭터의 Head 본을 찾는다.")]
    public Transform voiceAnchor;

    [Tooltip("이 거리(m) 안에서는 최대 음량으로 들린다.\n\n" +
             "이 씬은 월드가 2배 스케일이라, 체감 1m는 월드 2m다.\n" +
             "거리 값도 그만큼 키워 잡아야 한다.")]
    [Range(0.5f, 20f)] public float voiceMinDistance = 2f;

    [Tooltip("이 거리(m)를 넘으면 들리지 않는다.")]
    [Range(2f, 200f)] public float voiceMaxDistance = 30f;

    [Header("얼굴 연출")]
    [Tooltip("대사의 '표정 번호'를 적용할 컴포넌트.\n비워두면 씬에서 자동으로 찾는다.")]
    public VRProject.Character.FacialExpression 표정;

    [Tooltip("대사의 '입모양 재생'을 담당할 컴포넌트.\n비워두면 씬에서 자동으로 찾는다.")]
    public VRProject.Character.MouthFlap 입모양;

    [Tooltip("대사의 표정 번호가 -1일 때 되돌릴 표정 번호.\n" +
             "보통 0(기본)이다. 표정 목록의 순서를 바꿨다면 여기도 맞출 것.")]
    public int 기본표정번호 = 0;
    private Coroutine 입모양음성동기루틴;

    [Header("전신 애니메이션")]
    [Tooltip("시나리오의 전신 애니메이션을 재생할 아루의 Animator. 비워두면 자동으로 찾습니다.")]
    public Animator bodyAnimator;

    [Tooltip("전신 연출이 끝난 뒤 돌아갈 Animator 상태 이름")]
    public string bodyIdleStateName = "Standing Idle";

    [Range(0f, 1f)] public float bodyAnimationBlendTime = 0.15f;
    private Coroutine bodyAnimationRoutine;
    private bool 타임라인발고정중;
    private float 타임라인바닥발높이;

    [Header("타이핑")]
    [Tooltip("글자 하나가 찍히는 간격(초). 작을수록 빠르다.")]
    [Range(0f, 0.2f)] public float typeSpeed = 0.05f;

    [Tooltip("입력이 막힌 원인을 콘솔에 찍는다. 평소에는 꺼둘 것.")]
    public bool logBlockedClicks = false;

    // InputActionReference로 참조한 액션은 자동으로 켜지지 않는다.
    // 켜주지 않으면 WasPressedThisFrame()이 영원히 false다.
    void OnEnable()
    {
        if (vrAdvanceAction != null && vrAdvanceAction.action != null)
        {
            vrAdvanceAction.action.Enable();
        }
        else
        {
            // 씬이나 프리팹의 InputActionReference가 빠져도 Quest에서 대사가
            // 막히지 않도록 A/B/X/Y 입력을 런타임에 직접 만든다.
            자동VR넘김액션 = new UnityEngine.InputSystem.InputAction(
                "DialogueAdvanceFallback",
                UnityEngine.InputSystem.InputActionType.Button);
            자동VR넘김액션.AddBinding("<XRController>{RightHand}/primaryButton");
            자동VR넘김액션.AddBinding("<XRController>{RightHand}/secondaryButton");
            자동VR넘김액션.AddBinding("<XRController>{LeftHand}/primaryButton");
            자동VR넘김액션.AddBinding("<XRController>{LeftHand}/secondaryButton");
            자동VR넘김액션.Enable();

            Debug.LogWarning(
                "<color=orange>[ChatManager] VR Advance Action이 비어 있어 " +
                "Quest A/B/X/Y 자동 입력을 사용합니다.</color>", this);
        }

        // 표정과 입모양은 캐릭터에 붙어 있고 ChatManager는 UI 쪽에 있어서
        // 인스펙터로 잇는 것을 잊기 쉽다. 비어 있으면 씬에서 찾아 쓴다.
        if (표정 == null) 표정 = FindAnyObjectByType<VRProject.Character.FacialExpression>();
        if (입모양 == null) 입모양 = FindAnyObjectByType<VRProject.Character.MouthFlap>();
        if (bodyAnimator == null)
        {
            if (표정 != null) bodyAnimator = 표정.GetComponentInParent<Animator>();
            if (bodyAnimator == null)
            {
                var follow = FindAnyObjectByType<VRProject.Character.CharacterFollow>();
                if (follow != null) bodyAnimator = follow.GetComponentInChildren<Animator>();
            }
        }

        EnsureVoiceSource();

        VRProject.Sound.SoundSettings.Changed += 더빙음량반영;
    }

    /// <summary>
    /// 보이스 전용 AudioSource를 마련한다.
    ///
    /// BGMManager.PlayOneShotSE는 PlayClipAtPoint를 쓰는데, 그건 임시 오브젝트를
    /// 만들어 재생하고 끝날 때까지 손댈 수 없다. 대사를 넘겼는데 이전 보이스가
    /// 계속 들리면 곤란하므로, 멈출 수 있는 전용 소스를 따로 쓴다.
    /// </summary>
    void EnsureVoiceSource()
    {
        if (voiceSource == null)
        {
            // 3D면 소리가 캐릭터 입에서 나야 하므로 그쪽에 붙인다.
            // 2D는 위치가 의미 없으니 자기 자신에 둔다.
            Transform 붙일곳 = voice3D ? (voiceAnchor != null ? voiceAnchor : 머리찾기()) : transform;
            voiceSource = 붙일곳.gameObject.AddComponent<AudioSource>();
        }

        voiceSource.playOnAwake = false;
        voiceSource.loop = false;

        voiceSource.spatialBlend = voice3D ? 1f : 0f;

        // spatialize를 켜야 스페셜라이저 플러그인이 이 소스를 처리한다.
        // spatialBlend만 1로 올리면 Unity 기본 좌우 패닝에 그친다.
        voiceSource.spatialize = voice3D;
        voiceSource.spatializePostEffects = false;

        voiceSource.rolloffMode = AudioRolloffMode.Logarithmic;
        voiceSource.minDistance = voiceMinDistance;
        voiceSource.maxDistance = Mathf.Max(voiceMaxDistance, voiceMinDistance + 0.1f);

        // 도플러는 반드시 끈다.
        //
        // VR 텔레포트는 한 프레임에 수십 미터를 순간이동하는데, 도플러가 켜져 있으면
        // 그 순간 속도를 엄청나게 계산해서 목소리 음정이 괴상하게 튄다.
        // 대사에는 도플러가 줄 이득이 없다.
        voiceSource.dopplerLevel = 0f;
    }

    /// <summary>
    /// 보이스가 나올 자리를 찾는다.
    ///
    /// 입 위치가 가장 정확하지만 입 본이 따로 없는 모델이라 Head 본을 쓴다.
    /// 그것도 없으면 캐릭터 루트, 그마저 없으면 자기 자신으로 물러선다.
    /// </summary>
    Transform 머리찾기()
    {
        // Unity 오브젝트는 파괴된 뒤에도 ??가 null로 안 잡히므로 명시적으로 비교한다.
        Component 캐릭터 = 표정 != null ? (Component)표정 : 입모양;
        if (캐릭터 == null) return transform;

        foreach (Transform t in 캐릭터.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == "Head" || t.name == "頭") return t;
        }
        return 캐릭터.transform;
    }

    void OnDisable()
    {
        타임라인발고정중 = false;
        선택지배치복원();

        if (vrAdvanceAction != null && vrAdvanceAction.action != null)
            vrAdvanceAction.action.Disable();

        if (자동VR넘김액션 != null)
        {
            자동VR넘김액션.Disable();
            자동VR넘김액션.Dispose();
            자동VR넘김액션 = null;
        }

        VRProject.Sound.SoundSettings.Changed -= 더빙음량반영;

        if (입모양음성동기루틴 != null)
        {
            StopCoroutine(입모양음성동기루틴);
            입모양음성동기루틴 = null;
        }
        if (입모양 != null) 입모양.재생중지();
    }

    void Start()
    {
        // ScenarioController가 챕터를 굴리는 씬이면 시작은 그쪽에 맡김.
        // 둘 다 시작하면 같은 대사가 두 번 타이핑됨
        ScenarioController controller = FindAnyObjectByType<ScenarioController>();
        if (controller != null && controller.currentScenario != null) return;

        if (currentScenario == null || currentScenario.groups.Count == 0) return;

        if (currentScenario.groups[0].entries.Count == 0)
        {
            Debug.LogWarning($"{currentScenario.name} 의 첫 그룹에 대사가 없습니다!");
            return;
        }

        int firstID = currentScenario.groups[0].entries[0].id;
        StartCoroutine(PlayDialogue(firstID));
    }

    public IEnumerator PlayDialogue(int startID)
    {
        //int currentGroupIdx = 0;
        //currentEntry = currentScenario.groups[currentGroupIdx].entries.Find(x => x.id == startID);

        DialogueEntry EntryToPlay = null;

        DialogueEntry entry = GetEntryById(startID);


        foreach (var group in currentScenario.groups)
        {
            EntryToPlay = group.entries.Find(x => x.id == startID);
            if (EntryToPlay != null)
                break;
        }

        currentEntry = EntryToPlay;

        while (currentEntry != null)
        {

            CheckBGMEvent(currentEntry.id);

            if (currentEntry.EffectSound != null && BGMManager.instance != null)
            {

                BGMManager.instance.PlayOneShotSE(currentEntry.EffectSound, currentEntry.seVolune);
            }



            if (ChatImage != null)
                ChatImage.gameObject.SetActive(currentEntry.showChatUI);

            PlayVoice(currentEntry);
            ApplyFace(currentEntry);
            ApplyBodyAnimation(currentEntry);

            yield return StartCoroutine(NormalChatOnlyText(currentEntry.speakerName, currentEntry.dialogueText));

            // 보이스가 없는 대사는 기존처럼 타이핑이 끝날 때 입을 닫는다.
            // 보이스가 있으면 아래 음성 동기 루틴이 AudioSource의 실제 종료 시점에 닫는다.
            if (입모양음성동기루틴 == null && 입모양 != null)
                입모양.재생중지();

            yield return StartCoroutine(WaitForInput());


            int nextID = -1;
            if (currentEntry.choices != null && currentEntry.choices.Count > 0)
            {
                yield return StartCoroutine(ShowScenarioChoices(currentEntry.choices));
                // 선택지 UI 오류로 자식 코루틴이 중단된 경우 -1을 엔딩으로
                // 해석하지 않는다. 현재 대사에서 멈춰 원인을 보존한다.
                if (nextIDResult == -1)
                {
                    Debug.LogError("[ChatManager] 선택 결과가 없어 대화를 현재 ID에서 멈춥니다.", this);
                    yield break;
                }
                nextID = nextIDResult;
            }
            else if (currentEntry.nextIndexOverride != -1)
            {
                nextID = currentEntry.nextIndexOverride;
            }
            else
            {
                nextID = currentEntry.id + 1;
            }


             DialogueEntry NextfoundEntry = null;


            foreach (var group in currentScenario.groups)
            {
                NextfoundEntry = group.entries.Find(x => x.id == nextID);

                if (NextfoundEntry != null) break;

            }

            currentEntry = NextfoundEntry;

            if (currentEntry == null)
            {
                // 마지막 대사의 보이스가 다음 챕터까지 넘어가지 않게 여기서 끊는다.
                if (voiceSource != null) voiceSource.Stop();
                if (입모양음성동기루틴 != null)
                {
                    StopCoroutine(입모양음성동기루틴);
                    입모양음성동기루틴 = null;
                }
                if (입모양 != null) 입모양.재생중지();

                Debug.Log("<color=yellow>시나리오가 끝났습니다!</color>");

                ScenarioController controller = FindAnyObjectByType<ScenarioController>();
                if (controller != null)
                {
                    controller.EndOfDialogue();
                }
                break;
            }
        }
    }


    /// <summary>
    /// 대사 한 줄의 보이스를 재생한다.
    ///
    /// 보이스가 없는 대사에서도 항상 먼저 Stop을 부른다. 그래야 앞 대사의 보이스가
    /// 다음 대사까지 물고 늘어지지 않는다. 대사를 빨리 넘길 때 목소리가 겹쳐서
    /// 들리는 것이 이걸 빠뜨렸을 때 나오는 증상이다.
    /// </summary>
    void PlayVoice(DialogueEntry entry)
    {
        if (entry == null) return;

        EnsureVoiceSource();
        if (voiceSource == null) return;

        voiceSource.Stop();

        if (entry.voice == null) return;

        voiceSource.clip = entry.voice;

        // 제작자가 대사마다 잡아둔 음량 × 플레이어가 옵션에서 고른 더빙 음량.
        // 곱해서 쓰므로 플레이어가 전체를 줄여도 대사들 사이의 균형은 유지된다.
        voiceSource.volume = Mathf.Clamp01(entry.voiceVolume) * VRProject.Sound.SoundSettings.Voice;
        voiceSource.Play();
    }

    /// <summary>
    /// 재생 중인 대사에 바뀐 음량을 즉시 반영한다.
    ///
    /// 옵션을 대화 도중에도 열 수 있으므로, 슬라이더를 움직이면 지금 나오는
    /// 목소리부터 바뀌어야 한다. 다음 대사를 기다리게 하면 맞추기 어렵다.
    /// </summary>
    void 더빙음량반영()
    {
        if (voiceSource == null || !voiceSource.isPlaying) return;
        if (currentEntry == null) return;

        voiceSource.volume = Mathf.Clamp01(currentEntry.voiceVolume)
                             * VRProject.Sound.SoundSettings.Voice;
    }

    /// <summary>
    /// 대사 한 줄의 얼굴 연출을 적용한다.
    ///
    /// 표정 번호 -1은 '기본표정번호로 되돌린다'는 뜻이다.
    /// 한 대사에서 웃겼으면 다음 대사에서 저절로 풀려야지, 지정하지 않은 대사가
    /// 앞 표정을 물려받으면 장면 내내 웃는 얼굴이 남는다.
    /// 표정을 이어가고 싶으면 같은 번호를 다시 적어주면 된다.
    /// </summary>
    void ApplyFace(DialogueEntry entry)
    {
        if (entry == null) return;

        // 씬 로딩 때 ChatManager가 캐릭터보다 먼저 켜지면 OnEnable의 자동 검색은
        // 한 번 실패할 수 있다. 실제 대사를 적용하는 시점에 다시 찾아 연결한다.
        if (표정 == null) 표정 = FindAnyObjectByType<VRProject.Character.FacialExpression>();
        if (입모양 == null) 입모양 = FindAnyObjectByType<VRProject.Character.MouthFlap>();

        if (표정 != null)
        {
            int index = entry.facialExpressionIndex >= 0
                ? entry.facialExpressionIndex
                : 기본표정번호;

            if (index >= 0 && index < 표정.Count)
            {
                표정.Play(index);
            }
            else
            {
                Debug.LogWarning(
                    $"<color=orange>[ChatManager] ID {entry.id}의 표정 번호 {index}는 " +
                    $"표정 목록 범위(0~{표정.Count - 1})를 벗어났습니다. " +
                    $"표정을 바꾸지 않습니다.</color>", this);
            }
        }

        if (입모양 == null)
        {
            // 여기서 조용히 넘어가면 "체크했는데 입이 안 움직인다"가 되고
            // 단서가 하나도 남지 않는다. 요구한 대사에서만 한 번 짚어준다.
            if (entry.playMouthAnimation)
            {
                Debug.LogWarning(
                    $"<color=orange>[ChatManager] ID {entry.id}가 입모양 재생을 요청했지만 " +
                    "씬에 MouthFlap 컴포넌트가 없습니다.\n" +
                    "메뉴 [Tools > VRProject > 입모양 컴포넌트 설정]을 실행하세요.</color>", this);
            }
            return;
        }

        if (입모양음성동기루틴 != null)
        {
            StopCoroutine(입모양음성동기루틴);
            입모양음성동기루틴 = null;
        }

        if (entry.playMouthAnimation)
        {
            if (entry.voice != null && voiceSource != null && voiceSource.isPlaying)
            {
                입모양.음성동기재생시작();
                입모양음성동기루틴 = StartCoroutine(음성이끝나면입닫기(entry));
            }
            else
            {
                입모양.재생시작();
            }
        }
        else
        {
            입모양.재생중지();
        }
    }

    void LateUpdate()
    {
        // Animator와 Timeline의 포즈 계산이 끝난 뒤 보정해야 화면에 내려간 프레임이
        // 한 장이라도 보이지 않는다. 코루틴 안에서 고치면 평가 순서에 따라 늦을 수 있다.
        if (타임라인발고정중)
            타임라인발높이보정();
    }

    IEnumerator 음성이끝나면입닫기(DialogueEntry entry)
    {
        // PlayVoice에서 같은 프레임에 시작된 AudioSource가 실제 재생 상태를
        // 반영할 때까지 한 프레임 기다린다.
        yield return null;

        while (currentEntry == entry && voiceSource != null && voiceSource.isPlaying)
            yield return null;

        // 다음 대사가 이미 시작됐다면 그 대사의 입모양을 건드리지 않는다.
        if (currentEntry == entry && 입모양 != null)
            입모양.재생중지();

        입모양음성동기루틴 = null;
    }

    void ApplyBodyAnimation(DialogueEntry entry)
    {
        if (entry == null || entry.bodyAnimation == null) return;

        if (bodyAnimator == null)
        {
            Debug.LogWarning(
                $"<color=orange>[ChatManager] ID {entry.id}에 전신 애니메이션이 있지만 " +
                "재생할 Animator를 찾지 못했습니다.</color>", this);
            return;
        }

        string stateName = string.IsNullOrWhiteSpace(entry.bodyAnimationStateName)
            ? entry.bodyAnimation.name
            : entry.bodyAnimationStateName;

        if (!bodyAnimator.HasState(0, Animator.StringToHash(stateName)))
        {
            Debug.LogWarning(
                $"<color=orange>[ChatManager] ID {entry.id}: Animator에 '{stateName}' 상태가 없습니다.</color>",
                bodyAnimator);
            return;
        }

        if (bodyAnimationRoutine != null)
        {
            StopCoroutine(bodyAnimationRoutine);
            bodyAnimationRoutine = null;
        }

        bodyAnimator.CrossFadeInFixedTime(stateName, bodyAnimationBlendTime, 0);

        if (entry.returnToIdleAfterBodyAnimation)
            bodyAnimationRoutine = StartCoroutine(ReturnBodyToIdle(entry.bodyAnimation.length));
    }

    IEnumerator ReturnBodyToIdle(float clipLength)
    {
        yield return new WaitForSeconds(Mathf.Max(0.01f, clipLength));

        if (bodyAnimator != null &&
            bodyAnimator.HasState(0, Animator.StringToHash(bodyIdleStateName)))
        {
            bodyAnimator.CrossFadeInFixedTime(bodyIdleStateName, bodyAnimationBlendTime, 0);
        }

        bodyAnimationRoutine = null;
    }

    public DialogueEntry GetEntryById(int targetID)
    {
        foreach (var group in currentScenario.groups)
        {
            var entry = group.entries.Find(x => x.id == targetID);
            if (entry != null) return entry;
        }
        return null;
    }

    private bool 선택지UI확보()
    {
        if (choicePanel == null)
        {
            foreach (Transform t in Resources.FindObjectsOfTypeAll<Transform>())
            {
                if (t.gameObject.scene.IsValid() && t.name == "ChoicePanel")
                {
                    choicePanel = t.gameObject;
                    break;
                }
            }
        }

        if (choicePanel == null) return false;

        bool 비어있음 = choiceButtonsText == null || choiceButtonsText.Length == 0;
        if (!비어있음)
        {
            foreach (TextMeshProUGUI text in choiceButtonsText)
            {
                if (text == null) { 비어있음 = true; break; }
            }
        }

        if (!비어있음) return true;

        var 찾은텍스트 = new List<TextMeshProUGUI>();
        foreach (Button button in choicePanel.GetComponentsInChildren<Button>(true))
        {
            TextMeshProUGUI text = button.GetComponentInChildren<TextMeshProUGUI>(true);
            if (text != null)
                찾은텍스트.Add(text);
        }

        찾은텍스트.Sort((a, b) =>
            a.GetComponentInParent<Button>().transform.GetSiblingIndex().CompareTo(
                b.GetComponentInParent<Button>().transform.GetSiblingIndex()));

        choiceButtonsText = 찾은텍스트.ToArray();
        return choiceButtonsText.Length > 0;
    }

    private void 선택지배치적용()
    {
        if (choicePanel == null || 선택지배치변경중) return;

        선택지대화UI = choicePanel.GetComponentInParent<VRProject.Dialogue.VRDialogueUI>();
        선택지전크기 = choicePanel.transform.localScale;

        // 기존 대화창은 가까운 위치를 유지하되, 선택 중에는 컨트롤러보다 앞에 둔다.
        // 0.5m 부근은 손/레이 시작점보다 안쪽이라 보이면서도 누를 수 없는 경우가 있다.
        if (선택지대화UI != null)
        {
            선택지전거리 = 선택지대화UI.거리설정;
            선택지대화UI.거리설정 = Mathf.Max(선택지전거리, 선택지거리);
            선택지대화UI.SnapToTarget();
        }

        choicePanel.transform.localScale = 선택지전크기 * 선택지크기배율;
        선택지배치변경중 = true;
        Canvas.ForceUpdateCanvases();
    }

    private void 선택지배치복원()
    {
        if (!선택지배치변경중) return;

        if (choicePanel != null)
            choicePanel.transform.localScale = 선택지전크기;

        if (선택지대화UI != null)
        {
            선택지대화UI.거리설정 = 선택지전거리;
            선택지대화UI.SnapToTarget();
        }

        선택지대화UI = null;
        선택지배치변경중 = false;
    }

    IEnumerator ShowScenarioChoices(List<ChoiceData> choices)
    {
        nextIDResult = -1;

        if (!선택지UI확보())
        {
            Debug.LogError(
                "[ChatManager] ChoicePanel 안에서 선택지 버튼 텍스트를 찾지 못했습니다. " +
                "대화를 종료하지 않고 현재 위치에서 멈춥니다.", this);
            yield break;
        }

        // 직전 대사를 넘긴 트리거의 Pointer Up이 같은 프레임에 새 선택지까지
        // 눌러 버리지 않도록, 화면이 뜬 뒤 새 입력만 받는다.
        선택입력허용시간 = Time.unscaledTime + 0.35f;
        선택지배치적용();
        choicePanel.SetActive(true);

        for (int i = 0; i < choiceButtonsText.Length; i++)
        {
            if (i < choices.Count)
            {
                choiceButtonsText[i].gameObject.transform.parent.gameObject.SetActive(true);
                choiceButtonsText[i].text = choices[i].choiceText;

                int targetID = choices[i].choiceIndex;
                var timeline = choices[i].timeline;   // VR: 선택 시 재생할 타임라인
                Button btn = choiceButtonsText[i].GetComponentInParent<Button>();

                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() =>
                {
                    if (Time.unscaledTime < 선택입력허용시간) return;
                    StartCoroutine(OnchoieClicked(targetID, timeline));
                });
            }
            else
            {
                choiceButtonsText[i].gameObject.transform.parent.gameObject.SetActive(false);
            }
        }
        yield return new WaitUntil(() => nextIDResult != -1);
    }

    IEnumerator OnchoieClicked(int targetID, UnityEngine.Timeline.TimelineAsset timeline = null)
    {
        if (GetEntryById(targetID) == null)
        {
            Debug.LogError($"[ChatManager] 선택지 대상 ID {targetID}가 없어 이동하지 않습니다.", this);
            yield break;
        }

        yield return new WaitForSecondsRealtime(0.15f);
        choicePanel.SetActive(false);
        선택지배치복원();

        if (timeline != null)
            선택지Director확보(timeline);

        // ── VR: 선택 시 타임라인 재생 ──────────────────────────────
        // 타임라인이 끝난 뒤에 다음 대사로 넘어간다.
        // 그동안 대사 진행을 멈춰 연출과 텍스트가 겹치지 않게 한다.
        if (timeline != null && choiceDirector != null)
        {
            bool wasPaused = isPausedByMenu;
            if (pauseDuringTimeline) isPausedByMenu = true;

            // 타임라인이 캐릭터의 포즈를 잡는 동안 CharacterFollow가 루트를 계속 밀면
            // 연출 중에 캐릭터가 걸어가 버린다. 재생 동안만 멈춰 세운다.
            var follow = choiceDirector.GetComponentInParent<VRProject.Character.CharacterFollow>();
            bool followWasPaused = follow != null && follow.Paused;
            if (follow != null && pauseDuringTimeline) follow.Paused = true;

            choiceDirector.playableAsset = timeline;
            WarnIfTracksUnbound(timeline);

            float groundY = bodyAnimator != null ? bodyAnimator.transform.position.y : 0f;
            타임라인발고정중 = 발높이읽기(out 타임라인바닥발높이);

            choiceDirector.time = 0;
            choiceDirector.Play();

            // duration은 재생을 시작해야 확정되므로 한 프레임 기다린 뒤에 읽는다.
            yield return null;
            double length = choiceDirector.duration;

            while (choiceDirector.state == UnityEngine.Playables.PlayState.Playing
                   && choiceDirector.time < length)
            {
                yield return null;
            }

            타임라인발고정중 = false;
            choiceDirector.Stop();

            if (bodyAnimator != null)
            {
                Vector3 position = bodyAnimator.transform.position;
                position.y = groundY;
                bodyAnimator.transform.position = position;
            }

            if (follow != null && pauseDuringTimeline) follow.Paused = followWasPaused;
            if (pauseDuringTimeline) isPausedByMenu = wasPaused;
        }
        else if (timeline != null)
        {
            Debug.LogWarning($"<color=orange>선택지에 타임라인이 있지만 " +
                             $"ChatManager.choiceDirector가 비어 있어 건너뜁니다: {timeline.name}</color>", this);
        }

        nextIDResult = targetID;
    }

    private bool 발높이읽기(out float lowestY)
    {
        lowestY = 0f;
        if (bodyAnimator == null || !bodyAnimator.isHuman || bodyAnimator.avatar == null)
            return false;

        Transform leftFoot = bodyAnimator.GetBoneTransform(HumanBodyBones.LeftFoot);
        Transform rightFoot = bodyAnimator.GetBoneTransform(HumanBodyBones.RightFoot);
        Transform leftToes = bodyAnimator.GetBoneTransform(HumanBodyBones.LeftToes);
        Transform rightToes = bodyAnimator.GetBoneTransform(HumanBodyBones.RightToes);

        bool found = false;
        lowestY = float.PositiveInfinity;
        Transform[] feet = { leftFoot, rightFoot, leftToes, rightToes };
        foreach (Transform foot in feet)
        {
            if (foot == null) continue;
            lowestY = Mathf.Min(lowestY, foot.position.y);
            found = true;
        }
        return found;
    }

    private void 타임라인발높이보정()
    {
        if (bodyAnimator == null || !발높이읽기(out float currentFootY)) return;

        float correction = 타임라인바닥발높이 - currentFootY;
        // 발이 올라가는 동작은 애니메이션 그대로 둔다. 기준 바닥 아래로 내려갈 때만 올린다.
        if (correction <= 0.0001f) return;

        // 잘못된 리그가 수십 미터를 반환해도 캐릭터가 튀지 않게 한 프레임 보정량을 제한한다.
        correction = Mathf.Min(correction, 0.5f);
        Vector3 position = bodyAnimator.transform.position;
        position.y += correction;
        bodyAnimator.transform.position = position;
    }

    private void 선택지Director확보(UnityEngine.Timeline.TimelineAsset timeline)
    {
        if (timeline == null) return;

        if (bodyAnimator == null)
        {
            var follow = FindAnyObjectByType<VRProject.Character.CharacterFollow>();
            if (follow != null) bodyAnimator = follow.GetComponentInChildren<Animator>();
        }

        if (bodyAnimator == null)
        {
            Debug.LogError($"[ChatManager] '{timeline.name}'을 재생할 아루 Animator를 찾지 못했습니다.", this);
            return;
        }

        if (choiceDirector == null)
        {
            choiceDirector = bodyAnimator.GetComponent<UnityEngine.Playables.PlayableDirector>();
            if (choiceDirector == null)
                choiceDirector = bodyAnimator.gameObject.AddComponent<UnityEngine.Playables.PlayableDirector>();
        }

        choiceDirector.playOnAwake = false;
        choiceDirector.playableAsset = timeline;

        foreach (var track in timeline.GetOutputTracks())
        {
            if (track is UnityEngine.Timeline.AnimationTrack)
                choiceDirector.SetGenericBinding(track, bodyAnimator);
        }
    }

    /// <summary>
    /// 타임라인의 애니메이션 트랙에 연기할 대상이 물려 있는지 확인한다.
    ///
    /// 바인딩은 타임라인 에셋이 아니라 (Director, Track) 짝으로 Director에 저장된다.
    /// 그래서 playableAsset만 갈아끼우면, 새 타임라인의 트랙에는 바인딩이 없어서
    /// 재생은 정상으로 돌아가는데 화면에서는 아무 일도 일어나지 않는다.
    /// 에러가 안 나기 때문에 원인을 짚기가 특히 어렵다.
    /// </summary>
    void WarnIfTracksUnbound(UnityEngine.Timeline.TimelineAsset timeline)
    {
        foreach (var track in timeline.GetOutputTracks())
        {
            // 애니메이션 트랙만 본다. Activation 트랙 등은 비워두는 경우가 많다.
            if (!(track is UnityEngine.Timeline.AnimationTrack)) continue;

            if (choiceDirector.GetGenericBinding(track) == null)
            {
                Debug.LogWarning(
                    $"<color=orange>[ChatManager] '{timeline.name}'의 '{track.name}' 트랙에 " +
                    $"바인딩이 없습니다. 재생은 되지만 아무것도 움직이지 않습니다.\n" +
                    $"{choiceDirector.name}의 Playable Director에서 이 트랙에 " +
                    $"Animator를 연결하세요.</color>", choiceDirector);
            }
        }
    }

    IEnumerator NormalChatOnlyText(string narrator, string narration)
    {
        CharacterName.text = (narrator == "나") ? " " : narrator;
        ChatText.text = "";

        // 타이핑 도중에 입력이 들어오면 남은 글자를 한 번에 채운다.
        //
        // 예전에는 타이핑이 다 끝나야 WaitForInput이 시작해서, 글자당 0.05초씩
        // 걸리는 동안 아무리 눌러도 반응이 없었다. 40자면 2초다.
        // 사용자 입장에서는 "눌렀는데 안 넘어가다가 갑자기 넘어가는" 것으로 느껴진다.
        // 비주얼노벨에서는 타이핑 중 입력 = 즉시 완성이 표준 동작이다.
        bool skipped = false;

        foreach (char letter in narration)
        {
            if (isPausedByMenu) yield return new WaitUntil(() => !isPausedByMenu);

            ChatText.text += letter;

            // WaitForSeconds로 통째로 기다리면 그 사이 입력을 볼 수 없다.
            // 직접 세면서 매 프레임 입력을 확인한다.
            float elapsed = 0f;
            while (elapsed < typeSpeed)
            {
                if (AdvancePressedThisFrame())
                {
                    skipped = true;
                    break;
                }
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (skipped) break;
        }

        if (skipped)
        {
            ChatText.text = narration;

            // 한 번 누른 것이 '완성'과 '다음 대사'로 두 번 먹지 않도록 한 프레임 흘린다.
            // WasPressedThisFrame은 눌린 프레임에만 참이므로 이걸로 충분하다.
            yield return null;
        }
    }

    IEnumerator WaitForInput()
    {
        // 대사가 막 끝난 프레임의 입력이 그대로 흘러들어오지 않게 한 프레임 띄운다.
        yield return null;

        bool wasPaused = isPausedByMenu;

        while (true)
        {
            // 로그는 상태가 바뀌는 순간에만 찍는다.
            //
            // 예전에는 이 로그가 if 바깥에 있어서 입력을 기다리는 내내 매 프레임
            // 찍혔다. 콘솔이 잠기고 프레임이 떨어져서 입력이 씹히는 것처럼 느껴졌다.
            // 게다가 "설정창 닫힘" 로그가 오히려 열려 있는 동안 찍혀 의미가 뒤집혀 있었다.
            if (isPausedByMenu != wasPaused)
            {
                wasPaused = isPausedByMenu;
                Debug.Log(wasPaused
                    ? "<color=orange>ChatManager: 설정창 열림, 입력 대기 중단</color>"
                    : "<color=lime>ChatManager: 설정창 닫힘, 입력 감지 재개</color>");
            }

            if (!isPausedByMenu && AdvancePressedThisFrame())
            {
                // 이 입력을 여기서 한 프레임 흘려 소진한다.
                //
                // 안 그러면 같은 프레임 안에서 다음 대사가 시작되고, 그 대사의
                // 타이핑 루프가 방금 그 입력을 '스킵'으로 다시 읽는다. 결과적으로
                // 모든 대사가 즉시 완성되고, 대사에 딸린 입모양도 한 프레임 만에
                // 끝나서 움직이지 않는 것처럼 보인다.
                yield return null;
                break;
            }

            yield return null;
        }
    }

    /// <summary>
    /// 다음으로 넘기는 입력이 이번 프레임에 눌렸는지.
    ///
    /// 타이핑 중 스킵과 대사 대기가 같은 판정을 쓰도록 한곳에 모았다.
    /// 양쪽이 각자 입력을 보면 한쪽만 고쳤을 때 동작이 어긋난다.
    /// </summary>
    bool AdvancePressedThisFrame()
    {
        // Quest 컨트롤러의 A/B(오른손), X/Y(왼손). VR에서는 이게 주 입력이다.
        if (vrAdvanceAction != null && vrAdvanceAction.action != null
            && vrAdvanceAction.action.WasPressedThisFrame())
        {
            return true;
        }

        if (자동VR넘김액션 != null && 자동VR넘김액션.WasPressedThisFrame())
        {
            return true;
        }

        // 아래 마우스·키보드는 헤드셋 없이 에디터에서 볼 때를 위한 보조 입력이다.
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            if (!IsPointerOverUI()) return true;
        }

        // 예전에는 anyKey를 봤는데, 그러면 WASD로 움직이기만 해도 대사가 넘어갔다.
        // 넘김에 쓸 키만 명시한다.
        if (Keyboard.current != null
            && (Keyboard.current.spaceKey.wasPressedThisFrame
                || Keyboard.current.enterKey.wasPressedThisFrame
                || Keyboard.current.numpadEnterKey.wasPressedThisFrame))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// 마우스가 UI 위에 있는지. 선택지 버튼을 누른 클릭이 대사까지 넘겨버리는 것을 막는다.
    /// </summary>
    bool IsPointerOverUI()
    {
        if (EventSystem.current == null) return false;
        if (!EventSystem.current.IsPointerOverGameObject()) return false;

        if (logBlockedClicks)
        {
            var pointerData = new PointerEventData(EventSystem.current)
            {
                position = Mouse.current.position.ReadValue()
            };
            var results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointerData, results);

            if (results.Count > 0)
            {
                Debug.Log($"<color=red>클릭을 막은 UI: {results[0].gameObject.name}</color>");
            }
        }

        return true;
    }



    void CheckBGMEvent(int currentID)
    {

        var bgmEvent = bgmSetting.BGMEvents.Find(e => currentID >= e.StartID && currentID <= e.EndID);
        if (bgmEvent != null)
        {
            BGMManager.instance.CheckAndPlayBGM(currentID);
        }
    }
}
