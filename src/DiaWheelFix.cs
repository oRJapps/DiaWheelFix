// DiaWheelFix: Dia (Windows) drops mouse-wheel input in its WinUI layer. This watches the wheel and,
// when the cursor is over a Dia page, forwards it straight to the page's Chromium window.
//   DiaWheelFix.exe              ask to install / uninstall
//   DiaWheelFix.exe --install    register at sign-in and start (silent)
//   DiaWheelFix.exe --uninstall  stop and unregister (silent)
//   DiaWheelFix.exe --run        run the wheel forwarder (used by the startup shortcut)
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

[assembly: AssemblyTitle("DiaWheelFix")]
[assembly: AssemblyDescription("Makes the mouse wheel scroll pages in the Dia browser on Windows")]
[assembly: AssemblyProduct("DiaWheelFix")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

static class DiaWheelFix {
  const string AppName = "DiaWheelFix";

  [STAThread]
  static int Main(string[] args) {
    string mode = args.Length > 0 ? args[0].ToLowerInvariant() : "";
    if (mode == "--run") { RunForwarder(); return 0; }
    if (mode == "--install") { Install(); return 0; }
    if (mode == "--uninstall") { Uninstall(); return 0; }

    // Double-clicked: ask the user
    if (File.Exists(ShortcutPath)) {
      var r = MessageBox.Show("DiaWheelFix はインストール済みです。\n\nアンインストールしますか？\nツールを止めて、自動起動も解除します。",
                              AppName, MessageBoxButtons.YesNo, MessageBoxIcon.Question);
      if (r == DialogResult.Yes) { Uninstall(); MessageBox.Show("アンインストールしました。", AppName); }
      else StartForwarder();
    } else {
      var r = MessageBox.Show("Dia のページでマウスホイールが効くようにします。\n\nインストールしますか？\nすぐに起動し、次からは Windows へのサインイン時に自動で起動します。",
                              AppName, MessageBoxButtons.YesNo, MessageBoxIcon.Question);
      if (r == DialogResult.Yes) { Install(); MessageBox.Show("インストールしました。Dia のページでホイールを回してみてください。", AppName); }
    }
    return 0;
  }

  // Install / uninstall
  static string ExePath { get { return Assembly.GetExecutingAssembly().Location; } }
  static string ShortcutPath {
    get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), AppName + ".lnk"); }
  }

  static void Install() {
    StopOthers();
    Type t = Type.GetTypeFromProgID("WScript.Shell");
    object shell = Activator.CreateInstance(t);
    object lnk = t.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { ShortcutPath });
    Type lt = lnk.GetType();
    lt.InvokeMember("TargetPath", BindingFlags.SetProperty, null, lnk, new object[] { ExePath });
    lt.InvokeMember("Arguments", BindingFlags.SetProperty, null, lnk, new object[] { "--run" });
    lt.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, lnk, new object[] { Path.GetDirectoryName(ExePath) });
    lt.InvokeMember("Save", BindingFlags.InvokeMethod, null, lnk, null);
    Marshal.ReleaseComObject(lnk); Marshal.ReleaseComObject(shell);
    StartForwarder();
  }

  static void Uninstall() {
    StopOthers();
    if (File.Exists(ShortcutPath)) File.Delete(ShortcutPath);
  }

  static void StartForwarder() {
    Process.Start(new ProcessStartInfo(ExePath, "--run") { UseShellExecute = false });
  }

  static void StopOthers() {
    int self = Process.GetCurrentProcess().Id;
    foreach (var p in Process.GetProcessesByName(AppName)) {
      if (p.Id == self) continue;
      try { p.Kill(); p.WaitForExit(2000); } catch { }
    }
  }

  // Wheel forwarder
  [StructLayout(LayoutKind.Sequential)] struct POINT { public int X, Y; }
  [StructLayout(LayoutKind.Sequential)] struct RECT { public int L, T, R, B; }
  [StructLayout(LayoutKind.Sequential)] struct HOOK { public POINT pt; public uint mouseData, flags, time; public IntPtr extra; }
  [StructLayout(LayoutKind.Sequential)] struct MSG { public IntPtr h; public uint m; public IntPtr w, l; public uint t; public POINT p; }
  delegate IntPtr HookProc(int n, IntPtr w, IntPtr l);
  delegate bool EnumProc(IntPtr h, IntPtr l);
  [DllImport("user32.dll")] static extern IntPtr SetWindowsHookEx(int id, HookProc f, IntPtr mod, uint tid);
  [DllImport("user32.dll")] static extern IntPtr CallNextHookEx(IntPtr h, int n, IntPtr w, IntPtr l);
  [DllImport("user32.dll")] static extern int GetMessage(out MSG m, IntPtr h, uint a, uint b);
  [DllImport("kernel32.dll")] static extern IntPtr GetModuleHandle(string n);
  [DllImport("kernel32.dll")] static extern IntPtr OpenProcess(uint access, bool inherit, uint pid);
  [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr h);
  [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern bool QueryFullProcessImageName(IntPtr h, uint flags, StringBuilder name, ref uint size);
  [DllImport("user32.dll")] static extern IntPtr WindowFromPoint(POINT p);
  [DllImport("user32.dll")] static extern IntPtr GetAncestor(IntPtr h, uint f);
  [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint p);
  [DllImport("user32.dll")] static extern bool EnumChildWindows(IntPtr p, EnumProc f, IntPtr l);
  [DllImport("user32.dll")] static extern int GetClassName(IntPtr h, StringBuilder s, int n);
  [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] static extern bool PostMessage(IntPtr h, uint m, IntPtr w, IntPtr l);
  [DllImport("user32.dll")] static extern short GetKeyState(int k);

  const int WM_MOUSEWHEEL = 0x020A, WM_MOUSEHWHEEL = 0x020E;
  static HookProc proc = OnHook;   // kept referenced so the GC does not collect it

  static void RunForwarder() {
    bool created;
    using (var mutex = new Mutex(true, @"Local\DiaWheelFix", out created)) {
      if (!created) return;   // already running for this user
      SetWindowsHookEx(14, proc, GetModuleHandle(null), 0);   // WH_MOUSE_LL
      MSG m; while (GetMessage(out m, IntPtr.Zero, 0, 0) > 0) { }
    }
  }

  // Checked on every wheel event (no PID cache), so a reused PID can never be mistaken for Dia
  static bool IsDia(uint pid) {
    IntPtr h = OpenProcess(0x1000, false, pid);   // PROCESS_QUERY_LIMITED_INFORMATION
    if (h == IntPtr.Zero) return false;
    try {
      var sb = new StringBuilder(1024); uint size = (uint)sb.Capacity;
      if (!QueryFullProcessImageName(h, 0, sb, ref size)) return false;
      return sb.ToString().EndsWith("\\Dia.exe", StringComparison.OrdinalIgnoreCase);
    } finally { CloseHandle(h); }
  }

  // Dia page window (Chromium) under the cursor, or zero
  static IntPtr FindPage(POINT pt) {
    IntPtr h = WindowFromPoint(pt);
    if (h == IntPtr.Zero) return IntPtr.Zero;
    IntPtr root = GetAncestor(h, 2);   // GA_ROOT
    uint pid; GetWindowThreadProcessId(root, out pid);
    if (!IsDia(pid)) return IntPtr.Zero;
    IntPtr found = IntPtr.Zero;
    EnumChildWindows(root, (c, l) => {
      var s = new StringBuilder(64); GetClassName(c, s, 64);
      RECT r;
      if (s.ToString() == "Chrome_RenderWidgetHostHWND" && IsWindowVisible(c) && GetWindowRect(c, out r)
          && pt.X >= r.L && pt.X < r.R && pt.Y >= r.T && pt.Y < r.B) { found = c; return false; }
      return true;
    }, IntPtr.Zero);
    return found;
  }

  static IntPtr OnHook(int n, IntPtr w, IntPtr l) {
    int msg = w.ToInt32();
    if (n >= 0 && (msg == WM_MOUSEWHEEL || msg == WM_MOUSEHWHEEL)) {
      HOOK s = (HOOK)Marshal.PtrToStructure(l, typeof(HOOK));
      IntPtr page = FindPage(s.pt);
      if (page != IntPtr.Zero) {
        uint keys = 0;
        if (GetKeyState(0x11) < 0) keys |= 0x8;   // Ctrl  -> zoom
        if (GetKeyState(0x10) < 0) keys |= 0x4;   // Shift -> horizontal
        IntPtr wp = new IntPtr(unchecked((int)((s.mouseData & 0xFFFF0000) | keys)));
        IntPtr lp = new IntPtr(((s.pt.Y & 0xFFFF) << 16) | (s.pt.X & 0xFFFF));
        PostMessage(page, (uint)msg, wp, lp);
        return new IntPtr(1);   // swallow the original, Dia would drop it anyway
      }
    }
    return CallNextHookEx(IntPtr.Zero, n, w, l);
  }
}
