using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;

// Explicit one-time owner-app bootstrap. Only the two proven writable strings
// are changed. Auto-start must already be 1 and is NEVER written here.
internal static class NativeStartup
{
 const string Expected="com.samsung.tv.bixby-promotion";
 const string Home="db/appfw/home_appid",Startup="db/appfw/startup_appid",Auto="db/menu/smart_hub/ontvsetting/auto_start";
 [DllImport("libvconf.so.0")]static extern IntPtr vconf_get_str(string key);
 [DllImport("libvconf.so.0")]static extern int vconf_get_int(string key,out int value);
 [DllImport("libvconf.so.0")]static extern int vconf_set_str(string key,string value);
 [DllImport("libc.so.6")]static extern void free(IntPtr pointer);
 private sealed class Values {public string home {get;set;} public string startup {get;set;} public int auto_start {get;set;} }
 private sealed class Journal {public int version {get;set;} public string target {get;set;} public Values baseline {get;set;} public string phase {get;set;} public DateTime updated_utc {get;set;} }
 private static string Get(string key){var p=vconf_get_str(key);if(p==IntPtr.Zero)throw new InvalidOperationException("Missing selector "+key);try{return Marshal.PtrToStringUTF8(p);}finally{free(p);}}
 private static Values Read(){int n;int rc=vconf_get_int(Auto,out n);if(rc!=0)throw new InvalidOperationException("Auto-start read failed rc="+rc);return new Values{home=Get(Home),startup=Get(Startup),auto_start=n};}
 private static bool Matches(Values value,string home,string startup)=>value!=null&&value.home==home&&value.startup==startup&&value.auto_start==1;
 private static bool Known(Values value,string target)=>value!=null&&value.auto_start==1&&(value.home==Expected||value.home==target)&&(value.startup==Expected||value.startup==target);
 private static bool Valid(Journal value,string target)=>value!=null&&value.version==1&&value.target==target&&Matches(value.baseline,Expected,Expected);
 private static Journal LoadJournal(string path,string target){var journal=JsonSerializer.Deserialize<Journal>(File.ReadAllText(path));if(!Valid(journal,target))throw new InvalidOperationException("Existing startup journal differs or is invalid.");return journal;}
 private static void SaveJournal(string path,Journal journal,string phase)
 {
  // Persist the baseline before the first selector write. Replacement is atomic
  // on the same filesystem; a power loss leaves the old complete journal or new.
  if(File.Exists(path))LoadJournal(path,journal.target);
  journal.phase=phase;journal.updated_utc=DateTime.UtcNow;
  string temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";
  try{
   using(var stream=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None)){
    JsonSerializer.Serialize(stream,journal);stream.Flush(true);
   }
   File.Move(temporary,path,true);
  }finally{try{if(File.Exists(temporary))File.Delete(temporary);}catch{}}
 }
 private static async Task<bool> Stable(string home,string startup,List<Values> samples)
 {
  int consecutive=0;long until=Environment.TickCount64+2500;
  while(Environment.TickCount64<until){
   try{var values=Read();samples.Add(values);consecutive=Matches(values,home,startup)?consecutive+1:0;if(consecutive>=5)return true;}catch{consecutive=0;}
   await Task.Delay(100);
  }return false;
 }
 public static async Task<object> ConfigureAsync(string target,string dataRoot)
 {
  var report=new Dictionary<string,object>{{"target",target},{"expected_baseline",Expected},{"auto_start_written",false},{"setter_results",new Dictionary<string,int>()}};
  Values before=null;Journal journal=null;string backup=null;bool authorized=false;var setters=(Dictionary<string,int>)report["setter_results"];
  try{
   if(target!="org.bettertizen.home")throw new InvalidOperationException("Unknown target app.");
   before=Read();report["before"]=before;
   if(Matches(before,target,target)){report["status"]="already_configured";report["after"]=before;return report;}
   if(!Known(before,target)){report["status"]="refused_unexpected_baseline";return report;}
   backup=Path.Combine(dataRoot,"startup-baseline.json");
   if(File.Exists(backup))journal=LoadJournal(backup,target);
   else{
    if(!Matches(before,Expected,Expected)){report["status"]="refused_mixed_state_without_valid_journal";return report;}
    journal=new Journal{version=1,target=target,baseline=before};
   }
   SaveJournal(backup,journal,"prepared");authorized=true;
   report["baseline_saved"]=true;report["recovering_mixed_state"]=!Matches(before,Expected,Expected);
   // Startup first: if power fails between the writes, the owner app starts on
   // the next boot and can finish from the durable baseline instead of guessing.
   foreach(var pair in new[]{new[]{Startup,"startup"},new[]{Home,"home"}}){
    var current=Read();if(!Known(current,target))throw new InvalidOperationException("Selector changed concurrently; refusing overwrite.");
    string value=pair[0]==Home?current.home:current.startup;
    if(value==target){setters[pair[1]]=0;continue;}
    setters[pair[1]]=vconf_set_str(pair[0],target);if(setters[pair[1]]!=0)throw new InvalidOperationException(pair[1]+" selector write failed.");
    SaveJournal(backup,journal,pair[1]+"_written");
   }
   var samples=new List<Values>();bool stable=await Stable(target,target,samples);report["samples"]=samples;
   if(!stable)throw new InvalidOperationException("Selector readback did not stabilize.");
   SaveJournal(backup,journal,"configured_verified");
   report["after"]=Read();report["status"]="configured_verified";return report;
  }catch(Exception error){
   report["error"]=error.GetType().Name+": "+error.Message;
   if(authorized&&Valid(journal,target)){
    var rollback=new Dictionary<string,object>();report["rollback"]=rollback;
    foreach(var pair in new[]{new[]{Home,journal.baseline.home,"home"},new[]{Startup,journal.baseline.startup,"startup"}}){
     try{
      var current=Read();string value=pair[0]==Home?current.home:current.startup;
      if(current.auto_start!=1)rollback[pair[2]]="refused_concurrent_auto_start_change";
      else if(value==target)rollback[pair[2]]=vconf_set_str(pair[0],pair[1]);
      else if(value==pair[1])rollback[pair[2]]="unchanged";
      else rollback[pair[2]]="refused_concurrent_selector_change";
     }catch(Exception e){rollback[pair[2]]=e.GetType().Name+": "+e.Message;}
    }
    var samples=new List<Values>();bool restored=await Stable(journal.baseline.home,journal.baseline.startup,samples);rollback["verified"]=restored;rollback["samples"]=samples;
    try{SaveJournal(backup,journal,restored?"rolled_back_verified":"rollback_incomplete");}catch(Exception e){rollback["journal_error"]=e.Message;}
   }
   try{report["after"]=Read();}catch(Exception e){report["after_error"]=e.Message;}
   report["status"]="not_configured";return report;
  }
 }
}
