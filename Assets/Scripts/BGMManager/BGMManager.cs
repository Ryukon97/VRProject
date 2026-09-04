using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BGMManager : MonoBehaviour
{
    public static BGMManager instance;

    [Header("데이터 연결")]
    public SoundDataSO SoundData; 

    [Header("오디오 소스")]
    [Tooltip("주 오디오 소스. 하나만 써도 동작한다.")]
    public AudioSource SourceA;

    [Tooltip("크로스페이드용 보조 소스. 비워두면 단일 소스 모드로 동작한다.\n" +
             "곡을 바꿀 때 겹치지 않고 페이드아웃 → 교체 → 페이드인 순으로 넘어간다.")]
    public AudioSource SourceB;

    private bool isSourceAActive = true;
    private AudioClip currentPlayingClip;

    /// <summary>
    /// 소스가 하나뿐인지. SourceB가 비었거나 SourceA와 같은 것을 가리키면 단일 소스다.
    ///
    /// 같은 소스로 크로스페이드를 하면 볼륨을 올렸다 내렸다 상쇄한 뒤
    /// 마지막에 Stop()까지 불러서 소리가 아예 나지 않는다.
    /// </summary>
    private bool IsSingleSource => SourceB == null || SourceB == SourceA;

    void Awake()
    {
        if (instance == null) instance = this;
        else Destroy(gameObject);
    }

    public void PlayOneShotSE(AudioClip clip, float volume)
    {
        if (clip == null) return;

        // Camera.main이 없으면(MainCamera 태그 누락) 여기서 NullReference가 난다.
        Camera cam = Camera.main;
        Vector3 at = cam != null ? cam.transform.position : transform.position;

        AudioSource.PlayClipAtPoint(clip, at, volume);
    }

    public void CheckAndPlayBGM(int currentID)
    {
        if (SoundData == null)
        {
            Debug.LogWarning("BGMManager: SoundDataSO가 연결되지 않았습니다!");
            return;
        }

     
        foreach (var bgmEvent in SoundData.BGMEvents)
        {
            if (currentID >= bgmEvent.StartID && currentID <= bgmEvent.EndID)
            {
               
                Debug.Log($"[BGM] ID {currentID} 발견! '{bgmEvent.EventName}' 재생 시도");

                if (currentPlayingClip == bgmEvent.BGMClip) return;

                currentPlayingClip = bgmEvent.BGMClip;
                StartCoroutine(CrossFade(bgmEvent.BGMClip, bgmEvent.FadeDuration));
                return;
            }
        }
    }

    IEnumerator CrossFade(AudioClip clip, float Duration)
    {
        if (clip == null) yield break;

        if (SourceA == null)
        {
            Debug.LogWarning("BGMManager: SourceA가 연결되지 않았습니다!");
            yield break;
        }

        if (IsSingleSource)
        {
            yield return SingleSourceSwap(clip, Duration);
            yield break;
        }

        AudioSource Active = isSourceAActive ? SourceA : SourceB;
        AudioSource Next = isSourceAActive ? SourceB : SourceA;

        Next.clip = clip;
        Next.volume = 0;
        Next.Play();

        float elapsed = 0;
        while (elapsed < Duration)
        {
            elapsed += Time.deltaTime;
            float percent = elapsed / Duration;
            Active.volume = 1 - percent;
            Next.volume = percent;
            yield return null;
        }

        Active.Stop();
        isSourceAActive = !isSourceAActive;
    }

    /// <summary>
    /// 소스가 하나일 때. 겹쳐서 섞을 수 없으므로 페이드아웃 → 교체 → 페이드인 순으로 넘어간다.
    /// 총 시간은 크로스페이드와 같게 맞춰 절반씩 나눠 쓴다.
    /// </summary>
    IEnumerator SingleSourceSwap(AudioClip clip, float Duration)
    {
        float half = Mathf.Max(0.01f, Duration * 0.5f);

        // 이미 뭔가 나오고 있으면 먼저 줄인다.
        if (SourceA.isPlaying)
        {
            float from = SourceA.volume;
            float t = 0f;
            while (t < half)
            {
                t += Time.deltaTime;
                SourceA.volume = Mathf.Lerp(from, 0f, t / half);
                yield return null;
            }
            SourceA.Stop();
        }

        SourceA.clip = clip;
        SourceA.volume = 0f;
        SourceA.loop = true;   // BGM은 반복 재생이 기본이다
        SourceA.Play();

        float e = 0f;
        while (e < half)
        {
            e += Time.deltaTime;
            SourceA.volume = Mathf.Lerp(0f, 1f, e / half);
            yield return null;
        }
        SourceA.volume = 1f;
    }
}