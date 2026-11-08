using System;
using System.Runtime.InteropServices;
using System.Threading;

// Exact-device, reversible preference change. It does not replace binaries, launch
// apps, reboot, install services, or change the separate operation/main assignment.
internal static class HomeBoot
{
    private const string Launcher = "DuskLaunch.Dusk";
    private const string OriginalHome = "com.samsung.tv.csfs";
    private const string OriginalStartup = "org.tizen.tv-viewer";
    private const string HomeKey = "db/appfw/home_appid";
    private const string StartupKey = "db/appfw/startup_appid";
    private const string AutoKey = "db/menu/smart_hub/ontvsetting/auto_start";
    private const string Operation = "http://tizen.org/appcontrol/operation/main";

    [DllImport("/usr/lib/libvconf.so.0", EntryPoint="vconf_get_str")]
    private static extern IntPtr GetStringNative(string key);
    [DllImport("/usr/lib/libvconf.so.0", EntryPoint="vconf_get_int")]
    private static extern int GetIntNative(string key, out int value);
    [DllImport("/usr/lib/libvconf.so.0", EntryPoint="vconf_set_str")]
    private static extern int SetStringNative(string key, string value);
    [DllImport("/usr/lib/libvconf.so.0", EntryPoint="vconf_set_int")]
    private static extern int SetIntNative(string key, int value);
    [DllImport("/usr/lib/libbundle.so.0", EntryPoint="bundle_create")]
    private static extern IntPtr BundleCreate();
    [DllImport("/usr/lib/libbundle.so.0", EntryPoint="bundle_free")]
    private static extern int BundleFree(IntPtr bundle);
    [DllImport("/usr/lib/libaul.so.0", EntryPoint="aul_svc_set_operation")]
    private static extern int AulSetOperation(IntPtr bundle, string operation);
    [DllImport("/usr/lib/libaul.so.0", EntryPoint="aul_get_default_app")]
    private static extern int AulGetDefaultApp(IntPtr bundle, out IntPtr appId);
    [DllImport("libc.so.6", EntryPoint="free")]
    private static extern void Free(IntPtr memory);
    [DllImport("libc.so.6", EntryPoint="getuid")]
    private static extern uint GetUid();

    private sealed class State
    {
        internal string Home, Startup, Main, FirstScreen, Autorun;
        internal int Auto, AppBooting;
        internal bool Original => Home == OriginalHome && Startup == OriginalStartup && Auto == 0;
        internal bool Enabled => Home == Launcher && Startup == Launcher && Auto == 1;
        internal bool Known => (Home == OriginalHome || Home == Launcher) &&
            (Startup == OriginalStartup || Startup == Launcher || Startup == OriginalHome) &&
            (Auto == 0 || Auto == 1);
    }
    private static string GetString(string key)
    {
        IntPtr value = GetStringNative(key);
        if (value == IntPtr.Zero) throw new Exception("Cannot read String key " + key);
        try { return Marshal.PtrToStringAnsi(value); }
        finally { Free(value); }
    }
    private static int GetInt(string key)
    {
        int value;
        int rc = GetIntNative(key, out value);
        if (rc != 0) throw new Exception("Cannot read Int32 key " + key + " rc=" + rc);
        return value;
    }
    private static string GetMain()
    {
        IntPtr bundle = BundleCreate(), appId = IntPtr.Zero;
        if (bundle == IntPtr.Zero) throw new Exception("bundle_create failed");
        try {
            int rc = AulSetOperation(bundle, Operation);
            if (rc != 0) throw new Exception("aul_svc_set_operation rc=" + rc);
            rc = AulGetDefaultApp(bundle, out appId);
            if (rc != 0 || appId == IntPtr.Zero) throw new Exception("aul_get_default_app rc=" + rc);
            return Marshal.PtrToStringAnsi(appId);
        } finally {
            if (appId != IntPtr.Zero) Free(appId);
            BundleFree(bundle);
        }
    }
    private static State Read()
    {
        return new State {
            Home = GetString(HomeKey), Startup = GetString(StartupKey),
            Auto = GetInt(AutoKey), Main = GetMain(),
            AppBooting = GetInt("db/menu/app_booting"),
            FirstScreen = GetString("db/first_screen/type"),
            Autorun = GetString("db/menu/system/smartfeatures/autorun-defaultapp")
        };
    }
    private static void Print(string label, State value)
    {
        Console.WriteLine(label + " home=" + value.Home + " startup=" + value.Startup +
            " auto_start(Int32)=" + value.Auto + " main=" + value.Main);
        Console.WriteLine(label + " preserved app_booting(Int32)=" + value.AppBooting +
            " first_screen(String)=" + value.FirstScreen + " autorun-defaultapp(String)=" + value.Autorun);
    }
    private static void SetString(string key, string value)
    {
        int rc = SetStringNative(key, value);
        Console.WriteLine("SET_STRING key=" + key + " value=" + value + " rc=" + rc);
        if (rc != 0 || GetString(key) != value) throw new Exception("String write verification failed: " + key);
    }
    private static void SetAuto(int value)
    {
        int rc = SetIntNative(AutoKey, value);
        Console.WriteLine("SET_INT key=" + AutoKey + " value=" + value + " rc=" + rc);
        if (rc != 0 || GetInt(AutoKey) != value) throw new Exception("Int32 write verification failed: " + AutoKey);
    }
    private static void Apply(string home, string startup, int auto)
    {
        // home_appid notifies the context manager's last-home cache. Let it settle
        // before auto_start notifies the handler that derives startup_appid from it.
        SetString(HomeKey, home);
        Thread.Sleep(1500);
        SetAuto(auto);
        Thread.Sleep(1500);
        string automatic = GetString(StartupKey);
        Console.WriteLine("STARTUP_AFTER_CALLBACK=" + automatic);
        // Also write the explicit saved startup value. This handles restore and
        // avoids relying on delivery timing of the asynchronous vconf callback.
        SetString(StartupKey, startup);
        Thread.Sleep(1500);
    }
    private static bool Preserved(State before, State after)
    {
        return before.Main == after.Main && before.AppBooting == after.AppBooting &&
            before.FirstScreen == after.FirstScreen && before.Autorun == after.Autorun;
    }
    public static int Main(string[] args)
    {
        try {
            if (args.Length > 1 || (args.Length == 1 && args[0] != "read" && args[0] != "enable" && args[0] != "restore")) {
                Console.Error.WriteLine("usage: HomeBoot.dll read | enable | restore"); return 2;
            }
            Console.WriteLine("UID=" + GetUid());
            State before = Read();
            Print("BEFORE", before);
            if (args.Length == 0 || args[0] == "read") return 0;
            if (GetUid() != 0) { Console.Error.WriteLine("REFUSED: write requires UID 0"); return 3; }
            bool enabling = args[0] == "enable";
            if (enabling && before.Enabled) { Console.WriteLine("ALREADY_ENABLED"); return 0; }
            if (!enabling && before.Original) { Console.WriteLine("ALREADY_RESTORED"); return 0; }
            if ((enabling && (!before.Original || before.Main != Launcher || before.AppBooting != 0 || before.FirstScreen != OriginalHome)) ||
                (!enabling && !before.Known)) {
                Console.Error.WriteLine("REFUSED: values differ from exact-device baseline or this helper's known states"); return 3;
            }
            try {
                Apply(enabling ? Launcher : OriginalHome, enabling ? Launcher : OriginalStartup, enabling ? 1 : 0);
                State after = Read();
                Print("AFTER", after);
                if (!(enabling ? after.Enabled : after.Original) || !Preserved(before, after))
                    throw new Exception("Final saved-state verification failed");
                Console.WriteLine(enabling ? "VERIFIED_PREFERENCES_ENABLED" : "VERIFIED_PREFERENCES_RESTORED");
                Console.WriteLine("Home-button and power-cycle behavior require separate live verification.");
                return 0;
            } catch (Exception failure) {
                Console.Error.WriteLine("CHANGE_FAILED: " + failure.Message);
                if (enabling) {
                    try {
                        Apply(before.Home, before.Startup, before.Auto);
                        State rolledBack = Read();
                        Print("ROLLBACK", rolledBack);
                        if (!rolledBack.Original || !Preserved(before, rolledBack))
                            throw new Exception("rollback readback mismatch");
                        Console.Error.WriteLine("ROLLBACK_VERIFIED");
                    } catch (Exception rollbackFailure) {
                        Console.Error.WriteLine("ROLLBACK_FAILED: " + rollbackFailure.Message);
                    }
                }
                return 4;
            }
        } catch (Exception error) {
            Console.Error.WriteLine(error.GetType().Name + ": " + error.Message); return 1;
        }
    }
}
