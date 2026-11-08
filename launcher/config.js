/* Samsung Home ID verified on this TV by the native API and Vconf backup. */
window.DUSK_CONFIG = {
  samsungHomeAppId: "com.samsung.tv.csfs",
  locale: "en-US",
  wallpaper: "assets/navy-alpine.png",
  wallpapers: [
    { src: "assets/navy-alpine.png", periods: ["morning", "day", "night"] },
    { src: "assets/navy-forest-lake.png", periods: ["morning", "day", "evening"] },
    { src: "assets/bluehour-clouds.png", periods: ["evening", "night"] }
  ],
  wallpaperIntervalSeconds: 45,
  wallpaperFadeSeconds: 6,
  ambientVideos: [
    { src: "assets/snoopy-cards.mp4", poster: "assets/snoopy-cards.jpg" },
    { src: "assets/snoopy-sleep.mp4", poster: "assets/snoopy-sleep.jpg" },
    { src: "assets/snoopy-cinema.mp4", poster: "assets/snoopy-cinema.jpg" },
    { src: "assets/snoopy-space.mp4", poster: "assets/snoopy-space.jpg" },
    { src: "assets/snoopy-football.mp4", poster: "assets/snoopy-football.jpg" },
    { src: "assets/snoopy-records.mp4", poster: "assets/snoopy-records.jpg" },
    { src: "assets/snoopy-cardtower.mp4", poster: "assets/snoopy-cardtower.jpg" },
    { src: "assets/snoopy-painting.mp4", poster: "assets/snoopy-painting.jpg" },
    { src: "assets/snoopy-stargazing.mp4", poster: "assets/snoopy-stargazing.jpg" },
    { src: "assets/snoopy-stage.mp4", poster: "assets/snoopy-stage.jpg" }
  ],
  ambientIntervalSeconds: 45,
  weather: { city: "Oslo", latitude: 59.9139, longitude: 10.7522, timezone: "Europe/Oslo" },
  maxFavoriteApps: 5,
  sourceLabels: {},
  // Explicit opt-in for a system namespace app discovered by getAppsInfo.
  extraAllowedAppIds: [],
  // These opaque IDs identify system/promotional helpers in this TV's live inventory.
  hiddenAppIds: ["2025ePop01.SmartTV", "yL49PNFmjW.PromotionApp", "w2VwCK465D.OTApp", "25emanual2.emanual", "NuvioTV001.EngineFsService", "NuvioTV001.PluginService"]
};
