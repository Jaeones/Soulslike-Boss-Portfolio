using System;
using Cinemachine;
using UnityEngine;

public sealed class PlayerCameraReferenceResolver
{
    public void Resolve(PlayerCam owner)
    {
        if (owner == null)
        {
            return;
        }

        if (owner.lockOnCamera == null)
        {
            owner.lockOnCamera = ResolveByName<CinemachineVirtualCamera>("CM_LockOn");
        }

        if (owner.freeLookCamera == null)
        {
            owner.freeLookCamera = ResolveFreeLookCamera();
        }

        if (owner.StateDrivenCamera == null)
        {
            owner.StateDrivenCamera = UnityEngine.Object.FindObjectOfType<CinemachineStateDrivenCamera>(true);
        }

        if (owner.CameraAnimator == null && owner.StateDrivenCamera != null)
        {
            owner.CameraAnimator = owner.StateDrivenCamera.GetComponent<Animator>();
        }

        if (owner.CameraTargetRoot == null && owner.player != null)
        {
            owner.CameraTargetRoot = ResolveChildByName(owner.player.transform, "CameraRoot");
        }
    }

    public void ReportUnresolvedReferences(PlayerCam owner)
    {
        if (owner == null)
        {
            return;
        }

        if (owner.lockOnCamera == null)
        {
            Debug.LogWarning("PlayerCam.lockOnCamera를 찾지 못했습니다. developScenes 씬 오버라이드 또는 CameraManager 참조를 확인하세요.");
        }

        if (owner.freeLookCamera == null)
        {
            Debug.LogWarning("PlayerCam.freeLookCamera를 찾지 못했습니다. developScenes 씬 오버라이드 또는 CM_FreeLook 참조를 확인하세요.");
        }

        if (owner.StateDrivenCamera == null)
        {
            Debug.LogWarning("PlayerCam.stateDrivenCamera를 찾지 못했습니다. developScenes 씬 오버라이드 또는 State-Driven Camera 참조를 확인하세요.");
        }

        if (owner.CameraAnimator == null)
        {
            Debug.LogWarning("PlayerCam.cameraAnimator를 찾지 못했습니다. State-Driven Camera Animator 연결을 확인하세요.");
        }
    }

    private CinemachineVirtualCameraBase ResolveFreeLookCamera()
    {
        CinemachineFreeLook freeLook = ResolveByName<CinemachineFreeLook>("CM_FreeLook");
        if (freeLook != null)
        {
            return freeLook;
        }

        return ResolveByPredicate<CinemachineVirtualCameraBase>(
            camera => camera is CinemachineFreeLook || camera.name.IndexOf("FreeLook", StringComparison.OrdinalIgnoreCase) >= 0);
    }

    private T ResolveByName<T>(string targetName) where T : UnityEngine.Object
    {
        return ResolveByPredicate<T>(candidate =>
            string.Equals(candidate.name, targetName, StringComparison.OrdinalIgnoreCase));
    }

    private T ResolveByPredicate<T>(Func<T, bool> predicate) where T : UnityEngine.Object
    {
        T[] candidates = UnityEngine.Object.FindObjectsOfType<T>(true);
        for (int i = 0; i < candidates.Length; i++)
        {
            if (predicate(candidates[i]))
            {
                return candidates[i];
            }
        }

        return null;
    }

    private Transform ResolveChildByName(Transform root, string targetName)
    {
        if (root == null)
        {
            return null;
        }

        if (string.Equals(root.name, targetName, StringComparison.OrdinalIgnoreCase))
        {
            return root;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform resolved = ResolveChildByName(root.GetChild(i), targetName);
            if (resolved != null)
            {
                return resolved;
            }
        }

        return null;
    }
}
