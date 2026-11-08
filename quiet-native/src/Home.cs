// Adapted from Quiet Tizen cf518d7a9278dca7990e27756218258e44044a29.
// Original MIT license and asset/font notices are retained with this build.
using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Linq;
using System.Xml.Linq;
using System.Text.RegularExpressions;
using Tizen.NUI;
using Tizen.NUI.BaseComponents;
using Tizen.Applications;

public sealed class QuietHome : NUIApplication
{
 const string Root="/opt/usr/apps/com.samsung.tv.bixby-promotion/res/quiet-home/";
 const string StateFile="/tmp/quiet-home-state.txt";
 const string LocalData="/opt/usr/home/owner/apps_rw/com.samsung.tv.bixby-promotion/data/quiet-local.json";
 NativeData data; NativeAmbient ambient; bool resumed=true; string ambientStatus="";
 TextLabel dateLabel,weatherLabel;
 Tizen.NUI.Timer holdTimer; bool holdPending,holdHandled,suppressEnterRelease;
 bool optionsOpen,movingFavorite; int optionIndex; string optionAction="",optionName="";
 readonly List<View> optionViews=new List<View>();
 readonly List<string> optionCommands=new List<string>();
 sealed class RuntimeCard { public string Name,Action,Description,Asset; public LibraryApp App; public bool Source; }
 sealed class Slot { public View Card,Shadow,FocusRing; public TextLabel Label; public string Name,Action,Description; public int Row,X,Y,W,H; }
 sealed class CachedArtwork { public ImageView Image; public View Parent; }
 readonly List<Slot> slots=new List<Slot>();
 readonly List<View> scene=new List<View>();
 readonly Dictionary<string,CachedArtwork> artworkCache=new Dictionary<string,CachedArtwork>();
 readonly Dictionary<string,float> assetAspects=new Dictionary<string,float>();
 View artworkPreloadRoot;
 int artworkViewsCreated,pageArtworkCold;
 List<LibraryApp> library=new List<LibraryApp>();
 int libraryPage;
 string libraryError="";
 TextLabel clock, notice, detail;
 TextLabel[] tabs;
 TextLabel toolbarHint;
 Animation focusAnimation;
 bool fontLoaded;
 Tizen.NUI.Timer clockTimer;
 int page, selected, area=1, lastSource;
 string editing="";
 long buildMilliseconds;
 int currentVolume, currentMute;
 readonly Dictionary<string,string> settingValues=new Dictionary<string,string>();
 readonly Dictionary<string,string> settingNames=new Dictionary<string,string>();
 readonly Dictionary<string,bool> settingEnabled=new Dictionary<string,bool>();
 double brightnessMin,brightnessMax=50,brightnessStep=1;
 string lastKey="";
 long lastKeyAt;
 readonly Color ink=new Color(0.114f,0.114f,0.122f,1);
 readonly Color muted=new Color(0.431f,0.431f,0.451f,1);
 readonly Color white=new Color(1,1,1,1);
 readonly Color accent=new Color(0.24f,0.29f,0.72f,1);
 [DllImport("libsource-api.so")] static extern int connect_source(int sourceType);
 [DllImport("libvconf.so.0")] static extern int vconf_get_int(string key,out int value);
 [DllImport("libcapi-appfw-app-control.so.0")] static extern int app_control_create(out IntPtr app);
 [DllImport("libcapi-appfw-app-control.so.0")] static extern int app_control_set_app_id(IntPtr app,string id);
 [DllImport("libcapi-appfw-app-control.so.0")] static extern int app_control_set_operation(IntPtr app,string operation);
 [DllImport("libcapi-appfw-app-control.so.0")] static extern int app_control_add_extra_data(IntPtr app,string key,string value);
 [DllImport("libcapi-appfw-app-control.so.0")] static extern int app_control_send_launch_request(IntPtr app,IntPtr cb,IntPtr data);
 [DllImport("libcapi-appfw-app-control.so.0")] static extern int app_control_destroy(IntPtr app);
 [DllImport("libcapi-media-sound-manager-tv.so.0")] static extern int sound_manager_get_master_volume(out int value);
 [DllImport("libcapi-media-sound-manager-tv.so.0")] static extern int sound_manager_set_master_volume(int value);
 [DllImport("libcapi-media-sound-manager-tv.so.0")] static extern int sound_manager_get_master_mute(out int value);
 [DllImport("libcapi-media-sound-manager-tv.so.0")] static extern int sound_manager_set_master_mute(int value);
 [DllImport("libsettings-api.so")] static extern bool CSharpGetItemValue(string id,out string name,out string value);
 [DllImport("libsettings-api.so")] static extern bool CSharpSetItemValue(string id,string value);
 [DllImport("libsettings-api.so")] static extern bool CSharpGetItemEnable(string id,out bool enabled);
 [DllImport("libsettings-api.so")] static extern bool CSharpGetItemMinMaxStepValue(string id,out double min,out double max,out double step);



 protected override void OnCreate()
 {
  base.OnCreate();
  try{fontLoaded=FontClient.Instance.AddCustomFontDirectory(Root+"fonts");}catch{fontLoaded=false;}
  try{data=new NativeData(Root,LocalData);data.Refresh();data.RefreshWeather();library=data.Apps;}catch(Exception ex){libraryError=ex.Message;throw;}
  ambient=new NativeAmbient(Root+"ambient/",activity=>{ambientStatus=activity;WriteState(activity);});
  Window.Default.BackgroundColor=new Color(0.96f,0.96f,0.97f,1);
  Window.Default.Title="Quiet Home";Window.Default.KeyEvent+=OnKey;
  PreloadArtwork();
  RefreshSource();selected=SourceIndex();BuildPage();
  clockTimer=new Tizen.NUI.Timer(30000);
  clockTimer.Tick+=(s,e)=>{RefreshData();UpdateHeader();return true;};clockTimer.Start();
  holdTimer=new Tizen.NUI.Timer(650);holdTimer.Tick+=(s,e)=>{if(holdPending){holdHandled=true;OpenOptions();}return false;};WriteState("created");
 }
 protected override void OnResume(){base.OnResume();resumed=true;clockTimer?.Start();RefreshData();RefreshSource();if(tabs!=null&&page==0)BuildPage();if(tabs!=null&&page==2){RefreshSettings();UpdateFocus(false);}ambient?.SetActive(page==0);WriteState("resumed");}
 protected override void OnAppControlReceived(AppControlReceivedEventArgs e){base.OnAppControlReceived(e);string requested; if(e.ReceivedAppControl.ExtraData.TryGet("quiet_page",out requested)&&requested=="settings")OpenSettings("settings_request");else if(requested=="apps"){page=1;area=1;selected=0;BuildPage();}else Home("home_request");}
 protected override void OnPause(){base.OnPause();resumed=false;editing="";CancelHold();CloseOptions();clockTimer?.Stop();ambient?.SetActive(false);WriteState("paused");}
 void Home(string activity){clockTimer?.Start();CancelHold();movingFavorite=false;RefreshSource();if(tabs!=null){page=0;area=1;editing="";selected=SourceIndex();BuildPage();WriteState(activity);}}
 void OpenSettings(string activity){clockTimer?.Start();page=2;area=1;selected=0;editing="";BuildPage();WriteState(activity);}
 void RefreshSource(){try{int n=data.CurrentSourceId;if(data.Sources.Any(source=>source.Id==n))lastSource=n;}catch{}}
 int SourceIndex(){return 0;}
 void UpdateHeader(){if(clock!=null)clock.Text=DateTime.Now.ToString("HH:mm");if(dateLabel!=null)dateLabel.Text=DateTime.Now.ToString("dddd, MMMM d",CultureInfo.GetCultureInfo("en-US"));if(weatherLabel!=null)weatherLabel.Text=string.IsNullOrWhiteSpace(data.WeatherText)?"":data.WeatherText+" · "+data.WeatherCity;}
 void RefreshData(){try{string previous=SelectedAction();bool changed=data.Refresh();data.RefreshWeather();library=data.Apps;RefreshSource();if(changed&&tabs!=null&&!optionsOpen&&!movingFavorite)BuildPage(previous);}catch(Exception ex){libraryError=ex.Message;}}
 string SelectedAction(){return selected>=0&&selected<slots.Count?slots[selected].Action:"";}
 View Add(View v){scene.Add(v);Window.Default.Add(v);return v;}
 View Panel(int x,int y,int w,int h,Color color,int radius=18){return Add(new View{Position2D=new Position2D(x,y),Size2D=new Size2D(w,h),BackgroundColor=color,CornerRadius=new Vector4(radius,radius,radius,radius)});}
 TextLabel Text(string text,int x,int y,int w,int h,float size,Color color,string face="Medium")
 {return (TextLabel)Add(Label(text,x,y,w,h,size,color,face));}
 TextLabel Label(string text,int x,int y,int w,int h,float size,Color color,string face="Medium")
 {return new TextLabel{Text=text,Position2D=new Position2D(x,y),Size2D=new Size2D(w,h),PixelSize=size,FontFamily="Quiet Inter "+face,TextColor=color,VerticalAlignment=VerticalAlignment.Center};}
 void BuildPage(string restoreAction=null)
 {
  CloseOptions();ambient?.Detach();
  var stopwatch=Stopwatch.StartNew();
  if(focusAnimation!=null){focusAnimation.Stop();focusAnimation.Dispose();focusAnimation=null;}
  ParkArtwork();pageArtworkCold=0;
  foreach(var v in scene){Window.Default.Remove(v);v.Dispose();}scene.Clear();slots.Clear();
  Add(new ImageView{ResourceUrl=Root+"free-background.svg",Size2D=new Size2D(1920,1080)});
  Text(page==0?"Home":page==1?"Your Apps":"Quick Settings",76,55,1350,100,76,ink,"Display");
  tabs=new TextLabel[3];string[] nav={"Inputs","Apps","Settings"};int[] tx={54,221,366};int[] tw={153,131,182};
  for(int i=0;i<3;i++){tabs[i]=Text(nav[i],tx[i],158,tw[i],68,30,muted);tabs[i].HorizontalAlignment=HorizontalAlignment.Center;}
  clock=Text(DateTime.Now.ToString("HH:mm"),1250,65,290,45,32,ink);clock.HorizontalAlignment=HorizontalAlignment.End;
  dateLabel=Text("",1120,111,420,32,20,muted);dateLabel.HorizontalAlignment=HorizontalAlignment.End;
  weatherLabel=Text("",1040,150,500,32,20,muted);weatherLabel.HorizontalAlignment=HorizontalAlignment.End;UpdateHeader();
  if(page==0){
   var features=HomeCards();
   for(int i=0;i<features.Count;i++){
    var entry=features[i];int x=i==0?80:i==1?1160:1520,w=i==0?1040:320;
    var feature=Feature(entry.Name,entry.Action,entry.Description,entry.Asset,x,262,w,442,i==0?"hero":"portrait",i==0,entry.App);
    if(i==0){
     ambient.Attach(feature.Card);
     feature.Card.Add(new View{Size2D=new Size2D(1040,442),BackgroundColor=new Color(.035f,.06f,.13f,.24f),CornerRadius=new Vector4(18,18,18,18)});
     if(entry.Source&&entry.App!=null&&entry.App.Asset=="brands/appletv.svg")AddBrandImage(feature.Card,"brands/appletv.svg",44,18,160,160);else feature.Card.Add(Label(entry.Name.ToUpperInvariant(),44,52,750,40,24,new Color(.9f,.93f,1,1),"Semibold"));
     feature.Card.Add(Label(entry.Source?"Welcome home.":"Something good is on.",40,124,850,100,64,white,"Display"));
     feature.Card.Add(Label(entry.Source?"Switch to "+SourcePort(entry.Action):"Open "+entry.Name,46,246,870,48,30,new Color(.9f,.93f,1,1)));
    }
   }
  }else if(page==1){
   BuildLibrary();
  }else{
   RefreshSettings();
   SettingsTile("Volume","volume","Select · Left / Right to adjust","volume",0,0);
   SettingsTile("Brightness","brightness","Select · Left / Right to adjust","brightness",1,0);
   SettingsTile("Game Mode","game-mode","Select to switch on or off","game",2,0);
   SettingsTile("Sleep Timer","sleeptimer","Select to change the timer","sleep",3,0);
   SettingsTile("Sound","mute","Select to mute or unmute","mute",0,1);
   SettingsTile("Controllers","controllers","DualSense · hold PS + Create","controller",1,1);
   SettingsTile("Network","network","Wi-Fi and connection setup","network",2,1);
   SettingsTile("Advanced","app:org.tizen.menu","Open Samsung's full settings","settings",3,1);
  }
  if(page==0){
   Add(new ImageView{ResourceUrl=Root+"glass-shelf.svg",Position2D=new Position2D(0,784),Size2D=new Size2D(1920,280)});
   var favorites=FavoriteCards();
   for(int i=0;i<Math.Min(6,favorites.Count);i++)Dock(favorites[i].Name,favorites[i].Action,favorites[i].Asset,i,favorites[i].App);
   if(favorites.Count<6)Dock("Your Apps","page:1","apps",favorites.Count);
  }
  Toolbar("Apps","page:1","apps",0);Toolbar("Inputs","page:0","hdmi",1);Toolbar("Settings","page:2","settings",2);
  toolbarHint=Text("",1530,170,310,45,24,ink);toolbarHint.HorizontalAlignment=HorizontalAlignment.End;
  detail=Text("",80,1030,1320,35,20,muted);
  notice=Text("",80,1008,1760,34,22,accent);
  var hint=Text(page==1?(AppLibrary.PageCount(library.Count)<=1?"Select to open · Back to Home":libraryPage+1<AppLibrary.PageCount(library.Count)?"Down for more apps · Back to Home":"Up for previous apps · Back to Home"):"Back to input",1310,1030,530,35,20,muted);hint.HorizontalAlignment=HorizontalAlignment.End;
  if(restoreAction!=null){int found=slots.FindIndex(slot=>slot.Action==restoreAction&&slot.Row==(area==2?1:0));if(found<0)found=slots.FindIndex(slot=>slot.Action==restoreAction);if(found>=0)selected=found;}
  selected=Math.Max(0,Math.Min(selected,slots.Count-1));UpdateFocus(false);
  buildMilliseconds=stopwatch.ElapsedMilliseconds;ambient?.SetActive(page==0&&resumed);WriteState("page_ready");
 }
 RuntimeCard ResolveCard(string action)
 {
  if(string.IsNullOrEmpty(action))return null;
  if(action.StartsWith("hdmi:")){
   int id;if(!int.TryParse(action.Substring(5),out id))return null;
   var source=data.Sources.FirstOrDefault(item=>item.Id==id);if(source==null)return null;
   string name=source.Name??("HDMI "+source.Port),lower=name.ToLowerInvariant();
   string asset=lower.Contains("playstation")||lower.Contains("ps5")?"feature-ps5.svg":lower.Contains("nintendo")||lower.Contains("switch")?"feature-switch.svg":"feature-hdmi.svg";
   LibraryApp brand=null;if(lower.Contains("apple")&&lower.Contains("tv"))brand=new LibraryApp{Id=action,Name=name,Asset="brands/appletv.svg",Fit="contain",Background="#171717",Width=24,Height=24};else if(lower.Contains("playstation")||lower.Contains("ps5"))brand=new LibraryApp{Id=action,Name=name,Asset="brands/playstation5.svg",Fit="contain",Background="#3346B5",Width=24,Height=6};
   return new RuntimeCard{Name=name,Action=action,Description="HDMI "+source.Port+" · "+(source.Connected?"Connected":"Idle"),Asset=asset,Source=true,App=brand};
  }
  if(action.StartsWith("app:")){
   var app=library.FirstOrDefault(item=>item.Id==action.Substring(4));
   if(app!=null)return new RuntimeCard{Name=app.Name,Action=action,Description="Installed on your TV",Asset=app.Asset,App=app};
  }
  return null;
 }
 string SourcePort(string action){int id;var source=action.StartsWith("hdmi:")&&int.TryParse(action.Substring(5),out id)?data.Sources.FirstOrDefault(item=>item.Id==id):null;return source==null?"input":"HDMI "+source.Port;}
 List<RuntimeCard> FavoriteCards(){return data.FavoriteKeys.Select(ResolveCard).Where(item=>item!=null).ToList();}
 List<RuntimeCard> HomeCards()
 {
  var sources=data.Sources.Select(source=>ResolveCard("hdmi:"+source.Id)).Where(item=>item!=null).ToList();
  var favorites=FavoriteCards();var recent=data.RecentKeys.Select(ResolveCard).Where(item=>item!=null).Take(2).ToList();
  var primary=sources.FirstOrDefault(item=>item.Action=="hdmi:"+lastSource)??sources.FirstOrDefault()??favorites.FirstOrDefault()??recent.FirstOrDefault();
  if(primary==null)return new List<RuntimeCard>{new RuntimeCard{Name="Your Apps",Action="page:1",Description="Open installed apps",Asset="free-dock-apps.svg"}};
  var fallback=sources.Concat(favorites).Concat(library.Select(app=>ResolveCard("app:"+app.Id))).Where(item=>item!=null);
  foreach(var item in fallback){if(recent.Count>=2)break;if(item.Action!=primary.Action&&!recent.Any(existing=>existing.Action==item.Action))recent.Add(item);}
  var result=new List<RuntimeCard>{primary};result.AddRange(recent);
  if(result.Count<3)result.Add(new RuntimeCard{Name="Your Apps",Action="page:1",Description="Open installed apps",Asset="free-dock-apps.svg"});
  if(result.Count<3)result.Add(new RuntimeCard{Name="Settings",Action="page:2",Description="Your everyday controls",Asset="quick-settings.svg"});
  return result;
 }
 void CancelHold(){holdTimer?.Stop();holdPending=false;holdHandled=false;}
 void OpenOptions()
 {
  if(page>1||area==0||area==3||editing!=""||selected<0||selected>=slots.Count){holdHandled=false;return;}var slot=slots[selected];
  if(!slot.Action.StartsWith("app:")&&!slot.Action.StartsWith("hdmi:")){holdHandled=false;return;}
  optionAction=slot.Action;optionName=slot.Name;optionsOpen=true;optionIndex=0;
  DrawOptions();
 }
 void AddOptionView(View view){optionViews.Add(view);Window.Default.Add(view);}
 void CloseOptions(){foreach(var view in optionViews){Window.Default.Remove(view);view.Dispose();}optionViews.Clear();optionsOpen=false;}
 void DrawOptions()
 {
  foreach(var view in optionViews){Window.Default.Remove(view);view.Dispose();}optionViews.Clear();optionCommands.Clear();
  bool favorite=data.FavoriteKeys.Contains(optionAction);
  optionCommands.Add("pin");if(favorite)optionCommands.Add("move");optionCommands.Add("cancel");
  AddOptionView(new View{Size2D=new Size2D(1920,1080),BackgroundColor=new Color(.07f,.09f,.16f,.58f)});
  var panel=new View{Position2D=new Position2D(580,265),Size2D=new Size2D(760,545),BackgroundColor=new Color(.96f,.97f,.99f,1),CornerRadius=new Vector4(30,30,30,30)};
  panel.Add(Label(optionName,42,30,680,65,36,ink,"Display"));
  panel.Add(Label("Your favorites stay on this TV.",42,98,680,40,21,muted));
  for(int i=0;i<optionCommands.Count;i++){
   string command=optionCommands[i],name=command=="pin"?(favorite?"Remove from favorites":"Add to favorites"):command=="move"?"Move favorite":"Cancel";
   var row=new View{Position2D=new Position2D(40,165+i*100),Size2D=new Size2D(680,78),BackgroundColor=i==optionIndex?accent:new Color(.88f,.9f,.95f,1),CornerRadius=new Vector4(15,15,15,15)};
   row.Add(Label(name,25,10,630,58,26,i==optionIndex?white:ink,"Semibold"));panel.Add(row);
  }
  AddOptionView(panel);
 }
 void HandleOptionsKey(string key)
 {
  if(key=="Up"||key=="Down"){optionIndex=(optionIndex+(key=="Up"?-1:1)+optionCommands.Count)%optionCommands.Count;DrawOptions();return;}
  if(key=="XF86Back"||key=="Back"||key=="Escape"){CloseOptions();CancelHold();return;}
  if(key!="Return"&&key!="KP_Enter")return;
  string command=optionCommands[optionIndex],action=optionAction;suppressEnterRelease=true;CloseOptions();CancelHold();
  if(command=="pin"){data.ToggleFavorite(action);BuildPage(action);}
  if(command=="move"){movingFavorite=true;area=2;BuildPage(action);notice.Text="Left / Right to move · Select or Back to finish";}
 }
 void HandleMoveKey(string key)
 {
  if(key=="Left"||key=="Right"){data.MoveFavorite(optionAction,key=="Left"?-1:1);area=2;BuildPage(optionAction);notice.Text="Left / Right to move · Select or Back to finish";}
  else if(key=="Return"||key=="KP_Enter"||key=="XF86Back"||key=="Back"||key=="Escape"){movingFavorite=false;if(key=="Return"||key=="KP_Enter")suppressEnterRelease=true;BuildPage(optionAction);}
 }
 void BuildLibrary()
 {
  if(library.Count==0){Text("Apps could not be loaded.",80,300,1500,70,32,ink);return;}
  libraryPage=Math.Clamp(libraryPage,0,AppLibrary.PageCount(library.Count)-1);
  int start=libraryPage*AppLibrary.PageSize,count=AppLibrary.VisibleCount(library.Count,libraryPage);
  for(int i=0;i<count;i++)
  {
   var app=library[start+i];int x=80+(i%5)*358,y=266+(i/5)*245,w=328,h=184;
   var shadow=Panel(x-5,y+7,w+10,h+8,new Color(.12f,.15f,.25f,.08f),22);
   var card=Panel(x+w/2,y+h/2,w,h,HexColor(app.Background),18);
   card.PivotPoint=new Position(.5f,.5f,.5f);card.PositionUsesPivotPoint=true;
   AddAppArtwork(card,app,w,h);
   var ring=new View{Size2D=new Size2D(w,h),BackgroundColor=new Color(0,0,0,0),CornerRadius=new Vector4(18,18,18,18),BorderlineWidth=4,BorderlineColor=accent,Opacity=0};
   card.Add(ring);
   var label=Text(app.Name,x,y+h+14,w,38,27,ink,"Semibold");
   slots.Add(new Slot{Card=card,Shadow=shadow,FocusRing=ring,Label=label,Name=app.Name,Action="app:"+app.Id,Description=app.Description,Row=i/5,X=x,Y=y,W=w,H=h});
  }
  Text(library.FindAll(a=>!a.Utility).Count+" apps",80,218,500,30,21,muted);
  if(AppLibrary.PageCount(library.Count)>1){
   var pages=Text((libraryPage+1)+" / "+AppLibrary.PageCount(library.Count),1650,218,190,30,21,muted);
   pages.HorizontalAlignment=HorizontalAlignment.End;
  }
 }
 Color HexColor(string hex)
 {
  try{int n=Convert.ToInt32(hex.TrimStart('#'),16);return new Color(((n>>16)&255)/255f,((n>>8)&255)/255f,(n&255)/255f,1);}
  catch{return white;}
 }
 void AddAppArtwork(View card,LibraryApp app,int w,int h)
 {
  if(string.IsNullOrWhiteSpace(app.Asset)){var name=Label(app.Name,20,0,w-40,h,Math.Min(29,h/4),ink,"Semibold");name.HorizontalAlignment=HorizontalAlignment.Center;card.Add(name);return;}
  if(app.Id.StartsWith("hdmi:")&&app.Asset=="brands/playstation5.svg"){
   card.Add(new ImageView{ResourceUrl=Root+"brands/source-playstation-bg.svg",Size2D=new Size2D(w,h)});
   if(h>300){AddBrandImage(card,"brands/playstation.svg",w/2-54,h/2-96,108,108);AddBrandImage(card,"brands/playstation5.svg",w/2-116,h/2+43,232,58);}
   else{AddBrandImage(card,"brands/playstation.svg",22,h/2-26,52,52);AddBrandImage(card,"brands/playstation5.svg",91,h/2-18,144,36);}return;
  }
  if(app.Name.IndexOf("stremio",StringComparison.OrdinalIgnoreCase)>=0){
   if(h>300){AddBrandImage(card,app.Asset,w/2-49,h/2-96,98,98);var wordmark=Label("stremio",10,h/2+27,w-20,58,38,white,"Semibold");wordmark.HorizontalAlignment=HorizontalAlignment.Center;card.Add(wordmark);}
   else{int side=h<150?43:57;int total=side+16+(h<150?120:145),left=(w-total)/2;AddBrandImage(card,app.Asset,left,h/2-side/2,side,side);card.Add(Label("stremio",left+side+16,h/2-23,total-side-16,46,h<150?27:31,white,"Semibold"));}return;
  }
  if(!app.Utility){
   var cached=GetArtwork(app,w,h);
   if(cached.Image.LoadingStatus!=ImageView.LoadingStatusType.Ready)pageArtworkCold++;
   cached.Parent.Remove(cached.Image);card.Add(cached.Image);cached.Parent=card;
  }
  if(app.Utility){
   card.Add(new View{Position2D=new Position2D(w/2-28,h/2-2),Size2D=new Size2D(56,4),BackgroundColor=ink});
   card.Add(new View{Position2D=new Position2D(w/2-2,h/2-28),Size2D=new Size2D(4,56),BackgroundColor=ink});
  }
 }
 void PreloadArtwork()
 {
  // Keep the real image views alive on the scene; moving between pages never
  // destroys their decoded textures. The zero-opacity parent is never drawn.
  artworkPreloadRoot=new View{Size2D=new Size2D(1920,1080),Opacity=0};
  Window.Default.Add(artworkPreloadRoot);
  foreach(var app in library)if(!app.Utility&&!string.IsNullOrWhiteSpace(app.Asset))GetArtwork(app,328,184);
  foreach(var id in new[]{"org.tizen.netflix-app","com.samsung.tv.cobalt-yt","org.tizen.primevideo"}){
   var app=library.Find(a=>a.Id==id);if(app!=null&&!string.IsNullOrWhiteSpace(app.Asset))GetArtwork(app,253,126);
  }
 }
 void AddBrandImage(View parent,string asset,int x,int y,int width,int height){var image=new ImageView{Position2D=new Position2D(x,y),Size2D=new Size2D(width,height),DesiredWidth=width*2,DesiredHeight=height*2,FittingMode=FittingModeType.ShrinkToFit,SamplingMode=SamplingModeType.BoxThenLanczos};parent.Add(image);image.ResourceUrl=Root+asset;}
 float AssetAspect(LibraryApp app)
 {
  float result;if(assetAspects.TryGetValue(app.Asset,out result))return result;
  result=app.Width>0&&app.Height>0?app.Width/(float)app.Height:1;
  try{
   string path=Root+app.Asset;
   if(path.EndsWith(".svg",StringComparison.OrdinalIgnoreCase)){
    var svg=XDocument.Load(path).Root;var box=Regex.Split(((string)svg.Attribute("viewBox")??"").Trim(),@"[ ,]+");float width,height;
    if(box.Length==4&&float.TryParse(box[2],NumberStyles.Float,CultureInfo.InvariantCulture,out width)&&float.TryParse(box[3],NumberStyles.Float,CultureInfo.InvariantCulture,out height)&&width>0&&height>0)result=width/height;
   }else if(path.EndsWith(".png",StringComparison.OrdinalIgnoreCase)){
    byte[] header=new byte[24];using(var file=File.OpenRead(path)){if(file.Read(header,0,24)==24){int width=(header[16]<<24)|(header[17]<<16)|(header[18]<<8)|header[19],height=(header[20]<<24)|(header[21]<<16)|(header[22]<<8)|header[23];if(width>0&&height>0)result=width/(float)height;}}
   }
  }catch{}assetAspects[app.Asset]=result;return result;
 }
 CachedArtwork GetArtwork(LibraryApp app,int w,int h)
 {
  string key=app.Id+"@"+w+"x"+h;
  if(artworkCache.TryGetValue(key,out var cached))return cached;
  bool cover=app.Fit=="cover",full=cover||app.Fit=="contain-full";
  int iw=full?w:Math.Min(w-54,240),ih=full?h:Math.Min(h-40,h>300?116:86);var crop=app.Crop;
  if(!full){float aspect=AssetAspect(app);if(iw/(float)ih>aspect)iw=Math.Max(1,(int)Math.Round(ih*aspect));else ih=Math.Max(1,(int)Math.Round(iw/aspect));}
  if(app.Asset=="brands/appletv.svg"){iw=Math.Min(w-50,h>300?230:175);ih=iw;}
  float cropWidth=crop==null?1:crop[2],cropHeight=crop==null?1:crop[3];
  var image=new ImageView{
   Position2D=new Position2D((w-iw)/2,(h-ih)/2),Size2D=new Size2D(iw,ih),
   FittingMode=cover?FittingModeType.ScaleToFill:FittingModeType.ShrinkToFit,
   SamplingMode=SamplingModeType.BoxThenLanczos,
   DesiredWidth=(int)Math.Ceiling(iw*2/cropWidth),DesiredHeight=(int)Math.Ceiling(ih*2/cropHeight),
   ReleasePolicy=ReleasePolicyType.Destroyed,CornerRadius=new Vector4(18,18,18,18)};
  if(crop!=null)image.PixelArea=new RelativeVector4(crop[0],crop[1],crop[2],crop[3]);
  cached=new CachedArtwork{Image=image,Parent=artworkPreloadRoot};
  artworkCache.Add(key,cached);artworkViewsCreated++;
  image.ResourceReady+=(s,e)=>WriteState("artwork_ready");
  artworkPreloadRoot.Add(image);image.ResourceUrl=Root+app.Asset;
  return cached;
 }
 void ParkArtwork()
 {
  foreach(var cached in artworkCache.Values)if(cached.Parent!=artworkPreloadRoot){
   cached.Parent.Remove(cached.Image);artworkPreloadRoot.Add(cached.Image);cached.Parent=artworkPreloadRoot;
  }
 }
 int ArtworkReadyCount()
 {
  int count=0;foreach(var cached in artworkCache.Values)
   if(cached.Image.LoadingStatus==ImageView.LoadingStatusType.Ready)count++;
  return count;
 }
 string ArtworkPending()
 {
  var pending=new List<string>();
  foreach(var entry in artworkCache)
   if(entry.Value.Image.LoadingStatus!=ImageView.LoadingStatusType.Ready)
    pending.Add(entry.Key+":"+entry.Value.Image.LoadingStatus);
  return string.Join(";",pending);
 }
 void SettingsTile(string name,string action,string description,string icon,int column,int row)
 {
  int x=80+column*450,y=262+row*332,w=410,h=286;
  var card=Panel(x+w/2,y+h/2,w,h,new Color(0,0,0,0),24);card.PivotPoint=new Position(.5f,.5f,.5f);card.PositionUsesPivotPoint=true;
  card.Add(new ImageView{ResourceUrl=Root+"quick-"+icon+".svg",Size2D=new Size2D(w,h)});
  card.Add(Label(name,26,25,348,42,29,white,"Semibold"));
  var value=Label("",26,151,358,56,38,white,"Display");card.Add(value);
  card.Add(Label(description,26,226,370,34,20,new Color(.95f,.96f,1,1)));
  slots.Add(new Slot{Card=card,Label=value,Name=name,Action=action,Description=description,Row=row,X=x,Y=y,W=w,H=h});
 }
 Slot Feature(string label,string action,string description,string asset,int x,int y,int w,int h,string shape,bool isAmbient=false,LibraryApp app=null)
 {
  var shadow=Add(new ImageView{ResourceUrl=Root+"shadow-"+shape+".svg",Position2D=new Position2D(x-24,y-10),Size2D=new Size2D(w+48,h+64)});
  var card=Panel(x+w/2,y+h/2,w,h,new Color(0,0,0,0),18);card.PivotPoint=new Position(.5f,.5f,.5f);card.PositionUsesPivotPoint=true;
  if(!isAmbient){if(app!=null){card.BackgroundColor=HexColor(app.Background);AddAppArtwork(card,app,w,h);}else card.Add(new ImageView{ResourceUrl=Root+asset,Size2D=new Size2D(w,h)});}
  var caption=Text(label,x,716,w,45,31,ink,"Semibold");
  Text(description,x,760,w,36,24,muted);
  var slot=new Slot{Card=card,Shadow=shadow,Label=caption,Name=label,Action=action,Description=description,Row=0,X=x,Y=y,W=w,H=h};slots.Add(slot);return slot;
 }
 void Dock(string label,string action,string asset,int n,LibraryApp configuredApp=null)
 {
  int x=(int)Math.Round(80+n*(253.333+48));
  var shadow=Add(new ImageView{ResourceUrl=Root+"shadow-dock.svg",Position2D=new Position2D(x-24,842),Size2D=new Size2D(301,190)});
  var card=Panel(x+126,917,253,126,new Color(0,0,0,0),22);card.PivotPoint=new Position(.5f,.5f,.5f);card.PositionUsesPivotPoint=true;
  var official=configuredApp??library.Find(a=>a.Id==action.Substring(action.IndexOf(':')+1));
  if(official!=null){card.BackgroundColor=HexColor(official.Background);AddAppArtwork(card,official,253,126);}
  else card.Add(new ImageView{ResourceUrl=Root+(asset.StartsWith("feature-")?asset:"free-dock-"+asset+".svg"),Size2D=new Size2D(253,126)});
  slots.Add(new Slot{Card=card,Shadow=shadow,Name=label,Action=action,Description=label,Row=1,X=x,Y=854,W=253,H=126});
 }
 void Toolbar(string label,string action,string icon,int n)
 {
  int x=1596+n*88;
  var card=Panel(x+34,103,68,68,new Color(0,0,0,0),34);card.PivotPoint=new Position(.5f,.5f,.5f);card.PositionUsesPivotPoint=true;
  card.Add(new ImageView{ResourceUrl=Root+"glass-button.svg",Position2D=new Position2D(-16,-16),Size2D=new Size2D(100,112)});
  card.Add(new ImageView{ResourceUrl=Root+"symbol-"+icon+".png",Color=ink,Position2D=new Position2D(19,19),Size2D=new Size2D(30,30)});
  slots.Add(new Slot{Card=card,Name=label,Action=action,Description=label,Row=-1,X=x,Y=69,W=68,H=68});
 }
 List<int> Row(int row){var r=new List<int>();for(int i=0;i<slots.Count;i++)if(slots[i].Row==row)r.Add(i);return r;}
 void UpdateFocus(bool animate=true)
 {
  if(focusAnimation!=null){focusAnimation.Stop();focusAnimation.Dispose();focusAnimation=null;}
  if(animate)focusAnimation=new Animation(180);
  for(int i=0;i<3;i++){tabs[i].TextColor=i==page?ink:muted;tabs[i].FontFamily="Quiet Inter "+(i==page?"Semibold":"Medium");tabs[i].BackgroundColor=area==0&&i==page?new Color(1,1,1,.7f):new Color(0,0,0,0);tabs[i].CornerRadius=new Vector4(34,34,34,34);}
  for(int i=0;i<slots.Count;i++){
   var s=slots[i];bool f=area!=0&&selected==i;
   bool fixedMedia=page==0&&s.Row==0&&s.W==1040;
   float scale=f?(page==1&&s.Row>=0?1.055f:page==2&&s.Row>=0?1.035f:s.Row==0?1.05f:s.Row==1?1.20f:1.10f):1;
   if(fixedMedia)scale=1;
   s.Card.BorderlineWidth=fixedMedia?(f?4:1):page==1&&s.Row>=0?(f?4:1):s.Row==0?1:0;
   s.Card.BorderlineColor=(fixedMedia||page==1)&&f?accent:new Color(1,1,1,f?.8f:.28f);
   var pos=new Position(s.X+s.W/2f,s.Y+s.H/2f-(f&&!fixedMedia&&(s.Row==0||page==1&&s.Row>=0)?5:0),0);
   if(animate){focusAnimation.AnimateTo(s.Card,"Scale",new Vector3(scale,scale,1));focusAnimation.AnimateTo(s.Card,"Position",pos);}else{s.Card.Scale=new Vector3(scale,scale,1);s.Card.Position=pos;}
   if(s.Shadow!=null)s.Shadow.Opacity=f?1:.55f;
   if(s.FocusRing!=null)s.FocusRing.Opacity=f?1:0;
   if(s.Label!=null&&page!=2)s.Label.FontFamily="Quiet Inter "+(f?"Semibold":"Medium");
   if(f)s.Card.RaiseToTop();
  }
  if(focusAnimation!=null)focusAnimation.Play();
  clock.Opacity=area==3?0:1;toolbarHint.Text=area==3?slots[selected].Name:"";
  detail.Text=page==1&&area==1&&slots[selected].Row>=0?slots[selected].Name+" · "+slots[selected].Description:area==0?"":area==2?slots[selected].Name:"";
  if(page==2){
   slots[0].Label.Text=currentVolume<0?"Unavailable":currentVolume.ToString();
   slots[1].Label.Text=SettingName("brightness");
   slots[2].Label.Text=SettingName("game-mode");
   slots[3].Label.Text=SettingName("sleeptimer");
   slots[4].Label.Text=currentMute<0?"Unavailable":currentMute==1?"Muted":"Sound on";
   slots[5].Label.Text="Pair controller";
   slots[6].Label.Text="Connections";
   slots[7].Label.Text="More settings";
   detail.Text="Your everyday controls";
  }
  if(editing!="")detail.Text="Left / Right to adjust "+editing+" · Select or Back to finish";
  WriteState("focus");
 }
 void OnKey(object sender,Window.KeyEventArgs e)
 {
  if(e.Key==null)return;string k=e.Key.KeyPressedName;
  if(e.Key.State==Key.StateType.Down){if(k=="XF86Home"){Home("home_key");return;}if(k=="XF86SysMenu"||k=="XF86More"){CancelHold();movingFavorite=false;OpenSettings("settings_key");return;}}
  if((k=="Return"||k=="KP_Enter")&&e.Key.State==Key.StateType.Up&&suppressEnterRelease){suppressEnterRelease=false;CancelHold();return;}
  if(optionsOpen){if(e.Key.State==Key.StateType.Down)HandleOptionsKey(k);return;}
  if(movingFavorite){if(e.Key.State==Key.StateType.Down)HandleMoveKey(k);return;}
  if(k=="Return"||k=="KP_Enter"){if(e.Key.State==Key.StateType.Down){if(holdPending)return;holdPending=true;holdHandled=false;holdTimer?.Start();}else if(e.Key.State==Key.StateType.Up){bool handled=holdHandled;CancelHold();if(!handled){try{if(editing!=""){editing="";UpdateFocus();}else if(area==0){area=1;selected=0;UpdateFocus();}else Activate();}catch(Exception ex){ShowError(ex.Message);ambient?.SetActive(page==0&&resumed);}}}return;}
  if(e.Key.State!=Key.StateType.Down)return;CancelHold();
  long now=Environment.TickCount64;if(k==lastKey&&now-lastKeyAt<180)return;lastKey=k;lastKeyAt=now;
  try{
   if(k=="XF86Home"){Home("home_key");return;}
   if(k=="XF86SysMenu"||k=="XF86More"){OpenSettings("settings_key");return;}
   if(page==1&&HandleLibraryKey(k))return;
   if(editing!=""){if(k=="Left"||k=="Right"){int d=k=="Left"?-1:1;if(editing=="volume")ChangeVolume(d);else ChangeBrightness(d);}else if(k=="Return"||k=="KP_Enter"||k=="XF86Back"||k=="Back"||k=="Escape"){editing="";UpdateFocus();}return;}
   if(k=="Up"){if(area==2){int col=Row(1).IndexOf(selected);area=1;selected=Row(0)[page==2?col:0];}else if(area==1)area=0;UpdateFocus();}
   else if(k=="Down"){if(area==0||area==3){area=1;selected=Row(0)[0];}else if(area==1){int col=Row(0).IndexOf(selected);area=2;selected=Row(1)[page==2?col:0];}UpdateFocus();}
   else if(k=="Left"||k=="Right"){
    int d=k=="Left"?-1:1;
    if(area==0){int next=page+d;if(next<0||next>2){area=3;selected=Row(-1)[next<0?2:0];UpdateFocus();}else{page=next;selected=0;BuildPage();}}
    else if(area==3){var r=Row(-1);int n=r.IndexOf(selected)+d;if(n<0||n>=r.Count){area=0;page=n<0?2:0;selected=0;BuildPage();}else{selected=r[n];UpdateFocus();}}
    else{var r=Row(area-1);int n=r.IndexOf(selected);selected=r[(n+d+r.Count)%r.Count];UpdateFocus();}
   }
   else if(k=="Return"||k=="KP_Enter"){if(area==0){area=1;selected=0;UpdateFocus();}else Activate();}
   else if(k=="XF86Back"||k=="Back"||k=="Escape"||k=="XF86Exit")SelectSource(lastSource);
  }catch(Exception ex){ShowError(ex.Message);}
 }
 bool HandleLibraryKey(string key)
 {
  if(key=="XF86Back"||key=="Back"||key=="Escape"||key=="XF86Exit"){Home("apps_back");return true;}
  if(library.Count==0)return key=="Up"||key=="Down";
  if((area==0||area==3)&&key=="Down"){area=1;selected=0;UpdateFocus();return true;}
  if(area!=1)return false;
  if(key=="Left"||key=="Right"||key=="Up"||key=="Down"){
   int next=AppLibrary.Move(library.Count,libraryPage*AppLibrary.PageSize+selected,key);
   if(next<0){area=0;UpdateFocus();return true;}
   int nextPage=next/AppLibrary.PageSize;selected=next%AppLibrary.PageSize;
   if(nextPage!=libraryPage){libraryPage=nextPage;BuildPage();}else UpdateFocus();
   return true;
  }
  return false;
 }
 void Activate()
 {
  string a=slots[selected].Action;notice.Text="";
  if(a.StartsWith("hdmi:")){SelectSource(int.Parse(a.Substring(5)));return;}
  if(a.StartsWith("page:")){page=int.Parse(a.Substring(5));selected=page==0?SourceIndex():0;area=1;BuildPage();return;}
  if(a.StartsWith("app:")){Launch(a.Substring(4));return;}
  if(a=="controllers"){Launch("org.tizen.menu","add-bluetooth-keyboard");return;}
  if(a=="network"){Launch("org.tizen.menu","networksettings");return;}
  if(a=="volume"||a=="brightness"){RefreshSettings();if(a=="brightness"&&!CanEdit(a)){ShowError("Brightness is unavailable for the current input.");return;}editing=a;UpdateFocus();return;}
  if(a=="game-mode"){RefreshSettings();SetSetting(a,SettingValue(a)=="1"?"0":"1");return;}
  if(a=="sleeptimer"){RefreshSettings();int n;if(int.TryParse(SettingValue(a),out n))SetSetting(a,((n+1)%5).ToString());return;}
  if(a=="mute"){int n;int r=sound_manager_get_master_mute(out n);if(r==0)r=sound_manager_set_master_mute(n==1?0:1);if(r!=0)ShowError("Use the remote's mute button.");RefreshSettings();UpdateFocus();WriteState("mute_changed");}
 }
 void RefreshSettings(){
  currentVolume=sound_manager_get_master_volume(out var v)==0?v:-1;
  currentMute=sound_manager_get_master_mute(out var m)==0?m:-1;
  foreach(var id in new[]{"brightness","game-mode","sleeptimer"}){
   try{if(CSharpGetItemValue(id,out var name,out var value)&&!string.IsNullOrEmpty(value)){settingValues[id]=value;settingNames[id]=name;}else{settingValues.Remove(id);settingNames.Remove(id);}
    settingEnabled[id]=CSharpGetItemEnable(id,out var enabled)&&enabled;
   }catch{settingValues.Remove(id);settingNames.Remove(id);settingEnabled[id]=false;}
  }
  try{if(!CSharpGetItemMinMaxStepValue("brightness",out brightnessMin,out brightnessMax,out brightnessStep)||brightnessStep<=0){brightnessMin=0;brightnessMax=50;brightnessStep=1;}}catch{}
 }
 string SettingValue(string id){return settingValues.TryGetValue(id,out var v)?v:"";}
 string SettingName(string id){return settingNames.TryGetValue(id,out var v)?v:"Unavailable";}
 bool CanEdit(string id){return settingEnabled.TryGetValue(id,out var enabled)&&enabled&&settingValues.ContainsKey(id);}
 void SetSetting(string id,string value){
  if(!CanEdit(id)){ShowError("This setting is unavailable for the current input.");return;}
  bool ok=false;try{ok=CSharpSetItemValue(id,value);}catch{}
  RefreshSettings();UpdateFocus();
  bool verified=ok&&SettingValue(id)==value;
  if(!verified)ShowError("The TV could not apply this setting. Try Advanced settings.");
  WriteState("setting:"+id+":"+value+":"+verified);
 }
 void ChangeBrightness(int delta){double n;if(double.TryParse(SettingValue("brightness"),NumberStyles.Float,CultureInfo.InvariantCulture,out n))SetSetting("brightness",Math.Max(brightnessMin,Math.Min(brightnessMax,n+delta*brightnessStep)).ToString(CultureInfo.InvariantCulture));}
 void ChangeVolume(int delta){int n;int r=sound_manager_get_master_volume(out n);if(r==0)r=sound_manager_set_master_volume(Math.Max(0,Math.Min(100,n+delta)));if(r!=0)ShowError("Use the remote's volume buttons.");RefreshSettings();UpdateFocus();WriteState("volume_changed");}
 void SelectSource(int source){if(!data.Sources.Any(item=>item.Id==source)){ShowError("No available HDMI input. Choose a source from Home.");return;}ambient?.SetActive(false);int r=connect_source(source);if(r<0){ShowError("Input could not be switched. Open Advanced settings to check the connection.");WriteState("source_failed:"+r);ambient?.SetActive(page==0&&resumed);return;}lastSource=source;if(Launch("org.tizen.tv-viewer")){data.RecordLaunch("hdmi:"+source);ambient?.SetActive(false);}}
 bool Launch(string id,string menuItem=null)
 {
  ambient?.SetActive(false);IntPtr c;int r=app_control_create(out c);if(r!=0){ShowError("Could not open the app.");ambient?.SetActive(page==0&&resumed);return false;}
  try{r=app_control_set_app_id(c,id);if(r==0)r=app_control_set_operation(c,"http://tizen.org/appcontrol/operation/default");
   if(r==0&&id=="org.tizen.menu"){
    if(menuItem!=null){
     r=app_control_add_extra_data(c,"menuitem",menuItem);
     if(r==0)r=app_control_add_extra_data(c,"launchoption","pressed");
    }
    if(r==0)r=app_control_add_extra_data(c,"__K_EX_FORCE_BOOST","enable");
    if(r==0)r=app_control_add_extra_data(c,"__K_SPLASH_SCREEN","LA_UI_VR_TRANSPARENT");
   }
   if(r==0)r=app_control_send_launch_request(c,IntPtr.Zero,IntPtr.Zero);WriteState("launch:"+id+(menuItem==null?"":":"+menuItem)+":"+r);if(r!=0){ShowError("Could not open this screen. Try Advanced settings.");ambient?.SetActive(page==0&&resumed);}else if(library.Any(app=>app.Id==id)){data.RecordLaunch("app:"+id);}return r==0;}finally{app_control_destroy(c);}
 }
 void ShowError(string text){notice.Text=text;}
 void WriteState(string activity){try{File.WriteAllText(StateFile,DateTime.UtcNow.ToString("o")+"\npage="+page+"\nselected="+selected+"\narea="+area+"\nactivity="+activity+"\nkey="+lastKey+"\nfont_loaded="+fontLoaded+"\nstyle=free-my-tv\nbuild_ms="+buildMilliseconds+"\nlibrary_page="+libraryPage+"\nlibrary_count="+library.Count+"\nlibrary_error="+libraryError+"\nselected_name="+(selected>=0&&selected<slots.Count?slots[selected].Name:"")+"\nartwork_cached="+artworkCache.Count+"\nartwork_ready="+ArtworkReadyCount()+"\nartwork_pending="+ArtworkPending()+"\nartwork_views_created="+artworkViewsCreated+"\npage_artwork_cold="+pageArtworkCold+"\nprocess_id="+Process.GetCurrentProcess().Id+"\nediting="+editing+"\nvolume="+currentVolume+"\nmute="+currentMute+"\nbrightness="+SettingValue("brightness")+"\ngame-mode="+SettingValue("game-mode")+"\nsleeptimer="+SettingValue("sleeptimer")+"\ndata_error="+StateValue(data?.LastError)+"\nsources="+StateValue(data==null?"":string.Join("|",data.Sources.Select(source=>source.Action+":"+source.Name+":"+(source.Connected?"connected":"idle"))))+"\nrecent_keys="+StateValue(data==null?"":string.Join("|",data.RecentKeys.Take(2)))+"\nright_cards="+StateValue(data==null?"":string.Join("|",HomeCards().Skip(1).Take(2).Select(card=>card.Name+":"+card.Action)))+"\nweather="+StateValue(data?.WeatherText)+"\nambient="+StateValue(ambientStatus)+"\n");}catch{}}
 string StateValue(string value){value=(value??"").Replace("\r"," ").Replace("\n"," ").Replace(LocalData,"[preferences]").Replace(Root,"[resources]");return value.Length>600?value.Substring(0,600):value;}
 public static void Main(string[] args){using(var app=new QuietHome())app.Run(args);}
}
