using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace jumpcutter_dot_net_wpf2
{
    static class NativeMethods
    {

        // [DllImport("kernel32.dll")]
        // public static extern IntPtr GetConsoleWindow();
        //
        // [DllImport("user32.dll")]
        // public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        //
        // public const int SW_HIDE = 0;
        // public const int SW_SHOW = 5;
        [DllImport("Kernel32")]
        public static extern void AllocConsole();

        [DllImport("Kernel32")]
        public static extern void FreeConsole();

        public const UInt32 StdOutputHandle = 0xFFFFFFF5;
        [DllImport("kernel32.dll")]
        public static extern IntPtr GetStdHandle(UInt32 nStdHandle);
        [DllImport("kernel32.dll")]
        public static extern void SetStdHandle(UInt32 nStdHandle, IntPtr handle);

    }

}
