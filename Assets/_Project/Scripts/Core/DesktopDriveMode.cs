namespace VRKart.Core
{
    // PC(헤드셋 없음) 테스트용 키보드 운전 모드가 켜져 있는지. 켜고 끄는 건 Race/Testing/DesktopDriveController.
    // 실기기(Quest) 빌드에서는 항상 false.
    public static class DesktopDriveMode
    {
        public static bool Active { get; set; }
    }
}
