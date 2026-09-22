using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

public class ScenarioController : MonoBehaviour
{
    public DialogueDataSO currentScenario;
    public int currentGroupIndex =0;
    public int currentEntryIndex =0;

    [Header("엔딩")]
    [Tooltip("마지막 시나리오가 끝난 뒤 재생할 엔딩 크레딧 영상")]
    public VideoClip endCredits;

    [Tooltip("엔딩 크레딧 재생이 끝나면 돌아갈 타이틀 씬")]
    public string titleSceneName = "01Title";

    private bool ending;

    void Start()
    {
        if(currentScenario !=null)
        {
            StartChapter(currentScenario);
        }
    }
    public void StartChapter(DialogueDataSO newSO)
    {
        if (newSO == null) return;

        currentScenario = newSO;
        currentGroupIndex = 0;
        currentEntryIndex = 0;

        Debug.Log($"<color=pink>{newSO.name} 파트를 시작합니다!</color>");


        ChatManager chatManager = Object.FindAnyObjectByType<ChatManager>();
        if (chatManager == null) return;

        // 첫 대사가 없는 챕터는 여기서 걸러냄 (빈 그룹이면 아래에서 터짐)
        if (newSO.groups.Count == 0 || newSO.groups[0].entries.Count == 0)
        {
            Debug.LogWarning($"{newSO.name} 에 재생할 대사가 없습니다!");
            return;
        }

        // ChatManager는 자기 currentScenario에서 ID를 찾으므로 여기서 같이 바꿔줘야
        // 다음 챕터로 넘어갔을 때 이전 SO를 계속 뒤지지 않음
        chatManager.currentScenario = newSO;

        int firstID = newSO.groups[0].entries[0].id;
        chatManager.StartCoroutine(chatManager.PlayDialogue(firstID));
    }

   

    public void RequestNextDialogue()
    {
        
        if (currentScenario.groups.Count == 0) return;

       
        DialogueGroup targetGroup = currentScenario.groups[currentGroupIndex];

      
        if (currentEntryIndex < targetGroup.entries.Count)
        {
            DialogueEntry data = targetGroup.entries[currentEntryIndex];

          
            currentEntryIndex++;
        }
        else
        {
          
            OnGroupFinished();
        }
    }

    public void EndOfDialogue()
    {
        if(currentScenario.nextStorySO !=null)
        {
            StartChapter(currentScenario.nextStorySO);
            return;
        }

        if (!ending) StartCoroutine(PlayEndCredits());
    }

    private IEnumerator PlayEndCredits()
    {
        ending = true;

        // 엔딩 영상의 소리만 들리도록 게임 내 BGM을 먼저 완전히 정지한다.
        if (BGMManager.instance != null)
            BGMManager.instance.StopBGM();

        if (endCredits == null)
        {
            Debug.LogWarning("[ScenarioController] EndCredits 영상이 없어 타이틀로 바로 이동합니다.", this);
            yield return LoadTitle();
            yield break;
        }

        // 엔딩 영상 위에 대화창과 선택지가 남지 않게 현재 씬의 UI를 감춘다.
        foreach (Canvas canvas in FindObjectsByType<Canvas>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            canvas.gameObject.SetActive(false);
        }

        Camera camera = Camera.main;
        if (camera == null)
        {
            Debug.LogError("[ScenarioController] MainCamera가 없어 엔딩 영상을 표시할 수 없습니다.", this);
            yield return LoadTitle();
            yield break;
        }

        GameObject playerObject = new GameObject("EndCreditsPlayer");
        VideoPlayer player = playerObject.AddComponent<VideoPlayer>();
        player.playOnAwake = false;
        player.isLooping = false;
        player.skipOnDrop = true;
        player.source = VideoSource.VideoClip;
        player.clip = endCredits;
        // CameraNearPlane은 항상 화면 전체를 덮어서 VR에서는 지나치게 크게 보인다.
        // RenderTexture를 월드 공간 RawImage에 출력해 시야 중앙 40%로 제한한다.
        int videoWidth = endCredits.width > 0 ? (int)endCredits.width : 1920;
        int videoHeight = endCredits.height > 0 ? (int)endCredits.height : 1080;
        int textureWidth = Mathf.Clamp(videoWidth, 16, 2048);
        int textureHeight = Mathf.Clamp(videoHeight, 16, 2048);
        RenderTexture videoTexture = new RenderTexture(textureWidth, textureHeight, 0);
        videoTexture.name = "EndCreditsTexture";
        videoTexture.Create();

        player.renderMode = VideoRenderMode.RenderTexture;
        player.targetTexture = videoTexture;
        player.aspectRatio = VideoAspectRatio.FitInside;
        player.audioOutputMode = VideoAudioOutputMode.Direct;

        const float distance = 0.5f;
        const float canvasPixelWidth = 2000f;
        float viewHeight = 2f * distance * Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float viewWidth = viewHeight * camera.aspect;
        float canvasPixelHeight = canvasPixelWidth * viewHeight / viewWidth;

        GameObject screenObject = new GameObject(
            "EndCreditsScreen", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        RectTransform screen = screenObject.GetComponent<RectTransform>();
        screen.SetParent(camera.transform, false);
        screen.localPosition = new Vector3(0f, 0f, distance);
        screen.localRotation = Quaternion.identity;
        screen.sizeDelta = new Vector2(canvasPixelWidth, canvasPixelHeight);

        // XR Origin에 X/Y가 다른 스케일이 들어 있어도 영상이 눌리거나 늘어나지 않게
        // 카메라의 실제 월드 스케일을 축별로 역보정한다.
        Vector3 cameraScale = camera.transform.lossyScale;
        float baseScale = viewWidth / canvasPixelWidth;
        screen.localScale = new Vector3(
            baseScale / Mathf.Max(0.0001f, Mathf.Abs(cameraScale.x)),
            baseScale / Mathf.Max(0.0001f, Mathf.Abs(cameraScale.y)),
            baseScale / Mathf.Max(0.0001f, Mathf.Abs(cameraScale.z)));

        Canvas creditsCanvas = screenObject.GetComponent<Canvas>();
        creditsCanvas.renderMode = RenderMode.WorldSpace;
        creditsCanvas.worldCamera = camera;

        // 영상 밖의 게임 화면이 전혀 보이지 않도록 시야보다 넓은 검은 배경을 깐다.
        GameObject blackoutObject = new GameObject("Blackout", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform blackout = blackoutObject.GetComponent<RectTransform>();
        blackout.SetParent(screen, false);
        blackout.anchorMin = Vector2.zero;
        blackout.anchorMax = Vector2.one;
        blackout.offsetMin = new Vector2(-200f, -200f);
        blackout.offsetMax = new Vector2(200f, 200f);
        Image blackoutImage = blackoutObject.GetComponent<Image>();
        blackoutImage.color = Color.black;
        blackoutImage.raycastTarget = false;

        GameObject videoObject = new GameObject(
            "EndCreditsVideo", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        RectTransform videoRect = videoObject.GetComponent<RectTransform>();
        videoRect.SetParent(screen, false);
        videoRect.anchorMin = videoRect.anchorMax = new Vector2(0.5f, 0.5f);
        videoRect.anchoredPosition = Vector2.zero;

        float videoAspect = (float)videoWidth / videoHeight;
        float maxVideoWidth = canvasPixelWidth * 0.4f;
        float maxVideoHeight = canvasPixelHeight * 0.4f;
        float fittedWidth = Mathf.Min(maxVideoWidth, maxVideoHeight * videoAspect);
        float fittedHeight = fittedWidth / videoAspect;
        videoRect.sizeDelta = new Vector2(fittedWidth, fittedHeight);
        videoRect.localScale = Vector3.one;

        RawImage image = videoObject.GetComponent<RawImage>();
        image.texture = videoTexture;
        image.color = Color.white;
        image.raycastTarget = false;

        bool prepared = false;
        bool failed = false;
        player.prepareCompleted += _ => prepared = true;
        player.errorReceived += (_, message) =>
        {
            failed = true;
            Debug.LogError($"[ScenarioController] 엔딩 영상 재생 오류: {message}", this);
        };

        player.Prepare();
        while (!prepared && !failed) yield return null;

        if (!failed)
        {
            player.Play();
            yield return null;
            while (player.isPlaying) yield return null;
        }

        Destroy(screenObject);
        Destroy(playerObject);
        videoTexture.Release();
        Destroy(videoTexture);
        yield return LoadTitle();
    }

    private IEnumerator LoadTitle()
    {
        VRProject.Sound.SoundSettings.Save();

        if (string.IsNullOrWhiteSpace(titleSceneName))
            titleSceneName = "01Title";

        AsyncOperation operation = SceneManager.LoadSceneAsync(titleSceneName);
        if (operation == null)
        {
            Debug.LogError($"[ScenarioController] '{titleSceneName}' 씬을 불러오지 못했습니다.", this);
            yield break;
        }

        while (!operation.isDone) yield return null;
    }

    void OnGroupFinished()
    {
        Debug.Log($"{currentScenario.groups[currentGroupIndex].GroupName} 그룹의 대사가 끝났습니다.");
      
    }
}
