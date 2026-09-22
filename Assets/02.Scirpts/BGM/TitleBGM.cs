using UnityEngine;

namespace VRProject.Sound
{
    /// <summary>타이틀에서 반복 재생되는 2D 배경음을 옵션 슬라이더와 연결한다.</summary>
    [RequireComponent(typeof(AudioSource))]
    [AddComponentMenu("VRProject/Title BGM")]
    public sealed class TitleBGM : MonoBehaviour
    {
        [SerializeField, Range(0f, 1f)] private float 기본음량 = 0.45f;
        private AudioSource source;

        private void Awake()
        {
            source = GetComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f;
        }

        private void OnEnable()
        {
            if (source == null) source = GetComponent<AudioSource>();
            SoundSettings.Changed += 음량반영;
            음량반영();

            if (source.clip != null && !source.isPlaying)
                source.Play();
        }

        private void OnDisable()
        {
            SoundSettings.Changed -= 음량반영;
        }

        private void 음량반영()
        {
            if (source != null)
                source.volume = 기본음량 * SoundSettings.Bgm;
        }
    }
}
