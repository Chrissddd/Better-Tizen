using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
internal static class StartupTests
{
 const string E="com.samsung.tv.bixby-promotion",T="org.bettertizen.home";
 [DllImport("test-vconf")]static extern void reset(string h,string s,int a,int fail,int unknown);
 [DllImport("test-vconf")]static extern int write_count();
 [DllImport("test-vconf")]static extern IntPtr write_order();
 [DllImport("test-vconf")]static extern IntPtr home_value();
 [DllImport("test-vconf")]static extern IntPtr startup_value();
 static int checks;
 static void Check(bool value,string label){if(!value)throw new Exception(label);checks++;}
 static async Task<JsonElement> Run(string data){return JsonSerializer.SerializeToElement(await NativeStartup.ConfigureAsync(T,data));}
 static string Status(JsonElement r)=>r.GetProperty("status").GetString();
 public static async Task Main(string[] args)
 {
  string library=Path.GetFullPath(args[0]);
  NativeLibrary.SetDllImportResolver(typeof(NativeStartup).Assembly,(name,assembly,path)=>name=="test-vconf"||name=="libvconf.so.0"?NativeLibrary.Load(library):name=="libc.so.6"?NativeLibrary.Load(RuntimeInformation.IsOSPlatform(OSPlatform.OSX)?"/usr/lib/libSystem.B.dylib":"libc.so.6"):IntPtr.Zero);
  string data=Path.Combine(Path.GetTempPath(),"quiet-startup-tests-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(data);
  try{
   reset(E,E,1,0,0);var r=await Run(data);Check(Status(r)=="configured_verified","clean baseline setup");Check(Marshal.PtrToStringUTF8(write_order())=="startup;home;","startup is configured before Home");Check(File.Exists(Path.Combine(data,"startup-baseline.json")),"durable journal exists");Check(Directory.GetFiles(data,"*.tmp").Length==0,"atomic journal leaves no temporary files");
   reset(E,T,1,0,0);r=await Run(data);Check(Status(r)=="configured_verified"&&r.GetProperty("recovering_mixed_state").GetBoolean(),"crash after startup write resumes");Check(Marshal.PtrToStringUTF8(write_order())=="home;","recovery writes only missing Home selector");
   reset(T,E,1,0,0);r=await Run(data);Check(Status(r)=="configured_verified"&&Marshal.PtrToStringUTF8(write_order())=="startup;","opposite valid mixed state resumes");
   reset(T,T,1,0,0);r=await Run(data);Check(Status(r)=="already_configured"&&write_count()==0,"already configured has no writes");
   reset(E,E,1,1,0);r=await Run(data);Check(Status(r)=="not_configured"&&r.GetProperty("rollback").GetProperty("verified").GetBoolean(),"partial setter failure rolls back");Check(Marshal.PtrToStringUTF8(home_value())==E&&Marshal.PtrToStringUTF8(startup_value())==E,"rollback restores baseline");
   reset(E,E,1,0,1);r=await Run(data);Check(Status(r)=="not_configured","concurrent unknown selector rejects setup");Check(Marshal.PtrToStringUTF8(home_value())=="external.concurrent.app","unknown selector is preserved");Check(Marshal.PtrToStringUTF8(startup_value())==E,"known partial target is restored");
   reset(E,E,0,0,0);r=await Run(data);Check(Status(r)=="refused_unexpected_baseline"&&write_count()==0,"auto-start mismatch causes zero writes");
   File.Delete(Path.Combine(data,"startup-baseline.json"));reset(E,T,1,0,0);r=await Run(data);Check(Status(r)=="refused_mixed_state_without_valid_journal"&&write_count()==0,"mixed state requires valid prior journal");
   File.WriteAllText(Path.Combine(data,"startup-baseline.json"),"{invalid");reset(E,E,1,0,0);r=await Run(data);Check(Status(r)=="not_configured"&&write_count()==0,"invalid journal causes zero writes");Check(File.ReadAllText(Path.Combine(data,"startup-baseline.json"))=="{invalid","invalid journal is not overwritten");
   Console.WriteLine(checks+" startup crash/recovery and rollback checks passed; fake local vconf only.");
  }finally{Directory.Delete(data,true);}
 }
}
