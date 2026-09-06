using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BGMManager : MonoBehaviour
{
    public static BGMManager instance;

    [Header("데이터 연결")]
    public SoundDataSO SoundData; 

    [Header("오디오 소스")]
    public AudioSource SourceA;
    public AudioSource SourceB;

    private bool isSourceAActive = true;
    private AudioClip currentPlayingClip; 

    void Awake()
    {
        if (instance == null) instance = this;
        else Destroy(gameObject);
    }

    public void PlayOneShotSE(AudioClip clip, float volume)
    {
        if (clip == null) return;
        AudioSource.PlayClipAtPoint(clip, Camera.main.transform.position, volume);
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
}