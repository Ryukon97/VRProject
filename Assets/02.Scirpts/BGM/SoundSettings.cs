using System;
using UnityEngine;

namespace VRProject.Sound
{
    /// <summary>
    /// 플레이어가 고른 음량을 담아두는 곳.
    ///
    /// MonoBehaviour가 아니라 static인 이유는 씬을 넘어가야 하기 때문이다.
    /// Title에서 슬라이더를 만졌는데 본편 씬으로 넘어가면서 값이 사라지면 의미가 없다.
    /// PlayerPrefs에 저장하므로 게임을 껐다 켜도 유지된다.
    ///
    /// 값의 성격이 두 층으로 나뉜다는 점이 중요하다.
    ///   · 제작자가 정하는 값 — 곡마다의 기본 음량(BGMEvent.BaseVolume),
    ///     대사마다의 음량(DialogueEntry.voiceVolume). 소재별 편차를 먼저 고른다.
    ///   · 플레이어가 정하는 값 — 여기 있는 Bgm/Voice. 전체를 한 번에 키우고 줄인다.
    /// 최종 음량은 둘을 곱한 값이다. 그래야 플레이어가 슬라이더를 반으로 내려도
    /// 곡들 사이의 균형은 제작자가 잡아둔 대로 유지된다.
    /// </summary>
    public static class SoundSettings
    {
        private const string BgmKey = "sound.bgm";
        private const string VoiceKey = "sound.voice";

        // 처음 켰을 때의 기본값.
        //
        // BGM을 1이 아니라 0.6으로 두는 것은, 아무 설정도 안 한 상태에서 음악이
        // 대사를 덮어버리면 첫인상이 나빠지기 때문이다. 대사는 내용 자체라 1로 둔다.
        // 플레이어가 올리는 것은 쉽지만, 시끄러워서 껐다 켜게 만드는 것은 되돌리기 어렵다.
        public const float DefaultBgm = 0.6f;
        public const float DefaultVoice = 1f;

        private static float bgm = DefaultBgm;
        private static float voice = DefaultVoice;
        private static bool loaded;

        /// <summary>음량이 바뀔 때마다 불린다. 재생 중인 소리를 즉시 따라가게 할 때 쓴다.</summary>
        public static event Action Changed;

        /// <summary>배경음 음량 (0~1).</summary>
        public static float Bgm
        {
            get { EnsureLoaded(); return bgm; }
            set => Apply(ref bgm, BgmKey, value);
        }

        /// <summary>더빙(대사) 음량 (0~1).</summary>
        public static float Voice
        {
            get { EnsureLoaded(); return voice; }
            set => Apply(ref voice, VoiceKey, value);
        }

        private static void Apply(ref float 대상, string 키, float 값)
        {
            EnsureLoaded();

            값 = Mathf.Clamp01(값);
            if (Mathf.Approximately(대상, 값)) return;   // 같은 값이면 이벤트를 안 날린다

            대상 = 값;
            PlayerPrefs.SetFloat(키, 값);
            Changed?.Invoke();
        }

        /// <summary>
        /// 저장된 값을 처음 읽을 때 한 번만 불러온다.
        ///
        /// static 생성자를 쓰지 않는 것은, 그쪽은 호출 시점을 우리가 정할 수 없어서
        /// 메인 스레드가 아닌 곳에서 PlayerPrefs를 건드릴 위험이 있기 때문이다.
        /// </summary>
        private static void EnsureLoaded()
        {
            if (loaded) return;
            loaded = true;

            bgm = PlayerPrefs.GetFloat(BgmKey, DefaultBgm);
            voice = PlayerPrefs.GetFloat(VoiceKey, DefaultVoice);
        }

        /// <summary>디스크에 확실히 기록한다. 옵션 창을 닫을 때 한 번 부르면 된다.</summary>
        public static void Save() => PlayerPrefs.Save();

        /// <summary>제작자가 정한 기본값으로 되돌린다.</summary>
        public static void ResetToDefault()
        {
            Bgm = DefaultBgm;
            Voice = DefaultVoice;
            Save();
        }
    }
}
