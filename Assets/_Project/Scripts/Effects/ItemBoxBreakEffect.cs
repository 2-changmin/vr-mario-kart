using UnityEngine;
using VRKart.Items;

namespace VRKart.Effects
{
    // 아이템 박스가 깨지는 효과 (#14). ItemBox.PickedUp(획득) 때 박스 자리에서 조각 파티클을 한 번 터뜨린다.
    // ItemBox 코드는 바꾸지 않고 이벤트만 듣는다. 파티클 프리팹은 끝나면 스스로 없어진다(Stop Action = Destroy).
    [RequireComponent(typeof(ItemBox))]
    public sealed class ItemBoxBreakEffect : MonoBehaviour
    {
        [SerializeField] private ParticleSystem _burstPrefab;
        [SerializeField] private Vector3 _offset = new Vector3(0f, 0.9f, 0f);   // 박스 모양(Visual) 높이

        private ItemBox _box;

        private void Awake() => _box = GetComponent<ItemBox>();
        private void OnEnable() => _box.PickedUp += OnPickedUp;
        private void OnDisable() => _box.PickedUp -= OnPickedUp;

        private void OnPickedUp(ItemHolder holder, VRKart.Core.ItemType item)
        {
            if (_burstPrefab == null) return;
            var burst = Instantiate(_burstPrefab, transform.TransformPoint(_offset), Quaternion.identity);
            burst.Play();
        }
    }
}
