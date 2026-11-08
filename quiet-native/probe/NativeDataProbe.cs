using System;
using System.IO;
using System.Text.Json;
using System.Runtime.InteropServices;
public static class NativeDataProbe
{
 [DllImport("libc.so.6")]static extern uint alarm(uint seconds);
 public static int Main(string[] args)
 {
  if(args.Length>0&&args[0]=="parse"){
   Console.WriteLine(JsonSerializer.Serialize(NativeData.ParseSources(File.ReadAllText(args[1]))));return 0;
  }
  // A read-only probe; never calls recording, preferences saving or account APIs.
  try{alarm(10);}catch{}
  Console.WriteLine("PROBE_START");Console.Out.Flush();
  var data=new NativeData(args.Length>0?args[0]:"/nonexistent", "/tmp/quiet-native-probe-no-write.json");
  Console.WriteLine("SOURCES_BEGIN");Console.Out.Flush();
  data.RefreshSources();
  Console.WriteLine("SOURCES_END "+JsonSerializer.Serialize(new{data.LastSourceJson,data.Sources,data.CurrentSourceId,data.LastError}));Console.Out.Flush();
  Console.WriteLine("APPS_BEGIN");Console.Out.Flush();
  data.RefreshApps();
  Console.WriteLine("APPS_END "+JsonSerializer.Serialize(new{data.LastAppResult,data.InstalledApps,data.LastError}));Console.Out.Flush();
  try{alarm(0);}catch{}
  return data.LastAppResult==0?0:1;
 }
}
