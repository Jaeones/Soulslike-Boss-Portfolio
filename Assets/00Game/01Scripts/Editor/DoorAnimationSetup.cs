using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class DoorAnimationSetup
{
    private const string ControllerPath = "Assets/00Game/03Art/Animations/Prop/Door.controller";
    private const string ClipPath = "Assets/00Game/03Art/Animations/Prop/Clips/DoorOpen.anim";
    private const string IdleStateName = "Idle";
    private const string OpenStateName = "DoorOpen";
    private const float OpenDuration = 1f;
    private const float LeftDoorOpenAngle = -100f;
    private const float RightDoorOpenAngle = 100f;

    static DoorAnimationSetup()
    {
        EditorApplication.delayCall += EnsureAssets;
    }

    [MenuItem("Tools/00Game/Prop/Setup Door Animation")]
    public static void EnsureAssets()
    {
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (clip == null || controller == null)
        {
            return;
        }

        bool changed = false;
        changed |= EnsureDoorOpenClip(clip);
        changed |= EnsureDoorController(controller, clip);

        if (!changed)
        {
            return;
        }

        EditorUtility.SetDirty(clip);
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[DoorAnimationSetup] Door animation assets were updated.");
    }

    private static bool EnsureDoorOpenClip(AnimationClip clip)
    {
        bool hasLeftCurve = HasBinding(clip, "LeftDoorRoot", "localEulerAnglesRaw.y");
        bool hasRightCurve = HasBinding(clip, "RightDoorRoot", "localEulerAnglesRaw.y");
        if (hasLeftCurve && hasRightCurve)
        {
            return false;
        }

        clip.frameRate = 60f;
        clip.wrapMode = WrapMode.Once;

        AnimationUtility.SetEditorCurve(
            clip,
            EditorCurveBinding.FloatCurve("LeftDoorRoot", typeof(Transform), "localEulerAnglesRaw.y"),
            AnimationCurve.EaseInOut(0f, 0f, OpenDuration, LeftDoorOpenAngle));

        AnimationUtility.SetEditorCurve(
            clip,
            EditorCurveBinding.FloatCurve("RightDoorRoot", typeof(Transform), "localEulerAnglesRaw.y"),
            AnimationCurve.EaseInOut(0f, 0f, OpenDuration, RightDoorOpenAngle));

        return true;
    }

    private static bool EnsureDoorController(AnimatorController controller, AnimationClip clip)
    {
        bool changed = false;
        AnimatorStateMachine stateMachine = GetOrCreateBaseStateMachine(controller, ref changed);
        AnimatorState idleState = FindState(stateMachine, IdleStateName);
        if (idleState == null)
        {
            idleState = stateMachine.AddState(IdleStateName, new Vector3(250f, 120f, 0f));
            changed = true;
        }

        AnimatorState openState = FindState(stateMachine, OpenStateName);
        if (openState == null)
        {
            openState = stateMachine.AddState(OpenStateName, new Vector3(250f, 240f, 0f));
            changed = true;
        }

        if (openState.motion != clip)
        {
            openState.motion = clip;
            changed = true;
        }

        if (stateMachine.defaultState != idleState)
        {
            stateMachine.defaultState = idleState;
            changed = true;
        }

        return changed;
    }

    private static AnimatorStateMachine GetOrCreateBaseStateMachine(AnimatorController controller, ref bool changed)
    {
        if (controller.layers != null && controller.layers.Length > 0 && controller.layers[0].stateMachine != null)
        {
            return controller.layers[0].stateMachine;
        }

        AnimatorStateMachine stateMachine = new AnimatorStateMachine
        {
            name = "Base Layer",
        };

        AssetDatabase.AddObjectToAsset(stateMachine, controller);

        AnimatorControllerLayer layer = new AnimatorControllerLayer
        {
            name = "Base Layer",
            defaultWeight = 1f,
            stateMachine = stateMachine,
        };

        controller.layers = new[] { layer };
        changed = true;
        return stateMachine;
    }

    private static AnimatorState FindState(AnimatorStateMachine stateMachine, string stateName)
    {
        return stateMachine.states.FirstOrDefault(childState => childState.state != null && childState.state.name == stateName).state;
    }

    private static bool HasBinding(AnimationClip clip, string path, string propertyName)
    {
        EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(clip);
        for (int i = 0; i < bindings.Length; i++)
        {
            if (bindings[i].path == path && bindings[i].propertyName == propertyName)
            {
                return true;
            }
        }

        return false;
    }
}
