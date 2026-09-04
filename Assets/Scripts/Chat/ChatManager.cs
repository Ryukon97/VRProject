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

    // InputActionReference로 참조한 액션은 자동으로 켜지지 않는다.
    // 켜주지 않으면 WasPressedThisFrame()이 영원히 false다.
    void OnEnable()
    {
        if (vrAdvanceAction != null && vrAdvanceAction.action != null)
            vrAdvanceAction.action.Enable();
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





            yield return StartCoroutine(NormalChatOnlyText(currentEntry.speakerName, currentEntry.dialogueText));
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

            choiceDirector.playableAsset = timeline;
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
            if (pauseDuringTimeline) isPausedByMenu = wasPaused;
        }
        else if (timeline != null)
        {
            Debug.LogWarning($"<color=orange>선택지에 타임라인이 있지만 " +
                             $"ChatManager.choiceDirector가 비어 있어 건너뜁니다: {timeline.name}</color>", this);
        }

        nextIDResult = targetID;
    }

    IEnumerator NormalChatOnlyText(string narrator, string narration)
    {
        CharacterName.text = (narrator == "나") ? " " : narrator;
        ChatText.text = "";

        foreach (char letter in narration.ToCharArray())
        {
            if (isPausedByMenu) yield return new WaitUntil(() => !isPausedByMenu);
            ChatText.text += letter;
            yield return new WaitForSeconds(0.05f);
        }
    }

    IEnumerator WaitForInput()
    {
        yield return new WaitForSeconds(0.1f);
        bool clicked = false;
        while (!clicked)
        {
            Debug.Log("<color=orange>ChatManager: 설정창 열림 감지, 대기 중...</color>");
            if (isPausedByMenu)
            {
                yield return null;
                Debug.Log("<color=lime>ChatManager: 설정창 닫힘, 입력 감지 재개!</color>");
                continue;

            } // 대사클릭 관련은 여기있음

            // ── VR 입력 ────────────────────────────────────────────
            // Quest 컨트롤러의 A/B(오른손), X/Y(왼손)로 다음 대사로 넘어간다.
            // VR에는 마우스가 없으므로 이쪽이 주 입력이고, 아래 마우스·키보드는
            // 에디터에서 헤드셋 없이 테스트할 때를 위해 남겨둔다.
            if (vrAdvanceAction != null && vrAdvanceAction.action != null
                && vrAdvanceAction.action.WasPressedThisFrame())
            {
                clicked = true;
            }

            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                if (EventSystem.current.IsPointerOverGameObject())
                {
                    // [중요] 여기서 마스타를 괴롭히는 범인의 이름을 로그로 찍어봅시다!
                    PointerEventData pointerData = new PointerEventData(EventSystem.current) { position = Mouse.current.position.ReadValue() };
                    List<RaycastResult> results = new List<RaycastResult>();
                    EventSystem.current.RaycastAll(pointerData, results);
                    if (results.Count > 0)
                    {
                        Debug.Log("<color=red>클릭을 막는 범인 발견: " + results[0].gameObject.name + "</color>");
                    }
                }
                else
                {
                    clicked = true; // UI가 아닌 곳(빨간색 영역 등)을 클릭하면 정상 작동
                }
            }
            if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
            {
                clicked = true;
            }
         
            yield return null;
        }
        Debug.Log("<color=white>입력 감지됨: 다음 대사로 진행합니다.</color>");
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