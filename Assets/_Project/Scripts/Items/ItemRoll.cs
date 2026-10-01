using UnityEngine;
using VRKart.Core;

namespace VRKart.Items
{
    // 아이템 뽑기 (FR-ITEM-04). 순위가 낮을수록(뒤에 있을수록) 부스터가, 앞에 있을수록 바나나가 잘 나온다.
    // 순위를 모르면(레이스 밖, 참가자 1명) 중간 확률.
    public static class ItemRoll
    {
        //                                 1등    꼴찌
        private static readonly Vector2 Booster = new(0.15f, 0.60f);
        private static readonly Vector2 Shell   = new(0.40f, 0.30f);
        private static readonly Vector2 Banana  = new(0.45f, 0.10f);

        public static ItemType Roll(IRaceParticipant participant, int participantCount) =>
            Roll(BackRatio(participant, participantCount), Random.value);

        // backRatio: 0 = 1등 ~ 1 = 꼴찌, random: 0~1
        public static ItemType Roll(float backRatio, float random)
        {
            float booster = Mathf.Lerp(Booster.x, Booster.y, backRatio);
            float shell = Mathf.Lerp(Shell.x, Shell.y, backRatio);
            float banana = Mathf.Lerp(Banana.x, Banana.y, backRatio);

            float pick = random * (booster + shell + banana);
            if (pick < booster) return ItemType.Booster;
            return pick < booster + shell ? ItemType.Shell : ItemType.Banana;
        }

        private static float BackRatio(IRaceParticipant participant, int participantCount)
        {
            if (participant == null || participantCount <= 1) return 0.5f;
            return Mathf.InverseLerp(1f, participantCount, participant.Rank);
        }
    }
}
