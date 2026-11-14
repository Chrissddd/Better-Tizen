using System;
using System.IO;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

// Explicit, app-private local diagnostics only. No configuration means no
// diagnostic network traffic and no page capture. This code changes no routing.
internal static class NativeDiagnostics
{
 public static Func<object> WatcherSnapshot;
 private static int eventReports;
 private static string resourceRoot,applicationId;
 public static void Initialize(string resource,string id){resourceRoot=resource;applicationId=id;}
 private sealed class Configuration { public string host {get;set;} public int port {get;set;} public string token {get;set;} public bool capturePages {get;set;} public bool captureHomeOnly {get;set;} public bool configureStartup {get;set;} public string expiresUtc {get;set;} }
 private static Configuration Load()
 {
  string path=Path.Combine(resourceRoot??Tizen.Applications.Application.Current.DirectoryInfo.Resource,"config.json");
  if(!File.Exists(path))return null;
  var config=JsonSerializer.Deserialize<Configuration>(File.ReadAllText(path));
  IPAddress address;
  if(config==null||config.port<1||config.port>65535||config.token==null||config.token.Length<32||config.token.Length>256||!IPAddress.TryParse(config.host,out address)||address.AddressFamily!=AddressFamily.InterNetwork)return null;
  var bytes=address.GetAddressBytes();
  if(!(bytes[0]==10||bytes[0]==127||bytes[0]==192&&bytes[1]==168||bytes[0]==172&&bytes[1]>=16&&bytes[1]<=31))return null;
  foreach(char c in config.token)if(!Uri.IsHexDigit(c))return null;
  return Current(config)?config:null;
 }
 private static bool Current(Configuration config)
 {
  if(config==null)return false;if(config.expiresUtc==null)return true;
  DateTimeOffset expires;return DateTimeOffset.TryParse(config.expiresUtc,CultureInfo.InvariantCulture,DateTimeStyles.AssumeUniversal|DateTimeStyles.AdjustToUniversal,out expires)&&expires>DateTimeOffset.UtcNow;
 }
 public static bool CaptureRequested {get{try{var config=Load();return config!=null&&(config.capturePages||config.captureHomeOnly);}catch{return false;}}}
 public static bool CaptureAllPages {get{try{return Load()?.capturePages==true;}catch{return false;}}}
 public static bool StartupConfigurationRequested {get{try{return Load()?.configureStartup==true;}catch{return false;}}}
 public static async Task SendStartupAsync(object setup){try{var config=Load();if(config!=null)await SendObjectAsync(new{kind="report",stage="startup_configuration",utc=DateTime.UtcNow,setup},config);}catch{}}
 public static void Schedule(string statePath)
 {
  Configuration config;try{config=Load();}catch{return;}if(config==null)return;
  _=Task.Run(async()=>{
   foreach(int delay in new[]{1000,4000,10000}){
    await Task.Delay(delay);
    string state;try{state=File.ReadAllText(statePath);}catch(Exception error){state=error.GetType().Name;}
    await SendReportAsync("state",state,config);
   }
  });
 }
 private static string BootId(){try{return File.ReadAllText("/proc/sys/kernel/random/boot_id").Trim();}catch{return null;}}
 public static async Task SendAsync(string stage,string state)
 {
  try{if(System.Threading.Interlocked.Increment(ref eventReports)>48)return;var config=Load();if(config!=null)await SendReportAsync(stage,state,config);}catch{ }
 }
 private static Task SendReportAsync(string stage,string state,Configuration config)
 {
  state=state??"";if(state.Length>32000)state=state.Substring(0,32000);
  var report=new{kind="report",app_id=applicationId??Tizen.Applications.Application.Current.ApplicationInfo.ApplicationId,stage,utc=DateTime.UtcNow,framework=RuntimeInformation.FrameworkDescription,architecture=RuntimeInformation.ProcessArchitecture.ToString(),tick_count64=Environment.TickCount64,boot_id=BootId(),watcher=WatcherSnapshot?.Invoke(),state};
  return SendObjectAsync(report,config);
 }
 public static async Task SendCaptureAsync(string name,byte[] png,string error)
 {
  try{
   var config=Load();if(config==null||!config.capturePages&&!config.captureHomeOnly)return;
   if(name!="home.png"&&name!="apps.png"&&name!="settings.png")return;
   if(png!=null&&png.Length>1400000){png=null;error="Scene PNG exceeds the bounded callback limit.";}
   await SendObjectAsync(new{kind="capture",name,png=png==null?null:Convert.ToBase64String(png),error},config);
  }catch{ }
 }
 private static async Task SendObjectAsync(object report,Configuration config)
 {
  try{
   if(!Current(config))return;
   byte[] body=Encoding.UTF8.GetBytes(config.token+"\n"+JsonSerializer.Serialize(report)+"\n");
   if(body.Length>2*1024*1024)return;
   using(var client=new TcpClient()){
    await client.ConnectAsync(config.host,config.port).WaitAsync(TimeSpan.FromSeconds(7));
    await client.GetStream().WriteAsync(body).AsTask().WaitAsync(TimeSpan.FromSeconds(7));
   }
  }catch{ }
 }
}
