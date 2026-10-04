using UnityEngine;

namespace VRKart.XR
{
    // 시야 수평 유지 (FR-XR-07). 카트 루트와 XR Origin 사이의 ViewPivot(좌석 눈 위치)에 붙인다.
    // 켜져 있으면 카트의 앞뒤·좌우 기울기를 상쇄하고 방향(요)만 따라간다 → 언덕·점프대에서도 지평선이 기울지 않는다.
    // 조종석(핸들·대시보드)은 카트와 함께 기울고, 머리 기준 수평만 고정된다.
    public sealed class HorizonLock : MonoBehaviour
    {
        // 카트 Rigidbody 보간이 끝난 뒤(LateUpdate) 맞춘다
        private void LateUpdate()
        {
            var parent = transform.parent;
            if (ComfortSettings.HorizonLock && parent != null)
            {
                var forward = Vector3.ProjectOnPlane(parent.forward, Vector3.up);
                if (forward.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
            }
            else if (transform.localRotation != Quaternion.identity)
            {
                transform.localRotation = Quaternion.identity;
            }
        }
    }
}
