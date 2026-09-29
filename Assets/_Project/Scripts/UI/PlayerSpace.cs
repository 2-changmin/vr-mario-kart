using UnityEngine;

namespace VRKart.UI
{
    // UI를 머리(메인 카메라)의 수평 정면 기준으로 배치하고 플레이어 루트(XR Origin, 나중엔 카트)의 자식으로 붙인다.
    // 카트와 함께 움직이고, 고개를 돌려도 따라오지 않는다(head-locked 아님).
    public static class PlayerSpace
    {
        public static bool PlaceInFront(Transform target, float distance, float heightOffset)
        {
            Camera head = Camera.main;
            if (head == null) return false;

            Vector3 forward = Vector3.ProjectOnPlane(head.transform.forward, Vector3.up);
            if (forward.sqrMagnitude < 1e-4f) forward = Vector3.ProjectOnPlane(head.transform.up, Vector3.up);
            forward.Normalize();

            target.SetParent(head.transform.root, true);
            target.SetPositionAndRotation(
                head.transform.position + forward * distance + Vector3.up * heightOffset,
                Quaternion.LookRotation(forward, Vector3.up));
            return true;
        }
    }
}
