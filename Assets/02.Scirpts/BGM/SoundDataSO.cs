using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "NewSoundData",menuName ="Scenario/SoundData")]

public class SoundDataSO :ScriptableObject
{
    public List<BGMEvent> BGMEvents = new List<BGMEvent>();
}
[System.Serializable]
public class BGMEvent
{
    public string EventName;
    public int StartID;
    public int EndID;
    public int BGMIndex;
    public AudioClip BGMClip;
    public float FadeDuration = 1.5f;

    /// <summary>
    /// 이 곡의 기본 음량 (0~1). 제작자가 정한다.
    ///
    /// 곡마다 녹음된 크기가 제각각이라, 같은 1로 틀어도 어떤 곡은 시끄럽고
    /// 어떤 곡은 안 들린다. 여기서 곡들 사이의 균형을 먼저 맞춰두면,
    /// 플레이어가 전체 음량을 반으로 줄여도 그 균형은 그대로 유지된다.
    ///
    /// 실제 재생 음량 = BaseVolume × SoundSettings.Bgm
    /// </summary>
    [Range(0f, 1f)] public float BaseVolume = 1f;
}