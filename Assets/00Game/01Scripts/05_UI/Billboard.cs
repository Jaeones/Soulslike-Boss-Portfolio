using UnityEngine;

public class Billboard : MonoBehaviour
{
    [Header("Billboard 설정")]
    [SerializeField] private Camera targetCamera;
    [SerializeField] private bool useWorldUp = true;

    private Transform _targetCameraTransform;

    private void Awake()
    {
        ResolveCamera();
    }

    private void LateUpdate()
    {
        if (_targetCameraTransform == null)
        {
            ResolveCamera();
            if (_targetCameraTransform == null)
            {
                return;
            }
        }

        // 월드 스페이스 UI가 항상 카메라를 바라보게 처리
        if (useWorldUp)
        {
            transform.LookAt(transform.position + _targetCameraTransform.forward, Vector3.up);
        }
        else
        {
            transform.LookAt(_targetCameraTransform.position);
        }
    }

    private void ResolveCamera()
    {
        if (targetCamera != null)
        {
            _targetCameraTransform = targetCamera.transform;
            return;
        }

        if (Define.MainCam != null)
        {
            _targetCameraTransform = Define.MainCam.transform;
            return;
        }

        if (Camera.main != null)
        {
            _targetCameraTransform = Camera.main.transform;
        }
    }
}
