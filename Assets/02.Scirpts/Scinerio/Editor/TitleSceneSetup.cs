using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VRProject.Flow;
using VRProject.Sound;

namespace VRProject.EditorTools
{
    /// <summary>
    /// 01Title 씬을 VR 타이틀 화면으로 구성한다.
    ///
    ///   게임시작   → Scene_Manager.GameStart  (02 Street로 페이드 전환)
    ///   사운드 세팅 → TitleMenu.사운드열기     (SoundOption 창, 닫기 버튼으로 복귀)
    ///   게임종료   → Scene_Manager.게임종료
    ///
    /// 함께 정리하는 것:
    ///   · 캔버스를 월드 공간으로 (Overlay는 HMD에 안 보인다) — TitleVRUI
    ///   · XR Origin 배치 (없으면 컨트롤러 레이가 없어 버튼을 못 누른다)
    ///   · 스크립트 이름이 바뀌어 비어버린 옛 Scencemanager 오브젝트 제거
    ///
    /// 여러 번 실행해도 안전하다. 이미 있는 것은 찾아서 다시 연결만 한다.
    ///
    /// 메뉴: Tools ▸ VRProject ▸ 타이틀 화면 구성
    /// </summary>
    public static class TitleSceneSetup
    {
        private const string 타이틀씬경로 = "Assets/01 Scene/01Title.unity";
        private const string 다음씬경로 = "Assets/01 Scene/02 Street.unity";
        private const string XR리그경로 =
            "Assets/Samples/XR Interaction Toolkit/3.3.2/Starter Assets/Prefabs/XR Origin (XR Rig).prefab";

        private const float 버튼높이 = 70f;
        private const float 버튼간격 = 20f;

        [MenuItem("Tools/VRProject/타이틀 화면 구성")]
        private static void 메뉴실행()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != 타이틀씬경로)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                scene = EditorSceneManager.OpenScene(타이틀씬경로, OpenSceneMode.Single);
            }

            if (구성(scene))
            {
                EditorSceneManager.MarkSceneDirty(scene);
                Debug.Log("[TitleSceneSetup] 타이틀 화면 구성 완료. 씬을 저장할 것 (Ctrl+S).");
            }
        }

        /// <summary>배치모드용. 씬을 열고 구성한 뒤 저장까지 한다.</summary>
        public static void BatchRun()
        {
            Scene scene = EditorSceneManager.OpenScene(타이틀씬경로, OpenSceneMode.Single);
            if (!구성(scene))
            {
                EditorApplication.Exit(1);
                return;
            }
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[TitleSceneSetup] 타이틀 화면 구성 후 저장 완료.");
        }

        // ────────────────────────────────────────────────────────────
        private static bool 구성(Scene scene)
        {
            GameObject[] roots = scene.GetRootGameObjects();

            Canvas canvas = 메인캔버스찾기(roots);
            if (canvas == null)
            {
                Debug.LogError("[TitleSceneSetup] 'Canvas' 오브젝트를 찾지 못했다.");
                return false;
            }

            Button 시작버튼 = 버튼찾기(canvas.transform, "게임시작", "GameStart");
            if (시작버튼 == null)
            {
                Debug.LogError("[TitleSceneSetup] 게임시작(GameStart) 버튼을 찾지 못했다. " +
                               "나머지 버튼은 이 버튼을 복제해 만들므로 먼저 있어야 한다.");
                return false;
            }

            Transform 사운드창 = 자식찾기(canvas.transform, "SoundOption");
            if (사운드창 == null || 사운드창.GetComponent<SoundOptionUI>() == null)
            {
                Debug.LogError("[TitleSceneSetup] SoundOption 패널이 없다. " +
                               "Tools ▸ VRProject ▸ 사운드 옵션 UI 만들기를 먼저 실행할 것.");
                return false;
            }

            옛씬매니저제거(roots);
            XR리그확보(scene, roots);

            Scene_Manager 씬매니저 = 씬매니저확보(scene, roots);
            TitleMenu 메뉴 = 캔버스정리(canvas, 시작버튼.transform.parent.gameObject, 사운드창.gameObject);

            // ── 메뉴 버튼 세 개 ────────────────────────────────────
            Transform 묶음 = 시작버튼.transform.parent;
            메뉴묶음배치(묶음);

            시작버튼 = 버튼다듬기(시작버튼, "게임시작", "게임시작", 0);
            Button 사운드버튼 = 버튼다듬기(
                버튼찾기(묶음, "사운드세팅", "SoundOtion") ?? 복제(시작버튼, 묶음),
                "사운드세팅", "사운드 세팅", 1);
            Button 종료버튼 = 버튼다듬기(
                버튼찾기(묶음, "게임종료") ?? 복제(시작버튼, 묶음),
                "게임종료", "게임종료", 2);

            연결(시작버튼, 씬매니저.GameStart);
            연결(사운드버튼, 메뉴.사운드열기);
            연결(종료버튼, 씬매니저.게임종료);

            // ── 사운드 창 닫기 버튼 ────────────────────────────────
            Button 닫기 = 닫기버튼확보(사운드창, 시작버튼);
            연결(닫기, 메뉴.사운드닫기);

            사운드창.gameObject.SetActive(false);
            return true;
        }

        // ── 찾기 ────────────────────────────────────────────────────

        private static Canvas 메인캔버스찾기(GameObject[] roots)
        {
            foreach (GameObject go in roots)
            {
                if (go.name == "Canvas" && go.TryGetComponent(out Canvas c)) return c;
            }
            return null;
        }

        private static Transform 자식찾기(Transform 부모, string 이름)
        {
            foreach (Transform t in 부모.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == 이름) return t;
            }
            return null;
        }

        private static Button 버튼찾기(Transform 부모, params string[] 이름들)
        {
            foreach (string 이름 in 이름들)
            {
                Transform t = 자식찾기(부모, 이름);
                if (t != null && t.TryGetComponent(out Button b)) return b;
            }
            return null;
        }

        // ── 씬 정리 ─────────────────────────────────────────────────

        /// <summary>
        /// 스크립트 파일 이름이 Scence_Manager → Scene_Manager로 바뀌면서
        /// 참조가 끊긴 오브젝트. 게임시작 버튼이 이걸 가리키고 있어서 눌러도 반응이 없었다.
        /// 여기 붙은 캔버스와 TitleVRUI도 메인 캔버스와 중복이라 함께 치운다.
        /// </summary>
        private static void 옛씬매니저제거(GameObject[] roots)
        {
            foreach (GameObject go in roots)
            {
                if (go != null && go.name == "Scencemanager")
                {
                    Undo.DestroyObjectImmediate(go);
                    Debug.Log("[TitleSceneSetup] 스크립트가 끊긴 옛 'Scencemanager'를 제거했다.");
                }
            }
        }

        /// <summary>
        /// XR Origin이 없으면 02 Street와 같은 리그를 배치한다.
        /// 원래 있던 평범한 Main Camera는 태그가 겹치므로 끈다(지우지 않는다).
        /// </summary>
        private static void XR리그확보(Scene scene, GameObject[] roots)
        {
            if (Object.FindAnyObjectByType<Unity.XR.CoreUtils.XROrigin>(FindObjectsInactive.Include) != null)
                return;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(XR리그경로);
            if (prefab == null)
            {
                Debug.LogWarning($"[TitleSceneSetup] XR 리그 프리팹을 찾지 못했다: {XR리그경로}\n" +
                                 "씬에 XR Origin을 직접 배치할 것. 없으면 버튼을 누를 수 없다.");
                return;
            }

            foreach (GameObject go in roots)
            {
                if (go != null && go.name == "Main Camera" && go.TryGetComponent(out Camera _))
                {
                    Undo.RecordObject(go, "Main Camera 끄기");
                    go.SetActive(false);
                    go.name = "Main Camera (XR 리그로 대체)";
                }
            }

            var rig = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            rig.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            Undo.RegisterCreatedObjectUndo(rig, "XR 리그 배치");
            Debug.Log("[TitleSceneSetup] XR Origin (XR Rig)를 배치했다.", rig);
        }

        private static Scene_Manager 씬매니저확보(Scene scene, GameObject[] roots)
        {
            Scene_Manager sm = null;
            foreach (GameObject go in roots)
            {
                if (go != null && go.TryGetComponent(out sm)) break;
            }

            if (sm == null)
            {
                var go = new GameObject("SceneManager");
                SceneManager.MoveGameObjectToScene(go, scene);
                Undo.RegisterCreatedObjectUndo(go, "SceneManager 만들기");
                sm = go.AddComponent<Scene_Manager>();
            }

            var so = new SerializedObject(sm);
            so.FindProperty("씬에셋").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<SceneAsset>(다음씬경로);
            so.FindProperty("씬이름").stringValue =
                System.IO.Path.GetFileNameWithoutExtension(다음씬경로);   // "02 Street"
            so.ApplyModifiedPropertiesWithoutUndo();
            return sm;
        }

        /// <summary>캔버스를 VR용으로 돌리고 메뉴 전환 컴포넌트를 붙인다.</summary>
        private static TitleMenu 캔버스정리(Canvas canvas, GameObject 메뉴묶음, GameObject 사운드창)
        {
            GameObject go = canvas.gameObject;

            // 월드 공간 전환 · 크기 · 스케일은 TitleVRUI가 OnEnable/OnValidate에서 처리한다.
            if (!go.TryGetComponent(out TitleVRUI vr))
                vr = Undo.AddComponent<TitleVRUI>(go);

            var vso = new SerializedObject(vr);
            vso.FindProperty("XR리그프리팹").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<GameObject>(XR리그경로);
            vso.ApplyModifiedPropertiesWithoutUndo();

            // Overlay에서 넘어오면 크기가 0으로 저장돼 있다. TitleVRUI 기본 규격으로 맞춘다.
            var rt = (RectTransform)go.transform;
            if (rt.sizeDelta.x < 100f) rt.sizeDelta = new Vector2(1200f, 800f);
            canvas.renderMode = RenderMode.WorldSpace;

            if (!go.TryGetComponent(out TitleMenu menu))
                menu = Undo.AddComponent<TitleMenu>(go);

            var mso = new SerializedObject(menu);
            mso.FindProperty("메뉴").objectReferenceValue = 메뉴묶음;
            mso.FindProperty("사운드창").objectReferenceValue = 사운드창;
            mso.ApplyModifiedPropertiesWithoutUndo();
            return menu;
        }

        // ── 버튼 ────────────────────────────────────────────────────

        /// <summary>
        /// 세 버튼을 세로로 고르게 쌓는다. 타이틀 이미지(위쪽) 아래에 들어가도록
        /// 캔버스 하단부에 둔다.
        /// </summary>
        private static void 메뉴묶음배치(Transform 묶음)
        {
            var rt = (RectTransform)묶음;
            Undo.RecordObject(rt, "메뉴 배치");
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(440f, 버튼높이 * 3 + 버튼간격 * 2);
            rt.anchoredPosition = new Vector2(0f, -235f);
            묶음.name = "메뉴";

            if (!묶음.TryGetComponent(out VerticalLayoutGroup vlg))
                vlg = Undo.AddComponent<VerticalLayoutGroup>(묶음.gameObject);

            Undo.RecordObject(vlg, "메뉴 배치");
            vlg.spacing = 버튼간격;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
        }

        private static Button 복제(Button 원본, Transform 부모)
        {
            GameObject go = Object.Instantiate(원본.gameObject, 부모);
            Undo.RegisterCreatedObjectUndo(go, "버튼 복제");
            return go.GetComponent<Button>();
        }

        private static Button 버튼다듬기(Button b, string 오브젝트이름, string 표시글자, int 순서)
        {
            GameObject go = b.gameObject;
            Undo.RecordObject(go, "버튼 다듬기");
            go.name = 오브젝트이름;
            go.transform.SetSiblingIndex(순서);

            // 레이아웃 그룹 안에서 ContentSizeFitter가 크기를 두고 다퉈 경고가 뜬다.
            if (go.TryGetComponent(out ContentSizeFitter fitter))
                Undo.DestroyObjectImmediate(fitter);

            if (!go.TryGetComponent(out LayoutElement le))
                le = Undo.AddComponent<LayoutElement>(go);
            le.preferredHeight = 버튼높이;
            le.minHeight = 버튼높이;

            글자바꾸기(go, 표시글자);
            return b;
        }

        /// <summary>
        /// 사운드 창 하단에 닫기 버튼을 둔다. 창 높이를 늘려 슬라이더와 겹치지 않게 한다.
        /// </summary>
        private static Button 닫기버튼확보(Transform 사운드창, Button 견본)
        {
            var prt = (RectTransform)사운드창;
            Undo.RecordObject(prt, "사운드 창 배치");
            prt.anchorMin = prt.anchorMax = prt.pivot = new Vector2(0.5f, 0.5f);
            prt.sizeDelta = new Vector2(520f, 280f);
            prt.anchoredPosition = new Vector2(0f, -215f);

            Button 닫기 = 버튼찾기(사운드창, "닫기");
            if (닫기 == null)
            {
                닫기 = 복제(견본, 사운드창);
                닫기.name = "닫기";
            }

            GameObject go = 닫기.gameObject;
            if (go.TryGetComponent(out ContentSizeFitter fitter)) Undo.DestroyObjectImmediate(fitter);
            if (go.TryGetComponent(out LayoutElement le)) Undo.DestroyObjectImmediate(le);

            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(180f, 52f);
            rt.anchoredPosition = new Vector2(0f, 18f);

            글자바꾸기(go, "닫기");
            return 닫기;
        }

        private static void 글자바꾸기(GameObject 버튼, string 글자)
        {
            var t = 버튼.GetComponentInChildren<TextMeshProUGUI>(true);
            if (t == null) return;
            Undo.RecordObject(t, "버튼 글자");
            t.text = 글자;
        }

        /// <summary>
        /// OnClick을 비우고 하나만 연결한다. 복제한 버튼은 원본의 연결을 그대로 들고 오므로
        /// 비우지 않으면 게임종료를 눌렀는데 게임이 시작되는 식의 사고가 난다.
        /// </summary>
        private static void 연결(Button b, UnityAction 동작)
        {
            Undo.RecordObject(b, "버튼 연결");
            while (b.onClick.GetPersistentEventCount() > 0)
                UnityEventTools.RemovePersistentListener(b.onClick, 0);

            UnityEventTools.AddPersistentListener(b.onClick, 동작);
            b.onClick.SetPersistentListenerState(0, UnityEventCallState.RuntimeOnly);
            EditorUtility.SetDirty(b);
        }
    }
}
