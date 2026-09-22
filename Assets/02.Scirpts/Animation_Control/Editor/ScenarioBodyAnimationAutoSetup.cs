using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace VRProject.EditorTools
{
    [InitializeOnLoad]
    internal static class ScenarioBodyAnimationAutoSetup
    {
        private const string ControllerPath =
            "Assets/08animation/Aru_Real2_Controller.controller";
        private const string ScenarioPath =
            "Assets/09Scenario/Toghter Aru.asset";

        private static readonly Setting[] Settings =
        {
            new Setting(16, "Right Turn", false, true),
            new Setting(33, "Left Turn", false, true),
            new Setting(52, "Run Look Back", false, false),
            new Setting(55, "Running", true, false),
        };

        static ScenarioBodyAnimationAutoSetup()
        {
            EditorApplication.delayCall += Setup;
        }

        private static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            AnimatorController controller =
                AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            DialogueDataSO scenario =
                AssetDatabase.LoadAssetAtPath<DialogueDataSO>(ScenarioPath);
            if (controller == null || scenario == null) return;

            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            bool changed = false;

            foreach (Setting setting in Settings)
            {
                string path = $"Assets/08animation/{setting.StateName}.fbx";
                AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(path)
                    .OfType<AnimationClip>()
                    .FirstOrDefault(c => !c.name.StartsWith("__preview__", StringComparison.Ordinal));
                if (clip == null) continue;

                AnimatorState state = machine.states
                    .Select(s => s.state)
                    .FirstOrDefault(s => s.name == setting.StateName);
                if (state == null)
                {
                    state = machine.AddState(setting.StateName);
                    changed = true;
                }
                if (state.motion != clip)
                {
                    state.motion = clip;
                    changed = true;
                }
                state.speed = 1f;

                DialogueEntry entry = scenario.groups
                    .SelectMany(g => g.entries)
                    .FirstOrDefault(e => e.id == setting.Id);
                if (entry == null) continue;

                if (entry.bodyAnimation != clip ||
                    entry.bodyAnimationStateName != setting.StateName ||
                    entry.returnToIdleAfterBodyAnimation != setting.ReturnToIdle)
                {
                    entry.bodyAnimation = clip;
                    entry.bodyAnimationStateName = setting.StateName;
                    entry.returnToIdleAfterBodyAnimation = setting.ReturnToIdle;
                    changed = true;
                }

                ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer != null)
                {
                    ModelImporterClipAnimation[] clips = importer.clipAnimations;
                    if (clips == null || clips.Length == 0)
                        clips = importer.defaultClipAnimations;

                    bool clipChanged = false;
                    foreach (ModelImporterClipAnimation importedClip in clips)
                    {
                        if (importedClip.loopTime == setting.Loop) continue;
                        importedClip.loopTime = setting.Loop;
                        clipChanged = true;
                    }
                    if (clipChanged)
                    {
                        importer.clipAnimations = clips;
                        importer.SaveAndReimport();
                        EditorApplication.delayCall += Setup;
                        return;
                    }
                }
            }

            if (!changed) return;

            EditorUtility.SetDirty(controller);
            EditorUtility.SetDirty(scenario);
            AssetDatabase.SaveAssets();
            Debug.Log("[ScenarioBodyAnimation] ID 16, 33, 52, 55 전신 연출 연결 완료.");
        }

        private readonly struct Setting
        {
            public readonly int Id;
            public readonly string StateName;
            public readonly bool Loop;
            public readonly bool ReturnToIdle;

            public Setting(int id, string stateName, bool loop, bool returnToIdle)
            {
                Id = id;
                StateName = stateName;
                Loop = loop;
                ReturnToIdle = returnToIdle;
            }
        }
    }
}
