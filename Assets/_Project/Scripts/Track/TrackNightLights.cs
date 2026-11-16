using UnityEngine;
using VRKart.Race;

namespace VRKart.Track
{
    // 야간 트랙 연출 (광안대교·캠퍼스 야간). 씬에 하나 둔다.
    //  - 플레이어 카트에 전조등(스포트라이트 1개, 그림자 없음)을 실행 중에 붙인다 → 카트 프리팹은 그대로, 낮 트랙에는 영향 없음.
    //    AI 차는 헤드라이트·미등이 이미 Unlit이라 빛나 보이므로 실제 라이트는 달지 않는다(Quest 부담).
    //  - LED 렌더러들의 색을 천천히 바꾼다(광안대교 케이블 조명). 머티리얼 사본을 만들어 바꾸므로 에셋은 바뀌지 않는다.
    //  - 모든 카트 앞 노면에 전조등 불빛 판(가산 합성 쿼드 1장)을 붙인다 → 실시간 라이트 없이 앞길이 밝고, 밤에 다른 차 위치가 보인다.
    //    카메라는 건드리지 않는다(NFR-03).
    public sealed class TrackNightLights : MonoBehaviour
    {
        [Header("플레이어 전조등")]
        [SerializeField] private bool _playerHeadlights = true;
        [SerializeField] private Vector3 _headlightOffset = new Vector3(0f, 0.8f, 1.6f);   // 카트 기준
        [SerializeField, Range(0f, 30f)] private float _headlightPitch = 7f;
        [SerializeField, Min(1f)] private float _headlightRange = 55f;
        [SerializeField, Range(20f, 120f)] private float _headlightAngle = 75f;
        [SerializeField, Min(0f)] private float _headlightIntensity = 4f;
        [SerializeField] private Color _headlightColor = new Color(1f, 0.95f, 0.85f);

        [Header("카트 앞 노면 불빛 판 (모든 카트, 비우면 안 함)")]
        [SerializeField] private Material _beamMaterial;                                   // 가산 합성 (Night_HeadlightBeam)
        [SerializeField] private Vector2 _beamSize = new Vector2(6.5f, 13f);               // 폭, 길이 (m)
        [SerializeField, Min(0f)] private float _beamStart = 1.8f;                         // 카트 원점에서 앞쪽으로
        [SerializeField] private float _beamHeight = 0.07f;                                // 카트 원점(= 노면) 위

        [Header("LED 색 순환 (비우면 안 함)")]
        [SerializeField] private Renderer[] _ledRenderers;
        [SerializeField] private Gradient _ledColors;
        [SerializeField, Min(1f)] private float _ledCycleSeconds = 24f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private Material[] _ledMaterials;
        private Light _headlight;
        private Mesh _beamMesh;

        private void Start()
        {
            if (_playerHeadlights) AttachHeadlight();
            if (_beamMaterial != null) AttachBeams();
            if (_ledRenderers != null && _ledRenderers.Length > 0 && _ledColors != null)
            {
                _ledMaterials = new Material[_ledRenderers.Length];
                for (int i = 0; i < _ledRenderers.Length; i++)
                    if (_ledRenderers[i] != null) _ledMaterials[i] = _ledRenderers[i].material;   // 사본
            }
        }

        private void AttachHeadlight()
        {
            var race = FindAnyObjectByType<RaceManager>();
            Transform kart = race != null && race.Player != null ? race.Player.transform : null;
            if (kart == null) return;
            var go = new GameObject("Headlights");
            go.transform.SetParent(kart, false);
            go.transform.localPosition = _headlightOffset;
            go.transform.localRotation = Quaternion.Euler(_headlightPitch, 0f, 0f);
            _headlight = go.AddComponent<Light>();
            _headlight.type = LightType.Spot;
            _headlight.range = _headlightRange;
            _headlight.spotAngle = _headlightAngle;
            _headlight.innerSpotAngle = _headlightAngle * 0.5f;
            _headlight.intensity = _headlightIntensity;
            _headlight.color = _headlightColor;
            _headlight.shadows = LightShadows.None;
            _headlight.renderMode = LightRenderMode.ForcePixel;
        }

        private void AttachBeams()
        {
            _beamMesh = new Mesh { name = "HeadlightBeam" };
            float w = _beamSize.x * 0.5f, z0 = _beamStart, z1 = _beamStart + _beamSize.y;
            _beamMesh.vertices = new[] { new Vector3(-w, _beamHeight, z0), new Vector3(-w, _beamHeight, z1), new Vector3(w, _beamHeight, z1), new Vector3(w, _beamHeight, z0) };
            _beamMesh.uv = new[] { new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f) };
            _beamMesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            _beamMesh.RecalculateBounds();
            foreach (var progress in FindObjectsByType<RaceProgress>(FindObjectsSortMode.None))
            {
                var go = new GameObject("HeadlightBeam");
                go.transform.SetParent(progress.transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = _beamMesh;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = _beamMaterial;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            }
        }

        private void Update()
        {
            if (_ledMaterials == null) return;
            float t = Mathf.Repeat(Time.time / _ledCycleSeconds, 1f);
            Color c = _ledColors.Evaluate(t);
            foreach (var m in _ledMaterials) if (m != null) m.SetColor(BaseColorId, c);
        }

        private void OnDestroy()
        {
            if (_beamMesh != null) Destroy(_beamMesh);
            if (_ledMaterials == null) return;
            foreach (var m in _ledMaterials) if (m != null) Destroy(m);
        }
    }
}
