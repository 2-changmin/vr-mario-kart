using System.Collections.Generic;
using UnityEngine;
using VRKart.Core;

namespace VRKart.Items
{
    // 대시 패드 (FR-KART-09). 밟은 카트에 IKart.ApplyBoost를 건다.
    // 트리거 콜라이더는 Ignore Raycast 레이어여야 한다 → 카트 지면 레이(Road|Grass)에 걸리지 않음.
    // 카트 콜라이더가 여러 개라 트리거가 여러 번 들어오므로 카트마다 재사용 대기 시간을 둔다.
    [RequireComponent(typeof(BoxCollider))]
    public sealed class DashPad : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float _power = 0.35f;     // 최고 속도 +35%
        [SerializeField, Min(0f)] private float _duration = 1.2f;   // 초
        [SerializeField, Min(0f)] private float _cooldown = 0.5f;   // 같은 카트를 다시 부스트하기까지

        private readonly Dictionary<IKart, float> _lastBoost = new();

        private void Awake() => GetComponent<BoxCollider>().isTrigger = true;

        private void OnTriggerEnter(Collider other)
        {
            var kart = other.GetComponentInParent<IKart>();
            if (kart == null) return;
            if (_lastBoost.TryGetValue(kart, out float last) && Time.time - last < _cooldown) return;

            _lastBoost[kart] = Time.time;
            kart.ApplyBoost(_power, _duration);
        }
    }
}
