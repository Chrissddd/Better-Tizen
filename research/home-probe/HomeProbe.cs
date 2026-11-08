using System;
using System.Runtime.InteropServices;

// Read-only library/symbol inventory for the exact retail-TV build.
// No app launching, default assignment, preferences, or files are changed.
internal static class HomeProbe
{
    [DllImport("libdl.so.2", EntryPoint="dlopen")]
    private static extern IntPtr Dlopen(string filename, int flags);
    [DllImport("libdl.so.2", EntryPoint="dlsym")]
    private static extern IntPtr Dlsym(IntPtr handle, string symbol);
    [DllImport("libdl.so.2", EntryPoint="dlerror")]
    private static extern IntPtr Dlerror();
    [DllImport("libdl.so.2", EntryPoint="dlclose")]
    private static extern int Dlclose(IntPtr handle);
    [DllImport("/usr/lib/libbundle.so.0", EntryPoint="bundle_create")]
    private static extern IntPtr BundleCreate();
    [DllImport("/usr/lib/libbundle.so.0", EntryPoint="bundle_free")]
    private static extern int BundleFree(IntPtr bundle);
    [DllImport("/usr/lib/libaul.so.0", EntryPoint="aul_svc_set_operation")]
    private static extern int AulSetOperation(IntPtr bundle, string operation);
    [DllImport("/usr/lib/libaul.so.0", EntryPoint="aul_get_default_app")]
    private static extern int AulGetDefaultApp(IntPtr bundle, out IntPtr appId);
    [DllImport("/usr/lib/libcapi-appfw-app-operation-tv.so.0", EntryPoint="app_operation_get_default_app")]
    private static extern int FactoryDefaultApp(string operation, out IntPtr appId);
    [DllImport("/usr/lib/libvconf.so.0", EntryPoint="vconf_get_str")]
    private static extern IntPtr VconfGetString(string key);
    [DllImport("/usr/lib/libvconf.so.0", EntryPoint="vconf_get_int")]
    private static extern int VconfGetInt(string key, out int value);
    [DllImport("/usr/lib/libvconf.so.0", EntryPoint="vconf_get_bool")]
    private static extern int VconfGetBool(string key, out int value);
    [DllImport("libc.so.6", EntryPoint="free")]
    private static extern void Free(IntPtr memory);
    [DllImport("libc.so.6", EntryPoint="getuid")]
    private static extern uint GetUid();
    private static string Clean(string value) { return value == null ? "<null>" : value.Replace("\\", "\\\\").Replace("\n", "\\n").Replace("\r", "\\r"); }
    private static void ReadDefaults()
    {
        const string operation = "http://tizen.org/appcontrol/operation/main";
        IntPtr bundle = BundleCreate();
        Console.WriteLine("BUNDLE created=" + (bundle != IntPtr.Zero));
        if (bundle == IntPtr.Zero) return;
        IntPtr appId = IntPtr.Zero;
        try {
            int rc = AulSetOperation(bundle, operation);
            Console.WriteLine("BUNDLE_OPERATION rc=" + rc);
            if (rc == 0) {
                rc = AulGetDefaultApp(bundle, out appId);
                Console.WriteLine("MAIN_ASSIGNED rc=" + rc + " appid=" + Clean(Marshal.PtrToStringAnsi(appId)));
                if (appId != IntPtr.Zero) { Free(appId); appId = IntPtr.Zero; }
            }
        } finally { BundleFree(bundle); }
        int factoryRc = FactoryDefaultApp(operation, out appId);
        Console.WriteLine("MAIN_FACTORY_DEFAULT rc=" + factoryRc + " appid=" + Clean(Marshal.PtrToStringAnsi(appId)));
        if (appId != IntPtr.Zero) Free(appId);
    }
    private static void ReadVconf()
    {
        foreach (string key in new [] { "db/appfw/home_appid", "db/appfw/startup_appid", "db/appfw/endup_appid", "db/menu/system/smartfeatures/autorun-defaultapp" }) {
            IntPtr value = VconfGetString(key);
            Console.WriteLine("VCONF_STRING key=" + key + " value=" + Clean(Marshal.PtrToStringAnsi(value)));
            if (value != IntPtr.Zero) Free(value);
        }
        foreach (string key in new [] { "db/menu/smart_hub/ontvsetting/auto_start", "db/menu/app_booting", "db/first_screen/type", "db/appfw/is_root_cold_boot" }) {
            int value;
            int rc = VconfGetInt(key, out value);
            Console.WriteLine("VCONF_INT key=" + key + " rc=" + rc + " value=" + value);
            rc = VconfGetBool(key, out value);
            Console.WriteLine("VCONF_BOOL key=" + key + " rc=" + rc + " value=" + value);
        }
    }
    public static int Main(string[] args)
    {
        Console.WriteLine("UID=" + GetUid());
        if (args.Length == 1 && args[0] == "read") {
            ReadDefaults(); ReadVconf(); return 0;
        }
        var libraries = new [] {
          "/usr/lib/libcapi-appfw-app-operation-tv.so.0",
          "/usr/lib/libcapi-appfw-app-control.so.0",
          "/usr/lib/libvconf.so.0",
          "/usr/lib/libaul.so.0"
        };
        var symbols = new [] { "app_operation_get_current_app", "app_operation_set_current_app",
          "app_control_create", "app_control_set_operation", "app_control_set_defapp",
          "app_control_unset_defapp", "app_control_get_defapp", "app_control_get_default_app",
          "app_control_destroy", "vconf_get_str", "vconf_get_int", "vconf_get_bool",
          "aul_svc_get_all_defapps", "aul_svc_get_all_defapps_for_uid", "aul_svc_is_defapp" };
        foreach (string library in libraries) {
            IntPtr handle = Dlopen(library, 2); // RTLD_NOW
            Console.WriteLine("LIB " + library + " loaded=" + (handle != IntPtr.Zero));
            if (handle == IntPtr.Zero) { Console.WriteLine("ERROR " + Marshal.PtrToStringAnsi(Dlerror())); continue; }
            try { foreach (string symbol in symbols) if (Dlsym(handle, symbol) != IntPtr.Zero) Console.WriteLine("SYMBOL " + symbol); }
            finally { Dlclose(handle); }
        }
        return 0;
    }
}
