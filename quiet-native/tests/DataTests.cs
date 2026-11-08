using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;
public static class DataTests
{
 static int checks;
 static void Check(bool condition,string message){if(!condition)throw new Exception(message);checks++;}
 static void Discovery(NativeData data,List<SourceItem> sources,List<LibraryApp> apps){typeof(NativeData).GetProperty("Sources").GetSetMethod(true).Invoke(data,new object[]{sources});typeof(NativeData).GetProperty("Apps").GetSetMethod(true).Invoke(data,new object[]{apps});typeof(NativeData).GetMethod("RebuildKeys",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(data,null);}
 public static int Main(string[] args)
 {
  if(args.Length!=0&&args.Length!=2){Console.Error.WriteLine("Usage: DataTests [sources.json apps.json]. With no arguments, bundled synthetic fixtures are used.");return 2;}
  string sourceFixture=args.Length==2?args[0]:Path.Combine(AppContext.BaseDirectory,"fixtures","sources.synthetic.json");
  string appFixture=args.Length==2?args[1]:Path.Combine(AppContext.BaseDirectory,"fixtures","apps.synthetic.json");
  if(args.Length==0)Console.WriteLine("Using bundled synthetic fixtures; no TV or network access.");
  string root=Path.Combine(Path.GetTempPath(),"quiet-data-test-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
  try{
   var observed=NativeData.ParseSources(File.ReadAllText(sourceFixture));
   Check(observed.Count==3,"Native source parser must exclude TV/USB/network entries");
   Check(observed.Single(x=>x.Id==13).Name=="Apple TV","Percent encoded Apple TV name");
   Check(observed.Single(x=>x.Id==16).Port==4,"Actual HDMI4 native ID mapping");
   Check(!observed.Any(x=>x.Port==2),"Do not fabricate an absent port");
   var sources=observed.Where(x=>x.Id!=15).ToList();
   var inventory=JsonSerializer.Deserialize<List<NativeAppInfo>>(File.ReadAllText(appFixture));
   var apps=inventory.Select(x=>new LibraryApp{Id=x.Id,Name=x.Name,Asset="",Width=640,Height=360}).ToList();
   File.WriteAllText(Path.Combine(root,"dusk-settings.json"),JsonSerializer.Serialize(new{favoriteMode="manual",favoriteIds=new[]{"input:HDMI:1","input:HDMI:4","app:IkWsFHtOY9.Stremio","app:9Ur5IzDKqV.TizenYouTube","app:rJeHak5zRg.Spotify"},launchCounts=new Dictionary<string,int>{{"IkWsFHtOY9.Stremio",6},{"rJeHak5zRg.Spotify",1}},pinNuvio=true}));
   string state=Path.Combine(root,"state.json");var data=new NativeData(root,state);Discovery(data,sources,apps);
   var expected=new[]{"hdmi:13","hdmi:16","app:IkWsFHtOY9.Stremio","app:9Ur5IzDKqV.TizenYouTube","app:rJeHak5zRg.Spotify","app:NuvioTV001.NuvioTV"};
   Check(data.FavoriteKeys.SequenceEqual(expected),"Preserve true manual favorites and requested Nuvio pin");
   Check(data.RecentKeys.Count==0,"Usage counts do not fabricate chronological MRU");
   data.RecordLaunch("app:IkWsFHtOY9.Stremio");data.RecordLaunch("hdmi:16");data.RecordLaunch("app:IkWsFHtOY9.Stremio");
   Check(data.RecentKeys.SequenceEqual(new[]{"app:IkWsFHtOY9.Stremio","hdmi:16"}),"Successful MRU distinct recency order");
   Check(data.FavoriteKeys.SequenceEqual(expected),"MRU never changes manual pins");
   data.RecordLaunch("app:invented.notinstalled");Check(data.RecentKeys.Count==2,"Ignore unknown application recordings");
   var restored=new NativeData(root,state);Discovery(restored,sources,apps);
   Check(restored.RecentKeys.SequenceEqual(data.RecentKeys),"MRU persists across process restarts");
   Check(restored.FavoriteKeys.SequenceEqual(expected),"Pins persist across process restarts");
   restored.MoveFavorite("app:NuvioTV001.NuvioTV",-99);Check(restored.FavoriteKeys.First()=="app:NuvioTV001.NuvioTV","Reorder clamps to valid start");
   restored.ToggleFavorite("app:NuvioTV001.NuvioTV");Check(!restored.FavoriteKeys.Contains("app:NuvioTV001.NuvioTV"),"Manual unpin");
   restored.ToggleFavorite("app:NuvioTV001.NuvioTV");Check(restored.FavoriteKeys.Last()=="app:NuvioTV001.NuvioTV","Manual repin");
   foreach(var app in apps)restored.RecordLaunch("app:"+app.Id);
   Check(restored.RecentKeys.Count==12&&restored.RecentKeys.Distinct().Count()==12,"MRU bounded at12 real entries");
   var persisted=JsonSerializer.Deserialize<NativePreferences>(File.ReadAllText(state));
   Check(persisted.Usage.Single(x=>x.Key=="app:IkWsFHtOY9.Stremio").Count==9,"Preserve imported true launch count and increment successes");
   Check(persisted.Usage.Count>12,"MRU bounding does not discard usage count history");
   Console.WriteLine("PASS "+checks+" native data checks");return 0;
  }finally{Directory.Delete(root,true);}
 }
}
