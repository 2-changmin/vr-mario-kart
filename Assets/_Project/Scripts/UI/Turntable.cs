using UnityEngine;

namespace VRKart.UI
{
    // 로비 턴테이블 (#66): 붙은 물체(턴테이블 판 + 전시 차)만 Y축으로 천천히 돌린다. 플레이어 시점은 그대로(NFR-03).
    public sealed class Turntable : MonoBehaviour
    {
        [SerializeField] private float _degreesPerSecond = 12f;

        private void Update() => transform.Rotate(0f, _degreesPerSecond * Time.unscaledDeltaTime, 0f, Space.Self);
    }
}
