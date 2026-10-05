using UnityEngine;
using VRKart.Kart;

namespace VRKart.Effects
{
    // 카트 파티클(표시 전용, #14): 드리프트 중 뒷바퀴 불꽃(충전 단계마다 색이 바뀜), 부스트 중 배기구 불꽃.
    // 카트 루트의 DriftBoost·KartController 이벤트만 구독한다. 파티클은 카트의 DriftFX/RearLeft·RearRight, Effects/Exhaust_* 아래.
    public sealed class KartEffects : MonoBehaviour
    {
        [SerializeField] private ParticleSystem[] _driftSparks;
        [SerializeField] private ParticleSystem[] _boostFlames;
        [Tooltip("충전 단계 0 / 1 / 2의 불꽃 색")]
        [SerializeField] private Color[] _levelColors =
        {
            new Color(1f, 0.95f, 0.8f),
            new Color(1f, 0.55f, 0.1f),
            new Color(0.35f, 0.6f, 1f),
        };

        private KartController _kart;
        private DriftBoost _drift;
        private float _boostTimer;

        private void Awake()
        {
            _kart = GetComponentInParent<KartController>();
            _drift = GetComponentInParent<DriftBoost>();
            SetEmitting(_driftSparks, false);
            SetEmitting(_boostFlames, false);
        }

        private void OnEnable()
        {
            if (_kart != null) _kart.BoostStarted += HandleBoost;
            if (_drift != null) { _drift.DriftStarted += HandleDriftStarted; _drift.DriftEnded += HandleDriftEnded; _drift.BoostLevelChanged += HandleLevel; }
        }

        private void OnDisable()
        {
            if (_kart != null) _kart.BoostStarted -= HandleBoost;
            if (_drift != null) { _drift.DriftStarted -= HandleDriftStarted; _drift.DriftEnded -= HandleDriftEnded; _drift.BoostLevelChanged -= HandleLevel; }
        }

        private void Update()
        {
            if (_boostTimer <= 0f) return;
            _boostTimer -= Time.deltaTime;
            if (_boostTimer <= 0f) SetEmitting(_boostFlames, false);
        }

        private void HandleDriftStarted(int direction)
        {
            SetColor(_driftSparks, _levelColors[0]);
            SetEmitting(_driftSparks, true);
        }

        private void HandleDriftEnded() => SetEmitting(_driftSparks, false);

        private void HandleLevel(int level) => SetColor(_driftSparks, _levelColors[Mathf.Clamp(level, 0, _levelColors.Length - 1)]);

        private void HandleBoost(float power, float duration)
        {
            _boostTimer = Mathf.Max(_boostTimer, duration);
            SetEmitting(_boostFlames, true);
        }

        private static void SetEmitting(ParticleSystem[] systems, bool on)
        {
            foreach (var ps in systems)
            {
                if (ps == null) continue;
                var emission = ps.emission;
                emission.enabled = on;
                if (on && !ps.isPlaying) ps.Play();
            }
        }

        private static void SetColor(ParticleSystem[] systems, Color color)
        {
            foreach (var ps in systems)
            {
                if (ps == null) continue;
                var main = ps.main;
                main.startColor = color;
            }
        }
    }
}
