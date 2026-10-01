using System.Collections.Generic;
using UnityEngine.XR;

namespace VRKart.Audio
{
    // 양손 컨트롤러 진동. 헤드셋/컨트롤러가 없으면(에디터, 시뮬레이터) 아무 일도 하지 않는다.
    public static class ControllerHaptics
    {
        private static readonly List<InputDevice> Devices = new List<InputDevice>();

        public static void Pulse(float amplitude, float duration)
        {
            InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.HeldInHand | InputDeviceCharacteristics.Controller, Devices);
            foreach (var device in Devices)
                if (device.TryGetHapticCapabilities(out var caps) && caps.supportsImpulse)
                    device.SendHapticImpulse(0u, UnityEngine.Mathf.Clamp01(amplitude), duration);
        }
    }
}
