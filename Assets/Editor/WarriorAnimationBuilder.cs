#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class WarriorAnimationBuilder
{
    const string TexturePath = "Assets/Resources/Allies/warrior_choco_sheet.png";
    const string OutputDir = "Assets/Resources/Allies/WarriorAnimations";
    const string ControllerPath = OutputDir + "/Warrior.controller";

    static readonly string[] DownFrames = { "warrior_choco_sheet_1", "warrior_choco_sheet_5", "warrior_choco_sheet_2", "warrior_choco_sheet_5" };
    static readonly string[] UpFrames = { "warrior_choco_sheet_25", "warrior_choco_sheet_26", "warrior_choco_sheet_27", "warrior_choco_sheet_26" };
    static readonly string[] LeftFrames = { "warrior_choco_sheet_33", "warrior_choco_sheet_34", "warrior_choco_sheet_16", "warrior_choco_sheet_34" };
    static readonly string[] RightFrames = { "warrior_choco_sheet_33", "warrior_choco_sheet_34", "warrior_choco_sheet_16", "warrior_choco_sheet_34" };

    [MenuItem("Tools/Allies/Rebuild Warrior Animator")]
    public static void Build()
    {
        EnsureOutputFolder();

        var sprites = LoadSprites();
        if (sprites.Count == 0)
        {
            Debug.LogError("[WarriorAnimationBuilder] warrior_choco_sheet.png 슬라이스 스프라이트를 찾지 못했습니다.");
            return;
        }

        CreateClip("IdleDown", new[] { DownFrames[1] }, sprites, false);
        CreateClip("IdleUp", new[] { UpFrames[1] }, sprites, false);
        CreateClip("IdleLeft", new[] { LeftFrames[1] }, sprites, false);
        CreateClip("IdleRight", new[] { RightFrames[1] }, sprites, false);
        CreateClip("WalkDown", DownFrames, sprites, true);
        CreateClip("WalkUp", UpFrames, sprites, true);
        CreateClip("WalkLeft", LeftFrames, sprites, true);
        CreateClip("WalkRight", RightFrames, sprites, true);

        BuildController();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[WarriorAnimationBuilder] 전사 Animator/AnimationClip 재생성 완료");
    }

    static Dictionary<string, Sprite> LoadSprites()
    {
        var result = new Dictionary<string, Sprite>();
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(TexturePath))
        {
            if (asset is Sprite sprite)
                result[sprite.name] = sprite;
        }
        return result;
    }

    static void EnsureOutputFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");
        if (!AssetDatabase.IsValidFolder("Assets/Resources/Allies"))
            AssetDatabase.CreateFolder("Assets/Resources", "Allies");
        if (!AssetDatabase.IsValidFolder(OutputDir))
            AssetDatabase.CreateFolder("Assets/Resources/Allies", "WarriorAnimations");
    }

    static AnimationClip CreateClip(string clipName, string[] spriteNames, Dictionary<string, Sprite> sprites, bool loop)
    {
        string clipPath = $"{OutputDir}/{clipName}.anim";
        AssetDatabase.DeleteAsset(clipPath);

        var clip = new AnimationClip
        {
            frameRate = 4.5f,
            name = clipName
        };

        var binding = new EditorCurveBinding
        {
            type = typeof(SpriteRenderer),
            path = "",
            propertyName = "m_Sprite"
        };

        var keyframes = new ObjectReferenceKeyframe[spriteNames.Length];
        for (int i = 0; i < spriteNames.Length; i++)
        {
            if (!sprites.TryGetValue(spriteNames[i], out Sprite sprite))
                throw new KeyNotFoundException($"Sprite not found: {spriteNames[i]}");

            keyframes[i] = new ObjectReferenceKeyframe
            {
                time = i / clip.frameRate,
                value = sprite
            };
        }

        AnimationUtility.SetObjectReferenceCurve(clip, binding, keyframes);
        SetClipLoop(clip, loop);
        AssetDatabase.CreateAsset(clip, clipPath);
        return clip;
    }

    static void SetClipLoop(AnimationClip clip, bool loop)
    {
        var serializedObject = new SerializedObject(clip);
        serializedObject.FindProperty("m_AnimationClipSettings.m_LoopTime").boolValue = loop;
        serializedObject.ApplyModifiedProperties();
    }

    static void BuildController()
    {
        AssetDatabase.DeleteAsset(ControllerPath);
        var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        controller.AddParameter("Direction", AnimatorControllerParameterType.Int);
        controller.AddParameter("Moving", AnimatorControllerParameterType.Bool);

        var stateMachine = controller.layers[0].stateMachine;

        var idleDown = AddState(stateMachine, "IdleDown", $"{OutputDir}/IdleDown.anim");
        var idleUp = AddState(stateMachine, "IdleUp", $"{OutputDir}/IdleUp.anim");
        var idleLeft = AddState(stateMachine, "IdleLeft", $"{OutputDir}/IdleLeft.anim");
        var idleRight = AddState(stateMachine, "IdleRight", $"{OutputDir}/IdleRight.anim");
        var walkDown = AddState(stateMachine, "WalkDown", $"{OutputDir}/WalkDown.anim");
        var walkUp = AddState(stateMachine, "WalkUp", $"{OutputDir}/WalkUp.anim");
        var walkLeft = AddState(stateMachine, "WalkLeft", $"{OutputDir}/WalkLeft.anim");
        var walkRight = AddState(stateMachine, "WalkRight", $"{OutputDir}/WalkRight.anim");

        stateMachine.defaultState = idleDown;

        AddAnyStateTransition(stateMachine, idleDown, 0, false);
        AddAnyStateTransition(stateMachine, idleUp, 1, false);
        AddAnyStateTransition(stateMachine, idleLeft, 2, false);
        AddAnyStateTransition(stateMachine, idleRight, 3, false);
        AddAnyStateTransition(stateMachine, walkDown, 0, true);
        AddAnyStateTransition(stateMachine, walkUp, 1, true);
        AddAnyStateTransition(stateMachine, walkLeft, 2, true);
        AddAnyStateTransition(stateMachine, walkRight, 3, true);
    }

    static AnimatorState AddState(AnimatorStateMachine stateMachine, string stateName, string clipPath)
    {
        var state = stateMachine.AddState(stateName);
        state.motion = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
        state.writeDefaultValues = true;
        return state;
    }

    static void AddAnyStateTransition(AnimatorStateMachine stateMachine, AnimatorState state, int direction, bool moving)
    {
        var transition = stateMachine.AddAnyStateTransition(state);
        transition.hasExitTime = false;
        transition.hasFixedDuration = true;
        transition.duration = 0.03f;
        transition.exitTime = 0f;
        transition.canTransitionToSelf = false;
        transition.AddCondition(AnimatorConditionMode.Equals, direction, "Direction");
        transition.AddCondition(moving ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, "Moving");
    }
}
#endif
