using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace KeyboardHookLab.Interop;

public sealed class KeyboardHook : IDisposable
{
    private const int WH_KEYBOARD_LL = 13;

    private const int WM_KEYDOWN = 0x0100;
    private const int WM_SYSKEYDOWN = 0x0104;

    private delegate IntPtr LowLevelKeyboardProc(
        int nCode,
        IntPtr wParam,
        IntPtr lParam);

    // Делегат держим в поле, чтобы GC его не удалил,
    // пока Windows ещё на него рассчитывает.
    private readonly LowLevelKeyboardProc _callback;

    private IntPtr _hookHandle = IntPtr.Zero;

    public event Action<Keys>? KeyPressed;

    public bool IsInstalled => _hookHandle != IntPtr.Zero;

    public KeyboardHook()
    {
        _callback = HookCallback;
    }

    public void Start()
    {
        if (IsInstalled)
            return;

        _hookHandle = SetWindowsHookEx(
            WH_KEYBOARD_LL,
            _callback,
            IntPtr.Zero,
            0);

        if (_hookHandle == IntPtr.Zero)
        {
            int errorCode = Marshal.GetLastWin32Error();
            throw new Win32Exception(
                errorCode,
                "Не удалось установить клавиатурный hook.");
        }
    }

    public void Stop()
    {
        if (!IsInstalled)
            return;

        bool success = UnhookWindowsHookEx(_hookHandle);

        if (!success)
        {
            int errorCode = Marshal.GetLastWin32Error();
            throw new Win32Exception(
                errorCode,
                "Не удалось снять клавиатурный hook.");
        }

        _hookHandle = IntPtr.Zero;
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 &&
            (wParam == (IntPtr)WM_KEYDOWN ||
             wParam == (IntPtr)WM_SYSKEYDOWN))
        {
            try
            {
                var keyboardInfo =
                    Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);

                IntPtr foregroundWindow = GetForegroundWindow();

                if (foregroundWindow != IntPtr.Zero)
                {
                    GetWindowThreadProcessId(
                        foregroundWindow,
                        out uint processId);

                    // Пишем в журнал только события нашего процесса.
                    if (processId == (uint)Environment.ProcessId)
                    {
                        Keys key = (Keys)keyboardInfo.vkCode;
                        KeyPressed?.Invoke(key);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(
                    $"Ошибка callback клавиатуры: {ex}");
            }
        }

        // Событие не блокируем — передаём дальше.
        return CallNextHookEx(_hookHandle, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        Stop();
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KBDLLHOOKSTRUCT
    {
        public uint vkCode;
        public uint scanCode;
        public uint flags;
        public uint time;
        public UIntPtr dwExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true,
        CallingConvention = CallingConvention.Winapi)]
    private static extern IntPtr SetWindowsHookEx(
        int idHook,
        LowLevelKeyboardProc callback,
        IntPtr hMod,
        uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true,
        CallingConvention = CallingConvention.Winapi)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll",
        CallingConvention = CallingConvention.Winapi)]
    private static extern IntPtr CallNextHookEx(
        IntPtr hhk,
        int nCode,
        IntPtr wParam,
        IntPtr lParam);

    [DllImport("user32.dll",
        CallingConvention = CallingConvention.Winapi)]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll",
        CallingConvention = CallingConvention.Winapi)]
    private static extern uint GetWindowThreadProcessId(
        IntPtr hWnd,
        out uint processId);
}