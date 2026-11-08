using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
public static class Program {
 static void Log(string value){File.AppendAllText("/tmp/quiet-power-check.txt",DateTime.UtcNow.ToString("o")+" "+value+"\n");}
 static void Key(string key){foreach(string state in new[]{"1","0"}){
  var info=new ProcessStartInfo("/usr/bin/dbus-send"){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true};
  foreach(var a in new[]{"--system","--type=method_call","--print-reply","--dest=org.enlightenment.wm","/org/enlightenment/wm","org.enlightenment.wm.Test.EventKey","uint32:"+state,"string:"+key})info.ArgumentList.Add(a);
  using(var p=Process.Start(info)){if(!p.WaitForExit(3000)){p.Kill();throw new Exception("Key timeout");}Log(key+"/"+state+" rc="+p.ExitCode+" "+p.StandardOutput.ReadToEnd().Trim());if(p.ExitCode!=0)throw new Exception("Key rejected");}Thread.Sleep(150);
 }}
 public static int Main(){try{Log("BEGIN finite standby check");Key("XF86DiscretPowerOff");Thread.Sleep(8000);Key("XF86DiscretPowerOn");Log("DONE");return 0;}catch(Exception e){Log("ERROR "+e.Message);return 1;}}
}
