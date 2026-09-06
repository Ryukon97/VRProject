using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using VRProject.Sound;

namespace VRProject.EditorTools
{
    /// <summary>
    /// 타이틀 화면에 사운드 옵션 패널을 만들어 붙인다.
    ///
    /// 슬라이더 하나를 손으로 만들려면 RectTransform 네 겹에 Fill/Handle 계층까지
    /// 맞춰야 해서 잔손이 많이 간다. 두 개를 같은 규격으로 맞추는 것은 더 번거롭다.
    /// 한 번 만들고 나면 위치나 색은 인스펙터에서 자유롭게 다듬으면 된다.
    ///
    /// 이미 있으면 다시 만들지 않는다. 여러 번 눌러도 안전하다.
    /// </summary>
    public static class SoundOptionUIBuilder
    {
        private const string 패널이름 = "SoundOption";

        [MenuItem("Tools/VRProject/사운드 옵션 UI 만들기")]
        public static void Build()
        {
            Canvas canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                Debug.LogError("[SoundOptionUIBuilder] 씬에서 Canvas를 찾지 못했다. " +
                               "Title 씬을 열고 다시 실행할 것.");
                return;
            }

            Transform 기존 = canvas.transform.Find(패널이름);
            if (기존 != null)
            {
                Selection.activeGameObject = 기존.gameObject;
                Debug.Log($"[SoundOptionUIBuilder] '{패널이름}'이 이미 있다. 선택만 한다.", 기존);
                return;
            }

            GameObject 패널 = 패널만들기(canvas.transform);

            var ui = 패널.AddComponent<SoundOptionUI>();

            var (배경음슬라이더, 배경음수치) = 줄만들기(패널.transform, "배경음", 0);
            var (더빙슬라이더, 더빙수치) = 줄만들기(패널.transform, "더빙", 1);

            // private [SerializeField]라 직접 대입할 수 없다. 직렬화로 채운다.
            var so = new SerializedObject(ui);
            so.FindProperty("배경음슬라이더").objectReferenceValue = 배경음슬라이더;
            so.FindProperty("더빙슬라이더").objectReferenceValue = 더빙슬라이더;
            so.FindProperty("배경음수치").objectReferenceValue = 배경음수치;
            so.FindProperty("더빙수치").objectReferenceValue = 더빙수치;
            so.ApplyModifiedPropertiesWithoutUndo();

            Undo.RegisterCreatedObjectUndo(패널, "사운드 옵션 UI 만들기");
            EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
            Selection.activeGameObject = 패널;

            Debug.Log($"[SoundOptionUIBuilder] '{패널이름}' 생성 완료.\n" +
                      $"  배경음 기본값 {SoundSettings.DefaultBgm:P0} / " +
                      $"더빙 기본값 {SoundSettings.DefaultVoice:P0}\n" +
                      "위치와 색은 인스펙터에서 다듬을 것.", 패널);
        }

        private static GameObject 패널만들기(Transform 부모)
        {
            var go = new GameObject(패널이름, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(부모, false);

            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(520f, 200f);
            rt.anchoredPosition = Vector2.zero;

            var bg = go.GetComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.55f);

            제목만들기(rt);
            return go;
        }

        private static void 제목만들기(RectTransform 부모)
        {
            var go = new GameObject("제목", typeof(RectTransform));
            go.transform.SetParent(부모, false);

            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(-40f, 44f);
            rt.anchoredPosition = new Vector2(0f, -12f);

            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = "사운드";
            t.fontSize = 30f;
            t.alignment = TextAlignmentOptions.Left;
        }

        /// <summary>라벨 + 슬라이더 + 퍼센트 한 줄을 만든다.</summary>
        private static (Slider, TextMeshProUGUI) 줄만들기(Transform 부모, string 이름, int 번째)
        {
            var 줄 = new GameObject(이름, typeof(RectTransform));
            줄.transform.SetParent(부모, false);

            var rt = (RectTransform)줄.transform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(-40f, 48f);
            rt.anchoredPosition = new Vector2(0f, -66f - 번째 * 56f);

            라벨만들기(rt, 이름);
            Slider s = 슬라이더만들기(rt);
            TextMeshProUGUI 수치 = 수치만들기(rt);

            return (s, 수치);
        }

        private static void 라벨만들기(RectTransform 부모, string 이름)
        {
            var go = new GameObject("라벨", typeof(RectTransform));
            go.transform.SetParent(부모, false);

            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.sizeDelta = new Vector2(110f, 0f);
            rt.anchoredPosition = Vector2.zero;

            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = 이름;
            t.fontSize = 24f;
            t.alignment = TextAlignmentOptions.Left;
        }

        /// <summary>
        /// 슬라이더를 Background / Fill / Handle 계층까지 갖춰 만든다.
        ///
        /// Slider 컴포넌트만 붙이면 눈에 보이는 것이 없어서 만들다 만 것처럼 보인다.
        /// fillRect와 handleRect를 연결해야 값이 시각적으로 드러난다.
        /// </summary>
        private static Slider 슬라이더만들기(RectTransform 부모)
        {
            var go = new GameObject("슬라이더", typeof(RectTransform));
            go.transform.SetParent(부모, false);

            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, 0.5f);
            rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(120f, -10f);
            rt.offsetMax = new Vector2(-80f, 10f);

            var 배경 = 자식이미지(rt, "Background", new Color(1f, 1f, 1f, 0.25f));

            var fillArea = 늘린자식(rt, "Fill Area");
            var fill = 자식이미지(fillArea, "Fill", new Color(0.4f, 0.8f, 1f, 0.95f));

            var handleArea = 늘린자식(rt, "Handle Slide Area");
            var handle = 자식이미지(handleArea, "Handle", Color.white);
            handle.rectTransform.sizeDelta = new Vector2(20f, 0f);

            var s = go.AddComponent<Slider>();
            s.targetGraphic = handle;
            s.fillRect = fill.rectTransform;
            s.handleRect = handle.rectTransform;
            s.direction = Slider.Direction.LeftToRight;
            s.minValue = 0f;
            s.maxValue = 1f;
            s.value = 0.5f;

            return s;
        }

        private static TextMeshProUGUI 수치만들기(RectTransform 부모)
        {
            var go = new GameObject("수치", typeof(RectTransform));
            go.transform.SetParent(부모, false);

            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(1f, 0f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 0.5f);
            rt.sizeDelta = new Vector2(70f, 0f);
            rt.anchoredPosition = Vector2.zero;

            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = "100%";
            t.fontSize = 22f;
            t.alignment = TextAlignmentOptions.Right;
            return t;
        }

        private static RectTransform 늘린자식(RectTransform 부모, string 이름)
        {
            var go = new GameObject(이름, typeof(RectTransform));
            go.transform.SetParent(부모, false);

            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return rt;
        }

        private static Image 자식이미지(RectTransform 부모, string 이름, Color 색)
        {
            RectTransform rt = 늘린자식(부모, 이름);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = 색;
            return img;
        }
    }
}
