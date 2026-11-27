using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class SoulslikeAttackAuthoring
{
    private const string ControllerPath = "Assets/00Game/03Art/Animations/Player/Player_Soulslike_2H.controller";
    private const string OutputFolder = "Assets/00Game/03Art/Animations/Player/Soulslike2H_Attacks";

    private const float SlowTailHoldDurationSec = 0.40f;
    private const float SlowTailHoldSpeed = 0.10f;
    private const float SlowTailRampStartSpeed = 0.45f;
    private const float SlowTailRampEndSpeed = 1.20f;

    private static readonly AttackDesign[] Designs =
    {
        new AttackDesign(
            stateName: "Attack1",
            sourceAssetPath: "Assets/00Game/03Art/Imported/GrruzamSwordAnim/Animation/M_Katana_Blade/2_Attacks/2__5Combos/M_katana_Blade@Attack_5Combo_1_Inplace.FBX",
            sourceClipName: "Attack_5Combo_1_Inplace",
            outputClipName: "Attack1_Soulslike",
            attackName: "Attack1_BasicSlash",
            totalDurationSec: 1.62f,
            slowAddedDurationSec: 0.54f,
            preDelay: 0.42f,
            postDelay: 1.20f,
            inputWindowStartNormalized: 0.60f,
            inputWindowEndNormalized: 0.76f,
            trackUntilNormalized: 0.15f,
            facingLockNormalized: 0.24f,
            hitStartNormalized: 0.328f,
            hitNormalized: 0.38f,
            hitEndNormalized: 0.432f,
            anticipationLeadNormalized: 0.10f,
            slowStartNormalized: 0.48f),
        new AttackDesign(
            stateName: "Attack2",
            sourceAssetPath: "Assets/00Game/03Art/Imported/GrruzamSwordAnim/Animation/M_Katana_Blade/2_Attacks/2__5Combos/M_katana_Blade@Attack_5Combo_2_Inplace.FBX",
            sourceClipName: "Attack_5Combo_2_Inplace",
            outputClipName: "Attack2_Soulslike",
            attackName: "Attack2_DriveSlash",
            totalDurationSec: 1.75f,
            slowAddedDurationSec: 0.55f,
            preDelay: 0.47f,
            postDelay: 1.28f,
            inputWindowStartNormalized: 0.61f,
            inputWindowEndNormalized: 0.78f,
            trackUntilNormalized: 0.18f,
            facingLockNormalized: 0.27f,
            hitStartNormalized: 0.362f,
            hitNormalized: 0.42f,
            hitEndNormalized: 0.479f,
            anticipationLeadNormalized: 0.11f,
            slowStartNormalized: 0.51f),
        new AttackDesign(
            stateName: "Attack3",
            sourceAssetPath: "Assets/00Game/03Art/Imported/GrruzamSwordAnim/Animation/M_Katana_Blade/2_Attacks/2__5Combos/M_katana_Blade@Attack_5Combo_3_Inplace.FBX",
            sourceClipName: "Attack_5Combo_3_Inplace",
            outputClipName: "Attack3_Soulslike",
            attackName: "Attack3_HeavyCleave",
            totalDurationSec: 1.95f,
            slowAddedDurationSec: 0.55f,
            preDelay: 0.62f,
            postDelay: 1.33f,
            inputWindowStartNormalized: 0.64f,
            inputWindowEndNormalized: 0.80f,
            trackUntilNormalized: 0.14f,
            facingLockNormalized: 0.22f,
            hitStartNormalized: 0.458f,
            hitNormalized: 0.51f,
            hitEndNormalized: 0.562f,
            anticipationLeadNormalized: 0.13f,
            slowStartNormalized: 0.57f),
        new AttackDesign(
            stateName: "Attack4",
            sourceAssetPath: "Assets/00Game/03Art/Imported/GrruzamSwordAnim/Animation/M_Katana_Blade/2_Attacks/2__5Combos/M_katana_Blade@Attack_5Combo_4_Inplace.FBX",
            sourceClipName: "Attack_5Combo_4_Inplace",
            outputClipName: "Attack4_Soulslike",
            attackName: "Attack4_Finisher",
            totalDurationSec: 2.15f,
            slowAddedDurationSec: 0.53f,
            preDelay: 0.74f,
            postDelay: 1.41f,
            inputWindowStartNormalized: 0.68f,
            inputWindowEndNormalized: 0.84f,
            trackUntilNormalized: 0.12f,
            facingLockNormalized: 0.20f,
            hitStartNormalized: 0.495f,
            hitNormalized: 0.56f,
            hitEndNormalized: 0.625f,
            anticipationLeadNormalized: 0.14f,
            slowStartNormalized: 0.64f),
    };

    [MenuItem("Tools/00Game/Combat/Generate Soulslike 2H Attack Set")]
    public static void GenerateSoulslikeAttackSet()
    {
        EnsureFolder(OutputFolder);

        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
        {
            Debug.LogError($"[AttackAuthoring] Controller not found: {ControllerPath}");
            return;
        }

        foreach (AttackDesign design in Designs)
        {
            AnimationClip sourceClip = LoadSourceClip(design.SourceAssetPath, design.SourceClipName);
            if (sourceClip == null)
            {
                Debug.LogError($"[AttackAuthoring] Source clip not found: {design.SourceAssetPath} ({design.SourceClipName})");
                continue;
            }

            AnimationClip retimedClip = CreateRetimedClip(sourceClip, design);
            string assetPath = $"{OutputFolder}/{design.OutputClipName}.anim";
            if (AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath) != null)
            {
                AssetDatabase.DeleteAsset(assetPath);
            }

            AssetDatabase.CreateAsset(retimedClip, assetPath);
            AnimationClip savedClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
            AssignMotion(controller, design.StateName, savedClip);
            ConfigureAttackExitTransition(controller, design.StateName);
        }

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[AttackAuthoring] Soulslike 2H attack clips regenerated.");
    }

    private static AnimationClip CreateRetimedClip(AnimationClip sourceClip, AttackDesign design)
    {
        AnimationClip clip = new AnimationClip
        {
            name = design.OutputClipName,
            frameRate = sourceClip.frameRate > 0f ? sourceClip.frameRate : 60f,
            wrapMode = WrapMode.Once,
        };

        float sourceLength = Mathf.Max(0.01f, sourceClip.length);
        SourceEventData sourceEvents = ExtractSourceEvents(sourceClip);
        float baseDurationSec = design.MotionDurationSec;
        float slowStartTimeSec = Mathf.Clamp(
            design.SlowStartNormalized * baseDurationSec,
            0f,
            Mathf.Max(0f, baseDurationSec - 0.001f));

        foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(sourceClip))
        {
            AnimationCurve curve = AnimationUtility.GetEditorCurve(sourceClip, binding);
            if (curve == null)
            {
                continue;
            }

            AnimationCurve retimedCurve = new AnimationCurve(RetimeKeys(curve.keys, sourceLength, sourceEvents, design, baseDurationSec));
            if (design.SlowAddedDurationSec > 0f)
            {
                retimedCurve = ApplySlowTailProfile(retimedCurve, slowStartTimeSec);
            }

            retimedCurve.preWrapMode = curve.preWrapMode;
            retimedCurve.postWrapMode = curve.postWrapMode;
            AnimationUtility.SetEditorCurve(clip, binding, retimedCurve);
        }

        foreach (EditorCurveBinding binding in AnimationUtility.GetObjectReferenceCurveBindings(sourceClip))
        {
            ObjectReferenceKeyframe[] sourceKeys = AnimationUtility.GetObjectReferenceCurve(sourceClip, binding);
            ObjectReferenceKeyframe[] retimedKeys = new ObjectReferenceKeyframe[sourceKeys.Length];
            for (int i = 0; i < sourceKeys.Length; i++)
            {
                float normalizedTime = Mathf.Clamp01(sourceKeys[i].time / sourceLength);
                retimedKeys[i] = sourceKeys[i];
                retimedKeys[i].time = RemapTime(normalizedTime, sourceEvents, design) * baseDurationSec;
            }

            if (design.SlowAddedDurationSec > 0f)
            {
                retimedKeys = ApplySlowTailProfile(retimedKeys, slowStartTimeSec, baseDurationSec);
            }

            AnimationUtility.SetObjectReferenceCurve(clip, binding, retimedKeys);
        }

        AnimationUtility.SetAnimationEvents(clip, CreateEvents(design, baseDurationSec));
        clip.EnsureQuaternionContinuity();
        return clip;
    }

    private static Keyframe[] RetimeKeys(
        Keyframe[] sourceKeys,
        float sourceLength,
        SourceEventData sourceEvents,
        AttackDesign design,
        float baseDurationSec)
    {
        Keyframe[] retimedKeys = new Keyframe[sourceKeys.Length];
        for (int i = 0; i < sourceKeys.Length; i++)
        {
            Keyframe key = sourceKeys[i];
            float normalizedTime = Mathf.Clamp01(key.time / sourceLength);
            float segmentScale = GetSegmentScale(normalizedTime, sourceEvents, design);

            key.time = RemapTime(normalizedTime, sourceEvents, design) * baseDurationSec;
            if (!float.IsNaN(key.inTangent) && !float.IsInfinity(key.inTangent) && Mathf.Abs(segmentScale) > 0.0001f)
            {
                key.inTangent /= segmentScale;
            }

            if (!float.IsNaN(key.outTangent) && !float.IsInfinity(key.outTangent) && Mathf.Abs(segmentScale) > 0.0001f)
            {
                key.outTangent /= segmentScale;
            }

            retimedKeys[i] = key;
        }

        return retimedKeys;
    }

    private static AnimationCurve ApplySlowTailProfile(AnimationCurve curve, float slowStartTimeSec)
    {
        TailProfile profile = BuildTailProfile(curve[curve.length - 1].time, slowStartTimeSec);
        Keyframe[] slowedKeys = new Keyframe[curve.length];

        for (int i = 0; i < curve.length; i++)
        {
            Keyframe key = curve.keys[i];
            if (key.time > slowStartTimeSec)
            {
                RetimeTailOffset(key.time - slowStartTimeSec, profile, out float retimedOffset, out float timeScale);
                key.time = slowStartTimeSec + retimedOffset;

                if (!float.IsNaN(key.inTangent) && !float.IsInfinity(key.inTangent))
                {
                    key.inTangent /= timeScale;
                }

                if (!float.IsNaN(key.outTangent) && !float.IsInfinity(key.outTangent))
                {
                    key.outTangent /= timeScale;
                }
            }

            slowedKeys[i] = key;
        }

        return new AnimationCurve(slowedKeys);
    }

    private static ObjectReferenceKeyframe[] ApplySlowTailProfile(
        ObjectReferenceKeyframe[] keys,
        float slowStartTimeSec,
        float baseDurationSec)
    {
        TailProfile profile = BuildTailProfile(baseDurationSec, slowStartTimeSec);
        ObjectReferenceKeyframe[] slowedKeys = new ObjectReferenceKeyframe[keys.Length];

        for (int i = 0; i < keys.Length; i++)
        {
            slowedKeys[i] = keys[i];
            if (slowedKeys[i].time > slowStartTimeSec)
            {
                RetimeTailOffset(slowedKeys[i].time - slowStartTimeSec, profile, out float retimedOffset, out _);
                slowedKeys[i].time = slowStartTimeSec + retimedOffset;
            }
        }

        return slowedKeys;
    }

    private static TailProfile BuildTailProfile(float endTimeSec, float slowStartTimeSec)
    {
        float sourceTailDurationSec = Mathf.Max(0.0001f, endTimeSec - slowStartTimeSec);
        float sourceHoldDurationSec = Mathf.Clamp(
            SlowTailHoldDurationSec * SlowTailHoldSpeed,
            0.0001f,
            Mathf.Max(0.0001f, sourceTailDurationSec - 0.0001f));
        float remainingSourceDurationSec = Mathf.Max(0.0001f, sourceTailDurationSec - sourceHoldDurationSec);
        float rampAverageSpeed = (SlowTailRampStartSpeed + SlowTailRampEndSpeed) * 0.5f;
        float rampTargetDurationSec = remainingSourceDurationSec / Mathf.Max(0.0001f, rampAverageSpeed);
        float totalTargetTailDurationSec = SlowTailHoldDurationSec + rampTargetDurationSec;

        float rampStartSlope = Mathf.Max(
            0.0001f,
            SlowTailRampStartSpeed * rampTargetDurationSec / remainingSourceDurationSec);
        float rampEndSlope = Mathf.Max(
            rampStartSlope + 0.0001f,
            SlowTailRampEndSpeed * rampTargetDurationSec / remainingSourceDurationSec);

        return new TailProfile(
            sourceTailDurationSec,
            sourceHoldDurationSec,
            remainingSourceDurationSec,
            SlowTailHoldDurationSec,
            rampTargetDurationSec,
            totalTargetTailDurationSec,
            rampStartSlope,
            rampEndSlope);
    }

    private static void RetimeTailOffset(float sourceOffsetSec, TailProfile profile, out float targetOffsetSec, out float timeScale)
    {
        float clampedSourceOffsetSec = Mathf.Clamp(sourceOffsetSec, 0f, profile.SourceTailDurationSec);
        if (clampedSourceOffsetSec <= profile.SourceHoldDurationSec)
        {
            targetOffsetSec = clampedSourceOffsetSec / SlowTailHoldSpeed;
            timeScale = 1f / SlowTailHoldSpeed;
            return;
        }

        float rampSourceOffsetSec = clampedSourceOffsetSec - profile.SourceHoldDurationSec;
        float sourceProgress = Mathf.Clamp01(rampSourceOffsetSec / profile.RemainingSourceDurationSec);
        float targetProgress = InvertHermite01(sourceProgress, profile.RampStartSlope, profile.RampEndSlope);
        float derivative = Mathf.Max(0.0001f, EvaluateHermite01Derivative(targetProgress, profile.RampStartSlope, profile.RampEndSlope));

        targetOffsetSec = profile.TargetHoldDurationSec + (targetProgress * profile.TargetRampDurationSec);
        timeScale = (profile.TargetRampDurationSec / profile.RemainingSourceDurationSec) / derivative;
    }

    private static float EvaluateHermite01(float t, float startSlope, float endSlope)
    {
        float tt = t * t;
        float ttt = tt * t;
        float h10 = ttt - (2f * tt) + t;
        float h01 = (-2f * ttt) + (3f * tt);
        float h11 = ttt - tt;
        return (h10 * startSlope) + h01 + (h11 * endSlope);
    }

    private static float EvaluateHermite01Derivative(float t, float startSlope, float endSlope)
    {
        float tt = t * t;
        float dh10 = (3f * tt) - (4f * t) + 1f;
        float dh01 = (-6f * tt) + (6f * t);
        float dh11 = (3f * tt) - (2f * t);
        return (dh10 * startSlope) + dh01 + (dh11 * endSlope);
    }

    private static float InvertHermite01(float targetProgress, float startSlope, float endSlope)
    {
        float low = 0f;
        float high = 1f;
        for (int i = 0; i < 20; i++)
        {
            float mid = (low + high) * 0.5f;
            float value = EvaluateHermite01(mid, startSlope, endSlope);
            if (value < targetProgress)
            {
                low = mid;
            }
            else
            {
                high = mid;
            }
        }

        return (low + high) * 0.5f;
    }

    private static float RemapTime(float normalizedTime, SourceEventData sourceEvents, AttackDesign design)
    {
        BuildSegments(sourceEvents, design, out float[] sourcePoints, out float[] targetPoints);
        return EvaluatePiecewise(normalizedTime, sourcePoints, targetPoints);
    }

    private static float GetSegmentScale(float normalizedTime, SourceEventData sourceEvents, AttackDesign design)
    {
        BuildSegments(sourceEvents, design, out float[] sourcePoints, out float[] targetPoints);
        for (int i = 0; i < sourcePoints.Length - 1; i++)
        {
            if (normalizedTime <= sourcePoints[i + 1] || i == sourcePoints.Length - 2)
            {
                float sourceSegmentLength = Mathf.Max(0.0001f, sourcePoints[i + 1] - sourcePoints[i]);
                float targetSegmentLength = Mathf.Max(0.0001f, targetPoints[i + 1] - targetPoints[i]);
                return targetSegmentLength / sourceSegmentLength;
            }
        }

        return 1f;
    }

    private static void BuildSegments(SourceEventData sourceEvents, AttackDesign design, out float[] sourcePoints, out float[] targetPoints)
    {
        float sourceHoldStart = Mathf.Max(0f, sourceEvents.HitStartNormalized - Mathf.Min(0.12f, sourceEvents.HitStartNormalized * 0.5f));
        float targetHoldStart = Mathf.Max(0f, design.HitStartNormalized - design.AnticipationLeadNormalized);

        sourcePoints = new[]
        {
            0f,
            sourceHoldStart,
            sourceEvents.HitStartNormalized,
            sourceEvents.HitNormalized,
            sourceEvents.HitEndNormalized,
            1f,
        };

        targetPoints = new[]
        {
            0f,
            targetHoldStart,
            design.HitStartNormalized,
            design.HitNormalized,
            design.HitEndNormalized,
            1f,
        };
    }

    private static float EvaluatePiecewise(float value, IReadOnlyList<float> sourcePoints, IReadOnlyList<float> targetPoints)
    {
        if (value <= sourcePoints[0])
        {
            return targetPoints[0];
        }

        for (int i = 0; i < sourcePoints.Count - 1; i++)
        {
            float start = sourcePoints[i];
            float end = sourcePoints[i + 1];
            if (value <= end || i == sourcePoints.Count - 2)
            {
                float t = Mathf.Approximately(start, end) ? 0f : Mathf.InverseLerp(start, end, value);
                return Mathf.Lerp(targetPoints[i], targetPoints[i + 1], t);
            }
        }

        return targetPoints[targetPoints.Count - 1];
    }

    private static AnimationEvent[] CreateEvents(AttackDesign design, float baseDurationSec)
    {
        return new[]
        {
            new AnimationEvent { functionName = "HitStart", time = design.HitStartNormalized * baseDurationSec },
            new AnimationEvent { functionName = "Hit", time = design.HitNormalized * baseDurationSec },
            new AnimationEvent { functionName = "HitEnd", time = design.HitEndNormalized * baseDurationSec },
        };
    }

    private static SourceEventData ExtractSourceEvents(AnimationClip clip)
    {
        float clipLength = Mathf.Max(0.01f, clip.length);
        SourceEventData fallback = new SourceEventData(0.32f, 0.37f, 0.41f);
        AnimationEvent[] events = AnimationUtility.GetAnimationEvents(clip);
        if (events == null || events.Length == 0)
        {
            return fallback;
        }

        float hitStart = -1f;
        float hit = -1f;
        float hitEnd = -1f;

        foreach (AnimationEvent animationEvent in events)
        {
            float normalizedTime = Mathf.Clamp01(animationEvent.time / clipLength);
            switch (animationEvent.functionName)
            {
                case "HitStart":
                    hitStart = normalizedTime;
                    break;
                case "Hit":
                    hit = normalizedTime;
                    break;
                case "HitEnd":
                    hitEnd = normalizedTime;
                    break;
            }
        }

        if (hitStart < 0f || hit < 0f || hitEnd < 0f)
        {
            return fallback;
        }

        return new SourceEventData(hitStart, hit, hitEnd);
    }

    private static AnimationClip LoadSourceClip(string assetPath, string clipName)
    {
        UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
        foreach (UnityEngine.Object asset in assets)
        {
            if (asset is AnimationClip clip && clip.name == clipName)
            {
                return clip;
            }
        }

        return null;
    }

    private static void AssignMotion(AnimatorController controller, string stateName, Motion motion)
    {
        AnimatorState state = FindState(controller, stateName);
        if (state == null)
        {
            Debug.LogWarning($"[AttackAuthoring] State not found: {stateName}");
            return;
        }

        state.motion = motion;
        state.speed = 1f;
        EditorUtility.SetDirty(state);
    }

    private static void ConfigureAttackExitTransition(AnimatorController controller, string stateName)
    {
        AnimatorState state = FindState(controller, stateName);
        if (state == null)
        {
            return;
        }

        foreach (AnimatorStateTransition transition in state.transitions)
        {
            transition.hasExitTime = true;
            transition.exitTime = 1f;
            transition.duration = 0.04f;
            transition.hasFixedDuration = true;
            EditorUtility.SetDirty(transition);
        }
    }

    private static AnimatorState FindState(AnimatorController controller, string stateName)
    {
        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
        foreach (ChildAnimatorState childState in stateMachine.states)
        {
            if (childState.state.name == stateName)
            {
                return childState.state;
            }
        }

        return null;
    }

    private static void EnsureFolder(string assetPath)
    {
        string[] segments = assetPath.Split('/');
        string current = segments[0];
        for (int i = 1; i < segments.Length; i++)
        {
            string next = $"{current}/{segments[i]}";
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, segments[i]);
            }

            current = next;
        }
    }

    private readonly struct AttackDesign
    {
        public AttackDesign(
            string stateName,
            string sourceAssetPath,
            string sourceClipName,
            string outputClipName,
            string attackName,
            float totalDurationSec,
            float slowAddedDurationSec,
            float preDelay,
            float postDelay,
            float inputWindowStartNormalized,
            float inputWindowEndNormalized,
            float trackUntilNormalized,
            float facingLockNormalized,
            float hitStartNormalized,
            float hitNormalized,
            float hitEndNormalized,
            float anticipationLeadNormalized,
            float slowStartNormalized)
        {
            StateName = stateName;
            SourceAssetPath = sourceAssetPath;
            SourceClipName = sourceClipName;
            OutputClipName = outputClipName;
            AttackName = attackName;
            TotalDurationSec = totalDurationSec;
            SlowAddedDurationSec = slowAddedDurationSec;
            PreDelay = preDelay;
            PostDelay = postDelay;
            InputWindowStartNormalized = inputWindowStartNormalized;
            InputWindowEndNormalized = inputWindowEndNormalized;
            TrackUntilNormalized = trackUntilNormalized;
            FacingLockNormalized = facingLockNormalized;
            HitStartNormalized = hitStartNormalized;
            HitNormalized = hitNormalized;
            HitEndNormalized = hitEndNormalized;
            AnticipationLeadNormalized = anticipationLeadNormalized;
            SlowStartNormalized = slowStartNormalized;
        }

        public string StateName { get; }
        public string SourceAssetPath { get; }
        public string SourceClipName { get; }
        public string OutputClipName { get; }
        public string AttackName { get; }
        public float TotalDurationSec { get; }
        public float SlowAddedDurationSec { get; }
        public float PreDelay { get; }
        public float PostDelay { get; }
        public float InputWindowStartNormalized { get; }
        public float InputWindowEndNormalized { get; }
        public float TrackUntilNormalized { get; }
        public float FacingLockNormalized { get; }
        public float HitStartNormalized { get; }
        public float HitNormalized { get; }
        public float HitEndNormalized { get; }
        public float AnticipationLeadNormalized { get; }
        public float SlowStartNormalized { get; }
        public float MotionDurationSec => Mathf.Max(0.05f, TotalDurationSec - SlowAddedDurationSec);
    }

    private readonly struct SourceEventData
    {
        public SourceEventData(float hitStartNormalized, float hitNormalized, float hitEndNormalized)
        {
            HitStartNormalized = hitStartNormalized;
            HitNormalized = hitNormalized;
            HitEndNormalized = hitEndNormalized;
        }

        public float HitStartNormalized { get; }
        public float HitNormalized { get; }
        public float HitEndNormalized { get; }
    }

    private readonly struct TailProfile
    {
        public TailProfile(
            float sourceTailDurationSec,
            float sourceHoldDurationSec,
            float remainingSourceDurationSec,
            float targetHoldDurationSec,
            float targetRampDurationSec,
            float targetTailDurationSec,
            float rampStartSlope,
            float rampEndSlope)
        {
            SourceTailDurationSec = sourceTailDurationSec;
            SourceHoldDurationSec = sourceHoldDurationSec;
            RemainingSourceDurationSec = remainingSourceDurationSec;
            TargetHoldDurationSec = targetHoldDurationSec;
            TargetRampDurationSec = targetRampDurationSec;
            TargetTailDurationSec = targetTailDurationSec;
            RampStartSlope = rampStartSlope;
            RampEndSlope = rampEndSlope;
        }

        public float SourceTailDurationSec { get; }
        public float SourceHoldDurationSec { get; }
        public float RemainingSourceDurationSec { get; }
        public float TargetHoldDurationSec { get; }
        public float TargetRampDurationSec { get; }
        public float TargetTailDurationSec { get; }
        public float RampStartSlope { get; }
        public float RampEndSlope { get; }
    }
}



