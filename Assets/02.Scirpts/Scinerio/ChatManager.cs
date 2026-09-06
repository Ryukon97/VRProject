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

    // ── VR 확장 ─────────────────────────────────────────────────
    [Header("VR: 다음 대사 입력")]
    [Tooltip("Quest 컨트롤러의 A/B/X/Y를 묶은 InputAction.\n" +
             "비워두면 마우스·키보드만 동작한다(에디터 테스트용).")]
    public UnityEngine.InputSystem.InputActionReference vrAdvanceAction;

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
            vrAdvanceAction.action.Enable();

        // 표정과 입모양은 캐릭터에 붙어 있고 ChatManager는 UI 쪽에 있어서
        // 인스펙터로 잇는 것을 잊기 쉽다. 비어 있으면 씬에서 찾아 쓴다.
        if (표정 == null) 표정 = FindAnyObjectByType<VRProject.Character.FacialExpression>();
        if (입모양 == null) 입모양 = FindAnyObjectByType<VRProject.Character.MouthFlap>();

        EnsureVoiceSource();
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
        if (vrAdvanceAction != null && vrAdvanceAction.action != null)
            vrAdvanceAction.action.Disable();
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

            yield return StartCoroutine(NormalChatOnlyText(currentEntry.speakerName, currentEntry.dialogueText));

            // 글자가 다 찍히면 말이 끝난 것이므로 입을 닫는다.
            // MouthFlap 자체에도 최대 재생시간이 걸려 있지만, 그건 신호를 놓쳤을 때를
            // 대비한 안전장치다. 평소에는 이쪽에서 대사 길이에 맞춰 멈춘다.
            if (입모양 != null) 입모양.재생중지();

            yield return StartCoroutine(WaitForInput());


            int nextID = -1;
            if (currentEntry.choices != null && currentEntry.choices.Count > 0)
            {
                yield return StartCoroutine(ShowScenarioChoices(currentEntry.choices));
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
        voiceSource.volume = Mathf.Clamp01(entry.voiceVolume);
        voiceSource.Play();
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

        if (entry.playMouthAnimation) 입모양.재생시작();
        else 입모양.재생중지();
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
    IEnumerator ShowScenarioChoices(List<ChoiceData> choices)
    {
        nextIDResult = -1;
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
        yield return new WaitForSecondsRealtime(0.15f);
        choicePanel.SetActive(false);

        // ── VR: 선택 시 타임라인 재생 ──────────────────────────────
        // 타임라인이 끝난 뒤에 다음 대사로 넘어간다.
        // 그동안 대사 진행을 멈춰 연출과 텍스트가 겹치지 않게 한다.
        if (timeline != null && choiceDirector != null)
        {
            bool wasPaused = isPausedByMenu;
            if (pauseDuringTimeline) isPausedByMenu = true;

            // 타임라인이 캐릭터의 포즈를 잡는 동안 CharacterFollow가 루트를 계속 밀면
            // 연출 중에 캐릭터가 걸어가 버린다. 재생 동안만 멈춰 세운다.
            var follow = choiceDirector.GetComponent<VRProject.Character.CharacterFollow>();
            bool followWasPaused = follow != null && follow.Paused;
            if (follow != null && pauseDuringTimeline) follow.Paused = true;

            choiceDirector.playableAsset = timeline;
            WarnIfTracksUnbound(timeline);

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

            choiceDirector.Stop();

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