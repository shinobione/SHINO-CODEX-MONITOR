using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ShinoCodexMonitor
{
    // Window/taskbar identity is separate from the dynamically generated quota tray icon.
    internal class PeriscopeWindow : Form
    {
        private IntPtr bigIcon, smallIcon;
        private int iconDpi;
        private readonly Icon embedded;
        internal PeriscopeWindow()
        {
            embedded=Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            Icon=embedded ?? SystemIcons.Application; ShowIcon=true;
        }
        protected override void OnHandleCreated(EventArgs e)
        { base.OnHandleCreated(e); ApplyWindowIcons(); }
        protected override void OnShown(EventArgs e)
        { base.OnShown(e); ApplyWindowIcons(); }
        protected override void WndProc(ref Message m)
        {
            int message=m.Msg; base.WndProc(ref m);
            if (message == 0x02E0 || message == 0x007E || message == 0x0018) ApplyWindowIcons(); // DPI, display, visibility
        }
        private void ApplyWindowIcons()
        {
            if (!IsHandleCreated || IsDisposed) return;
            int dpi=96;
            try { uint result=GetDpiForWindow(Handle); if (result != 0) dpi=(int)result; }
            catch (EntryPointNotFoundException) { }
            if (bigIcon == IntPtr.Zero || smallIcon == IntPtr.Zero || dpi != iconDpi) {
                IntPtr module=GetModuleHandle(null);
                // csc /win32icon emits its group under 32512; verify via actual resource load.
                IntPtr big=LoadImage(module,new IntPtr(32512),1,Math.Max(16,32*dpi/96),Math.Max(16,32*dpi/96),0);
                IntPtr small=LoadImage(module,new IntPtr(32512),1,Math.Max(16,16*dpi/96),Math.Max(16,16*dpi/96),0);
                if (big == IntPtr.Zero || small == IntPtr.Zero) {
                    if (big != IntPtr.Zero) DestroyIcon(big); if (small != IntPtr.Zero) DestroyIcon(small);
                    return; // Managed Form.Icon remains the embedded fallback, never the quota tray ring.
                }
                IntPtr previousBig=bigIcon, previousSmall=smallIcon;
                bigIcon=big; smallIcon=small; iconDpi=dpi;
                SendMessage(Handle,0x0080,new IntPtr(1),bigIcon);
                SendMessage(Handle,0x0080,IntPtr.Zero,smallIcon);
                if (previousBig != IntPtr.Zero) DestroyIcon(previousBig); if (previousSmall != IntPtr.Zero) DestroyIcon(previousSmall);
            } else {
                SendMessage(Handle,0x0080,new IntPtr(1),bigIcon);
                SendMessage(Handle,0x0080,IntPtr.Zero,smallIcon);
            }
        }
        protected override void OnHandleDestroyed(EventArgs e)
        {
            base.OnHandleDestroyed(e);
            if (bigIcon != IntPtr.Zero) { DestroyIcon(bigIcon); bigIcon=IntPtr.Zero; }
            if (smallIcon != IntPtr.Zero) { DestroyIcon(smallIcon); smallIcon=IntPtr.Zero; }
        }
        protected override void Dispose(bool disposing)
        { base.Dispose(disposing); if (disposing && embedded != null) embedded.Dispose(); }
        [DllImport("user32.dll")] private static extern uint GetDpiForWindow(IntPtr hwnd);
        [DllImport("kernel32.dll",CharSet=CharSet.Unicode)] private static extern IntPtr GetModuleHandle(string name);
        [DllImport("user32.dll",CharSet=CharSet.Unicode)] private static extern IntPtr LoadImage(IntPtr instance,IntPtr name,uint type,int cx,int cy,uint flags);
        [DllImport("user32.dll")] private static extern bool DestroyIcon(IntPtr icon);
        [DllImport("user32.dll")] internal static extern IntPtr SendMessage(IntPtr hwnd,int message,IntPtr wparam,IntPtr lparam);
    }
}
