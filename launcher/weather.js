(function () {
  "use strict";
  var config = (window.DUSK_CONFIG || {}).weather;
  var root = document.getElementById("weather");
  if (!root || !config || !window.fetch) { return; }
  var latitude = Number(config.latitude), longitude = Number(config.longitude);
  if (!isFinite(latitude) || !isFinite(longitude) || Math.abs(latitude) > 90 || Math.abs(longitude) > 180) { return; }
  var key = "dusk.weather.v1", refreshAge = 30 * 60 * 1000, maxAge = 3 * 60 * 60 * 1000;
  var location = latitude + "," + longitude + "," + (config.timezone || "auto");
  var record = null, busy = false, lastAttempt = 0;
  var endpoint = "https://api.open-meteo.com/v1/forecast?latitude=" + latitude + "&longitude=" + longitude + "&current=temperature_2m%2Cweather_code%2Cis_day&timeformat=unixtime&timezone=" + encodeURIComponent(config.timezone || "auto");
  function condition(code, day) {
    if (code === 0) { return ["Clear", day ? "sun" : "moon"]; }
    if (code === 1) { return ["Mostly clear", day ? "sun" : "moon"]; }
    if (code === 2) { return ["Partly cloudy", "cloud"]; }
    if (code === 3) { return ["Cloudy", "cloud"]; }
    if (code === 45 || code === 48) { return ["Fog", "fog"]; }
    if ([51, 53, 55].indexOf(code) >= 0) { return ["Drizzle", "rain"]; }
    if ([56, 57, 66, 67].indexOf(code) >= 0) { return ["Freezing rain", "rain"]; }
    if ([61, 63, 65].indexOf(code) >= 0) { return ["Rain", "rain"]; }
    if ([71, 73, 75, 77].indexOf(code) >= 0) { return ["Snow", "snow"]; }
    if ([80, 81, 82].indexOf(code) >= 0) { return ["Showers", "rain"]; }
    if ([85, 86].indexOf(code) >= 0) { return ["Snow showers", "snow"]; }
    if ([95, 96, 97, 99].indexOf(code) >= 0) { return ["Thunderstorm", "thunder"]; }
    return null;
  }
  function usable(value, now) {
    return !!(value && value.location === location && typeof value.temperature === "number" && isFinite(value.temperature) &&
      typeof value.fetchedAt === "number" && typeof value.observedAt === "number" && value.fetchedAt > 0 && value.observedAt > 0 &&
      now - value.fetchedAt <= maxAge && now - value.observedAt <= maxAge && value.fetchedAt <= now + 300000 && value.observedAt <= now + 900000 &&
      (value.isDay === 0 || value.isDay === 1) && condition(value.code, value.isDay));
  }
  function icon(kind) {
    var ns = "http://www.w3.org/2000/svg", svg = document.createElementNS(ns, "svg");
    svg.setAttribute("viewBox", "0 0 28 28"); svg.setAttribute("fill", "none"); svg.setAttribute("stroke", "currentColor"); svg.setAttribute("stroke-width", "1.6"); svg.setAttribute("stroke-linecap", "round"); svg.setAttribute("stroke-linejoin", "round");
    function shape(tag, attributes) { var node = document.createElementNS(ns, tag); Object.keys(attributes).forEach(function (name) { node.setAttribute(name, attributes[name]); }); svg.appendChild(node); }
    if (kind === "sun") {
      shape("circle", {cx:14, cy:14, r:4.5}); shape("path", {d:"M14 2v3M14 23v3M2 14h3M23 14h3M5.5 5.5l2.2 2.2M20.3 20.3l2.2 2.2M5.5 22.5l2.2-2.2M20.3 7.7l2.2-2.2"});
    } else if (kind === "moon") {
      shape("path", {d:"M22.7 17.1A9.5 9.5 0 0 1 10.9 5.3 9.5 9.5 0 1 0 22.7 17.1Z"});
    } else {
      shape("path", {d:"M7 18a4.5 4.5 0 0 1-.8-8.9 6.5 6.5 0 0 1 12.4 0A4.5 4.5 0 1 1 21 18Z"});
      if (kind === "rain") { shape("path", {d:"M9 21l-1 3M15 21l-1 3M21 21l-1 3"}); }
      if (kind === "fog") { shape("path", {d:"M5 22h18M8 25h12"}); }
      if (kind === "snow") { shape("path", {d:"M10 21v5M7.8 22.2l4.4 2.6M7.8 24.8l4.4-2.6M20 21v5M17.8 22.2l4.4 2.6M17.8 24.8l4.4-2.6"}); }
      if (kind === "thunder") { shape("path", {d:"M15 19l-3 4h4l-3 4"}); }
    }
    return svg;
  }
  function render(now) {
    if (!usable(record, now)) { root.hidden = true; return; }
    var description = condition(record.code, record.isDay), target = document.getElementById("weather-icon");
    while (target.firstChild) { target.removeChild(target.firstChild); } target.appendChild(icon(description[1]));
    document.getElementById("weather-temperature").textContent = Math.round(record.temperature) + "°C";
    document.getElementById("weather-condition").textContent = description[0];
    document.getElementById("weather-city").textContent = config.city || "";
    document.getElementById("weather-cached").hidden = now - record.fetchedAt < refreshAge;
    root.hidden = false;
  }
  function refresh(resume) {
    var now = Date.now(); render(now);
    if (document.hidden || busy || usable(record, now) && now - record.fetchedAt < refreshAge || lastAttempt && now - lastAttempt < (resume ? 60000 : refreshAge)) { return; }
    busy = true; lastAttempt = now;
    var controller = window.AbortController ? new window.AbortController() : null, finished = false;
    var timeout = setTimeout(function () { finished = true; busy = false; if (controller) { controller.abort(); } render(Date.now()); }, 12000);
    function complete() { clearTimeout(timeout); busy = false; render(Date.now()); }
    window.fetch(endpoint, { credentials: "omit", signal: controller ? controller.signal : undefined }).then(function (response) {
      if (!response.ok) { throw new Error("Weather unavailable"); } return response.json();
    }).then(function (data) {
      if (finished) { return; }
      var current = data && data.current;
      var next = current && { location: location, fetchedAt: Date.now(), observedAt: typeof current.time === "number" ? current.time * 1000 : NaN, temperature: current.temperature_2m, code: current.weather_code, isDay: current.is_day };
      if (usable(next, Date.now())) { record = next; try { localStorage.setItem(key, JSON.stringify(record)); } catch (ignore) {} }
      complete();
    }).catch(function () { if (!finished) { complete(); } });
  }
  try { record = JSON.parse(localStorage.getItem(key) || "null"); } catch (ignore) {}
  refresh(false);
  setInterval(function () { refresh(false); }, 60000);
  document.addEventListener("visibilitychange", function () { if (!document.hidden) { refresh(true); } });
}());
