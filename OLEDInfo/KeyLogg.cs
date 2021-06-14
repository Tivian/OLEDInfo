using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OLEDInfo {
    public class KeyLogg {
        public class KeyboardEventArgs : EventArgs {
            public Keys Key { get; set; }
            public Keys ModifierKeys { get; set; }
        }

        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_SYSKEYUP = 0x0105;

        private IntPtr hookID = IntPtr.Zero;
        private readonly CancellationTokenSource tokenSrc = new CancellationTokenSource();
        private LowLevelKeyboardProc callback;

        public event EventHandler<KeyboardEventArgs> OnKeyDown;
        public event EventHandler<KeyboardEventArgs> OnKeyUp;
        public event EventHandler<KeyboardEventArgs> OnSysKeyDown;
        public event EventHandler<KeyboardEventArgs> OnSysKeyUp;

        public static KeyLogg Instance { get; } = new KeyLogg();

        private KeyLogg() {
            Application.ThreadExit += (source, e) => {
                tokenSrc.Cancel();
            };

            Task.Run(() => {
                hookID = SetHook((nCode, wParam, lParam) => {
                    if (nCode >= 0) {
                        var eventType = (int)wParam;
                        var eventData = new KeyboardEventArgs() {
                            Key = ((KBDLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(KBDLLHOOKSTRUCT))).vkCode,
                            ModifierKeys = Control.ModifierKeys
                        };

                        switch (eventType) {
                            case WM_KEYDOWN:
                                OnKeyDown?.Invoke(this, eventData);
                                break;
                            case WM_KEYUP:
                                OnKeyUp?.Invoke(this, eventData);
                                break;
                            case WM_SYSKEYDOWN:
                                OnSysKeyDown?.Invoke(this, eventData);
                                break;
                            case WM_SYSKEYUP:
                                OnSysKeyUp?.Invoke(this, eventData);
                                break;
                        }
                    }

                    return CallNextHookEx(hookID, nCode, wParam, lParam);
                });

                Application.Run();
                Application.ApplicationExit += (src, e) => {
                    UnhookWindowsHookEx(hookID);
                };
            }, tokenSrc.Token);
        }

        private IntPtr SetHook(LowLevelKeyboardProc proc) {
            callback = new LowLevelKeyboardProc(proc);

            using (var curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
                return SetWindowsHookEx(WH_KEYBOARD_LL, callback, GetModuleHandle(curModule.ModuleName), 0);
        }

        [Flags]
        public enum KBDLLHOOKSTRUCTFlags {
            Extended = 1,
            InjectedLL = 2,
            Reserved2 = 4,
            Reserved3 = 8,
            Injected = 16,
            Context = 32,
            Reserved6 = 64,
            Transition = 128
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct KBDLLHOOKSTRUCT {
            public Keys vkCode;
            public uint scanCode;
            public KBDLLHOOKSTRUCTFlags flags;
            public uint time;
            public UIntPtr dwExtraInfo;
        }

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn,
            IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);
    }
}
