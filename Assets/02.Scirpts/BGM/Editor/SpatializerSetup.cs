using System.Linq;
using UnityEditor;
using UnityEngine;

namespace VRProject.EditorTools
{
    /// <summary>
    /// 프로젝트의 스페셜라이저 플러그인을 지정한다.
    ///
    /// 이걸 비워두면 AudioSource의 Spatial Blend를 1로 올려도 Unity 기본
    /// 좌우 패닝과 거리 감쇠만 걸린다. "뒤에서 들린다", "위에서 들린다" 같은
    /// 머리 전달 함수(HRTF) 기반 방향감은 플러그인이 있어야 나온다.
    ///
    /// 이 프로젝트에는 두 가지가 이미 들어와 있다.
    ///   · Meta XR Audio   (com.meta.xr.sdk.audio) — Quest 대상이면 이쪽이 낫다
    ///   · OculusSpatializer (com.unity.xr.oculus) — 예전 것, 폴백용
    /// 새로 설치할 것은 없고 고르기만 하면 된다.
    /// </summary>
    public static class SpatializerSetup
    {
        private const string AudioManagerPath = "ProjectSettings/AudioManager.asset";

        // 앞에 있는 것부터 우선 고른다.
        private static readonly string[] 선호순서 = { "Meta XR Audio", "OculusSpatializer" };

        [MenuItem("Tools/VRProject/공간 음향 플러그인 설정")]
        public static void Setup()
        {
            string[] 사용가능 = AudioSettings.GetSpatializerPluginNames();

            if (사용가능 == null || 사용가능.Length == 0)
            {
                Debug.LogError("[SpatializerSetup] 등록된 스페셜라이저 플러그인이 없다. " +
                               "패키지가 임포트됐는지 확인하고 에디터를 다시 켜볼 것.");
                return;
            }

            string 고를것 = 선호순서.FirstOrDefault(사용가능.Contains) ?? 사용가능[0];

            var assets = AssetDatabase.LoadAllAssetsAtPath(AudioManagerPath);
            if (assets == null || assets.Length == 0)
            {
                Debug.LogError($"[SpatializerSetup] {AudioManagerPath}를 열지 못했다.");
                return;
            }

            var audioManager = new SerializedObject(assets[0]);
            SerializedProperty 플러그인 = audioManager.FindProperty("m_SpatializerPlugin");

            string 이전 = 플러그인.stringValue;
            if (이전 == 고를것)
            {
                Debug.Log($"[SpatializerSetup] 이미 '{고를것}'으로 되어 있다.\n" +
                          $"  사용 가능: {string.Join(", ", 사용가능)}");
                return;
            }

            플러그인.stringValue = 고를것;
            audioManager.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();

            Debug.Log($"[SpatializerSetup] 스페셜라이저를 '{고를것}'으로 지정했다 " +
                      $"(이전: {(string.IsNullOrEmpty(이전) ? "없음" : 이전)}).\n" +
                      $"  사용 가능: {string.Join(", ", 사용가능)}\n\n" +
                      "오디오 시스템이 다시 초기화되어야 하므로 " +
                      "에디터를 한 번 재시작하는 편이 확실하다.");
        }
    }
}
