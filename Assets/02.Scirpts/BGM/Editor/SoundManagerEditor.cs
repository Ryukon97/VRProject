using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

public class SoundManagerEditor : EditorWindow
{

    private SoundDataSO SoundData;

    [MenuItem("MasterTools/Background Music Manager")]
    public static void ShowWindow()
    {
        GetWindow<SoundManagerEditor>("BGM Manager");
    }

    private void OnGUI()
    {
        GUILayout.Label("배경음 ID 관리자", EditorStyles.boldLabel);

        // 1. 데이터 소스 체크 시작
        EditorGUI.BeginChangeCheck();
        SoundData = (SoundDataSO)EditorGUILayout.ObjectField("Sound Data SO", SoundData, typeof(SoundDataSO), false);
        if (EditorGUI.EndChangeCheck())
        {
            Repaint();
        }

        if (SoundData == null)
        {
            EditorGUILayout.HelpBox("SoundDataSO 파일을 드래그해서 넣어주세요", MessageType.Warning);
            return;
        }

        SerializedObject so = new SerializedObject(SoundData);
        so.Update();

        EditorGUILayout.Space(10);

   
        EditorGUI.BeginChangeCheck();

        if (GUILayout.Button("새 BGM이벤트 추가"))
        {
            if (SoundData.BGMEvents == null)
                SoundData.BGMEvents = new List<BGMEvent>();

            SoundData.BGMEvents.Add(new BGMEvent());
            EditorUtility.SetDirty(SoundData);
        }

        EditorGUILayout.Space(5);
        for (int i = 0; i < SoundData.BGMEvents.Count; i++)
        {
            var e = SoundData.BGMEvents[i];
            EditorGUILayout.BeginVertical("Box");

            EditorGUILayout.BeginHorizontal();
            e.EventName = EditorGUILayout.TextField("이름", e.EventName);
            if (GUILayout.Button("X", GUILayout.Width(20)))
            {
                SoundData.BGMEvents.RemoveAt(i);
                EditorUtility.SetDirty(SoundData);
                AssetDatabase.SaveAssets();
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                return; 
            }
            EditorGUILayout.EndHorizontal();

            e.BGMClip = (AudioClip)EditorGUILayout.ObjectField("BGM파일", e.BGMClip, typeof(AudioClip), false);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("ID범위", GUILayout.Width(50));
            e.StartID = EditorGUILayout.IntField(e.StartID);
            EditorGUILayout.LabelField("~", GUILayout.Width(15));
            e.EndID = EditorGUILayout.IntField(e.EndID);
            EditorGUILayout.EndHorizontal();

            e.BGMIndex = EditorGUILayout.IntField("BGM인덱스", e.BGMIndex);
            e.FadeDuration = EditorGUILayout.Slider("디졸브 시간", e.FadeDuration, 0f, 5f);

            e.BaseVolume = EditorGUILayout.Slider(
                new GUIContent("기본 음량",
                    "제작자가 정하는 이 곡의 음량입니다.\n" +
                    "곡마다 녹음 크기가 달라서 여기서 균형을 먼저 맞춰둡니다.\n\n" +
                    "실제 재생 음량 = 기본 음량 × 플레이어가 옵션에서 고른 배경음 음량"),
                e.BaseVolume, 0f, 1f);

            // 실제로 얼마로 나오는지 같이 보여준다.
            // 두 값을 곱한다는 것을 글로만 적어두면 놓치기 쉽다.
            float 플레이어값 = VRProject.Sound.SoundSettings.Bgm;
            EditorGUILayout.LabelField(" ",
                $"실제 재생 ≈ {e.BaseVolume * 플레이어값:P0}  " +
                $"(플레이어 설정 {플레이어값:P0} 기준)",
                EditorStyles.miniLabel);

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(5);
        }
        if (so.hasModifiedProperties)
        {
            so.ApplyModifiedProperties();
        }

       
        if (GUILayout.Button("사운드 설정 저장(Force Save)", GUILayout.Height(30)))
        {
            EditorUtility.SetDirty(SoundData); 
            AssetDatabase.SaveAssets();     
            Debug.Log("<color=lime>사운드 데이터 저장 완료!</color>");
        }

        if (EditorGUI.EndChangeCheck())
        {
            EditorUtility.SetDirty(SoundData);
        }

        if (Event.current.type == EventType.DragPerform)
        {
            Repaint();
        }
    }

}
