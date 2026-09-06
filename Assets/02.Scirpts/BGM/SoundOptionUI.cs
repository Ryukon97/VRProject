using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace VRProject.Sound
{
    /// <summary>
    /// 타이틀의 사운드 옵션. 배경음과 더빙 음량을 슬라이더로 조절한다.
    ///
    /// 값은 <see cref="SoundSettings"/>에 들어가므로 씬을 넘어가도 유지된다.
    /// 이 컴포넌트는 화면과 그 값을 이어주기만 한다.
    ///
    /// 슬라이더를 놓을 때가 아니라 끄는 동안 계속 반영한다. 소리는 귀로 맞추는 것이라,
    /// 손을 뗄 때까지 아무 변화가 없으면 어디가 맞는지 알 수 없다.
    /// </summary>
    [AddComponentMenu("VRProject/Sound Option UI")]
    public class SoundOptionUI : MonoBehaviour
    {
        [Header("슬라이더")]
        [SerializeField] private Slider 배경음슬라이더;
        [SerializeField] private Slider 더빙슬라이더;

        [Header("수치 표시 (선택)")]
        [Tooltip("비워둬도 된다. 넣으면 슬라이더 옆에 퍼센트가 뜬다.")]
        [SerializeField] private TextMeshProUGUI 배경음수치;
        [SerializeField] private TextMeshProUGUI 더빙수치;

        [Header("미리듣기 (선택)")]
        [Tooltip("더빙 슬라이더를 놓았을 때 한 번 재생할 샘플 음성.\n" +
                 "귀로 맞추려면 들어봐야 하는데, 타이틀에는 대사가 없어서 넣어둔다.")]
        [SerializeField] private AudioClip 더빙_미리듣기;

        [SerializeField] private AudioSource 미리듣기소스;

        private bool 갱신중;   // 코드가 슬라이더를 세팅할 때 콜백이 되돌아오는 것을 막는다

        private void OnEnable()
        {
            배치();

            if (배경음슬라이더 != null) 배경음슬라이더.onValueChanged.AddListener(배경음바뀜);
            if (더빙슬라이더 != null)
            {
                더빙슬라이더.onValueChanged.AddListener(더빙바뀜);

                // 값이 확정됐을 때만 미리듣기를 낸다.
                // 끄는 내내 재생하면 소리가 겹쳐서 오히려 판단이 안 된다.
                var trigger = 더빙슬라이더.gameObject.GetComponent<SliderReleaseNotifier>()
                              ?? 더빙슬라이더.gameObject.AddComponent<SliderReleaseNotifier>();
                trigger.놓았을때 = 미리듣기재생;
            }

            SoundSettings.Changed += 배치;
        }

        private void OnDisable()
        {
            if (배경음슬라이더 != null) 배경음슬라이더.onValueChanged.RemoveListener(배경음바뀜);
            if (더빙슬라이더 != null) 더빙슬라이더.onValueChanged.RemoveListener(더빙바뀜);

            SoundSettings.Changed -= 배치;
            SoundSettings.Save();   // 옵션 화면을 벗어날 때 디스크에 확정한다
        }

        /// <summary>저장된 값을 슬라이더와 숫자에 반영한다.</summary>
        private void 배치()
        {
            갱신중 = true;

            if (배경음슬라이더 != null) 배경음슬라이더.SetValueWithoutNotify(SoundSettings.Bgm);
            if (더빙슬라이더 != null) 더빙슬라이더.SetValueWithoutNotify(SoundSettings.Voice);

            수치갱신();
            갱신중 = false;
        }

        private void 배경음바뀜(float v)
        {
            if (갱신중) return;
            SoundSettings.Bgm = v;
            수치갱신();
        }

        private void 더빙바뀜(float v)
        {
            if (갱신중) return;
            SoundSettings.Voice = v;
            수치갱신();
        }

        private void 수치갱신()
        {
            if (배경음수치 != null) 배경음수치.text = $"{Mathf.RoundToInt(SoundSettings.Bgm * 100f)}%";
            if (더빙수치 != null) 더빙수치.text = $"{Mathf.RoundToInt(SoundSettings.Voice * 100f)}%";
        }

        private void 미리듣기재생()
        {
            if (더빙_미리듣기 == null) return;

            if (미리듣기소스 == null)
            {
                미리듣기소스 = gameObject.AddComponent<AudioSource>();
                미리듣기소스.playOnAwake = false;
                미리듣기소스.spatialBlend = 0f;   // 옵션 화면의 미리듣기는 방향감이 필요 없다
            }

            미리듣기소스.Stop();
            미리듣기소스.clip = 더빙_미리듣기;
            미리듣기소스.volume = SoundSettings.Voice;
            미리듣기소스.Play();
        }

        /// <summary>제작자가 정한 기본값으로. 버튼 OnClick에 연결해서 쓴다.</summary>
        public void 기본값으로()
        {
            SoundSettings.ResetToDefault();
            배치();
        }
    }
}
