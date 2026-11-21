using UnityEngine;

namespace VRKart.Core
{
    // PC 시연 화질 전용 조명 (#67). 품질 단계가 PC일 때만 시작 시 해·환경광·안개를 "늦은 오후"로 바꾼다.
    //  - 해를 낮춰(고도 약 24°) 건물·창틀·차의 그림자를 길게 → 입체감. 빛은 따뜻한 색, 하늘 환경광은 살짝 푸르게(대비).
    //  - 옅은 대기 안개로 먼 건물이 흐려져 거리감.
    // Quest(Mobile)에서는 아무것도 하지 않는다. 씬에 저장된 값(한낮)은 그대로이고, 구운 라이트 프로브·반사 프로브도 그대로 쓴다.
    // 해가 Mixed(간접광만 구움)라 실행 중 방향을 바꿔도 직사광·그림자는 실시간으로 따라온다.
    public sealed class PcLightingOverride : MonoBehaviour
    {
        [Header("해")]
        [SerializeField] private Vector3 _sunEuler = new Vector3(24f, 230f, 0f);   // 서남쪽 낮은 해: 건물 정면에 빛, 도로에 긴 그림자
        [SerializeField] private Color _sunColor = new Color(1f, 0.86f, 0.70f);
        [SerializeField, Min(0f)] private float _sunIntensity = 1.6f;

        [Header("환경광 (Trilight)")]
        [SerializeField] private Color _ambientSky = new Color(0.52f, 0.62f, 0.80f);
        [SerializeField] private Color _ambientEquator = new Color(0.50f, 0.50f, 0.50f);
        [SerializeField] private Color _ambientGround = new Color(0.26f, 0.24f, 0.21f);

        [Header("대기 안개 (Linear)")]
        [SerializeField] private Color _fogColor = new Color(0.78f, 0.78f, 0.80f);
        [SerializeField, Min(0f)] private float _fogStart = 70f;
        [SerializeField, Min(1f)] private float _fogEnd = 780f;

        private void Awake()
        {
            if (PcShowcaseQuality.IsPcTier) Apply();
        }

        public void Apply()
        {
            var sun = RenderSettings.sun;
            if (sun != null)
            {
                sun.transform.rotation = Quaternion.Euler(_sunEuler);
                sun.color = _sunColor;
                sun.intensity = _sunIntensity;
                sun.shadows = LightShadows.Soft;
                sun.shadowStrength = 1f;
            }
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = _ambientSky;
            RenderSettings.ambientEquatorColor = _ambientEquator;
            RenderSettings.ambientGroundColor = _ambientGround;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = _fogColor;
            RenderSettings.fogStartDistance = _fogStart;
            RenderSettings.fogEndDistance = _fogEnd;
        }
    }
}
