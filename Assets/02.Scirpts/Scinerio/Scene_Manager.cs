using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace VRProject.Flow
{
    /// <summary>
    /// 씬 전환을 담당한다. 타이틀의 GameStart 버튼이 이걸 호출한다.
    ///
    /// VR에서는 씬을 그냥 바꾸면 안 된다. 눈앞의 세상이 한 프레임 만에 통째로
    /// 갈리면 뇌가 따라가지 못해 어지럽다. 그래서 검게 덮고 → 로드하고 → 걷어내는
    /// 순서로 넘어간다. 평면 게임의 페이드가 연출이라면, VR에서는 편의 장치에 가깝다.
    ///
    /// 페이드 화면은 카메라의 자식으로 만든 월드 캔버스에 그린다.
    /// Screen Space - Overlay 캔버스는 HMD에 아예 렌더링되지 않아서 못 쓴다.
    /// </summary>
    [AddComponentMenu("VRProject/Scene Manager")]
    public class Scene_Manager : MonoBehaviour
    {
#if UNITY_EDITOR
        [Header("이동할 씬")]
        [Tooltip("씬 파일을 여기에 끌어다 놓으면 아래 이름이 자동으로 채워진다.\n" +
                 "이 칸은 에디터 전용이라 빌드에는 들어가지 않는다.")]
        [SerializeField] private UnityEditor.SceneAsset 씬에셋;
#endif

        [Tooltip("실제로 불러올 씬 이름. 위 칸에 넣으면 자동으로 채워진다.")]
        [SerializeField] private string 씬이름;

        [Header("전환 연출")]
        [Tooltip("검게 덮이고 걷히는 데 걸리는 시간(초).\n" +
                 "VR에서는 0.4초보다 짧으면 급해서 눈이 놀란다.")]
        [Range(0f, 3f)][SerializeField] private float 페이드시간 = 0.6f;

        [Tooltip("완전히 검어진 뒤 이만큼 더 기다렸다 넘어간다.\n" +
                 "로딩이 순식간이면 화면이 깜빡인 것처럼 보여서, 조금 붙잡아 둔다.")]
        [Range(0f, 2f)][SerializeField] private float 암전유지 = 0.2f;

        [Tooltip("페이드 판을 놓을 거리(m). 카메라 근접 클리핑보다 멀어야 한다.")]
        [Range(0.1f, 1f)][SerializeField] private float 페이드거리 = 0.35f;

        private bool 전환중;
        private Image 페이드판;

        /// <summary>GameStart 버튼의 OnClick에 연결한다.</summary>
        public void GameStart()
        {
            씬이동(씬이름);
        }

        /// <summary>씬 이름을 직접 주고 넘어간다. 버튼마다 다른 씬으로 보낼 때 쓴다.</summary>
        public void 씬이동(string 이름)
        {
            if (전환중) return;   // 버튼 연타로 로드가 두 번 걸리는 것을 막는다

            if (string.IsNullOrWhiteSpace(이름))
            {
                Debug.LogError("[Scene_Manager] 이동할 씬이 지정되지 않았다. " +
                               "인스펙터의 '씬에셋' 칸에 씬 파일을 넣을 것.", this);
                return;
            }

            if (!빌드에있나(이름))
            {
                Debug.LogError(
                    $"[Scene_Manager] '{이름}' 씬이 Build Settings 목록에 없거나 꺼져 있다.\n" +
                    "File ▸ Build Profiles(또는 Build Settings)에서 씬을 추가하고 체크할 것.\n" +
                    "이게 빠지면 버튼을 눌러도 아무 일도 일어나지 않는다.", this);
                return;
            }

            StartCoroutine(전환(이름));
        }

        /// <summary>애플리케이션 종료. 종료 버튼에 연결해서 쓴다.</summary>
        public void 게임종료()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private IEnumerator 전환(string 이름)
        {
            전환중 = true;

            yield return 페이드(0f, 1f);
            if (암전유지 > 0f) yield return new WaitForSeconds(암전유지);

            // 비동기로 불러오되, 다 읽을 때까지 활성화를 미룬다.
            // 그래야 로딩이 끝나는 시점과 화면이 넘어가는 시점을 우리가 정할 수 있다.
            AsyncOperation op = SceneManager.LoadSceneAsync(이름);
            if (op == null)
            {
                Debug.LogError($"[Scene_Manager] '{이름}' 로드를 시작하지 못했다.", this);
                전환중 = false;
                yield break;
            }

            op.allowSceneActivation = false;

            // allowSceneActivation이 false면 진행도가 0.9에서 멈춘다. 그게 '다 읽음'이다.
            while (op.progress < 0.9f) yield return null;

            op.allowSceneActivation = true;

            // 새 씬이 올라오면 이 오브젝트도 같이 사라지므로 뒷정리는 필요 없다.
            // DontDestroyOnLoad로 살려둘 생각이라면 여기서 페이드를 걷어야 한다.
        }

        /// <summary>
        /// 페이드 판의 알파를 시간에 걸쳐 바꾼다.
        ///
        /// Time.timeScale이 0이어도 동작하도록 unscaledDeltaTime을 쓴다.
        /// 옵션 창에서 시간을 멈춰둔 채 타이틀로 나가는 경우가 있다.
        /// </summary>
        private IEnumerator 페이드(float 시작, float 끝)
        {
            Image 판 = 페이드판확보();
            if (판 == null) yield break;

            if (페이드시간 <= 0f)
            {
                판.color = new Color(0f, 0f, 0f, 끝);
                yield break;
            }

            float t = 0f;
            while (t < 페이드시간)
            {
                t += Time.unscaledDeltaTime;
                float a = Mathf.Lerp(시작, 끝, t / 페이드시간);
                판.color = new Color(0f, 0f, 0f, a);
                yield return null;
            }
            판.color = new Color(0f, 0f, 0f, 끝);
        }

        /// <summary>
        /// 카메라 앞을 덮는 검은 판을 만든다.
        ///
        /// 카메라의 자식으로 두는 것이 핵심이다. 고개를 돌려도 시야가 계속 덮여 있어야
        /// 하는데, 월드에 고정하면 돌아보는 순간 옆으로 밝은 씬이 드러난다.
        /// </summary>
        private Image 페이드판확보()
        {
            if (페이드판 != null) return 페이드판;

            Camera cam = Camera.main;
            if (cam == null)
            {
                Debug.LogError("[Scene_Manager] Camera.main을 찾지 못해 페이드를 만들 수 없다. " +
                               "카메라에 MainCamera 태그가 있는지 확인할 것.", this);
                return null;
            }

            var go = new GameObject("SceneFade", typeof(Canvas));
            go.transform.SetParent(cam.transform, false);
            go.transform.localPosition = new Vector3(0f, 0f, 페이드거리);
            go.transform.localRotation = Quaternion.identity;

            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = cam;
            canvas.sortingOrder = 32767;   // 무엇보다도 앞에 그린다

            // 시야를 넉넉히 덮을 크기. 판이 가까우므로 조금만 커도 화면을 다 가린다.
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(2000f, 2000f);
            rt.localScale = Vector3.one * (페이드거리 * 0.005f);

            var img = new GameObject("Black", typeof(Image)).GetComponent<Image>();
            img.transform.SetParent(go.transform, false);
            img.color = new Color(0f, 0f, 0f, 0f);
            img.raycastTarget = false;   // 페이드가 버튼 클릭을 가로채면 안 된다

            var irt = img.rectTransform;
            irt.anchorMin = Vector2.zero;
            irt.anchorMax = Vector2.one;
            irt.offsetMin = irt.offsetMax = Vector2.zero;

            페이드판 = img;
            return 페이드판;
        }

        private static bool 빌드에있나(string 이름)
        {
            for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
            {
                string path = SceneUtility.GetScenePathByBuildIndex(i);
                if (System.IO.Path.GetFileNameWithoutExtension(path) == 이름) return true;
            }
            return false;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // 씬 에셋을 끌어다 놓으면 이름을 자동으로 맞춰준다.
            // 이름을 손으로 적게 두면 오타 하나에 버튼이 조용히 죽는다.
            if (씬에셋 != null) 씬이름 = 씬에셋.name;
        }
#endif
    }
}
