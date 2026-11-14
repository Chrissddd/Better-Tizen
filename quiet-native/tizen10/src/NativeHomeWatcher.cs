using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

// Owner-local Home re-entry. It never changes the default Home assignment or boot
// selectors. Native UI pause stops its animation timers, not this process timer.
internal sealed class NativeHomeWatcher : IDisposable
{
 private static readonly HashSet<string> SamsungHomeIds=new HashSet<string>(StringComparer.Ordinal){"com.samsung.tv.csfs"};
 private static readonly HttpClient PowerClient=new HttpClient(new HttpClientHandler{UseProxy=false,AllowAutoRedirect=false}){Timeout=TimeSpan.FromMilliseconds(850),MaxResponseContentBufferSize=65536};
 private readonly object gate=new object();
 private readonly string selfId,statePath;
 private readonly Timer timer;
 private bool disposed,resumed=true,hasResumed,awaitingExternal;
 private int busy,generation,reads,launches,reports,homeSamples;
 private long graceUntil,lastLaunch,powerReadAt,homeSeenAt;
 private string foreground="",lastAction="starting",lastError="",expectedExternal="",powerState="unknown";
 private int lastLaunchCode=int.MinValue;
 [DllImport("libvconf.so.0",CallingConvention=CallingConvention.Cdecl)] private static extern IntPtr vconf_get_str(string key);
 [DllImport("libc.so.6",CallingConvention=CallingConvention.Cdecl)] private static extern void free(IntPtr pointer);
 [DllImport("libcapi-appfw-app-control.so.0")] private static extern int app_control_create(out IntPtr app);
 [DllImport("libcapi-appfw-app-control.so.0")] private static extern int app_control_set_app_id(IntPtr app,string id);
 [DllImport("libcapi-appfw-app-control.so.0")] private static extern int app_control_set_operation(IntPtr app,string operation);
 [DllImport("libcapi-appfw-app-control.so.0")] private static extern int app_control_add_extra_data(IntPtr app,string key,string value);
 [DllImport("libcapi-appfw-app-control.so.0")] private static extern int app_control_send_launch_request(IntPtr app,IntPtr callback,IntPtr data);
 [DllImport("libcapi-appfw-app-control.so.0")] private static extern int app_control_destroy(IntPtr app);

 public NativeHomeWatcher(string selfId,string privateStatePath)
 {
  this.selfId=selfId??throw new ArgumentNullException(nameof(selfId));statePath=privateStatePath;
  lastLaunch=Environment.TickCount64;timer=new Timer(Poll,null,500,500);
 }
 public object Snapshot()
 {
  lock(gate)return new{self_id=selfId,foreground,last_action=lastAction,last_error=lastError,generation,reads,launches,last_launch_code=lastLaunchCode,expected_external=expectedExternal,external_grace_remaining_ms=Math.Max(0,graceUntil-Environment.TickCount64),resumed,has_resumed=hasResumed,power_state=powerState,power_gate="fresh loopback on immediately before launch; two Home samples at least 500ms apart",home_samples=homeSamples,home_observed_ms=homeSeenAt==0?0:Math.Max(0,Environment.TickCount64-homeSeenAt),disposed};
 }
 public void BeforeExternalLaunch(string applicationId)
 {
  lock(gate){if(disposed)return;generation++;awaitingExternal=true;expectedExternal=applicationId??"";graceUntil=Environment.TickCount64+8000;lastAction="external_launch_requested";}
  Publish();
 }
 public void ExternalLaunchCompleted(string applicationId,bool success)
 {
  lock(gate){if(disposed)return;if(!success&&expectedExternal==applicationId){generation++;awaitingExternal=false;expectedExternal="";graceUntil=0;}lastAction=success?"external_launch_accepted":"external_launch_failed";}
  Publish();
 }
 public void Lifecycle(bool isResumed)
 {
  lock(gate){if(disposed)return;resumed=isResumed;if(isResumed)hasResumed=true;generation++;if(isResumed){awaitingExternal=false;expectedExternal="";graceUntil=0;}lastAction=isResumed?"ui_resumed":"ui_paused";}
  Publish();
 }
 private static string ReadForeground()
 {
  IntPtr pointer=vconf_get_str("memory/appfw/current_fullscreen_appid");
  if(pointer==IntPtr.Zero)throw new InvalidOperationException("Foreground vconf value unavailable.");
  try{return Marshal.PtrToStringUTF8(pointer)??"";}finally{free(pointer);}
 }
 private async Task<bool> PowerIsOn()
 {
  // Only negative results are cached. An earlier on result cannot authorize
  // a new launch after the television has entered standby.
  lock(gate){if(powerState!="on"&&Environment.TickCount64-powerReadAt<1000)return false;}
  string value="unknown";
  try{
   string json=await PowerClient.GetStringAsync("http://127.0.0.1:8001/api/v2/");
   using(var document=JsonDocument.Parse(json)){
    JsonElement device,state;
    if(document.RootElement.TryGetProperty("device",out device)&&device.TryGetProperty("PowerState",out state)&&state.ValueKind==JsonValueKind.String)value=(state.GetString()??"unknown").ToLowerInvariant();
   }
  }catch{ }
  lock(gate){powerReadAt=Environment.TickCount64;powerState=value;}
  return value=="on";
 }
 private async void Poll(object ignored)
 {
  if(Interlocked.Exchange(ref busy,1)!=0)return;
  try{
   string current;try{current=ReadForeground();}catch(Exception error){bool errorChanged;lock(gate){if(disposed)return;reads++;string message=error.GetType().Name+": "+error.Message;errorChanged=lastError!=message||lastAction!="foreground_read_failed";lastError=message;lastAction="foreground_read_failed";homeSamples=0;homeSeenAt=0;}if(errorChanged)Publish();return;}
   bool changed;int observedGeneration;
   lock(gate){
    if(disposed)return;reads++;changed=foreground!=current;foreground=current;lastError="";
    if(changed){generation++;lastAction="foreground_changed";}
    if(!SamsungHomeIds.Contains(current)){
     homeSamples=0;homeSeenAt=0;
     if(current.Length>0&&current!=selfId&&awaitingExternal){awaitingExternal=false;expectedExternal="";graceUntil=0;generation++;lastAction="external_foreground_observed";changed=true;}
     if(current==selfId&&awaitingExternal&&Environment.TickCount64>=graceUntil){awaitingExternal=false;expectedExternal="";graceUntil=0;generation++;lastAction="external_grace_expired";changed=true;}
     if(changed)Publish();return;
    }
    if(homeSamples==0)homeSeenAt=Environment.TickCount64;homeSamples++;
    if(homeSamples<2||Environment.TickCount64-homeSeenAt<500){lastAction="home_waiting_for_stable_foreground";if(changed)Publish();return;}
    if(!hasResumed||awaitingExternal&&Environment.TickCount64<graceUntil)return;
    if(Environment.TickCount64-lastLaunch<2000)return;
    observedGeneration=generation;
   }
   // A stale CSFS foreground value in standby must never wake the television.
   if(!await PowerIsOn()){lock(gate){lastAction="home_waiting_for_power_on";}if(changed)Publish();return;}
   string fresh=ReadForeground();
   lock(gate){
    if(disposed||generation!=observedGeneration||fresh!=current||!SamsungHomeIds.Contains(fresh)||awaitingExternal&&Environment.TickCount64<graceUntil)return;
    if(Environment.TickCount64-lastLaunch<2000)return;
    lastLaunch=Environment.TickCount64;awaitingExternal=false;expectedExternal="";graceUntil=0;
    lastLaunchCode=LaunchSelf();launches++;generation++;lastAction=lastLaunchCode==0?"home_reentry_requested":"home_reentry_failed";
    if(lastLaunchCode!=0)lastError="app_control_send_launch_request rc="+lastLaunchCode;
   }
   Publish();
  }catch(Exception error){lock(gate){if(!disposed){lastError=error.GetType().Name+": "+error.Message;lastAction="watcher_error";}}Publish();}
  finally{Volatile.Write(ref busy,0);}
 }
 private int LaunchSelf()
 {
  IntPtr handle;int result=app_control_create(out handle);if(result!=0)return result;
  try{
   result=app_control_set_app_id(handle,selfId);
   if(result==0)result=app_control_set_operation(handle,"http://tizen.org/appcontrol/operation/main");
   if(result==0)result=app_control_add_extra_data(handle,"quiet_origin","home_watcher");
   if(result==0)result=app_control_send_launch_request(handle,IntPtr.Zero,IntPtr.Zero);
   return result;
  }finally{app_control_destroy(handle);}
 }
 private void Publish()
 {
  object state=Snapshot();
  try{string text=JsonSerializer.Serialize(state);File.WriteAllText(statePath,text+"\n");}catch{ }
  // Explicit private diagnostics are capped per process; normal builds have no config.
  if(Interlocked.Increment(ref reports)<=24)_=NativeDiagnostics.SendAsync("watcher",JsonSerializer.Serialize(state));
 }
 public void Dispose(){lock(gate){if(disposed)return;disposed=true;generation++;lastAction="stopped";}timer.Dispose();}
}
