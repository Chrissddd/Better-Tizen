using System;
using System.Runtime.InteropServices;
public static class Program {
 const string Home="db/appfw/home_appid",Startup="db/appfw/startup_appid",Auto="db/menu/smart_hub/ontvsetting/auto_start";
 const string Quiet="com.samsung.tv.bixby-promotion",Dusk="DuskLaunch.Dusk";
 [DllImport("libvconf.so.0")]static extern IntPtr vconf_get_str(string key);
 [DllImport("libvconf.so.0")]static extern int vconf_get_int(string key,out int value);
 [DllImport("libvconf.so.0")]static extern int vconf_set_str(string key,string value);
 [DllImport("libvconf.so.0")]static extern int vconf_set_int(string key,int value);
 [DllImport("libc.so.6")]static extern void free(IntPtr pointer);
 [DllImport("libc.so.6")]static extern uint getuid();
 static string Get(string key){var p=vconf_get_str(key);if(p==IntPtr.Zero)throw new Exception("Missing preference "+key);try{return Marshal.PtrToStringUTF8(p);}finally{free(p);}}
 static int Number(string key){int value;if(vconf_get_int(key,out value)!=0)throw new Exception("Missing integer "+key);return value;}
 static void Set(string key,string value){if(vconf_set_str(key,value)!=0)throw new Exception("Set failed "+key);}
 static void Show(){Console.WriteLine("home="+Get(Home)+" startup="+Get(Startup)+" auto_start="+Number(Auto));}
 public static int Main(string[] args){try{
  Show();if(args.Length==0||args[0]=="read")return 0;
  if(getuid()!=0)throw new Exception("Root required for selectors");
  string h=Get(Home),s=Get(Startup);int a=Number(Auto);
  if(h!="com.samsung.tv.csfs"&&h!=Dusk&&h!=Quiet||s!="org.tizen.tv-viewer"&&s!=Dusk&&s!=Quiet||a<0||a>1)throw new Exception("Unexpected selector drift");
  string target=args[0]=="enable"?Quiet:args[0]=="dusk"?Dusk:throw new Exception("Expected read, enable or dusk");
  Set(Home,target);if(vconf_set_int(Auto,1)!=0)throw new Exception("Set auto_start failed");Set(Startup,target);
  Show();if(Get(Home)!=target||Get(Startup)!=target||Number(Auto)!=1)throw new Exception("Readback mismatch");
  Console.WriteLine("VERIFIED_QUIET_BOOT_SELECTORS="+(target==Quiet));return 0;
 }catch(Exception error){Console.Error.WriteLine(error.Message);return 1;}}
}
