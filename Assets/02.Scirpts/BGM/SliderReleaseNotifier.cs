using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace VRProject.Sound
{
    /// <summary>
    /// 슬라이더에서 손을 뗀 순간을 알려준다.
    ///
    /// Slider.onValueChanged는 끄는 내내 매 프레임 불린다. 미리듣기처럼
    /// "값이 정해졌을 때 한 번"만 해야 하는 일은 그걸로 판단할 수 없다.
    /// EventSystem의 PointerUp을 받아 그 시점을 잡는다.
    ///
    /// VR 컨트롤러 레이도 결국 EventSystem을 타므로 그대로 동작한다.
    /// </summary>
    [RequireComponent(typeof(UnityEngine.UI.Slider))]
    public class SliderReleaseNotifier : MonoBehaviour, IPointerUpHandler
    {
        /// <summary>손을 뗐을 때 부를 것. 코드에서 대입한다.</summary>
        public Action 놓았을때;

        public void OnPointerUp(PointerEventData eventData) => 놓았을때?.Invoke();
    }
}
