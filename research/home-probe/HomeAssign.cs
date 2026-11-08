using System;
using System.Runtime.InteropServices;

// Changes only the operation/main default-app assignment after a known-original guard.
// No app is launched/uninstalled, no vconf preference is written, and unset is never used.
internal static class HomeAssign
{
    private const string Operation = "http://tizen.org/appcontrol/operation/main";
    private const string Original = "com.samsung.tv.csfs";
    [DllImport("/usr/lib/libbundle.so.0", EntryPoint="bundle_create")]
    private static extern IntPtr BundleCreate();
    [DllImport("/usr/lib/libbundle.so.0", EntryPoint="bundle_free")]
    private static extern int BundleFree(IntPtr bundle);
    [DllImport("/usr/lib/libaul.so.0", EntryPoint="aul_svc_set_operation")]
    private static extern int AulSetOperation(IntPtr bundle, string operation);
    [DllImport("/usr/lib/libaul.so.0", EntryPoint="aul_get_default_app")]
    private static extern int AulGetDefaultApp(IntPtr bundle, out IntPtr appId);
    [DllImport("/usr/lib/libcapi-appfw-app-control.so.0", EntryPoint="app_control_create")]
    private static extern int Create(out IntPtr handle);
    [DllImport("/usr/lib/libcapi-appfw-app-control.so.0", EntryPoint="app_control_set_operation")]
    private static extern int SetOperation(IntPtr handle, string operation);
    [DllImport("/usr/lib/libcapi-appfw-app-control.so.0", EntryPoint="app_control_set_defapp")]
    private static extern int SetDefault(IntPtr handle, string appId);
    [DllImport("/usr/lib/libcapi-appfw-app-control.so.0", EntryPoint="app_control_destroy")]
    private static extern int Destroy(IntPtr handle);
    [DllImport("libc.so.6", EntryPoint="free")]
    private static extern void Free(IntPtr memory);
    [DllImport("libc.so.6", EntryPoint="getuid")]
    private static extern uint GetUid();
    private static string Read()
    {
        IntPtr bundle = BundleCreate();
        if (bundle == IntPtr.Zero) throw new Exception("bundle_create failed");
        IntPtr appId = IntPtr.Zero;
        try {
            int rc = AulSetOperation(bundle, Operation);
            if (rc != 0) throw new Exception("aul_svc_set_operation failed rc=" + rc);
            rc = AulGetDefaultApp(bundle, out appId);
            if (rc != 0 || appId == IntPtr.Zero) throw new Exception("aul_get_default_app failed rc=" + rc);
            return Marshal.PtrToStringAnsi(appId);
        } finally {
            if (appId != IntPtr.Zero) Free(appId);
            BundleFree(bundle);
        }
    }
    private static int Assign(string appId)
    {
        IntPtr handle = IntPtr.Zero;
        int rc = Create(out handle);
        Console.WriteLine("CREATE rc=" + rc);
        if (rc != 0) return rc;
        try {
            rc = SetOperation(handle, Operation);
            Console.WriteLine("SET_OPERATION rc=" + rc);
            if (rc != 0) return rc;
            rc = SetDefault(handle, appId);
            Console.WriteLine("SET_DEFAPP rc=" + rc + " appid=" + appId);
            return rc;
        } finally { Destroy(handle); }
    }
    private static bool Valid(string value)
    {
        if (String.IsNullOrEmpty(value) || value.Length > 256 || value == Original) return false;
        foreach (char c in value) if (!((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') ||
            (c >= '0' && c <= '9') || c == '.' || c == '_' || c == '-')) return false;
        return true;
    }
    public static int Main(string[] args)
    {
        try {
            if (args.Length == 0 || (args.Length == 1 && args[0] == "read")) {
                Console.WriteLine("UID=" + GetUid());
                Console.WriteLine("MAIN_ASSIGNED=" + Read()); return 0;
            }
            bool restoring = args.Length == 1 && args[0] == "restore";
            bool reapplyingOriginal = args.Length == 1 && args[0] == "reapply-original";
            if (!restoring && !reapplyingOriginal && !(args.Length == 2 && args[0] == "set" && Valid(args[1]))) {
                Console.Error.WriteLine("usage: HomeAssign.dll read | set APPID | restore | reapply-original"); return 2;
            }
            string target = restoring || reapplyingOriginal ? Original : args[1];
            string before = Read();
            Console.WriteLine("UID=" + GetUid());
            Console.WriteLine("BEFORE=" + before);
            if (reapplyingOriginal && (GetUid() != 0 || before != Original)) {
                Console.Error.WriteLine("REFUSED: reapply-original requires UID 0 and current assignment " + Original); return 3;
            }
            if (!reapplyingOriginal && before == target) { Console.WriteLine("ALREADY_ASSIGNED=" + target); return 0; }
            if (!restoring && before != Original) {
                Console.Error.WriteLine("REFUSED: expected original " + Original + ", found " + before); return 3;
            }
            int rc = Assign(target);
            string after = Read();
            Console.WriteLine("AFTER=" + after);
            if (rc != 0 || after != target) {
                Console.Error.WriteLine("VERIFICATION_FAILED: rc=" + rc + " expected=" + target + " actual=" + after); return 4;
            }
            Console.WriteLine("VERIFIED=" + target); return 0;
        } catch (Exception error) {
            Console.Error.WriteLine(error.GetType().Name + ": " + error.Message); return 1;
        }
    }
}
