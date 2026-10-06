using UnityEngine;

namespace VRKart.Core
{
    // 씬에서 메인 카메라(플레이어 눈)의 먼 거리 잘림을 바꾼다 (#48: 캠퍼스 트랙의 먼 산 배경이 잘리지 않게).
    public sealed class CameraFarClip : MonoBehaviour
    {
        [SerializeField, Min(10f)] private float _farClip = 1600f;

        private void Start()
        {
            foreach (var cam in Camera.allCameras)
                if (cam.CompareTag("MainCamera")) cam.farClipPlane = Mathf.Max(cam.farClipPlane, _farClip);
        }
    }
}
