using System;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class SoulslikeAnimatorBuilder
{
    private const string SourceControllerPath = "Assets/00Game/03Art/Animations/Player/Player.controller";
    private const string OutputControllerPath = "Assets/00Game/03Art/Animations/Player/Player_Soulslike_2H.controller";

    private const string KatanaFolder = "Assets/00Game/03Art/Imported/GrruzamSwordAnim/Animation/M_Katana_Blade";
    private const string WeaponAvatarMaskPath = "Assets/00Game/03Art/Imported/GrruzamSwordAnim/Modeling/Weapon_Avatar Mask.mask";
    private const string PotionDrinkPath = "Assets/00Game/03Art/Animations/Clips/Astus/Potion_Drink.FBX";

    [MenuItem("Tools/00Game/Animator/Build Soulslike Controller (Keep Guard)")]
    public static void CreateSoulslikeControllerKeepGuard()
    {
        EnsureFolder("Assets/00Game/03Art/Animations/Player");

        Motion existingGuardStart = null;
        Motion existingGuardLoop = null;
        Motion existingGuardEnd = null;
        Motion existingGuardAccept = null;

        AnimatorController source = AssetDatabase.LoadAssetAtPath<AnimatorController>(SourceControllerPath);
        if (source != null)
        {
            TryGetStateMotion(source, "Guard_Start", out existingGuardStart);
            TryGetStateMotion(source, "Guard_Loop", out existingGuardLoop);
            TryGetStateMotion(source, "Guard_End", out existingGuardEnd);
            TryGetStateMotion(source, "Guard_Accept", out existingGuardAccept);
        }
        else
        {
            Debug.LogWarning($"[Animator] Failed to find the source controller for guard fallback: {SourceControllerPath}");
        }

        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(OutputControllerPath) != null)
        {
            AssetDatabase.DeleteAsset(OutputControllerPath);
        }

        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(OutputControllerPath);
        AddParameters(controller);

        AnimatorStateMachine sm = controller.layers[0].stateMachine;

        AnimatorState idleA = sm.AddState("Idle_A", new Vector3(-520f, -80f, 0f));
        AnimatorState idleB = sm.AddState("Idle_B", new Vector3(-520f, 80f, 0f));
        AnimatorState equip = sm.AddState("Idle_A_to_B", new Vector3(-320f, 80f, 0f));
        AnimatorState unequip = sm.AddState("Idle_B_to_A", new Vector3(-320f, -80f, 0f));
        AnimatorState idleAToWalkA = sm.AddState("Idle_A_To_Walk_A", new Vector3(-300f, -140f, 0f));
        AnimatorState idleBToWalkB = sm.AddState("Idle_B_To_Walk_B", new Vector3(-300f, 140f, 0f));
        AnimatorState walkA = sm.AddState("Walk_A", new Vector3(-100f, -20f, 0f));
        AnimatorState walkB = sm.AddState("Walk_B", new Vector3(-100f, 20f, 0f));
        AnimatorState walkAToIdleA = sm.AddState("Walk_A_To_Idle_A", new Vector3(-760f, -140f, 0f));
        AnimatorState walkBToIdleB = sm.AddState("Walk_B_To_Idle_B", new Vector3(-760f, 140f, 0f));
        AnimatorState runA = sm.AddState("Run_A", new Vector3(100f, -20f, 0f));
        AnimatorState runB = sm.AddState("Run_B", new Vector3(100f, 20f, 0f));
        AnimatorState walkLockOn = sm.AddState("WalkLockOn", new Vector3(-300f, 140f, 0f));
        AnimatorState runLockOn = sm.AddState("RunLockOn", new Vector3(-100f, 140f, 0f));
        AnimatorState roll = sm.AddState("Roll", new Vector3(60f, -200f, 0f));
        AnimatorState attack1 = sm.AddState("Attack1", new Vector3(60f, 0f, 0f));
        AnimatorState attack2 = sm.AddState("Attack2", new Vector3(260f, 0f, 0f));
        AnimatorState attack3 = sm.AddState("Attack3", new Vector3(460f, 0f, 0f));
        AnimatorState attack4 = sm.AddState("Attack4", new Vector3(660f, 0f, 0f));
        AnimatorState drinkAstus = sm.AddState("DrinkAstus", new Vector3(280f, -120f, 0f));
        AnimatorState guardStart = sm.AddState("Guard_Start", new Vector3(-300f, -220f, 0f));
        AnimatorState guardLoop = sm.AddState("Guard_Loop", new Vector3(-80f, -220f, 0f));
        AnimatorState guardEnd = sm.AddState("Guard_End", new Vector3(140f, -220f, 0f));
        AnimatorState guardAccept = sm.AddState("Guard_Accept", new Vector3(80f, -420f, 0f));
        AnimatorState hitFront = sm.AddState("HitFront", new Vector3(-420f, 120f, 0f));
        AnimatorState hitBack = sm.AddState("HitBack", new Vector3(-420f, 260f, 0f));
        AnimatorState hitLeft = sm.AddState("HitLeft", new Vector3(-420f, 400f, 0f));
        AnimatorState hitRight = sm.AddState("HitRight", new Vector3(-420f, 540f, 0f));

        idleA.motion = LoadClip(
            KatanaFolder + "/1_Movements/1__Idle/M_katana_Blade@Idle_ver_A.FBX");

        idleB.motion = LoadClip(
            KatanaFolder + "/1_Movements/1__Idle/M_katana_Blade@Idle_ver_B.FBX");

        equip.motion = LoadClip(
            KatanaFolder + "/1_Movements/1__Idle/M_katana_Blade@Idle_A_to_B.FBX");

        unequip.motion = LoadClip(
            KatanaFolder + "/1_Movements/1__Idle/M_katana_Blade@Idle_B_to_A.FBX");

        walkA.motion = LoadClip(
            KatanaFolder + "/1_Movements/2__Walk/A/M_katana_Blade@Walk_ver_A_Front.FBX");

        walkB.motion = LoadClip(
            KatanaFolder + "/1_Movements/2__Walk/B/M_katana_Blade@Walk_ver_B_Front.FBX");

        idleAToWalkA.motion = LoadClip(
            KatanaFolder + "/1_Movements/9__Idle_To_Move/M_katana_Blade@Idle_To_Walk_ver_A.FBX");

        idleBToWalkB.motion = LoadClip(
            KatanaFolder + "/1_Movements/9__Idle_To_Move/M_katana_Blade@Idle_To_Walk_ver_B.FBX");

        walkAToIdleA.motion = LoadClip(
            KatanaFolder + "/1_Movements/10__Move_to_Idle/Walk_To_Idle/M_katana_Blade@Walk_ver_A_To_Idle_ver_A.FBX");

        walkBToIdleB.motion = LoadClip(
            KatanaFolder + "/1_Movements/10__Move_to_Idle/Walk_To_Idle/M_katana_Blade@Walk_ver_B_To_Idle_ver_B.FBX");

        runA.motion = LoadClip(
            KatanaFolder + "/1_Movements/4__Run/M_katana_Blade@Run_ver_A.FBX");

        runB.motion = LoadClip(
            KatanaFolder + "/1_Movements/4__Run/M_katana_Blade@Run_ver_B.FBX");

        walkLockOn.motion = CreateLockOnWalkBlendTree(controller);
        runLockOn.motion = CreateLockOnRunBlendTree(controller);

        roll.motion = LoadClip(
            KatanaFolder + "/1_Movements/8__Dodge/M_katana_Blade@Dodge_Front.FBX");

        attack1.motion = LoadClip(KatanaFolder + "/2_Attacks/2__5Combos/M_katana_Blade@Attack_5Combo_1_Inplace.FBX");
        attack2.motion = LoadClip(KatanaFolder + "/2_Attacks/2__5Combos/M_katana_Blade@Attack_5Combo_2_Inplace.FBX");
        attack3.motion = LoadClip(KatanaFolder + "/2_Attacks/2__5Combos/M_katana_Blade@Attack_5Combo_3_Inplace.FBX");
        attack4.motion = LoadClip(KatanaFolder + "/2_Attacks/2__5Combos/M_katana_Blade@Attack_5Combo_4_Inplace.FBX");
        drinkAstus.motion = LoadClip(PotionDrinkPath);

        hitFront.motion = LoadClip(
            KatanaFolder + "/4_Damages/1__Front/M_katana_Blade@Damage_Front_Small_ver_B.FBX");

        hitBack.motion = LoadClip(
            KatanaFolder + "/4_Damages/2__Back/M_katana_Blade@Damage_Back_Small_ver_B.FBX");

        hitLeft.motion = LoadClip(
            KatanaFolder + "/4_Damages/3__Left/M_katana_Blade@Damage_Left_Small_ver_B.FBX");

        hitRight.motion = LoadClip(
            KatanaFolder + "/4_Damages/4__Right/M_katana_Blade@Damage_Right_Small_ver_B.FBX");

        guardStart.motion = LoadClip(
            KatanaFolder + "/5_Revenges/Guard_Revenges/M_katana_Blade@Revenge_Guard_Start.FBX")
            ?? existingGuardStart;

        guardLoop.motion = LoadClip(
            KatanaFolder + "/5_Revenges/Guard_Revenges/M_katana_Blade@Revenge_Guard_Loop.FBX")
            ?? existingGuardLoop;

        guardEnd.motion = LoadClip(
            KatanaFolder + "/5_Revenges/Guard_Revenges/M_katana_Blade@Revenge_Guard_End.FBX")
            ?? existingGuardEnd;

        guardAccept.motion = LoadClip(
            KatanaFolder + "/5_Revenges/Guard_Revenges/M_katana_Blade@Revenge_Guard_Accept.FBX")
            ?? existingGuardAccept;

        sm.defaultState = idleA;

        AddBoolTransition(idleA, equip, "isBattle", true, false, 0.05f, "isRun", false);
        AddBoolTransition(idleB, unequip, "isBattle", false, false, 0.05f, "isRun", false);
        AddExitTransition(equip, idleB, 0.9f, 0.05f);
        AddExitTransition(unequip, idleA, 0.9f, 0.05f);

        AddBoolTransition(idleA, idleAToWalkA, "isMove", true, false, 0.05f, "isRun", false, "isLockOn", false);
        AddBoolTransition(idleB, idleBToWalkB, "isMove", true, false, 0.05f, "isRun", false, "isLockOn", false);
        AddExitTransition(idleAToWalkA, walkA, 0.9f, 0.05f);
        AddExitTransition(idleBToWalkB, walkB, 0.9f, 0.05f);

        AddBoolTransition(idleA, runA, "isMove", true, false, 0.05f, "isRun", true, "isLockOn", false);
        AddBoolTransition(idleB, runB, "isMove", true, false, 0.05f, "isRun", true, "isLockOn", false);
        AddBoolTransition(idleA, walkLockOn, "isMove", true, false, 0.05f, "isRun", false, "isLockOn", true);
        AddBoolTransition(idleB, walkLockOn, "isMove", true, false, 0.05f, "isRun", false, "isLockOn", true);
        AddBoolTransition(idleA, runLockOn, "isMove", true, false, 0.05f, "isRun", true, "isLockOn", true);
        AddBoolTransition(idleB, runLockOn, "isMove", true, false, 0.05f, "isRun", true, "isLockOn", true);

        AddBoolTransition(walkA, walkAToIdleA, "isMove", false, false, 0.05f, "isBattle", false, "isLockOn", false);
        AddBoolTransition(walkB, walkBToIdleB, "isMove", false, false, 0.05f, "isBattle", true, "isLockOn", false);
        AddExitTransition(walkAToIdleA, idleA, 0.9f, 0.05f);
        AddExitTransition(walkBToIdleB, idleB, 0.9f, 0.05f);

        AddBoolTransition(runA, idleA, "isMove", false, false, 0.05f, "isBattle", false);
        AddBoolTransition(runB, idleB, "isMove", false, false, 0.05f, "isBattle", true);
        AddBoolTransition(walkLockOn, idleB, "isMove", false, false, 0.05f, "isBattle", true);
        AddBoolTransition(runLockOn, idleB, "isMove", false, false, 0.05f, "isBattle", true);

        AddBoolTransition(walkA, runA, "isRun", true, false, 0.05f, "isLockOn", false, "isBattle", false);
        AddBoolTransition(walkB, runB, "isRun", true, false, 0.05f, "isLockOn", false, "isBattle", true);
        AddBoolTransition(runA, walkA, "isRun", false, false, 0.05f, "isMove", true, "isLockOn", false);
        AddBoolTransition(runB, walkB, "isRun", false, false, 0.05f, "isMove", true, "isLockOn", false);

        AddBoolTransition(walkA, walkLockOn, "isLockOn", true, false, 0.05f, "isRun", false);
        AddBoolTransition(walkB, walkLockOn, "isLockOn", true, false, 0.05f, "isRun", false);
        AddBoolTransition(runA, runLockOn, "isLockOn", true, false, 0.05f, "isRun", true);
        AddBoolTransition(runB, runLockOn, "isLockOn", true, false, 0.05f, "isRun", true);
        AddBoolTransition(walkLockOn, runLockOn, "isRun", true, false, 0.05f, "isMove", true);
        AddBoolTransition(runLockOn, walkLockOn, "isRun", false, false, 0.05f, "isMove", true);
        AddBoolTransition(walkLockOn, walkA, "isLockOn", false, false, 0.05f, "isBattle", false, "isRun", false);
        AddBoolTransition(walkLockOn, walkB, "isLockOn", false, false, 0.05f, "isBattle", true, "isRun", false);
        AddBoolTransition(runLockOn, runA, "isLockOn", false, false, 0.05f, "isBattle", false, "isRun", true);
        AddBoolTransition(runLockOn, runB, "isLockOn", false, false, 0.05f, "isBattle", true, "isRun", true);

        AddBoolTransition(idleA, guardStart, "isGuard", true, false, 0.05f);
        AddBoolTransition(idleB, guardStart, "isGuard", true, false, 0.05f);
        AddBoolTransition(walkA, guardStart, "isGuard", true, false, 0.05f);
        AddBoolTransition(walkB, guardStart, "isGuard", true, false, 0.05f);
        AddBoolTransition(runA, guardStart, "isGuard", true, false, 0.05f);
        AddBoolTransition(runB, guardStart, "isGuard", true, false, 0.05f);
        AddBoolTransition(runLockOn, guardStart, "isGuard", true, false, 0.05f);
        AddExitTransition(guardStart, guardLoop, 0.9f, 0.05f);

        AddBoolTransition(guardLoop, guardEnd, "isGuard", false, false, 0.05f);
        AddTriggerTransition(guardLoop, guardAccept, "tGuardChipHit", false, 0.03f);

        AddBoolTransition(guardAccept, guardLoop, "isGuard", true, true, 0.05f, exitTime: 0.9f);
        AddBoolTransition(guardAccept, guardEnd, "isGuard", false, true, 0.05f, exitTime: 0.9f);

        AddBoolTransition(guardEnd, walkB, "isRun", true, true, 0.05f, "isLockOn", false, exitTime: 0.9f);
        AddBoolTransition(guardEnd, runLockOn, "isRun", true, true, 0.05f, "isLockOn", true, exitTime: 0.9f);
        AddBoolTransition(guardEnd, idleA, "isRun", false, true, 0.05f, "isBattle", false, exitTime: 0.9f);
        AddBoolTransition(guardEnd, idleB, "isRun", false, true, 0.05f, "isBattle", true, exitTime: 0.9f);

        AddAnyStateTriggerTransition(sm, roll, "tRoll", false, 0.03f);
        AddAnyStateTriggerTransition(sm, hitFront, "tHitFront", false, 0.02f);
        AddAnyStateTriggerTransition(sm, hitBack, "tHitSideBack", false, 0.02f);
        AddAnyStateTriggerTransition(sm, attack1, "tAttack", false, 0.05f);
        AddAnyStateTriggerTransition(sm, drinkAstus, "tDrink", false, 0.05f);

        AddBoolTransition(roll, idleA, "isBattle", false, true, 0.05f, exitTime: 0.9f);
        AddBoolTransition(roll, idleB, "isBattle", true, true, 0.05f, exitTime: 0.9f);
        AddHitRecoveryTransitions(hitFront, idleA, idleB, walkA, walkB, walkLockOn, runA, runB, runLockOn);
        AddHitRecoveryTransitions(hitBack, idleA, idleB, walkA, walkB, walkLockOn, runA, runB, runLockOn);
        AddHitRecoveryTransitions(hitLeft, idleA, idleB, walkA, walkB, walkLockOn, runA, runB, runLockOn);
        AddHitRecoveryTransitions(hitRight, idleA, idleB, walkA, walkB, walkLockOn, runA, runB, runLockOn);
        AddBoolTransition(attack1, idleA, "isBattle", false, true, 0.05f, exitTime: 0.9f);
        AddBoolTransition(attack1, idleB, "isBattle", true, true, 0.05f, exitTime: 0.9f);
        AddBoolTransition(attack2, idleA, "isBattle", false, true, 0.05f, exitTime: 0.9f);
        AddBoolTransition(attack2, idleB, "isBattle", true, true, 0.05f, exitTime: 0.9f);
        AddBoolTransition(attack3, idleA, "isBattle", false, true, 0.05f, exitTime: 0.9f);
        AddBoolTransition(attack3, idleB, "isBattle", true, true, 0.05f, exitTime: 0.9f);
        AddBoolTransition(attack4, idleA, "isBattle", false, true, 0.05f, exitTime: 0.9f);
        AddBoolTransition(attack4, idleB, "isBattle", true, true, 0.05f, exitTime: 0.9f);
        AddBoolTransition(drinkAstus, idleA, "isBattle", false, true, 0.1f, exitTime: 0.90625f);
        AddBoolTransition(drinkAstus, idleB, "isBattle", true, true, 0.1f, exitTime: 0.90625f);

        AddUpperBodyEquipLayer(controller);

        SetCanTransitionToSelfFalse(sm);

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[Animator] Generated controller: {OutputControllerPath}");
        Debug.Log("[Animator] Replace the Player Animator controller with the generated controller and test it.");
    }

    [MenuItem("Tools/00Game/Animator/Rebuild Player.controller In Place")]
    public static void RebuildPlayerControllerInPlace()
    {
        CreateSoulslikeControllerKeepGuard();

        if (!File.Exists(OutputControllerPath))
        {
            Debug.LogError($"[Animator] Generated controller is missing: {OutputControllerPath}");
            return;
        }

        File.Copy(OutputControllerPath, SourceControllerPath, true);
        AssetDatabase.ImportAsset(SourceControllerPath, ImportAssetOptions.ForceUpdate);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[Animator] Rebuilt source controller in place: {SourceControllerPath}");
    }

    [MenuItem("Tools/00Game/Animator/Print Katana Clip Names")]
    public static void PrintTwoHandAndUnarmedClipNames()
    {
        PrintClipNamesFromFolder(KatanaFolder, "[M_Katana_Blade]");
    }

    private static void PrintClipNamesFromFolder(string folderPath, string header)
    {
        string[] guids = AssetDatabase.FindAssets("t:AnimationClip", new[] { folderPath });
        Array.Sort(guids, StringComparer.Ordinal);

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null)
            {
                continue;
            }

            if (clip.name.StartsWith("__preview__", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            Debug.Log($"{header} {clip.name}");
        }
    }

    private static BlendTree CreateLockOnRunBlendTree(AnimatorController controller)
    {
        BlendTree tree = new BlendTree
        {
            name = "RunBlendTree_2D",
            blendType = BlendTreeType.FreeformDirectional2D,
            blendParameter = "InputX",
            blendParameterY = "InputY",
            useAutomaticThresholds = false
        };

        AssetDatabase.AddObjectToAsset(tree, controller);
        AddBlendChild(tree, LoadClip(KatanaFolder + "/1_Movements/3__Jogging/A/M_katana_Blade@Jogging_8Way_verA_F.FBX"), new Vector2(0f, 1f));
        AddBlendChild(tree, LoadClip(KatanaFolder + "/1_Movements/3__Jogging/A/M_katana_Blade@Jogging_8Way_verA_B.FBX"), new Vector2(0f, -1f));
        AddBlendChild(tree, LoadClip(KatanaFolder + "/1_Movements/3__Jogging/A/M_katana_Blade@Jogging_8Way_verA_L90.FBX"), new Vector2(-1f, 0f));
        AddBlendChild(tree, LoadClip(KatanaFolder + "/1_Movements/3__Jogging/A/M_katana_Blade@Jogging_8Way_verA_R90.FBX"), new Vector2(1f, 0f));

        AddBlendChild(tree, LoadClip(KatanaFolder + "/1_Movements/3__Jogging/A/M_katana_Blade@Jogging_8Way_verA_FL45.FBX"), new Vector2(-1f, 1f));
        AddBlendChild(tree, LoadClip(KatanaFolder + "/1_Movements/3__Jogging/A/M_katana_Blade@Jogging_8Way_verA_FR45.FBX"), new Vector2(1f, 1f));
        AddBlendChild(tree, LoadClip(KatanaFolder + "/1_Movements/3__Jogging/A/M_katana_Blade@Jogging_8Way_verA_BL45.FBX"), new Vector2(-1f, -1f));
        AddBlendChild(tree, LoadClip(KatanaFolder + "/1_Movements/3__Jogging/A/M_katana_Blade@Jogging_8Way_verA_BR45.FBX"), new Vector2(1f, -1f));

        AddBlendChild(tree, LoadClip(KatanaFolder + "/1_Movements/4__Run/M_katana_Blade@Run_ver_A.FBX"), new Vector2(0f, 0.2f));

        return tree;
    }

    private static BlendTree CreateLockOnWalkBlendTree(AnimatorController controller)
    {
        BlendTree tree = new BlendTree
        {
            name = "WalkBlendTree_2D",
            blendType = BlendTreeType.FreeformDirectional2D,
            blendParameter = "InputX",
            blendParameterY = "InputY",
            useAutomaticThresholds = false
        };

        AssetDatabase.AddObjectToAsset(tree, controller);
        AddBlendChild(tree, LoadClip(KatanaFolder + "/1_Movements/2__Walk/B/M_katana_Blade@Walk_ver_B_Front.FBX"), new Vector2(0f, 1f));
        AddBlendChild(tree, LoadClip(KatanaFolder + "/1_Movements/2__Walk/B/M_katana_Blade@Walk_ver_B_Back.FBX"), new Vector2(0f, -1f));
        AddBlendChild(tree, LoadClip(KatanaFolder + "/1_Movements/2__Walk/B/M_katana_Blade@Walk_ver_B_L90.FBX"), new Vector2(-1f, 0f));
        AddBlendChild(tree, LoadClip(KatanaFolder + "/1_Movements/2__Walk/B/M_katana_Blade@Walk_ver_B_R90.FBX"), new Vector2(1f, 0f));

        AddBlendChild(tree, LoadClip(KatanaFolder + "/1_Movements/2__Walk/B/M_katana_Blade@Walk_ver_B_Front_L45.FBX"), new Vector2(-1f, 1f));
        AddBlendChild(tree, LoadClip(KatanaFolder + "/1_Movements/2__Walk/B/M_katana_Blade@Walk_ver_B_Front_R45.FBX"), new Vector2(1f, 1f));
        AddBlendChild(tree, LoadClip(KatanaFolder + "/1_Movements/2__Walk/B/M_katana_Blade@Walk_ver_B_Back_L45.FBX"), new Vector2(-1f, -1f));
        AddBlendChild(tree, LoadClip(KatanaFolder + "/1_Movements/2__Walk/B/M_katana_Blade@Walk_ver_B_Back_R45.FBX"), new Vector2(1f, -1f));

        return tree;
    }

    private static void AddBlendChild(BlendTree tree, Motion motion, Vector2 threshold)
    {
        if (motion == null)
        {
            return;
        }

        tree.AddChild(motion, threshold);
    }

    private static Motion LoadClip(params string[] candidatePaths)
    {
        foreach (string path in candidatePaths)
        {
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip != null)
            {
                return clip;
            }
        }

        Debug.LogWarning($"[Animator] Failed to load clip: {string.Join(" | ", candidatePaths)}");
        return null;
    }

    private static void AddParameters(AnimatorController controller)
    {
        controller.AddParameter("isIdle", AnimatorControllerParameterType.Bool);
        controller.AddParameter("isMove", AnimatorControllerParameterType.Bool);
        controller.AddParameter("isRun", AnimatorControllerParameterType.Bool);
        controller.AddParameter("isLockOn", AnimatorControllerParameterType.Bool);
        controller.AddParameter("isAttack", AnimatorControllerParameterType.Bool);
        controller.AddParameter("isGuard", AnimatorControllerParameterType.Bool);
        controller.AddParameter("isBattle", AnimatorControllerParameterType.Bool);

        controller.AddParameter("InputX", AnimatorControllerParameterType.Float);
        controller.AddParameter("InputY", AnimatorControllerParameterType.Float);
        controller.AddParameter("comboIndex", AnimatorControllerParameterType.Int);

        controller.AddParameter("tRoll", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("tHitFront", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("tHitSideBack", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("tGuardChipHit", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("tAttack", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("tDrink", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("tDrinkUpper", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("tDead", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("tEquip", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("tUnequip", AnimatorControllerParameterType.Trigger);
    }

    private static void AddUpperBodyEquipLayer(AnimatorController controller)
    {
        var upperBodyStateMachine = new AnimatorStateMachine
        {
            name = "UpperBody_Equip"
        };
        AssetDatabase.AddObjectToAsset(upperBodyStateMachine, controller);

        var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(WeaponAvatarMaskPath);
        if (mask == null)
        {
            Debug.LogWarning($"[Animator] Failed to load upper-body mask: {WeaponAvatarMaskPath}");
        }

        var layers = controller.layers;
        Array.Resize(ref layers, layers.Length + 1);
        layers[^1] = new AnimatorControllerLayer
        {
            name = "UpperBody_Equip",
            defaultWeight = 1f,
            avatarMask = mask,
            blendingMode = AnimatorLayerBlendingMode.Override,
            iKPass = false,
            syncedLayerIndex = -1,
            stateMachine = upperBodyStateMachine
        };
        controller.layers = layers;

        AnimatorState empty = upperBodyStateMachine.AddState("Empty", new Vector3(-300f, 0f, 0f));
        AnimatorState equipUpper = upperBodyStateMachine.AddState("Equip_Upper", new Vector3(-40f, -100f, 0f));
        AnimatorState unequipUpper = upperBodyStateMachine.AddState("Unequip_Upper", new Vector3(-40f, 100f, 0f));
        AnimatorState drinkUpper = upperBodyStateMachine.AddState("Drink_Upper", new Vector3(220f, 0f, 0f));

        empty.motion = null;
        equipUpper.motion = LoadClip(
            KatanaFolder + "/1_Movements/1__Idle/M_katana_Blade@Idle_A_to_B.FBX");
        unequipUpper.motion = LoadClip(
            KatanaFolder + "/1_Movements/1__Idle/M_katana_Blade@Idle_B_to_A.FBX");
        drinkUpper.motion = LoadClip(PotionDrinkPath);

        upperBodyStateMachine.defaultState = empty;

        AddAnyStateTriggerTransition(upperBodyStateMachine, equipUpper, "tEquip", false, 0.03f);
        AddAnyStateTriggerTransition(upperBodyStateMachine, unequipUpper, "tUnequip", false, 0.03f);
        AddAnyStateTriggerTransition(upperBodyStateMachine, drinkUpper, "tDrinkUpper", false, 0.03f);
        AddExitTransition(equipUpper, empty, 0.9f, 0.03f);
        AddExitTransition(unequipUpper, empty, 0.9f, 0.03f);
        AddExitTransition(drinkUpper, empty, 0.9f, 0.05f);

        SetCanTransitionToSelfFalse(upperBodyStateMachine);
    }

    private static void TryGetStateMotion(AnimatorController controller, string stateName, out Motion motion)
    {
        motion = null;
        if (controller == null)
        {
            return;
        }

        AnimatorStateMachine sm = controller.layers[0].stateMachine;
        foreach (ChildAnimatorState child in sm.states)
        {
            if (child.state != null && child.state.name == stateName)
            {
                motion = child.state.motion;
                return;
            }
        }
    }

    private static AnimatorStateTransition AddBoolTransition(
        AnimatorState from,
        AnimatorState to,
        string conditionParam,
        bool conditionValue,
        bool hasExitTime,
        float duration,
        string secondConditionParam = null,
        bool secondConditionValue = false,
        string thirdConditionParam = null,
        bool thirdConditionValue = false,
        string fourthConditionParam = null,
        bool fourthConditionValue = false,
        float exitTime = 0f)
    {
        AnimatorStateTransition tr = from.AddTransition(to);
        tr.hasExitTime = hasExitTime;
        tr.hasFixedDuration = true;
        tr.duration = duration;
        tr.exitTime = exitTime;
        tr.canTransitionToSelf = false;

        tr.AddCondition(conditionValue ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, conditionParam);

        if (!string.IsNullOrEmpty(secondConditionParam))
        {
            tr.AddCondition(secondConditionValue ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, secondConditionParam);
        }

        if (!string.IsNullOrEmpty(thirdConditionParam))
        {
            tr.AddCondition(thirdConditionValue ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, thirdConditionParam);
        }

        if (!string.IsNullOrEmpty(fourthConditionParam))
        {
            tr.AddCondition(fourthConditionValue ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, fourthConditionParam);
        }

        return tr;
    }

    private static void AddHitRecoveryTransitions(
        AnimatorState hitState,
        AnimatorState idleA,
        AnimatorState idleB,
        AnimatorState walkA,
        AnimatorState walkB,
        AnimatorState walkLockOn,
        AnimatorState runA,
        AnimatorState runB,
        AnimatorState runLockOn)
    {
        const float duration = 0.05f;
        const float exitTime = 0.9f;

        AddBoolTransition(hitState, walkA, "isMove", true, true, duration, "isRun", false, "isLockOn", false, "isBattle", false, exitTime);
        AddBoolTransition(hitState, walkB, "isMove", true, true, duration, "isRun", false, "isLockOn", false, "isBattle", true, exitTime);
        AddBoolTransition(hitState, walkLockOn, "isMove", true, true, duration, "isRun", false, "isLockOn", true, exitTime: exitTime);

        AddBoolTransition(hitState, runA, "isMove", true, true, duration, "isRun", true, "isLockOn", false, "isBattle", false, exitTime);
        AddBoolTransition(hitState, runB, "isMove", true, true, duration, "isRun", true, "isLockOn", false, "isBattle", true, exitTime);
        AddBoolTransition(hitState, runLockOn, "isMove", true, true, duration, "isRun", true, "isLockOn", true, exitTime: exitTime);

        AddBoolTransition(hitState, idleA, "isMove", false, true, duration, "isBattle", false, exitTime: exitTime);
        AddBoolTransition(hitState, idleB, "isMove", false, true, duration, "isBattle", true, exitTime: exitTime);
    }

    private static AnimatorStateTransition AddTriggerTransition(AnimatorState from, AnimatorState to, string triggerParam, bool hasExitTime, float duration)
    {
        AnimatorStateTransition tr = from.AddTransition(to);
        tr.hasExitTime = hasExitTime;
        tr.hasFixedDuration = true;
        tr.duration = duration;
        tr.canTransitionToSelf = false;
        tr.AddCondition(AnimatorConditionMode.If, 0f, triggerParam);
        return tr;
    }

    private static AnimatorStateTransition AddAnyStateTriggerTransition(AnimatorStateMachine sm, AnimatorState to, string triggerParam, bool hasExitTime, float duration)
    {
        AnimatorStateTransition tr = sm.AddAnyStateTransition(to);
        tr.hasExitTime = hasExitTime;
        tr.hasFixedDuration = true;
        tr.duration = duration;
        tr.canTransitionToSelf = false;
        tr.AddCondition(AnimatorConditionMode.If, 0f, triggerParam);
        return tr;
    }

    private static AnimatorStateTransition AddExitTransition(AnimatorState from, AnimatorState to, float exitTime, float duration)
    {
        AnimatorStateTransition tr = from.AddTransition(to);
        tr.hasExitTime = true;
        tr.hasFixedDuration = true;
        tr.exitTime = exitTime;
        tr.duration = duration;
        tr.canTransitionToSelf = false;
        return tr;
    }

    private static void SetCanTransitionToSelfFalse(AnimatorStateMachine sm)
    {
        foreach (ChildAnimatorState child in sm.states)
        {
            foreach (AnimatorStateTransition tr in child.state.transitions)
            {
                tr.canTransitionToSelf = false;
            }
        }

        foreach (AnimatorStateTransition tr in sm.anyStateTransitions)
        {
            tr.canTransitionToSelf = false;
        }
    }

    private static void EnsureFolder(string folderPath)
    {
        string[] parts = folderPath.Split('/');
        string current = parts[0];

        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }

            current = next;
        }
    }
}
