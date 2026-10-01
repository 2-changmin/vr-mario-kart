using UnityEngine;

namespace VRKart.Core
{
    // 실제 변속기가 없는 카트용 가짜 변속기: 속도 비율로 단수와 회전수(rpm)를 흉내 낸다.
    // 계기판(CockpitGauges)과 엔진음(EngineAudio)이 같은 값을 쓰도록 공용으로 둔다. 표시·소리 전용.
    public sealed class SimulatedGearbox
    {
        public const float IdleRpm = 900f;
        public const float ShiftRpm = 7200f;

        // 최고 속도 대비 각 단의 상한 비율 (마지막 = 1)
        private static readonly float[] GearTopRatios = { 0.18f, 0.34f, 0.52f, 0.70f, 0.86f, 1f };

        public int Gear { get; private set; }   // -1 = 후진, 0 = 중립, 1~6
        public float Rpm { get; private set; } = IdleRpm;

        // 0(공회전) ~ 1(변속 직전)
        public float Load01 => Mathf.InverseLerp(IdleRpm, ShiftRpm, Rpm);

        public void Update(float speed, float maxSpeed)
        {
            float ratio = Mathf.Abs(speed) / Mathf.Max(maxSpeed, 0.1f);
            if (speed < -0.3f)
            {
                Gear = -1;
                Rpm = Mathf.Lerp(IdleRpm, 3500f, Mathf.Abs(speed) / 6f);
                return;
            }

            float low = 0f;
            for (int i = 0; i < GearTopRatios.Length; i++)
            {
                float high = GearTopRatios[i];
                if (ratio <= high || i == GearTopRatios.Length - 1)
                {
                    // 변속 직후에는 회전수가 떨어진다(이전 단 상한의 75%부터 시작)
                    float inGear = Mathf.Clamp01(Mathf.InverseLerp(i == 0 ? 0f : low * 0.75f, high, ratio));
                    Gear = Mathf.Abs(speed) < 0.3f ? 0 : i + 1;
                    Rpm = Mathf.Lerp(IdleRpm, ShiftRpm, inGear);
                    return;
                }
                low = high;
            }
        }
    }
}
