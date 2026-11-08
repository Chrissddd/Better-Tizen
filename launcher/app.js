(function () {
  "use strict";
  var config = window.DUSK_CONFIG || {};
  var storageKey = "dusk.settings.v1";
  var saved = readSettings();
  var settings = {
    samsungHomeAppId: saved.samsungHomeAppId || config.samsungHomeAppId || "",
    sourceLabels: saved.sourceLabels || config.sourceLabels || {},
    favoriteIds: Array.isArray(saved.favoriteIds) ? saved.favoriteIds : [],
    favoriteMode: saved.favoriteMode === "manual" ? "manual" : "automatic",
    launchCounts: saved.launchCounts || {},
    recentIds: Array.isArray(saved.recentIds) ? saved.recentIds.filter(function (id, index, ids) { return typeof id === "string" && ids.indexOf(id) === index; }).slice(0, 12) : []
  };
  var explicitFavorites = settings.favoriteMode === "manual";
  var state = { view: "home", items: [], apps: [], inputs: [], errors: [], pending: 0, focusIndex: 0, modalReturn: null, reorderId: "", sourceActive: false, busy: false };
  var saveRefreshedFavorites = false;
  var lastFocusedItem = "";
  var holdTimer = null;
  var enterHeld = false;
  var enterTarget = null;
  var toastTimer = null;
  var refreshGeneration = 0;
  var osdTimer = null;
  var sourceGeneration = 0;
  var sourcePending = false;
  var sourceReadGeneration = 0;
  var sourceWatchId = null;
  var sourceWindowHiding = false;
  var wallpaperLayer = 1;
  var currentWallpaper = "";
  var ambientGeneration = 0;
  var ambientIndex = -1;
  var ambientSource = "";
  var failedAmbientSources = {};
  var ambientPosterCache = {};
  var lastNativeHomeStamp = "";
  var reducedMotion = window.matchMedia ? window.matchMedia("(prefers-reduced-motion: reduce)") : { matches: false };
  var $ = function (id) { return document.getElementById(id); };

  // Consumer media entries observed in getAppsInfo on this TV. Dummy installers
  // and system components with similar names are deliberately absent.
  var mediaCatalog = [
    ["org.tizen.netflix-app", "Netflix", "netflix"],
    ["NuvioTV001.NuvioTV", "Nuvio", "nuvio"],
    ["9Ur5IzDKqV.TizenYouTube", "YouTube", "youtube"],
    ["PyZfkXAf88.tv2sumoTizen", "TV 2 Play", "tv2"],
    ["MCmYXNxgcu.DisneyPlus", "Disney+", "disney"],
    ["IkWsFHtOY9.Stremio", "Stremio", "stremio"],
    ["niYSnzL6h1.Viaplay", "Viaplay", "viaplay"],
    ["skysh0WTIM.SkyShowtime", "SkyShowtime", "skyshowtime"],
    ["org.tizen.primevideo", "Prime Video", "prime"],
    ["com.samsung.tv.aria-video", "Apple TV", "apple"],
    ["rJeHak5zRg.Spotify", "Spotify", "spotify"],
    ["Jz7aUrV2Q4.Twitch", "Twitch", "twitch"],
    ["vbUQClczfR.Wuakitv", "Rakuten TV", "rakuten"],
    ["com.samsung.tv.cobalt-ytkids", "YouTube Kids", "youtube-kids"],
    ["com.samsung.tv.cobalt-yttv", "YouTube TV", "youtube-tv"],
    ["com.samsung.tv.cobalt-gpm", "Google Play Movies", "google-play"],
    ["com.samsung.tv.cobalt-yt", "YouTube", "youtube"],
    ["org.tizen.browser", "Browser", "browser", false]
  ];
  function catalogEntry(app) {
    for (var i = 0; i < mediaCatalog.length; i++) {
      if (mediaCatalog[i][0] === app.id) { return { name: mediaCatalog[i][1], brand: mediaCatalog[i][2], rank: i, media: mediaCatalog[i][3] !== false }; }
    }
    return null;
  }
  function appAllowed(app, ownId) {
    if (!app.id || app.id === ownId || app.show === false) { return false; }
    if ((config.extraAllowedAppIds || []).indexOf(app.id) >= 0) { return true; }
    if ((config.hiddenAppIds || []).indexOf(app.id) >= 0) { return false; }
    if (catalogEntry(app)) { return true; }
    if (/^(org\.tizen|com\.samsung|com\.sausng|org\.volt)(\.|$)/i.test(app.id)) { return false; }
    // Third-party reverse-domain and opaque store IDs remain discoverable. Unqualified
    // platform executable names (gsl-chrome-app, ep-log-manager, etc.) are not app tiles.
    return app.id.indexOf(".") > 0;
  }
  function safeIconPath(path) {
    // Cross-package /usr/apps and /opt/usr/apps icons are inaccessible from this web app.
    return typeof path === "string" && /^assets\/[a-z0-9_./-]+$/i.test(path) && path.indexOf("..") < 0 ? path : "";
  }
  function usageScore(item) {
    var local = Number(settings.launchCounts[item.appId]) || 0;
    return Math.max(0, local);
  }
  function defaultFavorites() {
    var inputs = state.inputs.filter(function (item) { return item.relevant && item.source.type === "HDMI"; }).sort(function (a, b) { return a.source.number - b.source.number; });
    var apps = state.apps.filter(function (item) { return usageScore(item) > 0; }).sort(compareFavoriteRank).slice(0, Number(config.maxFavoriteApps) || 5);
    return inputs.concat(apps).map(function (item) { return item.id; });
  }
  function compareFavoriteRank(a, b) {
    var score = usageScore(b) - usageScore(a);
    if (score) { return score; }
    return a.rank - b.rank || a.name.localeCompare(b.name);
  }
  function inputVisible(item) { return item.kind !== "input" || item.relevant; }
  function sourceRelevance(source, connected) {
    var runtimeName = typeof source.editName === "string" && source.editName.trim() ? source.editName.trim() : (typeof source.name === "string" ? source.name.trim() : "");
    var generic = runtimeName.toLowerCase().replace(/[^a-z0-9]/g, "");
    var named = !!generic && !/^(hdmi\d*(arc|earc)?|input\d*|source\d*|unknown|none|unassigned|nodevice|notconnected|disconnected|null|undefined)$/.test(generic);
    var category = [source.deviceCategory, source.category, source.deviceType].filter(function (value) { return typeof value === "string"; }).join(" ").replace(/[_-]/g, " ");
    var audioOnly = source.audioOnly === true || source.isAudioOnly === true || /audio|sound\s*bar|speaker|receiver|home\s*theat(er|re)/i.test(category) || /\b(sonos|sound\s*bar|speaker|amplifier|audio|receiver|home\s*theat(er|re)|hjemmekino|lydplanke|h[oø]yttaler)\b/i.test(runtimeName);
    return { name: runtimeName, relevant: !audioOnly && (connected || named), audioOnly: audioOnly };
  }
  function applySources(result) {
    var seen = {}, inputs = [];
    function append(sources, connected) {
      (sources || []).forEach(function (source) {
        if (source.type !== "HDMI") { return; }
        var id = "input:" + source.type + ":" + source.number;
        if (seen[id]) { return; } seen[id] = true;
        var relevance = sourceRelevance(source, connected);
        inputs.push({ id: id, name: settings.sourceLabels[id] || relevance.name || source.type + " " + source.number, kind: "input", source: source, connected: connected, relevant: relevance.relevant, audioOnly: relevance.audioOnly });
      });
    }
    append(result.connected, true); append(result.disconnected, false);
    state.inputs = inputs; state.items = state.inputs.concat(state.apps);
    if (state.pending === 0 && !explicitFavorites && !state.reorderId) { settings.favoriteIds = defaultFavorites(); }
  }
  function refreshSources() {
    if (state.pending || !window.tizen || !tizen.systeminfo) { return; }
    var generation = ++sourceReadGeneration;
    try {
      tizen.systeminfo.getPropertyValue("VIDEOSOURCE", function (result) {
        if (generation !== sourceReadGeneration) { return; }
        applySources(result); render();
      }, function (error) { console.warn("Refresh inputs", error); });
    } catch (error) { console.warn("Refresh inputs", error); }
  }
  function watchSources() {
    try {
      if (window.tizen && tizen.systeminfo && tizen.systeminfo.addPropertyValueChangeListener) {
        sourceWatchId = tizen.systeminfo.addPropertyValueChangeListener("VIDEOSOURCE", function (result) {
          sourceReadGeneration++; applySources(result); render();
        }, null, function (error) { console.warn("Input change listener", error); });
      }
    } catch (error) { console.warn("Input change listener", error); }
    setInterval(function () { if (!document.hidden) { refreshSources(); } }, 15000);
  }
  function wallpaperEntries() {
    var entries = config.wallpapers && config.wallpapers.length ? config.wallpapers : [config.wallpaper];
    var hour = new Date().getHours(), period = hour < 6 || hour >= 22 ? "night" : hour < 11 ? "morning" : hour < 17 ? "day" : "evening";
    var all = entries.map(function (entry) { return typeof entry === "string" ? {src:entry} : entry; }).filter(function (entry) {
      return entry && typeof entry.src === "string" && /^assets\/[a-z0-9_./-]+$/i.test(entry.src) && entry.src.indexOf("..") < 0;
    });
    var matching = all.filter(function (entry) { return !entry.periods || entry.periods.indexOf(period) >= 0; });
    // Two images are required for a rotation; a smaller time-of-day set uses the full set.
    return matching.length >= 2 ? matching : (matching.length ? matching.concat(all.filter(function (entry) { return matching.indexOf(entry) < 0; })) : all);
  }
  function rotateWallpaper(initial) {
    if (!initial && (document.hidden || state.sourceActive)) { return; }
    var entries = wallpaperEntries(); if (!entries.length) { return; }
    var at = -1;
    entries.forEach(function (entry, index) { if (entry.src === currentWallpaper) { at = index; } });
    var src = entries[(at + 1) % entries.length].src;
    if (src === currentWallpaper) { return; }
    var preload = new Image();
    preload.onload = function () {
      var incoming = $(wallpaperLayer ? "wallpaper" : "wallpaper-next"), outgoing = $(wallpaperLayer ? "wallpaper-next" : "wallpaper");
      incoming.style.backgroundImage = 'url("' + src + '")';
      incoming.style.transitionDuration = initial || reducedMotion.matches ? "0s" : (Number(config.wallpaperFadeSeconds) || 6) + "s";
      incoming.style.animationName = "none"; incoming.offsetWidth; incoming.style.animationName = "dusk-drift";
      outgoing.style.transitionDuration = incoming.style.transitionDuration;
      incoming.style.opacity = "1"; outgoing.style.opacity = "0";
      wallpaperLayer = wallpaperLayer ? 0 : 1; currentWallpaper = src;
    };
    preload.onerror = function () { console.warn("Wallpaper could not be loaded", src); };
    preload.src = src;
  }
  function startWallpapers() {
    function motionChanged() { document.body.classList.toggle("reduced-motion", reducedMotion.matches); updateAmbientVideo(false); }
    motionChanged();
    if (reducedMotion.addEventListener) { reducedMotion.addEventListener("change", motionChanged); }
    else if (reducedMotion.addListener) { reducedMotion.addListener(motionChanged); }
    rotateWallpaper(true);
    setInterval(function () { rotateWallpaper(false); }, Math.max(30, Math.min(60, Number(config.wallpaperIntervalSeconds) || 45)) * 1000);
  }
  function shouldPlayAmbient() { return state.view === "home" && !document.hidden && !sourcePending && !state.sourceActive && !sourceWindowHiding; }
  function ambientEntries() {
    return (config.ambientVideos || []).map(function (entry) { return typeof entry === "string" ? { src: entry } : entry; }).filter(function (entry) {
      return entry && typeof entry.src === "string" && /^assets\/[a-z0-9_./-]+\.(mp4|webm)$/i.test(entry.src) && entry.src.indexOf("..") < 0 && !failedAmbientSources[entry.src];
    });
  }
  function prepareAmbientPoster(path, ready) {
    if (typeof path !== "string" || !/^assets\/[a-z0-9_./-]+\.(jpg|jpeg|png|webp)$/i.test(path) || path.indexOf("..") >= 0) { ready(false); return; }
    var cached = ambientPosterCache[path];
    if (cached) { if (cached.done) { ready(cached.loaded); } else { cached.callbacks.push(ready); } return; }
    cached = ambientPosterCache[path] = { image: new Image(), done: false, loaded: false, callbacks: [ready] };
    function complete(loaded) { cached.done = true; cached.loaded = loaded; var callbacks = cached.callbacks; cached.callbacks = []; callbacks.forEach(function (callback) { callback(loaded); }); }
    cached.image.onload = function () { complete(true); }; cached.image.onerror = function () { complete(false); }; cached.image.src = path;
  }
  function releaseAmbientVideo(keepPoster) {
    ambientGeneration++; ambientSource = "";
    var video = $("ambient-video"); video.pause(); video.hidden = true; video.classList.remove("video-ready");
    if (video.getAttribute("src")) { video.removeAttribute("src"); video.load(); }
    if (!keepPoster) { $("ambient-poster").hidden = true; }
  }
  function ambientFailed(generation, src, error) {
    if (generation !== ambientGeneration) { return; }
    if (error) { console.warn("Ambient video unavailable", error); }
    failedAmbientSources[src] = true; releaseAmbientVideo(true); updateAmbientVideo(false);
  }
  function updateAmbientVideo(rotate) {
    var entries = ambientEntries();
    if (!shouldPlayAmbient() || reducedMotion.matches || !entries.length) { releaseAmbientVideo(); return; }
    if (ambientSource && !rotate) { return; }
    var at = -1; entries.forEach(function (entry, index) { if (entry.src === ambientSource) { at = index; } });
    ambientIndex = rotate && at >= 0 ? (at + 1) % entries.length : (ambientIndex < 0 || ambientIndex >= entries.length ? 0 : ambientIndex);
    var entry = entries[ambientIndex], src = entry.src, waitingGeneration = ambientGeneration;
    if (src === ambientSource) { return; }
    // Load the matching first frame while the old video still covers the screen.
    // One decoder is then released and reused over that stable image.
    prepareAmbientPoster(entry.poster, function (loaded) {
      if (waitingGeneration !== ambientGeneration || !shouldPlayAmbient() || reducedMotion.matches) { return; }
      var poster = $("ambient-poster");
      if (loaded) { poster.style.backgroundImage = 'url("' + entry.poster + '")'; poster.hidden = false; }
      releaseAmbientVideo(true);
      var generation = ambientGeneration, video = $("ambient-video");
      ambientSource = src; video.muted = true; video.loop = true; video.hidden = false; video.src = src; video.load();
      try {
        var playing = video.play();
        if (playing && playing.catch) { playing.catch(function (error) { ambientFailed(generation, src, error); }); }
      } catch (error) { ambientFailed(generation, src, error); }
    });
  }
  function startAmbientVideo() {
    $("ambient-video").addEventListener("playing", function () { if (shouldPlayAmbient() && ambientSource) { this.classList.add("video-ready"); } else { releaseAmbientVideo(); } });
    $("ambient-video").addEventListener("error", function () { if (ambientSource) { ambientFailed(ambientGeneration, ambientSource); } });
    ambientEntries().forEach(function (entry) { prepareAmbientPoster(entry.poster, function () {}); });
    updateAmbientVideo(false);
    setInterval(function () { updateAmbientVideo(true); }, Math.max(30, Math.min(120, Number(config.ambientIntervalSeconds) || 45)) * 1000);
  }

  function readSettings() {
    try { return JSON.parse(localStorage.getItem(storageKey) || "{}") || {}; }
    catch (error) { console.warn("Dusk settings unavailable", error); return {}; }
  }
  function persist() {
    try { localStorage.setItem(storageKey, JSON.stringify(settings)); return true; }
    catch (error) { toast("Your changes could not be saved."); return false; }
  }
  function errorText(error) { return (error && (error.name || error.message)) || "Unavailable"; }
  function toast(message) {
    clearTimeout(toastTimer);
    $("toast").textContent = message;
    $("toast").hidden = false;
    toastTimer = setTimeout(function () { $("toast").hidden = true; }, 5500);
  }
  function reportError(area, error) {
    var message = area + ": " + errorText(error);
    state.errors.push(message);
    console.warn(message, error);
  }
  function tick() {
    var now = new Date();
    $("clock").textContent = (now.getHours() < 10 ? "0" : "") + now.getHours() + ":" + (now.getMinutes() < 10 ? "0" : "") + now.getMinutes();
    $("clock").dateTime = now.toISOString();
    try { $("date").textContent = now.toLocaleDateString(config.locale || "en-GB", { weekday: "long", month: "long", day: "numeric" }); }
    catch (error) { $("date").textContent = now.toDateString(); }
    var hour = now.getHours();
    $("salutation").textContent = hour < 12 ? "GOOD MORNING" : (hour < 18 ? "GOOD AFTERNOON" : "GOOD EVENING");
  }
  function showView(view, focusContent) {
    if (state.sourceActive || sourcePending) { stopSource(); }
    if (state.reorderId) { settings.favoriteIds = reorderSnapshot.slice(); state.reorderId = ""; render(); }
    state.view = view;
    document.body.setAttribute("data-view", view);
    $("page-title").textContent = view === "home" ? "Home" : view === "apps" ? "Your Apps" : "Settings";
    ["home", "apps", "settings"].forEach(function (name) { $(name + "-view").hidden = name !== view; });
    Array.prototype.forEach.call(document.querySelectorAll(".nav-button"), function (button) {
      var current = button.getAttribute("data-view") === view;
      button.classList.toggle("selected", current);
      button.setAttribute("aria-current", current ? "page" : "false");
    });
    if (focusContent) {
      var target = $(view + "-view").querySelector("button");
      if (target) { focusElement(target); }
      else { focusElement(document.querySelector('[data-view="' + view + '"]')); }
    }
    updateAmbientVideo(false);
  }
  function itemById(id) {
    for (var i = 0; i < state.items.length; i++) { if (state.items[i].id === id) { return state.items[i]; } }
    return null;
  }
  function visibleButtons() {
    var root = !$("modal").hidden ? $("modal") : $("shell");
    return Array.prototype.filter.call(root.querySelectorAll("button, input"), function (el) { return !el.disabled && el.getClientRects().length > 0; });
  }
  function focusElement(element) {
    try { element.focus({ preventScroll: true }); } catch (ignore) { element.focus(); }
    var container = element.closest(".favorites, .app-grid");
    if (container) {
      var box = element.getBoundingClientRect(), area = container.getBoundingClientRect(), scale = Math.min(window.innerWidth / 1920, window.innerHeight / 1080);
      if (box.left < area.left + 10) { container.scrollLeft -= (area.left + 10 - box.left) / scale; }
      else if (box.right > area.right - 10) { container.scrollLeft += (box.right - area.right + 10) / scale; }
      if (box.top < area.top + 10) { container.scrollTop -= (area.top + 10 - box.top) / scale; }
      else if (box.bottom > area.bottom - 10) { container.scrollTop += (box.bottom - area.bottom + 10) / scale; }
    }
    document.body.scrollLeft = 0; document.body.scrollTop = 0;
  }
  function restoreFocus(id, featured, preferredIndex) {
    var buttons = $(state.view === "apps" ? "all-items" : featured ? "featured-inputs" : "favorites").querySelectorAll("button");
    if (typeof preferredIndex === "number" && buttons[preferredIndex] && buttons[preferredIndex].getAttribute("data-item") === id) { focusElement(buttons[preferredIndex]); return; }
    for (var i = 0; i < buttons.length; i++) {
      if (buttons[i].getAttribute("data-item") === id) { focusElement(buttons[i]); return; }
    }
    var first = $(state.view + "-view").querySelector("button");
    if (first) { focusElement(first); }
  }
  function cardClass(item) {
    if (item.kind === "input") { return item.source.type === "HDMI" ? "source-hdmi" : "source-tv"; }
    var name = (item.brand || item.name).toLowerCase();
    var brands = ["netflix", "youtube", "plex", "spotify", "apple", "xbox", "browser", "disney", "prime", "stremio", "nuvio", "tv2", "viaplay"];
    for (var i = 0; i < brands.length; i++) { if (name.indexOf(brands[i]) >= 0) { return "brand-" + brands[i]; } }
    return "";
  }
  var brandAssets = { netflix: "netflix.svg", nuvio: "nuvio.png", youtube: "youtube.svg", tv2: "tv2play.svg", disney: "disneyplus.png", stremio: "stremio.svg", viaplay: "viaplay.svg", skyshowtime: "skyshowtime.svg", prime: "primevideo.png", apple: "appletv.svg", spotify: "spotify.svg", twitch: "twitch.svg", "youtube-kids": "youtubekids.svg", "youtube-tv": "youtubetv.svg", "google-play": "googleplay.svg" };
  function inputIcon() {
    var svg = document.createElementNS("http://www.w3.org/2000/svg", "svg");
    svg.setAttribute("viewBox", "0 0 64 64"); svg.setAttribute("class", "device-icon"); svg.setAttribute("aria-hidden", "true"); svg.setAttribute("focusable", "false");
    var path = document.createElementNS("http://www.w3.org/2000/svg", "path");
    path.setAttribute("d", "M10 19h44v19L43 48H21L10 38V19ZM21 27v10m7-10v10m8-10v10m7-10v10");
    path.setAttribute("fill", "none"); path.setAttribute("stroke", "currentColor"); path.setAttribute("stroke-width", "2.5"); path.setAttribute("stroke-linejoin", "round"); path.setAttribute("stroke-linecap", "round");
    svg.appendChild(path); return svg;
  }
  function makeCard(item, favorite) {
    var wrap = document.createElement("div");
    wrap.className = "card-wrap";
    var button = document.createElement("button");
    button.className = "app-card " + cardClass(item) + (state.reorderId === item.id ? " reordering" : "");
    button.setAttribute("data-item", item.id);
    button.setAttribute("data-kind", item.kind);
    button.setAttribute("aria-label", item.name + (item.kind === "input" && !item.connected ? ", idle" : ""));
    if (item.kind === "input") {
      button.appendChild(inputIcon());
      var port = document.createElement("span"); port.className = "input-port";
      port.textContent = item.source.type + " " + String(item.source.number) + (item.connected ? "" : " · Idle");
      button.appendChild(port);
    } else {
      var mono = document.createElement("span"); mono.className = "monogram";
      mono.textContent = item.name.length > 14 ? item.name.slice(0, 13) + "…" : item.name;
      var logoPath = brandAssets[item.brand] ? "assets/brands/" + brandAssets[item.brand] : item.iconPath;
      if (logoPath) {
        var img = document.createElement("img"); img.className = "app-icon" + (brandAssets[item.brand] ? " brand-logo brand-mark-" + item.brand : ""); img.alt = ""; img.src = logoPath;
        img.onerror = function () { this.hidden = true; mono.hidden = false; };
        mono.hidden = true; button.appendChild(img);
      }
      button.appendChild(mono);
    }
    if (favorite && state.view === "apps") { var star = document.createElement("span"); star.className = "star"; star.textContent = "★"; button.appendChild(star); }
    var label = document.createElement("span"); label.className = "card-label"; label.textContent = item.name;
    wrap.appendChild(button); wrap.appendChild(label);
    return wrap;
  }
  function appsShortcut() {
    var wrap = document.createElement("div"); wrap.className = "card-wrap";
    var button = document.createElement("button"); button.className = "app-card utility-apps";
    button.setAttribute("data-action", "open-apps"); button.setAttribute("aria-label", "Browse all apps and inputs");
    var icon = document.createElementNS("http://www.w3.org/2000/svg", "svg"); icon.setAttribute("viewBox", "0 0 64 64"); icon.setAttribute("aria-hidden", "true");
    [9, 26, 43].forEach(function (x) { [9, 26, 43].forEach(function (y) { var square = document.createElementNS("http://www.w3.org/2000/svg", "rect"); square.setAttribute("x", x); square.setAttribute("y", y); square.setAttribute("width", "12"); square.setAttribute("height", "12"); square.setAttribute("rx", "3"); icon.appendChild(square); }); });
    button.appendChild(icon); wrap.appendChild(button);
    var label = document.createElement("span"); label.className = "card-label"; label.textContent = "Your Apps"; wrap.appendChild(label);
    return wrap;
  }
  function featuredItems(favorites) {
    var fallback = state.inputs.filter(function (item) { return inputVisible(item) && item.source.type === "HDMI"; }).concat(favorites.filter(function (item) { return item.kind === "app"; }));
    // Real successful use, stored on the TV, owns the two portrait positions.
    // Keep a repeated primary input if it is one of those two recent choices.
    var recent = settings.recentIds.map(itemById).filter(Boolean).filter(inputVisible).slice(0, 2);
    var primary = fallback[0] || recent[0];
    if (!primary) { return []; }
    fallback.forEach(function (item) {
      if (recent.length < 2 && item.id !== primary.id && !recent.some(function (current) { return current.id === item.id; })) { recent.push(item); }
    });
    return [primary].concat(recent);
  }
  function recordRecent(id) {
    if (!itemById(id)) { return; }
    settings.recentIds = [id].concat(settings.recentIds.filter(function (previous) { return previous !== id; })).slice(0, 12);
    persist();
  }
  function makeFeature(item, index) {
    var wrap = makeCard(item, false), button = wrap.querySelector("button");
    var detail = document.createElement("span"); detail.className = "card-detail";
    if (item.kind === "input") {
      detail.textContent = item.source.type + " " + item.source.number + " · " + (item.connected ? "Connected" : "Idle");
      var name = item.name.toLowerCase(), artwork = /playstation|\bps5\b|\bps4\b/.test(name) ? "ps5" : /nintendo|switch/.test(name) ? "switch" : "hdmi";
      if (index) { button.classList.add("portrait-source", "portrait-" + artwork); }
    } else { detail.textContent = "Installed on your TV"; }
    wrap.appendChild(detail);
    if (!index) {
      button.textContent = "";
      var kicker = document.createElement("span"); kicker.className = "feature-kicker"; kicker.textContent = item.name;
      var title = document.createElement("span"); title.className = "feature-title"; title.textContent = item.kind === "input" && /playstation|\bps5\b|nintendo|xbox/i.test(item.name) ? "Let’s play." : "Welcome home.";
      var command = document.createElement("span"); command.className = "feature-command";
      if (item.kind === "input") { command.appendChild(inputIcon()); }
      command.appendChild(document.createTextNode(item.kind === "input" ? "Switch to " + item.source.type + " " + item.source.number : "Open " + item.name));
      var status = document.createElement("span"); status.className = "feature-status"; status.textContent = item.kind === "input" ? (item.connected ? "Ready to watch" : "Device is idle") : "Ready to open";
      button.appendChild(kicker); button.appendChild(title); button.appendChild(command); button.appendChild(status);
    }
    return wrap;
  }
  function render() {
    var focus = document.activeElement && document.activeElement.getAttribute("data-item");
    var focusedAction = document.activeElement && document.activeElement.getAttribute("data-action");
    var focusedRegion = document.activeElement && document.activeElement.closest("#featured-inputs, #favorites, #all-items");
    var focusedItemIndex = focusedRegion ? Array.prototype.indexOf.call(focusedRegion.querySelectorAll("button"), document.activeElement) : -1;
    var focusedActionIndex = focusedRegion ? Array.prototype.indexOf.call(focusedRegion.querySelectorAll("[data-action]"), document.activeElement) : -1;
    var featureFocus = !!(document.activeElement && document.activeElement.closest("#featured-inputs"));
    $("favorites").textContent = ""; $("all-items").textContent = ""; $("featured-inputs").textContent = "";
    var favorites = settings.favoriteIds.map(itemById).filter(Boolean).filter(inputVisible);
    featuredItems(favorites).forEach(function (item, index) { $("featured-inputs").appendChild(makeFeature(item, index)); });
    while ($("featured-inputs").children.length < 3) { $("featured-inputs").appendChild(appsShortcut()); }
    favorites.forEach(function (item) { $("favorites").appendChild(makeCard(item, true)); });
    $("favorites").appendChild(appsShortcut());
    state.items.filter(inputVisible).forEach(function (item) { $("all-items").appendChild(makeCard(item, settings.favoriteIds.indexOf(item.id) >= 0)); });
    $("home-empty").hidden = favorites.length > 0;
    $("favorite-hint").hidden = favorites.length === 0;
    $("home-empty-detail").textContent = state.pending ? "Finding installed apps and inputs…" : (state.items.length ? "Connect an HDMI device or open an app to see it here." : "TV apps and inputs are not available here. Open this app on the TV to connect.");
    $("apps-empty").hidden = state.items.length > 0;
    $("apps-empty-detail").textContent = state.pending ? "Reading TV inventory…" : state.errors.join(" · ");
    $("device-status").textContent = state.apps.length + " apps · " + state.inputs.filter(inputVisible).length + " inputs" + (state.errors.length ? " · " + state.errors.join(" · ") : "");
    $("favorite-hint").textContent = state.reorderId ? "← → Move · OK Done · Back Cancel" : "Hold OK for options";
    if (focus) { restoreFocus(focus, featureFocus, focusedItemIndex); }
    else if (focusedAction && focusedRegion) {
      var actions = focusedRegion.querySelectorAll("[data-action]");
      if (actions[focusedActionIndex] && actions[focusedActionIndex].getAttribute("data-action") === focusedAction) { focusElement(actions[focusedActionIndex]); }
      else { for (var i = 0; i < actions.length; i++) { if (actions[i].getAttribute("data-action") === focusedAction) { focusElement(actions[i]); break; } } }
    }
  }
  function refresh() {
    var generation = ++refreshGeneration;
    state.errors = []; state.apps = []; state.inputs = []; state.pending = 2;
    function done() {
      if (generation !== refreshGeneration) { return; }
      state.pending--;
      state.items = state.inputs.concat(state.apps);
      if (state.pending === 0 && !explicitFavorites) {
        settings.favoriteIds = defaultFavorites();
        if (saveRefreshedFavorites) { persist(); saveRefreshedFavorites = false; }
      }
      render();
      if (state.pending === 0 && (document.activeElement === document.body || document.activeElement === document.documentElement)) { showView(state.view, true); }
    }
    try {
      if (!window.tizen || !tizen.application || typeof tizen.application.getAppsInfo !== "function") { throw new Error("Tizen Application API unavailable"); }
      var ownId = "";
      try { ownId = tizen.application.getCurrentApplication().appInfo.id; } catch (ignore) {}
      tizen.application.getAppsInfo(function (apps) {
        if (generation !== refreshGeneration) { return; }
        var selectedBrands = {}, pinnedBrands = {};
        var candidates = apps.filter(function (app) { return appAllowed(app, ownId); }).map(function (app) {
          var entry = catalogEntry(app);
          return { id: "app:" + app.id, appId: app.id, name: entry ? entry.name : (app.name || app.id), kind: "app", iconPath: safeIconPath(app.iconPath), brand: entry ? entry.brand : "", rank: entry ? entry.rank : 10000, media: entry ? entry.media : false };
        }).sort(function (a, b) { return a.rank - b.rank || a.name.localeCompare(b.name); });
        candidates.forEach(function (item) {
          if (!item.brand) { return; }
          if (explicitFavorites && settings.favoriteIds.indexOf(item.id) >= 0) { pinnedBrands[item.brand] = true; }
          if (!selectedBrands[item.brand] || compareFavoriteRank(item, selectedBrands[item.brand]) < 0) { selectedBrands[item.brand] = item; }
        });
        state.apps = candidates.filter(function (item) {
          if (!item.brand) { return true; }
          if (pinnedBrands[item.brand]) { return settings.favoriteIds.indexOf(item.id) >= 0; }
          return selectedBrands[item.brand].id === item.id;
        }).sort(compareFavoriteRank);
        done();
      }, function (error) { if (generation !== refreshGeneration) { return; } reportError("Apps", error); done(); });
    } catch (error) { reportError("Apps", error); done(); }
    try {
      if (!window.tizen || !tizen.systeminfo) { throw new Error("Tizen SystemInfo API unavailable"); }
      var sourceRead = ++sourceReadGeneration;
      tizen.systeminfo.getPropertyValue("VIDEOSOURCE", function (result) {
        if (generation !== refreshGeneration) { return; }
        if (sourceRead === sourceReadGeneration) { applySources(result); }
        done();
      }, function (error) { if (generation !== refreshGeneration) { return; } reportError("Inputs", error); done(); });
    } catch (error) { reportError("Inputs", error); done(); }
    render();
  }
  function launchApp(appId, name) {
    if (!appId) { toast("Samsung Home is unavailable."); return; }
    try {
      if (!window.tizen || !tizen.application) { throw new Error("Tizen Application API unavailable"); }
      var failed = function (error) { state.busy = false; toast("Could not open " + name + ": " + errorText(error)); updateAmbientVideo(false); };
      var success = function () {
        state.busy = false;
        if (!itemById("app:" + appId)) { return; }
        recordRecent("app:" + appId);
        settings.launchCounts[appId] = (Number(settings.launchCounts[appId]) || 0) + 1;
        state.apps.sort(compareFavoriteRank); state.items = state.inputs.concat(state.apps);
        if (!explicitFavorites) { settings.favoriteIds = defaultFavorites(); }
        persist(); render();
      };
      state.busy = true;
      releaseAmbientVideo();
      if (typeof tizen.application.launch === "function") { tizen.application.launch(appId, success, failed); }
      else if (typeof tizen.application.launchAppControl === "function" && typeof tizen.ApplicationControl === "function") {
        tizen.application.launchAppControl(new tizen.ApplicationControl("http://tizen.org/appcontrol/operation/main"), appId, success, failed);
      } else { throw new Error("Application launch is unavailable"); }
    } catch (error) { state.busy = false; toast("Could not open " + name + ": " + errorText(error)); updateAmbientVideo(false); }
  }
  function startSource(item) {
    if (state.busy) { return; }
    markHomeRequestBaseline();
    releaseAmbientVideo();
    var generation = ++sourceGeneration;
    sourcePending = true;
    state.busy = true;
    function failed(error) { if (generation !== sourceGeneration) { return; } sourcePending = false; state.busy = false; toast("Could not show " + item.name + ": " + errorText(error)); updateAmbientVideo(false); }
    try {
      if (!window.tizen || !tizen.tvwindow) { throw new Error("TVWindow API unavailable"); }
      tizen.tvwindow.setSource(item.source, function () {
        if (generation !== sourceGeneration) { return; }
        try {
          tizen.tvwindow.show(function () {
            if (generation !== sourceGeneration) {
              if (!state.sourceActive && !sourcePending) { hideSourceWindow(); }
              return;
            }
            sourcePending = false; state.busy = false; state.sourceActive = true;
            recordRecent(item.id); render();
            document.body.classList.add("source-active");
            document.documentElement.classList.add("source-active");
            $("source-view").hidden = false; $("source-title").textContent = item.name;
            document.querySelector(".source-osd").style.opacity = "1";
            clearTimeout(osdTimer);
            osdTimer = setTimeout(function () { document.querySelector(".source-osd").style.opacity = "0"; }, 5000);
          }, failed, ["0px", "0px", "1920px", "1080px"], "MAIN", "BEHIND");
        } catch (error) { failed(error); }
      }, failed, "MAIN");
    } catch (error) { failed(error); }
  }
  function hideSourceWindow() {
    sourceWindowHiding = true;
    function hidden(error) { sourceWindowHiding = false; if (error) { console.warn("Hide source", error); } updateAmbientVideo(false); }
    try { if (window.tizen && tizen.tvwindow) { tizen.tvwindow.hide(function () { hidden(); }, hidden, "MAIN"); } else { hidden(); } }
    catch (error) { hidden(error); }
  }
  function stopSource() {
    sourceGeneration++; sourcePending = false; state.busy = false;
    state.sourceActive = false; clearTimeout(osdTimer);
    $("source-view").hidden = true; document.body.classList.remove("source-active");
    document.documentElement.classList.remove("source-active");
    hideSourceWindow();
    restoreFocus(lastFocusedItem);
  }
  function activateItem(id) {
    var item = itemById(id); if (!item || state.busy) { return; }
    lastFocusedItem = id;
    if (state.reorderId) { state.reorderId = ""; explicitFavorites = true; settings.favoriteMode = "manual"; persist(); render(); restoreFocus(id); return; }
    if (item.kind === "input") { startSource(item); } else { launchApp(item.appId, item.name); }
  }
  function closeModal() {
    $("modal").hidden = true;
    var previous = state.modalReturn; state.modalReturn = null;
    if (previous && previous.isConnected) { focusElement(previous); }
    else { restoreFocus(lastFocusedItem); }
  }
  function modal(title, description, actions) {
    state.modalReturn = document.activeElement;
    $("modal-title").textContent = title;
    $("modal-description").textContent = description;
    $("modal-actions").textContent = "";
    actions.forEach(function (action) {
      var button = document.createElement("button"); button.textContent = action.label;
      button.addEventListener("click", function () { closeModal(); action.run(); });
      $("modal-actions").appendChild(button);
    });
    var cancel = document.createElement("button"); cancel.textContent = "Cancel"; cancel.addEventListener("click", closeModal); $("modal-actions").appendChild(cancel);
    $("modal").hidden = false; focusElement($("modal-actions").querySelector("button"));
  }
  function editValue(title, description, initial, callback) {
    modal(title, description, []);
    var input = document.createElement("input"); input.type = "text"; input.value = initial; input.maxLength = 160; input.setAttribute("aria-label", title);
    input.style.cssText = "width:100%;padding:18px;font:24px Arial;color:#13203b;border-radius:12px;margin-bottom:10px;";
    var save = document.createElement("button"); save.textContent = "Save";
    save.addEventListener("click", function () { var value = input.value.trim(); closeModal(); callback(value); });
    $("modal-actions").insertBefore(save, $("modal-actions").firstChild);
    $("modal-actions").insertBefore(input, save); focusElement(input);
  }
  var reorderSnapshot = [];
  function itemOptions(id) {
    var item = itemById(id); if (!item) { return; }
    lastFocusedItem = id;
    var isFavorite = settings.favoriteIds.indexOf(id) >= 0;
    var actions = [{ label: isFavorite ? "Remove from favorites" : "Add to favorites", run: function () {
      explicitFavorites = true;
      settings.favoriteMode = "manual";
      if (isFavorite) { settings.favoriteIds = settings.favoriteIds.filter(function (value) { return value !== id; }); }
      else { settings.favoriteIds.push(id); }
      persist(); render(); restoreFocus(id);
    } }];
    if (isFavorite && state.view === "home") { actions.push({ label: "Move favorite", run: function () { reorderSnapshot = settings.favoriteIds.slice(); state.reorderId = id; render(); restoreFocus(id); } }); }
    if (item.kind === "input") { actions.push({ label: "Rename input", run: function () {
      editValue("Rename " + item.name, "Choose the name shown on your home screen.", item.name, function (value) {
        if (!value) { return; } settings.sourceLabels[id] = value; item.name = value; persist(); render(); restoreFocus(id);
      });
    } }); }
    modal(item.name, item.kind === "app" ? "Installed app" : (item.connected ? "Available TV input" : "TV input · Idle"), actions);
  }
  function navigate(dx, dy) {
    if (state.sourceActive) { return; }
    if (state.reorderId) {
      if (!dx) { return; }
      var at = settings.favoriteIds.indexOf(state.reorderId), next = at + dx;
      if (next >= 0 && next < settings.favoriteIds.length) {
        var swap = settings.favoriteIds[next]; settings.favoriteIds[next] = state.reorderId; settings.favoriteIds[at] = swap;
        render(); restoreFocus(state.reorderId);
      }
      return;
    }
    var current = document.activeElement;
    if (dy < 0 && current.closest("#featured-inputs")) { focusElement(document.querySelector('.nav-button[data-view="home"]')); return; }
    var candidates = visibleButtons();
    if (candidates.indexOf(current) < 0) { if (candidates[0]) { focusElement(candidates[0]); } return; }
    var box = current.getBoundingClientRect();
    var x = box.left + box.width / 2, y = box.top + box.height / 2;
    var best = null, score = Infinity;
    candidates.forEach(function (el) {
      if (el === current) { return; }
      var rect = el.getBoundingClientRect();
      var cx = rect.left + rect.width / 2, cy = rect.top + rect.height / 2;
      var primary = dx ? (cx - x) * dx : (cy - y) * dy;
      var secondary = dx ? Math.abs(cy - y) : Math.abs(cx - x);
      if (primary <= 10) { return; }
      // Edge distance keeps wide input cards reachable from every dock card below them.
      var forwardGap = dx > 0 ? rect.left - box.right : dx < 0 ? box.left - rect.right : dy > 0 ? rect.top - box.bottom : box.top - rect.bottom;
      var crossGap = dx ? Math.max(0, rect.top - box.bottom, box.top - rect.bottom) : Math.max(0, rect.left - box.right, box.left - rect.right);
      var weight = Math.max(0, forwardGap) + crossGap * 3.5 + primary * .05 + secondary * .01;
      if (weight < score) { score = weight; best = el; }
    });
    if (best) { focusElement(best); }
  }
  function back() {
    if (!$("modal").hidden) { closeModal(); return; }
    if (state.sourceActive || sourcePending) { stopSource(); return; }
    if (state.reorderId) { var id = state.reorderId; settings.favoriteIds = reorderSnapshot.slice(); state.reorderId = ""; render(); restoreFocus(id); return; }
    if (state.view !== "home") { showView("home", true); return; }
    modal("Leave Dusk?", "You can open Samsung Home or close this app.", [
      { label: "Samsung Home", run: function () { launchApp(settings.samsungHomeAppId, "Samsung Home"); } },
      { label: "Close Dusk", run: exit }
    ]);
  }
  function exit() {
    try { if (!window.tizen) { throw new Error("Tizen Application API unavailable"); } tizen.application.getCurrentApplication().exit(); }
    catch (error) { toast("Could not close Dusk: " + errorText(error)); }
  }
  function action(name) {
    if (name === "open-apps") { showView("apps", true); }
    if (name === "refresh") { refresh(); toast("Refreshing apps and inputs…"); }
    if (name === "samsung-home") { launchApp(settings.samsungHomeAppId, "Samsung Home"); }
    if (name === "native-settings") { launchApp("org.tizen.menu", "TV settings"); }
    if (name === "reset-favorites") { explicitFavorites = false; settings.favoriteMode = "automatic"; settings.favoriteIds = []; saveRefreshedFavorites = true; refresh(); }
    if (name === "exit") { exit(); }
  }
  document.addEventListener("click", function (event) {
    var button = event.target.closest("button"); if (!button) { return; }
    var view = button.getAttribute("data-view"), id = button.getAttribute("data-item"), command = button.getAttribute("data-action");
    if (view) { showView(view, true); } else if (id) { activateItem(id); } else if (command) { action(command); }
  });
  document.addEventListener("keydown", function (event) {
    var key = event.keyCode;
    if (key === 10009 || key === 27 || key === 8 && event.target.tagName !== "INPUT") { event.preventDefault(); clearTimeout(holdTimer); holdTimer = null; enterTarget = null; back(); return; }
    if (state.sourceActive) { return; }
    if (event.target.tagName === "INPUT") { if (key === 40) { event.preventDefault(); navigate(0, 1); } return; }
    if (key >= 37 && key <= 40) {
      event.preventDefault(); clearTimeout(holdTimer); holdTimer = null; enterTarget = null;
      navigate(key === 37 ? -1 : key === 39 ? 1 : 0, key === 38 ? -1 : key === 40 ? 1 : 0); return;
    }
    if (key === 13) {
      event.preventDefault();
      if (enterTarget || enterHeld || event.repeat) { return; }
      enterTarget = document.activeElement;
      var id = document.activeElement.getAttribute("data-item");
      if (id && !state.reorderId) { holdTimer = setTimeout(function () { holdTimer = null; enterHeld = true; itemOptions(id); }, 650); }
    }
  });
  document.addEventListener("keyup", function (event) {
    if (event.keyCode !== 13) { return; } event.preventDefault();
    clearTimeout(holdTimer); holdTimer = null;
    var target = enterTarget; enterTarget = null;
    if (enterHeld) { enterHeld = false; return; }
    if (target && target === document.activeElement && target.tagName === "BUTTON") { target.click(); }
  });
  document.addEventListener("visibilitychange", function () {
    clearTimeout(holdTimer); holdTimer = null; enterHeld = false; enterTarget = null; state.busy = false;
    document.body.classList.toggle("background-paused", document.hidden);
    if (!document.hidden) { tick(); refreshSources(); }
    updateAmbientVideo(false);
  });
  function readRequestedControl() {
    try {
      if (window.tizen && tizen.application) { return tizen.application.getCurrentApplication().getRequestedAppControl(); }
    } catch (ignore) {}
    return null;
  }
  function homeRequestInfo(request) {
    var rawOperation = request && request.appControl && request.appControl.operation;
    var operation = rawOperation === null || typeof rawOperation === "undefined" || rawOperation === "undefined" ? "" : rawOperation;
    var isHome = !!(request && request.appControl) && (!operation || operation === "http://tizen.org/appcontrol/operation/main" || operation === "http://tizen.org/appcontrol/operation/default");
    var data = request && request.appControl && request.appControl.data || [], entryPoints = [], stamp = "";
    for (var i = 0; i < data.length; i++) {
      var values = typeof data[i].value === "string" ? [data[i].value] : Array.prototype.slice.call(data[i].value || []);
      if (data[i].key === "entry_point") { entryPoints = values; }
      if (data[i].key === "__K_APP_LAUNCH_START_TIME" && values.length) { stamp = String(values[0] === null || typeof values[0] === "undefined" ? "" : values[0]).trim(); }
    }
    if (stamp === "undefined" || stamp === "null") { stamp = ""; }
    return { rawOperation: rawOperation, operationType: typeof rawOperation, isHome: isHome, entryPoints: entryPoints, stamp: stamp, nativeHome: entryPoints.indexOf("smarthub_key") >= 0, requestedControl: !!request, callerAppId: request && request.callerAppId || "" };
  }
  function logHomeRequest(info, origin, action) {
    var log = window.DUSK_DIAGNOSTICS || (window.DUSK_DIAGNOSTICS = []);
    log.push({ time: new Date().toISOString(), operation: String(info.rawOperation), operationType: info.operationType, entryPoints: info.entryPoints, launchStamp: info.stamp, requestedControl: info.requestedControl, callerAppId: info.callerAppId, origin: origin, action: action });
    if (log.length > 20) { log.splice(0, log.length - 20); }
  }
  function markHomeRequestBaseline() {
    var info = homeRequestInfo(readRequestedControl());
    if (info.nativeHome && info.stamp) { lastNativeHomeStamp = info.stamp; }
  }
  function handleHomeRequest(request, origin) {
    var info = homeRequestInfo(request);
    if (!info.isHome || origin === "poll" && (!info.nativeHome || !info.stamp)) {
      if (origin !== "poll") { logHomeRequest(info, origin, "ignored"); }
      return false;
    }
    if (info.nativeHome && info.stamp) {
      if (info.stamp === lastNativeHomeStamp) { if (origin !== "poll") { logHomeRequest(info, origin, "duplicate"); } return false; }
      lastNativeHomeStamp = info.stamp;
    }
    logHomeRequest(info, origin, "home");
    clearTimeout(holdTimer); holdTimer = null; enterHeld = false; enterTarget = null;
    $("modal").hidden = true; state.modalReturn = null; state.busy = false;
    showView("home", true); tick(); refreshSources();
    return true;
  }
  // Some TV builds update the requested native Home bundle without delivering appcontrol.
  // Only a fresh smarthub_key launch stamp is a fallback Home request; visibility is not.
  window.addEventListener("appcontrol", function () { handleHomeRequest(readRequestedControl(), "event"); });
  setInterval(function () { if (!document.hidden) { handleHomeRequest(readRequestedControl(), "poll"); } }, 500);
  window.addEventListener("blur", function () { clearTimeout(holdTimer); holdTimer = null; enterHeld = false; enterTarget = null; });
  function fit() { var scale = Math.min(window.innerWidth / 1920, window.innerHeight / 1080); document.body.style.width = "1920px"; document.body.style.height = "1080px"; document.body.style.transformOrigin = "0 0"; document.body.style.transform = "scale(" + scale + ")"; }
  window.addEventListener("resize", fit); fit();
  window.addEventListener("unload", function () { try { if (sourceWatchId !== null) { tizen.systeminfo.removePropertyValueChangeListener(sourceWatchId); } } catch (ignore) {} });
  markHomeRequestBaseline(); startWallpapers(); startAmbientVideo(); tick(); setInterval(tick, 15000); refresh(); watchSources();
}());
