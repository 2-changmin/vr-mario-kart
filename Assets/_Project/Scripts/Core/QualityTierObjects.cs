using UnityEngine;

namespace VRKart.Core
{
    // 품질 단계에 따라 오브젝트를 켜고 끈다 (#67). PC 시연 화질에서만 보이는 무거운 버전(_pcOnly)과
    // Quest(Mobile)에서 쓰는 가벼운 버전(_mobileOnly)을 같은 씬에 두고 시작할 때 한쪽만 남긴다.
    // 씬에는 Quest 상태(_pcOnly 꺼짐)로 저장한다.
    public sealed class QualityTierObjects : MonoBehaviour
    {
        [SerializeField] private GameObject[] _pcOnly;
        [SerializeField] private GameObject[] _mobileOnly;

        private void Awake() => Apply(PcShowcaseQuality.IsPcTier);

        public void Apply(bool pc)
        {
            if (_pcOnly != null) foreach (var go in _pcOnly) if (go != null) go.SetActive(pc);
            if (_mobileOnly != null) foreach (var go in _mobileOnly) if (go != null) go.SetActive(!pc);
        }
    }
}
