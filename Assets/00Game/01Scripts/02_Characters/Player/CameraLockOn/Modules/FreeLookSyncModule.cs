using Cinemachine;
using UnityEngine;

public sealed class FreeLookSyncModule
{
    private readonly CinemachineVirtualCameraBase _freeLookCamera;
    private readonly float _freeLookPitchMin;
    private readonly float _freeLookPitchMax;

    public FreeLookSyncModule(CinemachineVirtualCameraBase freeLookCamera, float freeLookPitchMin, float freeLookPitchMax)
    {
        _freeLookCamera = freeLookCamera;
        _freeLookPitchMin = freeLookPitchMin;
        _freeLookPitchMax = freeLookPitchMax;
    }

    // 실제 화면 카메라 회전을 FreeLook 축에 반영해 락온 해제 직후 시점 튐을 줄인다.
    public void SyncFromMainCamera(bool syncEnabled)
    {
        if (!syncEnabled)
        {
            return;
        }

        CinemachineFreeLook freeLook = _freeLookCamera as CinemachineFreeLook;
        if (freeLook == null || Camera.main == null)
        {
            return;
        }

        Vector3 euler = Camera.main.transform.eulerAngles;
        freeLook.m_XAxis.Value = euler.y;

        float pitch = euler.x;
        if (pitch > 180f)
        {
            pitch -= 360f;
        }

        float y01 = Mathf.InverseLerp(_freeLookPitchMin, _freeLookPitchMax, pitch);
        freeLook.m_YAxis.Value = Mathf.Clamp01(y01);
    }
}
