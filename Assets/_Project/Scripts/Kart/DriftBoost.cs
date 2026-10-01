using System;
using UnityEngine;
using VRKart.Core;

namespace VRKart.Kart
{
    // 드리프트 → 미니 터보 (FR-KART-07, 08). 카트 루트(KartController와 같은 오브젝트)에 붙인다.
    // 드리프트 버튼 + 조향으로 시작 → 유지 시간에 따라 1단 → 2단 충전 → 버튼을 떼면 단계에 맞는 부스트.
    // 회전·미끄러짐 물리는 KartController가, 판정·충전·이벤트는 여기서 맡는다. 값은 KartStats의 "드리프트" 항목.
    [DefaultExecutionOrder(-10)]   // KartController.FixedUpdate보다 먼저 드리프트 상태를 정한다
    [RequireComponent(typeof(KartController))]
    public sealed class DriftBoost : MonoBehaviour
    {
        private KartController _kart;
        private IKartInput _input;
        private float _driftTime;
        private int _level;
        private bool _wasDrifting;

        public event Action<int> DriftStarted;          // 방향 (1 = 오른쪽, -1 = 왼쪽)
        public event Action DriftEnded;                // 부스트 여부와 상관없이 드리프트가 끝날 때
        public event Action<int> BoostLevelChanged;    // 0 → 1 → 2 (불꽃 색), 드리프트가 끝나면 0
        public event Action<int, float> BoostFired;    // 단계, 지속 시간 (미니 터보)

        public bool IsDrifting => _kart.IsDrifting;
        public int Level => _level;

        private void Awake()
        {
            _kart = GetComponent<KartController>();
            _input = GetComponent<IKartInput>();
        }

        private void OnDisable()
        {
            if (_kart != null && _wasDrifting) EndDrift(false);
        }

        private void FixedUpdate()
        {
            if (_input == null) return;
            var stats = _kart.Stats;

            // 조작 잠금·스핀아웃·리스폰으로 KartController가 먼저 끝냈으면 부스트 없이 정리
            if (_wasDrifting && !_kart.IsDrifting) EndDrift(false);

            if (!_kart.IsDrifting)
            {
                if (CanStart(stats)) BeginDrift(_input.Steer > 0f ? 1 : -1);
                return;
            }

            if (!_input.Drift)
            {
                EndDrift(true);
                return;
            }
            if (_kart.CurrentSpeed < stats.DriftMinSpeed * 0.5f)
            {
                EndDrift(false);
                return;
            }

            // 공중에서는 충전하지 않는다 (점프로 단계를 쌓지 않게)
            if (_kart.IsGrounded) _driftTime += Time.fixedDeltaTime;
            int level = _driftTime >= stats.DriftLevel2Time ? 2 : _driftTime >= stats.DriftLevel1Time ? 1 : 0;
            if (level != _level)
            {
                _level = level;
                BoostLevelChanged?.Invoke(_level);
            }
        }

        private bool CanStart(KartStats stats) =>
            _input.Drift
            && Mathf.Abs(_input.Steer) >= stats.DriftSteerThreshold
            && _kart.CurrentSpeed >= stats.DriftMinSpeed
            && _kart.IsGrounded
            && !_kart.IsSpinningOut;

        private void BeginDrift(int direction)
        {
            _driftTime = 0f;
            _level = 0;
            _kart.StartDrift(direction);
            _wasDrifting = true;
            DriftStarted?.Invoke(direction);
        }

        private void EndDrift(bool fireBoost)
        {
            int level = _level;
            _kart.StopDrift();
            _wasDrifting = false;
            _driftTime = 0f;
            if (_level != 0)
            {
                _level = 0;
                BoostLevelChanged?.Invoke(0);
            }
            DriftEnded?.Invoke();

            if (!fireBoost || level == 0) return;
            var stats = _kart.Stats;
            float power = level == 2 ? stats.DriftLevel2BoostPower : stats.DriftLevel1BoostPower;
            float duration = level == 2 ? stats.DriftLevel2BoostDuration : stats.DriftLevel1BoostDuration;
            _kart.ApplyBoost(power, duration);
            BoostFired?.Invoke(level, duration);
        }
    }
}
