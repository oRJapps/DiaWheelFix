// DiaWheelFix: Dia (Windows) drops mouse-wheel input in its WinUI layer. This watches the wheel and,
// when the cursor is over a Dia page, forwards it straight to the page's Chromium window.
//   DiaWheelFix.exe              ask to install / uninstall
//   DiaWheelFix.exe --install    register at sign-in and start (silent)
//   DiaWheelFix.exe --uninstall  stop and unregister (silent)
//   DiaWheelFix.exe --run        run the wheel forwarder (used by the startup shortcut)
//   DiaWheelFix.exe --debug      like --run, and logs wheel events to %TEMP%\DiaWheelFix.log
//   DiaWheelFix.exe --check-update  write the update check result to %TEMP%\DiaWheelFix-update.log
//   DiaWheelFix.exe --update-now    self-update without asking (no restart), result in %TEMP%\DiaWheelFix-update.log
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
[assembly: AssemblyVersion("1.0.1.0")]
[assembly: AssemblyFileVersion("1.0.1.0")]

static class DiaWheelFix {
  const string AppName = "DiaWheelFix";

  [STAThread]
  static int Main(string[] args) {
    string mode = args.Length > 0 ? args[0].ToLowerInvariant() : "";
    if (mode == "--run") { RunForwarder(); return 0; }
    if (mode == "--debug") {   // like --run, but logs each wheel event to %TEMP%\DiaWheelFix.log
      log = new StreamWriter(Path.Combine(Path.GetTempPath(), "DiaWheelFix.log"), false) { AutoFlush = true };
      RunForwarder(); return 0;
    }
    if (mode == "--check-update") {   // writes the update check result to %TEMP%\DiaWheelFix-update.log
      string latest = LatestVersion();
      File.WriteAllText(Path.Combine(Path.GetTempPath(), "DiaWheelFix-update.log"),
        "mine=" + Assembly.GetExecutingAssembly().GetName().Version + " latest=" + latest + " newer=" + (latest != null && IsNewer(latest)));
      return 0;
    }
    if (mode == "--update-now") {   // self-update without asking (no restart); result in %TEMP%\DiaWheelFix-update.log
      string url;
      string latest = LatestRelease(out url);
      string result = latest == null ? "no release" : !IsNewer(latest) ? "up to date" : (SelfUpdate(url, latest) ?? "updated");
      File.WriteAllText(Path.Combine(Path.GetTempPath(), "DiaWheelFix-update.log"), "latest=" + latest + " url=" + url + " result=" + result);
      return 0;
    }
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
  [DllImport("user32.dll")] static extern bool UnhookWindowsHookEx(IntPtr h);
  [DllImport("user32.dll")] static extern IntPtr SetTimer(IntPtr h, IntPtr id, uint ms, IntPtr fn);
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
  static StreamWriter log;          // set only in --debug mode
  static int logCount;

  static void Log(string s) {
    if (log == null || logCount++ > 200) return;
    try { log.WriteLine(DateTime.Now.ToString("HH:mm:ss.fff") + " " + s); } catch { }
  }

  static string Cls(IntPtr h) { var s = new StringBuilder(64); GetClassName(h, s, 64); return s.ToString(); }

  static Mutex runMutex;   // held while the forwarder runs; closed by the updater before restarting

  static void RunForwarder() {
    bool created;
    runMutex = new Mutex(true, @"Local\DiaWheelFix", out created);
    if (!created) return;   // already running for this user
    DeleteQuietly(OldExePath);   // left over from a previous self-update
    new Thread(UpdateLoop) { IsBackground = true }.Start();
    IntPtr hook = SetWindowsHookEx(14, proc, GetModuleHandle(null), 0);   // WH_MOUSE_LL
    // Windows silently drops a low-level hook that once answers too slowly (e.g. after sleep),
    // so re-install it every 30 seconds.
    SetTimer(IntPtr.Zero, IntPtr.Zero, 30000, IntPtr.Zero);
    MSG m;
    while (GetMessage(out m, IntPtr.Zero, 0, 0) > 0) {
      if (m.m == 0x0113) {   // WM_TIMER
        IntPtr fresh = SetWindowsHookEx(14, proc, GetModuleHandle(null), 0);
        if (fresh != IntPtr.Zero) { UnhookWindowsHookEx(hook); hook = fresh; }
        Log("rehook " + (fresh != IntPtr.Zero));
      }
    }
  }

  // Update check: asks GitHub for the latest release once a day (only the version number is read)
  const string ReleasesApi = "https://api.github.com/repos/oRJapps/DiaWheelFix/releases/latest";
  const string ReleasesPage = "https://github.com/oRJapps/DiaWheelFix/releases/latest";

  static string NotifiedFile {
    get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppName, "notified.txt"); }
  }

  // Self-install on "Yes" is switched off for now: the prompt only opens the download page
  const bool AutoInstall = false;

  // Checks right after startup (retrying every minute, up to 10 times, while offline), then once a day.
  // Shows nothing when this is already the latest version.
  static void UpdateLoop() {
    Thread.Sleep(5000);
    int offlineRetries = 10;
    while (true) {
      string url;
      string latest = LatestRelease(out url);
      if (latest == null && offlineRetries-- > 0) { Thread.Sleep(60000); continue; }
      if (latest != null && IsNewer(latest) && ReadNotified() != latest) {
        WriteNotified(latest);
        if (!AutoInstall) {
          var r0 = MessageBox.Show("DiaWheelFix の新しいバージョン（" + latest + "）があります。\n\nダウンロードページを開きますか？",
                                   AppName, MessageBoxButtons.YesNo, MessageBoxIcon.Information);
          if (r0 == DialogResult.Yes) Process.Start(ReleasesPage);
          Thread.Sleep(TimeSpan.FromHours(24));
          continue;
        }
        var r = MessageBox.Show("DiaWheelFix の新しいバージョン（" + latest + "）があります。\n\n今すぐアップデートしますか？",
                                AppName, MessageBoxButtons.YesNo, MessageBoxIcon.Information);
        if (r == DialogResult.Yes) {
          string error = SelfUpdate(url, latest);
          if (error == null) {
            MessageBox.Show(latest + " にアップデートしました。", AppName);
            runMutex.Close();   // let the new exe take over
            Process.Start(new ProcessStartInfo(ExePath, "--run") { UseShellExecute = false });
            Environment.Exit(0);
          }
          var r2 = MessageBox.Show("アップデートできませんでした。\n" + error + "\n\nダウンロードページを開きますか？",
                                   AppName, MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
          if (r2 == DialogResult.Yes) Process.Start(ReleasesPage);
        }
      }
      Thread.Sleep(TimeSpan.FromHours(24));
    }
  }

  // Self-update: download the release exe next to this one, check it, then swap the files.
  // A running exe can be renamed but not overwritten, so this one becomes DiaWheelFix.old.exe.
  const string DownloadPrefix = "https://github.com/oRJapps/DiaWheelFix/releases/download/";
  static string OldExePath { get { return Path.Combine(Path.GetDirectoryName(ExePath), AppName + ".old.exe"); } }

  // Returns null on success, or a short reason on failure
  static string SelfUpdate(string url, string tag) {
    if (url == null || !url.StartsWith(DownloadPrefix)) return "ダウンロードするファイルが見つかりません。";
    string newPath = ExePath + ".new";
    try {
      using (var wc = new System.Net.WebClient()) {
        wc.Headers.Add("User-Agent", AppName);
        wc.DownloadFile(url, newPath);
      }
      var info = FileVersionInfo.GetVersionInfo(newPath);
      Version want;
      if (info.ProductName != AppName || !Version.TryParse(tag.TrimStart('v'), out want) ||
          info.FileMajorPart != want.Major || info.FileMinorPart != want.Minor || info.FileBuildPart != Math.Max(want.Build, 0)) {
        DeleteQuietly(newPath);
        return "ダウンロードしたファイルが " + tag + " の DiaWheelFix ではありません。";
      }
      DeleteQuietly(OldExePath);
      File.Move(ExePath, OldExePath);
      try { File.Move(newPath, ExePath); }
      catch { File.Move(OldExePath, ExePath); throw; }   // put the current exe back
      return null;
    } catch (Exception e) {
      DeleteQuietly(newPath);
      return e.Message;
    }
  }

  static void DeleteQuietly(string path) {
    try { if (File.Exists(path)) File.Delete(path); } catch { }
  }

  static string LatestVersion() { string url; return LatestRelease(out url); }

  // Latest release tag such as "v1.0.2" and its exe download URL; null when offline / anything fails
  static string LatestRelease(out string exeUrl) {
    exeUrl = null;
    try {
      System.Net.ServicePointManager.SecurityProtocol = (System.Net.SecurityProtocolType)3072;   // TLS 1.2
      using (var wc = new System.Net.WebClient()) {
        wc.Headers.Add("User-Agent", AppName);
        string json = wc.DownloadString(ReleasesApi);
        var m = System.Text.RegularExpressions.Regex.Match(json, "\"tag_name\"\\s*:\\s*\"(v?[0-9.]+)\"");
        var a = System.Text.RegularExpressions.Regex.Match(json, "\"browser_download_url\"\\s*:\\s*\"(https://[^\"]+/DiaWheelFix\\.exe)\"");
        if (a.Success) exeUrl = a.Groups[1].Value;
        return m.Success ? m.Groups[1].Value : null;
      }
    } catch { return null; }
  }

  static bool IsNewer(string tag) {
    Version v;
    if (!Version.TryParse(tag.TrimStart('v'), out v)) return false;
    Version mine = Assembly.GetExecutingAssembly().GetName().Version;
    return new Version(v.Major, v.Minor, Math.Max(v.Build, 0)) > new Version(mine.Major, mine.Minor, mine.Build);
  }

  static string ReadNotified() {
    try { return File.ReadAllText(NotifiedFile).Trim(); } catch { return null; }
  }

  static void WriteNotified(string tag) {
    try { Directory.CreateDirectory(Path.GetDirectoryName(NotifiedFile)); File.WriteAllText(NotifiedFile, tag); } catch { }
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
    bool dia = IsDia(pid);
    Log("pt=" + pt.X + "," + pt.Y + " under=" + Cls(h) + " root=" + Cls(root) + " pid=" + pid + " dia=" + dia);
    if (!dia) return IntPtr.Zero;
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
        bool ok = PostMessage(page, (uint)msg, wp, lp);
        Log("  forwarded to " + page + " delta=" + (short)(s.mouseData >> 16) + " post=" + ok);
        return new IntPtr(1);   // swallow the original, Dia would drop it anyway
      }
    }
    return CallNextHookEx(IntPtr.Zero, n, w, l);
  }
}
