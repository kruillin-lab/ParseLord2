using System.Runtime.InteropServices;

namespace ParseLord2.AutoRotation.Engine;

internal static class ManualInputDetector
{
    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    private static bool Down(int vk) => (GetAsyncKeyState(vk) & 0x8000) != 0;

    internal static bool IsManualInputActive()
    {
        // Mouse buttons
        if (Down(0x01) || Down(0x02) || Down(0x04) || Down(0x05) || Down(0x06)) return true;

        // Modifiers: Shift/Ctrl/Alt (Menu)
        if (Down(0x10) || Down(0x11) || Down(0x12)) return true;

        // Movement cluster (WASD + QE)
        if (Down(0x57) || Down(0x41) || Down(0x53) || Down(0x44) || Down(0x51) || Down(0x45)) return true;

        // Number row 0-9
        for (int vk = 0x30; vk <= 0x39; vk++)
            if (Down(vk)) return true;

        // Numpad 0-9
        for (int vk = 0x60; vk <= 0x69; vk++)
            if (Down(vk)) return true;

        return false;
    }
}
