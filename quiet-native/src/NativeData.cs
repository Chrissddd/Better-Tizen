// Native discovery and local preferences for the Quiet Home adaptation.
// Native app-manager ABI follows Samsung/TizenFX Interop.ApplicationManager (Apache-2.0).
// Discovery never invents HDMI identifiers, application installations or usage history.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

public sealed class SourceItem
{
 public int Id {get;set;}
 public int Port {get;set;}
 public string Name {get;set;}="";
 public bool Connected {get;set;}
 public string Description {get;set;}="";
 public string Action => "hdmi:"+Id.ToString(CultureInfo.InvariantCulture);
}
public sealed class NativeRecent
{
 public string Key {get;set;}="";
 public long LastUsed {get;set;}
 public int Count {get;set;}
}
public sealed class NativePreferences
{
 public int Version {get;set;}=1;
 public bool ManualFavorites {get;set;}
 public List<string> FavoriteKeys {get;set;}=new List<string>();
 public List<NativeRecent> Usage {get;set;}=new List<NativeRecent>();
 public List<string> RecentKeys {get;set;}=new List<string>();
 public Dictionary<string,string> SourceLabels {get;set;}=new Dictionary<string,string>();
 public List<SourceItem> KnownSources {get;set;}=new List<SourceItem>();
 public NativeWeather Weather {get;set;}
}
public sealed class NativeWeather
{
 public long FetchedAt {get;set;}
 public long ObservedAt {get;set;}
 public double Temperature {get;set;}
 public int Code {get;set;}
 public bool IsDay {get;set;}
}
public sealed class NativeAppInfo
{
 public string Id {get;set;}="";
 public string Name {get;set;}="";
 public string Icon {get;set;}="";
 public string Package {get;set;}="";
 public string Type {get;set;}="";
 public bool NoDisplay {get;set;}
}
public sealed class NativeData
{
 const string AppManagerLibrary="libcapi-appfw-app-manager.so.0";
 static readonly JsonSerializerOptions JsonOptions=new JsonSerializerOptions{PropertyNameCaseInsensitive=true,WriteIndented=true};
 static readonly HttpClient WeatherClient=new HttpClient{Timeout=TimeSpan.FromSeconds(8)};
 readonly string resourceRoot,statePath;
 readonly Dictionary<int,int> sourcePorts=new Dictionary<int,int>{{13,1},{14,2},{15,3},{16,4}};
 readonly object gate=new object();
 NativePreferences preferences;
 bool weatherRunning;
 long lastWeatherAttempt;
 public List<SourceItem> Sources {get;private set;}=new List<SourceItem>();
 public List<LibraryApp> Apps {get;private set;}=new List<LibraryApp>();
 public List<string> RecentKeys {get;private set;}=new List<string>();
 public List<string> FavoriteKeys {get;private set;}=new List<string>();
 public int CurrentSourceId {get;private set;}=-1;
 public string WeatherText {get;private set;}="";
 public string WeatherCity => "Oslo";
 public string WeatherSymbol {get;private set;}="";
 public string LastError {get;private set;}="";
 public string LastSourceJson {get;private set;}="";
 public int LastAppResult {get;private set;}=int.MinValue;
 public List<NativeAppInfo> InstalledApps {get;private set;}=new List<NativeAppInfo>();
 [DllImport("libsource-api.so",CallingConvention=CallingConvention.Cdecl)] static extern IntPtr get_source_list();
 [DllImport("libvconf.so.0",CallingConvention=CallingConvention.Cdecl)] static extern int vconf_get_int(string key,out int value);
 [DllImport("libc.so.6",CallingConvention=CallingConvention.Cdecl)] static extern void free(IntPtr pointer);
 [UnmanagedFunctionPointer(CallingConvention.Cdecl)] [return:MarshalAs(UnmanagedType.I1)]
 delegate bool AppInfoCallback(IntPtr app,IntPtr data);
 [DllImport(AppManagerLibrary,CallingConvention=CallingConvention.Cdecl)] static extern int app_manager_foreach_app_info(AppInfoCallback callback,IntPtr data);
 [DllImport(AppManagerLibrary,CallingConvention=CallingConvention.Cdecl)] static extern int app_info_get_app_id(IntPtr app,out IntPtr value);
 [DllImport(AppManagerLibrary,CallingConvention=CallingConvention.Cdecl)] static extern int app_info_get_label(IntPtr app,out IntPtr value);
 [DllImport(AppManagerLibrary,CallingConvention=CallingConvention.Cdecl)] static extern int app_info_get_icon(IntPtr app,out IntPtr value);
 [DllImport(AppManagerLibrary,CallingConvention=CallingConvention.Cdecl)] static extern int app_info_get_package(IntPtr app,out IntPtr value);
 [DllImport(AppManagerLibrary,CallingConvention=CallingConvention.Cdecl)] static extern int app_info_get_type(IntPtr app,out IntPtr value);
 [DllImport(AppManagerLibrary,CallingConvention=CallingConvention.Cdecl)] static extern int app_info_is_nodisplay(IntPtr app,[MarshalAs(UnmanagedType.I1)] out bool value);
 delegate int GetAppString(IntPtr app,out IntPtr value);

 public NativeData(string resourceRoot,string statePath)
 {
  this.resourceRoot=Path.GetFullPath(resourceRoot);
  this.statePath=Path.GetFullPath(statePath);
  try{preferences=File.Exists(this.statePath)?JsonSerializer.Deserialize<NativePreferences>(File.ReadAllText(this.statePath),JsonOptions):null;}catch(Exception e){LastError="Preferences: "+e.Message;}
  preferences=preferences??new NativePreferences();
  preferences.FavoriteKeys=preferences.FavoriteKeys??new List<string>();
  preferences.Usage=preferences.Usage??new List<NativeRecent>();
  preferences.RecentKeys=preferences.RecentKeys??new List<string>();
  preferences.KnownSources=preferences.KnownSources??new List<SourceItem>();
  preferences.SourceLabels=preferences.SourceLabels??new Dictionary<string,string>();
  preferences.Usage=preferences.Usage.Where(x=>x!=null&&ValidKey(x.Key)&&x.Count>0).GroupBy(x=>x.Key).Select(x=>x.OrderByDescending(y=>y.LastUsed).First()).OrderByDescending(x=>x.LastUsed).Take(256).ToList();
  preferences.RecentKeys=preferences.RecentKeys.Where(ValidKey).Distinct().Take(12).ToList();
  // This is a source-enum translation for the probed QN85F, not a device catalog.
  // Port entries appear only when get_source_list actually returns that sourceType.
  try{string mapPath=Path.Combine(this.resourceRoot,"source-ports.json");if(File.Exists(mapPath)){var map=JsonSerializer.Deserialize<Dictionary<int,int>>(File.ReadAllText(mapPath),JsonOptions);if(map!=null&&map.Count>0){sourcePorts.Clear();foreach(var pair in map)if(pair.Key>=0&&pair.Value>0)sourcePorts[pair.Key]=pair.Value;}}}catch(Exception e){LastError="Source map: "+e.Message;}
  if(!File.Exists(this.statePath))ImportDuskSeed();
  UpdateWeatherText();
 }
 public bool Refresh()
 {
  var before=Snapshot();
  RefreshSources();RefreshApps();
  lock(gate){RebuildKeys();UpdateWeatherText();}
  return before!=Snapshot();
 }
 string Snapshot()=>JsonSerializer.Serialize(new{Sources,Apps,RecentKeys,FavoriteKeys,CurrentSourceId,WeatherText,WeatherSymbol});
 public void RefreshSources()
 {
  try{
   int value;if(vconf_get_int("memory/system/source_type",out value)==0)CurrentSourceId=value;
   IntPtr pointer=get_source_list();
   // libsource-api owns the returned string. Upstream Probe intentionally does not free it.
   string raw=pointer==IntPtr.Zero?"":Marshal.PtrToStringUTF8(pointer)??"";
   LastSourceJson=raw;
   var current=ParseSources(raw,sourcePorts);
   lock(gate){
    foreach(var source in current){
     string overrideName;if(preferences.SourceLabels.TryGetValue(source.Action,out overrideName)&&!string.IsNullOrWhiteSpace(overrideName))source.Name=overrideName.Trim();
     var known=preferences.KnownSources.FirstOrDefault(x=>x.Id==source.Id);
     if(known!=null&&IsGenericName(source.Name)&&!IsGenericName(known.Name))source.Name=known.Name;
    }
    Sources=current.Where(x=>x.Connected||!IsGenericName(x.Name)).Where(x=>!AudioOnly(x.Name)).OrderBy(x=>x.Port).ThenBy(x=>x.Id).ToList();
    // Cache only runtime-observed entries, never manufacture ports from ordinal numbers.
    foreach(var source in current){var known=preferences.KnownSources.FindIndex(x=>x.Id==source.Id);if(known<0)preferences.KnownSources.Add(source);else preferences.KnownSources[known]=source;}
    RebuildKeys();
   }
  }catch(Exception e){LastError="Sources: "+e.Message;}
 }
 public static List<SourceItem> ParseSources(string raw,IDictionary<int,int> sourcePorts=null)
 {
  if(string.IsNullOrWhiteSpace(raw))return new List<SourceItem>();
  using(var document=JsonDocument.Parse(raw)){
   var found=new List<SourceItem>();WalkSources(document.RootElement,found,null,null,sourcePorts??new Dictionary<int,int>{{13,1},{14,2},{15,3},{16,4}});
   return found.GroupBy(x=>x.Id).Select(x=>x.OrderByDescending(y=>y.Connected).First()).ToList();
  }
 }
 static void WalkSources(JsonElement element,List<SourceItem> found,bool? connection,string dictionaryKey,IDictionary<int,int> sourcePorts)
 {
  if(element.ValueKind==JsonValueKind.Array){foreach(var child in element.EnumerateArray())WalkSources(child,found,connection,null,sourcePorts);return;}
  if(element.ValueKind!=JsonValueKind.Object)return;
  string type=ReadString(element,"type","sourceType","source_type","source","kind");
  string name=ReadString(element,"editName","edit_name","detectedName","deviceName","device_name","label","name","sourceName","source_name");
  int id=ReadInt(element,"id","sourceId","source_id","sourceTypeId","source_type_id","sourceType","source_type");
  if(id<0&&dictionaryKey!=null){int parsed;if(int.TryParse(dictionaryKey,NumberStyles.Integer,CultureInfo.InvariantCulture,out parsed))id=parsed;}
  try{name=Uri.UnescapeDataString(name);}catch{}
  int port=ReadInt(element,"number","port","portNumber","port_number","sourceNumber","hdmi_port");
  string combined=type+" "+name;
  Match hdmi=Regex.Match(combined,@"\bHDMI\s*[-_:]?\s*(\d+)\b",RegexOptions.IgnoreCase);
  bool isHdmi=sourcePorts.ContainsKey(id)||type.Equals("HDMI",StringComparison.OrdinalIgnoreCase)||hdmi.Success||ReadBool(element,"isHdmi","is_hdmi")==true;
  if(port<1&&sourcePorts.TryGetValue(id,out var mappedPort))port=mappedPort;
  if(port<1&&hdmi.Success)int.TryParse(hdmi.Groups[1].Value,out port);
  if(isHdmi&&id>=0&&port>0){
   bool connected=ReadBool(element,"connectionState","connected","isConnected","is_connected","isconnected","connect","signal")??connection??false;
   found.Add(new SourceItem{Id=id,Port=port,Name=string.IsNullOrWhiteSpace(name)?"HDMI "+port:name.Trim(),Connected=connected,Description="HDMI "+port+(connected?" · Tilkoblet":" · Hvilemodus")});
  }
  foreach(var property in element.EnumerateObject()){
   if(property.Value.ValueKind!=JsonValueKind.Array&&property.Value.ValueKind!=JsonValueKind.Object)continue;
   bool? next=property.Name.Equals("connected",StringComparison.OrdinalIgnoreCase)?true:property.Name.Equals("disconnected",StringComparison.OrdinalIgnoreCase)?false:connection;
   WalkSources(property.Value,found,next,property.Name,sourcePorts);
  }
 }
 static bool TryProperty(JsonElement element,string key,out JsonElement value){foreach(var p in element.EnumerateObject())if(p.Name.Equals(key,StringComparison.OrdinalIgnoreCase)){value=p.Value;return true;}value=default(JsonElement);return false;}
 static string ReadString(JsonElement element,params string[] keys){foreach(string key in keys){JsonElement value;if(TryProperty(element,key,out value)&&value.ValueKind==JsonValueKind.String&&!string.IsNullOrWhiteSpace(value.GetString()))return value.GetString();}return "";}
 static int ReadInt(JsonElement element,params string[] keys){foreach(string key in keys){JsonElement value;int result;if(TryProperty(element,key,out value)&&((value.ValueKind==JsonValueKind.Number&&value.TryGetInt32(out result))||(value.ValueKind==JsonValueKind.String&&int.TryParse(value.GetString(),NumberStyles.Integer,CultureInfo.InvariantCulture,out result))))return result;}return -1;}
 static bool? ReadBool(JsonElement element,params string[] keys){foreach(string key in keys){JsonElement value;if(!TryProperty(element,key,out value))continue;if(value.ValueKind==JsonValueKind.True)return true;if(value.ValueKind==JsonValueKind.False)return false;if(value.ValueKind==JsonValueKind.Number)return value.GetDouble()!=0;if(value.ValueKind==JsonValueKind.String){string text=value.GetString().ToLowerInvariant();if(text=="true"||text=="yes"||text=="connected"||text=="1")return true;if(text=="false"||text=="no"||text=="disconnected"||text=="0")return false;}}return null;}
 static bool IsGenericName(string name)=>string.IsNullOrWhiteSpace(name)||Regex.IsMatch(name.Trim(),@"^(HDMI\s*\d*\s*(\(?(e?ARC)\)?)?|input\s*\d*|source\s*\d*|unknown|none|unassigned|no\s*device|not\s*connected|disconnected|null|undefined)$",RegexOptions.IgnoreCase);
 static bool AudioOnly(string name)=>Regex.IsMatch(name??"",@"\b(sonos|sound\s*bar|speaker|amplifier|audio|receiver|home\s*theat(er|re)|hjemmekino|lydplanke|h[oø]yttaler)\b",RegexOptions.IgnoreCase);

 public static List<NativeAppInfo> DiscoverApps(out int result)
 {
  var found=new List<NativeAppInfo>();
  AppInfoCallback callback=(handle,data)=>{
   try{
    var item=new NativeAppInfo{Id=ReadAppString(handle,app_info_get_app_id),Name=ReadAppString(handle,app_info_get_label),Icon=ReadAppString(handle,app_info_get_icon),Package=ReadAppString(handle,app_info_get_package),Type=ReadAppString(handle,app_info_get_type)};
    bool hidden;item.NoDisplay=app_info_is_nodisplay(handle,out hidden)==0&&hidden;
    if(!string.IsNullOrWhiteSpace(item.Id))found.Add(item);
   }catch{/* Never let a managed exception cross a native callback. */}
   return true;
  };
  result=app_manager_foreach_app_info(callback,IntPtr.Zero);GC.KeepAlive(callback);
  return found.GroupBy(x=>x.Id).Select(x=>x.First()).ToList();
 }
 static string ReadAppString(IntPtr handle,GetAppString getter){IntPtr pointer=IntPtr.Zero;try{return getter(handle,out pointer)==0&&pointer!=IntPtr.Zero?Marshal.PtrToStringUTF8(pointer)??"":"";}finally{if(pointer!=IntPtr.Zero)free(pointer);}}
 public void RefreshApps()
 {
  try{
   int result;var installed=DiscoverApps(out result);LastAppResult=result;
   if(result!=0){LastError="Apps: native result "+result;return;}
   InstalledApps=installed;
   var catalog=new List<LibraryApp>();string catalogPath=Path.Combine(resourceRoot,"apps.json");
   if(File.Exists(catalogPath))catalog=JsonSerializer.Deserialize<List<LibraryApp>>(File.ReadAllText(catalogPath),JsonOptions)??catalog;
   var candidates=new List<LibraryApp>();
   foreach(var actual in installed.Where(AllowedApp)){
    var known=catalog.FirstOrDefault(x=>x.Id==actual.Id);
    candidates.Add(known??new LibraryApp{Name=string.IsNullOrWhiteSpace(actual.Name)?actual.Id:actual.Name,Id=actual.Id,Asset="",Description="Installert på TV-en",Fit="contain",Background="#FFFFFF",Width=640,Height=360});
   }
   // Prefer the real store launcher over the native YouTube duplicate when both exist.
   if(candidates.Any(x=>x.Id=="9Ur5IzDKqV.TizenYouTube"))candidates.RemoveAll(x=>x.Id=="com.samsung.tv.cobalt-yt");
   Apps=candidates.OrderBy(x=>catalog.FindIndex(y=>y.Id==x.Id)<0?int.MaxValue:catalog.FindIndex(y=>y.Id==x.Id)).ThenBy(x=>x.Name,StringComparer.CurrentCultureIgnoreCase).ToList();
   lock(gate){RebuildKeys();}
  }catch(Exception e){LastError="Apps: "+e.Message;}
 }
 static readonly HashSet<string> ConsumerSystemApps=new HashSet<string>{"org.tizen.netflix-app","org.tizen.primevideo","com.samsung.tv.aria-video","com.samsung.tv.cobalt-yt","com.samsung.tv.cobalt-ytkids","com.samsung.tv.cobalt-yttv","com.samsung.tv.cobalt-gpm","org.tizen.browser"};
 static readonly HashSet<string> HiddenApps=new HashSet<string>{"DuskLaunch.Dusk","com.samsung.tv.bixby-promotion","NuvioTV001.EngineFsService","NuvioTV001.PluginService","2025ePop01.SmartTV","yL49PNFmjW.PromotionApp","w2VwCK465D.OTApp","25emanual2.emanual"};
 static bool AllowedApp(NativeAppInfo app){if(app.NoDisplay||HiddenApps.Contains(app.Id)||Regex.IsMatch(app.Id,@"(dummy|\.service$|EngineFsService$|PluginService$)",RegexOptions.IgnoreCase))return false;if(ConsumerSystemApps.Contains(app.Id))return true;if(Regex.IsMatch(app.Id,@"^(org\.tizen|com\.samsung|com\.sausng|org\.volt)(\.|$)",RegexOptions.IgnoreCase))return false;return app.Id.IndexOf('.')>0;}
 bool Exists(string key)=>Sources.Any(x=>x.Action==key)||Apps.Any(x=>"app:"+x.Id==key);
 static bool ValidKey(string key)=>!string.IsNullOrWhiteSpace(key)&&(key.StartsWith("app:",StringComparison.Ordinal)&&key.Length>4||key.StartsWith("hdmi:",StringComparison.Ordinal)&&int.TryParse(key.Substring(5),out _));
 void RebuildKeys()
 {
  RecentKeys=preferences.RecentKeys.Where(Exists).Distinct().Take(12).ToList();
  FavoriteKeys=preferences.ManualFavorites?preferences.FavoriteKeys.Where(Exists).Distinct().ToList():Sources.Select(x=>x.Action).Concat(preferences.Usage.Where(x=>x.Key.StartsWith("app:")&&x.Count>0&&Exists(x.Key)).OrderByDescending(x=>x.Count).ThenByDescending(x=>x.LastUsed).Take(5).Select(x=>x.Key)).Distinct().ToList();
 }
 public void RecordLaunch(string key)
 {
  lock(gate){if(!Exists(key))return;var entry=preferences.Usage.FirstOrDefault(x=>x.Key==key);if(entry==null){entry=new NativeRecent{Key=key};preferences.Usage.Add(entry);}entry.Count=Math.Min(int.MaxValue-1,entry.Count)+1;entry.LastUsed=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();preferences.Usage=preferences.Usage.OrderByDescending(x=>x.LastUsed).Take(256).ToList();preferences.RecentKeys=new[]{key}.Concat(preferences.RecentKeys.Where(x=>x!=key)).Take(12).ToList();RebuildKeys();Save();}
 }
 public void ToggleFavorite(string key)
 {
  lock(gate){if(!Exists(key))return;if(!preferences.ManualFavorites){preferences.FavoriteKeys=new List<string>(FavoriteKeys);preferences.ManualFavorites=true;}if(!preferences.FavoriteKeys.Remove(key))preferences.FavoriteKeys.Add(key);RebuildKeys();Save();}
 }
 public void MoveFavorite(string key,int delta)
 {
  lock(gate){if(!Exists(key)||delta==0)return;if(!preferences.ManualFavorites){preferences.FavoriteKeys=new List<string>(FavoriteKeys);preferences.ManualFavorites=true;}int from=preferences.FavoriteKeys.IndexOf(key);if(from<0)return;int to=Math.Clamp(from+delta,0,preferences.FavoriteKeys.Count-1);preferences.FavoriteKeys.RemoveAt(from);preferences.FavoriteKeys.Insert(to,key);RebuildKeys();Save();}
 }
 void ImportDuskSeed()
 {
  string seedPath=Path.Combine(resourceRoot,"dusk-settings.json");
  if(!File.Exists(seedPath))return;
  try{
   using(var document=JsonDocument.Parse(File.ReadAllText(seedPath))){
    var root=document.RootElement;JsonElement node;
    preferences.ManualFavorites=ReadString(root,"favoriteMode")=="manual";
    if(TryProperty(root,"favoriteIds",out node)&&node.ValueKind==JsonValueKind.Array)preferences.FavoriteKeys=node.EnumerateArray().Where(x=>x.ValueKind==JsonValueKind.String).Select(x=>ImportKey(x.GetString())).Where(ValidKey).Distinct().ToList();
    if(TryProperty(root,"recentIds",out node)&&node.ValueKind==JsonValueKind.Array)preferences.RecentKeys=node.EnumerateArray().Where(x=>x.ValueKind==JsonValueKind.String).Select(x=>ImportKey(x.GetString())).Where(ValidKey).Distinct().Take(12).ToList();
    if(TryProperty(root,"launchCounts",out node)&&node.ValueKind==JsonValueKind.Object)foreach(var property in node.EnumerateObject()){int count;if(property.Value.TryGetInt32(out count)&&count>0)preferences.Usage.Add(new NativeRecent{Key="app:"+property.Name,Count=count,LastUsed=0});}
    if(TryProperty(root,"sourceLabels",out node)&&node.ValueKind==JsonValueKind.Object)foreach(var property in node.EnumerateObject()){string key=ImportKey(property.Name);if(ValidKey(key)&&property.Value.ValueKind==JsonValueKind.String)preferences.SourceLabels[key]=property.Value.GetString();}
    // Root writes this explicit option only for the user's requested Nuvio shortcut.
    if(TryProperty(root,"pinNuvio",out node)&&node.ValueKind==JsonValueKind.True&&!preferences.FavoriteKeys.Contains("app:NuvioTV001.NuvioTV"))preferences.FavoriteKeys.Add("app:NuvioTV001.NuvioTV");
   }
  }catch(Exception e){LastError="Dusk import: "+e.Message;}
 }
 string ImportKey(string key)
 {
  if(key!=null&&key.StartsWith("input:HDMI:",StringComparison.Ordinal)){int port;if(int.TryParse(key.Substring(11),out port)){foreach(var pair in sourcePorts)if(pair.Value==port)return "hdmi:"+pair.Key;}return "";}
  return key??"";
 }
 void Save()
 {
  try{string directory=Path.GetDirectoryName(statePath);Directory.CreateDirectory(directory);string temporary=statePath+".tmp";File.WriteAllText(temporary,JsonSerializer.Serialize(preferences,JsonOptions));File.Move(temporary,statePath,true);}catch(Exception e){LastError="Save: "+e.Message;}
 }
 public void RefreshWeather()
 {
  lock(gate){long now=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();UpdateWeatherText();if(weatherRunning||preferences.Weather!=null&&now-preferences.Weather.FetchedAt<25*60*1000||now-lastWeatherAttempt<60*1000)return;weatherRunning=true;lastWeatherAttempt=now;}
  _=FetchWeather();
 }
 async Task FetchWeather()
 {
  try{
   const string endpoint="https://api.open-meteo.com/v1/forecast?latitude=59.9139&longitude=10.7522&current=temperature_2m%2Cweather_code%2Cis_day&timeformat=unixtime&timezone=Europe%2FOslo";
   string raw=await WeatherClient.GetStringAsync(endpoint).ConfigureAwait(false);
   if(raw.Length>65536)throw new InvalidDataException("Unexpected weather response size");
   using(var document=JsonDocument.Parse(raw)){
    JsonElement current=document.RootElement.GetProperty("current");double temperature=current.GetProperty("temperature_2m").GetDouble();int code=current.GetProperty("weather_code").GetInt32();long observed=current.GetProperty("time").GetInt64()*1000;
    long now=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();if(!double.IsFinite(temperature)||temperature< -70||temperature>65||observed>now+15*60*1000||now-observed>3*60*60*1000)throw new InvalidDataException("Stale or invalid weather observation");
    lock(gate){preferences.Weather=new NativeWeather{FetchedAt=now,ObservedAt=observed,Temperature=temperature,Code=code,IsDay=current.GetProperty("is_day").GetInt32()==1};UpdateWeatherText();Save();}
   }
  }catch(Exception e){LastError="Weather: "+e.Message;}finally{lock(gate){weatherRunning=false;}}
 }
 void UpdateWeatherText()
 {
  var item=preferences.Weather;long now=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
  if(item==null||now-item.ObservedAt>3*60*60*1000||item.ObservedAt>now+15*60*1000){WeatherText="";WeatherSymbol="";return;}
  string description=item.Code==0?"Klart":item.Code==1?"Lettskyet":item.Code==2?"Delvis skyet":item.Code==3?"Skyet":item.Code==45||item.Code==48?"Tåke":item.Code>=71&&item.Code<=77||item.Code==85||item.Code==86?"Snø":item.Code>=95?"Torden":item.Code>=51?"Regn":"Vær";
  WeatherSymbol=item.Code==0?(item.IsDay?"☀":"☾"):description=="Snø"?"❄":description=="Regn"||description=="Torden"?"☂":"☁";
  WeatherText=Math.Round(item.Temperature,MidpointRounding.AwayFromZero).ToString("0",CultureInfo.InvariantCulture)+"° · "+description;
 }
}
