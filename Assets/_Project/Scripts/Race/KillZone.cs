using UnityEngine;
using VRKart.Core;

namespace VRKart.Race
{
    // 들어온 카트를 마지막으로 통과한 체크포인트로 리스폰한다.
    [RequireComponent(typeof(BoxCollider))]
    public sealed class KillZone : MonoBehaviour
    {
        private void Reset()
        {
            GetComponent<BoxCollider>().isTrigger = true;
            gameObject.layer = 2; // Ignore Raycast
        }

        private void OnTriggerEnter(Collider other)
        {
            RaceProgress progress = other.GetComponentInParent<RaceProgress>();
            if (progress == null) return;

            IKart kart = progress.GetComponent<IKart>();
            if (kart == null)
            {
                Debug.LogWarning($"[KillZone] {progress.name}에 IKart 구현이 없어 리스폰할 수 없습니다.", progress);
                return;
            }
            kart.Respawn(progress.LastCheckpointPose);
        }
    }
}
