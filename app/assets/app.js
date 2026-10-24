/**
 * GTA VI desktop countdown
 * Release moment: 2026-11-18 19:00 Asia/Shanghai (UTC+8)
 * Wait clock starts at the first GTA VI trailer: 2023-12-04 22:00 Asia/Shanghai.
 */
(() => {
  "use strict";

  const RELEASE_AT = Date.parse("2026-11-18T19:00:00+08:00");
  const WAIT_STARTED_AT = Date.parse("2023-12-04T22:00:00+08:00");

  const DEFAULTS = {
    blur: 34,
    brightness: 0.52,
    saturation: 1.4,
    glow: 1,
    grain: 0.085,
    vignette: 0.9,
    numberScale: 1,
    motion: true,
    accent: "#ff2f96",
    language: "zh",
    showTrayIcon: true
  };

  const I18N = {
    zh: {
      title: "GTA VI · 发售倒计时",
      headline: "GTA VI\u00A0\u00A0发售倒计时",
      released: "GTA VI 现已发售",
      release: "发售日",
      days: "天",
      hours: "小时",
      minutes: "分钟",
      seconds: "秒",
      progress: "等待进度",
      footLeft: "首支预告\u00A0\u00A0/\u00A0\u00A02023.12.04",
      footMid: "等待即将结束",
      footRight: "发售\u00A0\u00A0/\u00A0\u00A02026.11.18"
    },
    en: {
      title: "GTA VI · Release Countdown",
      headline: "GTA VI\u00A0\u00A0RELEASE COUNTDOWN",
      released: "GTA VI IS OUT NOW",
      release: "RELEASE",
      days: "DAYS",
      hours: "HOURS",
      minutes: "MINUTES",
      seconds: "SECONDS",
      progress: "WAIT PROGRESS",
      footLeft: "FIRST TRAILER\u00A0\u00A0/\u00A0\u00A02023.12.04",
      footMid: "THE WAIT IS ALMOST OVER",
      footRight: "RELEASE\u00A0\u00A0/\u00A0\u00A02026.11.18"
    }
  };

  const params = new URLSearchParams(window.location.search);
  const fixedNow = params.has("now") ? Date.parse(params.get("now")) : NaN;
  const isStatic = params.get("static") === "1";

  const stage = document.getElementById("stage");
  const wallpaper = document.getElementById("wallpaper");
  const headline = document.getElementById("headline");
  const daysEl = document.getElementById("days");
  const hoursEl = document.getElementById("hours");
  const minutesEl = document.getElementById("minutes");
  const secondsEl = document.getElementById("seconds");
  const progressFill = document.getElementById("progressFill");
  const progressGlow = document.getElementById("progressGlow");
  const progressHead = document.getElementById("progressHead");
  const progressValue = document.getElementById("progressValue");
  const releaseLabel = document.getElementById("releaseLabel");
  const progressLabel = document.getElementById("progressLabel");
  const labelDays = document.getElementById("labelDays");
  const labelHours = document.getElementById("labelHours");
  const labelMinutes = document.getElementById("labelMinutes");
  const labelSeconds = document.getElementById("labelSeconds");
  const footLeft = document.getElementById("footLeft");
  const footMid = document.getElementById("footMid");
  const footRight = document.getElementById("footRight");
  const countdownEl = document.getElementById("countdown");

  const clamp = (value, min, max) => Math.max(min, Math.min(max, value));
  const numberOr = (value, fallback) => (Number.isFinite(Number(value)) ? Number(value) : fallback);

  function sanitize(raw) {
    const value = { ...DEFAULTS, ...(raw || {}) };
    value.blur = clamp(numberOr(value.blur, DEFAULTS.blur), 0, 60);
    value.brightness = clamp(numberOr(value.brightness, DEFAULTS.brightness), 0.25, 0.85);
    value.saturation = clamp(numberOr(value.saturation, DEFAULTS.saturation), 0.6, 2);
    value.glow = clamp(numberOr(value.glow, DEFAULTS.glow), 0, 1.5);
    value.grain = clamp(numberOr(value.grain, DEFAULTS.grain), 0, 0.2);
    value.vignette = clamp(numberOr(value.vignette, DEFAULTS.vignette), 0, 1);
    value.numberScale = clamp(numberOr(value.numberScale, DEFAULTS.numberScale), 0.75, 1.25);
    value.motion = value.motion !== false;
    value.accent = /^#[0-9a-f]{6}$/i.test(String(value.accent)) ? String(value.accent).toLowerCase() : DEFAULTS.accent;
    value.language = String(value.language).toLowerCase() === "en" ? "en" : "zh";
    value.showTrayIcon = value.showTrayIcon !== false;
    return value;
  }

  function queryOverrides() {
    const overrides = {};
    if (params.has("lang")) overrides.language = params.get("lang");
    if (params.has("accent")) overrides.accent = `#${params.get("accent").replace(/^#/, "")}`;
    for (const key of ["blur", "brightness", "saturation", "glow", "grain", "vignette", "numberScale"]) {
      if (params.has(key)) overrides[key] = Number(params.get(key));
    }
    if (params.has("motion")) overrides.motion = params.get("motion") !== "0";
    return overrides;
  }

  let settings = sanitize({
    ...(window.__WIDGET_SETTINGS__ || {}),
    ...queryOverrides()
  });
  let lastLanguage = "";
  let lastReleased = null;

  if (isStatic) {
    stage.classList.add("static");
  }

  // ---------------------------------------------------------------
  // Colour helpers
  // ---------------------------------------------------------------
  function hexToRgb(hex) {
    const value = hex.replace("#", "");
    return [
      Number.parseInt(value.slice(0, 2), 16),
      Number.parseInt(value.slice(2, 4), 16),
      Number.parseInt(value.slice(4, 6), 16)
    ];
  }

  function mixWithWhite(hex, ratio) {
    const [r, g, b] = hexToRgb(hex).map((channel) =>
      Math.round(channel + (255 - channel) * ratio)
    );
    return `#${[r, g, b].map((channel) => channel.toString(16).padStart(2, "0")).join("")}`;
  }

  function applyVisualSettings() {
    const root = document.documentElement.style;
    root.setProperty("--bg-blur", `${settings.blur}px`);
    root.setProperty("--bg-brightness", settings.brightness.toFixed(3));
    root.setProperty("--bg-saturation", settings.saturation.toFixed(3));
    root.setProperty("--vignette-opacity", settings.vignette.toFixed(3));
    root.setProperty("--grain-opacity", settings.grain.toFixed(3));
    root.setProperty("--number-scale", settings.numberScale.toFixed(3));

    const accent = settings.accent;
    root.setProperty("--accent", accent);
    root.setProperty("--accent-soft", mixWithWhite(accent, 0.36));
    root.setProperty("--accent-rgb", hexToRgb(accent).join(", "));

    const glow = settings.glow;
    const alphas = [0.26, 0.7, 0.5, 0.3, 0.16];
    ["--glow-a", "--glow-b", "--glow-c", "--glow-d", "--glow-e"].forEach((name, index) => {
      root.setProperty(name, clamp(alphas[index] * glow, 0, 1).toFixed(3));
    });

    document.body.classList.toggle("no-motion", !settings.motion);
  }

  function applyLanguage() {
    const language = settings.language === "en" ? "en" : "zh";
    const copy = I18N[language];
    const released = lastReleased === true;

    document.documentElement.lang = language === "en" ? "en" : "zh-CN";
    document.body.classList.toggle("lang-en", language === "en");
    document.title = copy.title;

    releaseLabel.textContent = copy.release;
    progressLabel.textContent = copy.progress;
    labelDays.textContent = copy.days;
    labelHours.textContent = copy.hours;
    labelMinutes.textContent = copy.minutes;
    labelSeconds.textContent = copy.seconds;
    footLeft.textContent = copy.footLeft;
    footMid.textContent = copy.footMid;
    footRight.textContent = copy.footRight;
    headline.textContent = released ? copy.released : copy.headline;

    lastLanguage = language;
  }

  function updateAriaLabel(days, hours, minutes, seconds, released) {
    if (settings.language === "en") {
      countdownEl.setAttribute(
        "aria-label",
        released
          ? "GTA VI is out now"
          : `GTA VI releases in ${days} days ${hours} hours ${minutes} minutes ${seconds} seconds`
      );
    } else {
      countdownEl.setAttribute(
        "aria-label",
        released
          ? "GTA VI 现已发售"
          : `距离 GTA VI 发售还有 ${days} 天 ${hours} 小时 ${minutes} 分钟 ${seconds} 秒`
      );
    }
  }

  // ---------------------------------------------------------------
  // Public settings hook, called by the WPF host.
  // ---------------------------------------------------------------
  window.applySettings = function applySettings(next) {
    settings = sanitize({ ...settings, ...(next || {}) });
    applyVisualSettings();
    applyLanguage();
    render(currentTime());
  };

  // ---------------------------------------------------------------
  // Wallpaper
  // ---------------------------------------------------------------
  const fallbackWallpaper =
    window.location.protocol === "file:" ? "wallpaper-preview.jpg" : "https://gta6.data/wallpaper.jpg";
  const wallpaperUrl = params.get("wallpaper") || fallbackWallpaper;

  function setWallpaper(url) {
    wallpaper.src = `${url}${url.includes("?") ? "&" : "?"}v=${Date.now()}`;
  }

  function reveal() {
    stage.classList.add("is-ready");
  }

  wallpaper.addEventListener("load", reveal, { once: true });
  wallpaper.addEventListener("error", reveal, { once: true });
  setWallpaper(wallpaperUrl);

  // Called from the desktop host when the Windows wallpaper changes.
  window.reloadWallpaper = function reloadWallpaper(url) {
    if (url) {
      wallpaper.src = `${url}${url.includes("?") ? "&" : "?"}v=${Date.now()}`;
    } else {
      setWallpaper(wallpaperUrl);
    }
  };

  if (isStatic) {
    reveal();
  } else {
    window.setTimeout(reveal, 1600);
  }

  // ---------------------------------------------------------------
  // Clock
  // ---------------------------------------------------------------
  const pad = (value) => String(value).padStart(2, "0");
  const last = { days: "", hours: "", minutes: "", seconds: "", progress: "" };

  function setText(element, key, value) {
    if (last[key] === value) return;
    last[key] = value;
    element.textContent = value;
  }

  function setProgress(percent) {
    const value = Math.max(0, Math.min(100, percent));
    const width = `${value.toFixed(3)}%`;
    // Truncate instead of rounding: the readout should only advance once earned.
    const text = `${(Math.floor(value * 10) / 10).toFixed(1)}%`;

    progressFill.style.width = width;
    progressGlow.style.left = width;
    progressHead.style.left = width;
    if (last.progress !== text) {
      last.progress = text;
      progressValue.textContent = text;
    }
  }

  function render(now) {
    const remaining = Math.max(0, RELEASE_AT - now);
    const wholeSeconds = Math.ceil(remaining / 1000);
    const days = Math.floor(wholeSeconds / 86_400);
    const hours = Math.floor((wholeSeconds % 86_400) / 3_600);
    const minutes = Math.floor((wholeSeconds % 3_600) / 60);
    const seconds = wholeSeconds % 60;

    setText(daysEl, "days", pad(days));
    setText(hoursEl, "hours", pad(hours));
    setText(minutesEl, "minutes", pad(minutes));
    setText(secondsEl, "seconds", pad(seconds));

    const progress = ((now - WAIT_STARTED_AT) / (RELEASE_AT - WAIT_STARTED_AT)) * 100;
    setProgress(remaining === 0 ? 100 : progress);

    const released = remaining === 0;
    stage.classList.toggle("is-released", released);

    if (lastReleased !== released) {
      lastReleased = released;
      applyLanguage();
    } else if (lastLanguage !== (settings.language === "en" ? "en" : "zh")) {
      applyLanguage();
    }

    updateAriaLabel(days, hours, minutes, seconds, released);
  }

  function currentTime() {
    return Number.isFinite(fixedNow) ? fixedNow : Date.now();
  }

  function start() {
    applyVisualSettings();
    applyLanguage();
    render(currentTime());

    if (!Number.isFinite(fixedNow)) {
      window.setInterval(() => render(currentTime()), 250);

      // Resync immediately after sleep / resume or when the widget becomes visible.
      document.addEventListener("visibilitychange", () => {
        if (!document.hidden) render(currentTime());
      });
      window.addEventListener("focus", () => render(currentTime()));
    }

    if (isStatic) {
      // Make the intro-dependent layers visible in deterministic screenshots.
      document.querySelectorAll(".unit, .rule, .eyebrow, .release, .heading, .progress").forEach((el) => {
        el.style.opacity = "1";
        el.style.filter = "none";
      });
    }
  }

  start();
})();
