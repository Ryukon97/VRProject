using UnityEngine;

namespace VRProject.Flow
{
    /// <summary>
    /// 타이틀의 메인 메뉴와 사운드 세팅 창을 서로 바꿔 띄운다.
    ///
    /// 둘을 동시에 띄우지 않는다. VR에서는 패널이 겹치면 레이가 뒤쪽 버튼에
    /// 닿는지 앞쪽 버튼에 닿는지 눈으로 구분하기 어렵다.
    ///
    /// 사운드 창을 닫을 때 SoundOptionUI.OnDisable이 값을 디스크에 저장하므로
    /// 여기서 따로 저장할 필요는 없다.
    /// </summary>
    [AddComponentMenu("VRProject/Title Menu")]
    public class TitleMenu : MonoBehaviour
    {
        [Tooltip("게임시작 / 사운드 세팅 / 게임종료 버튼을 묶은 오브젝트.")]
        [SerializeField] private GameObject 메뉴;

        [Tooltip("사운드 세팅 창 (SoundOptionUI가 붙은 패널).")]
        [SerializeField] private GameObject 사운드창;

        private void Start()
        {
            // 씬을 사운드 창이 켜진 채로 저장해도 시작은 항상 메뉴부터.
            사운드닫기();
        }

        /// <summary>사운드 세팅 버튼의 OnClick에 연결한다.</summary>
        public void 사운드열기()
        {
            if (사운드창 != null) 사운드창.SetActive(true);
            if (메뉴 != null) 메뉴.SetActive(true);
        }

        /// <summary>
        /// 타이틀의 같은 옵션 버튼으로 열고 닫는다. 옵션창 아래쪽에 버튼이 계속
        /// 보이므로 VR에서도 별도의 작은 닫기 버튼을 찾지 않아도 된다.
        /// </summary>
        public void 사운드전환()
        {
            if (사운드창 == null) return;
            사운드창.SetActive(!사운드창.activeSelf);
            if (메뉴 != null) 메뉴.SetActive(true);
        }

        /// <summary>사운드 창의 닫기 버튼 OnClick에 연결한다.</summary>
        public void 사운드닫기()
        {
            if (사운드창 != null) 사운드창.SetActive(false);
            if (메뉴 != null) 메뉴.SetActive(true);
        }
    }
}
