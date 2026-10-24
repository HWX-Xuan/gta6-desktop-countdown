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
      kicker: "GTA VI · DESKTOP WIDGET",
      title: "效果设置",
      subtitle: "把桌面倒计时调成你喜欢的样子。",
      appearance: "外观",
      appearanceHint: "大小、主题色和动效",
      numberScale: "数字大小",
      small: "小",
      large: "大",
      accent: "主题色",
      accentHint: "霓虹光晕的颜色",
      motion: "动态效果",
      motionHint: "开场动画与每分钟进度高光（关闭可进一步省电）",
      background: "背景效果",
      backgroundHint: "全部实时作用于桌面层",
      blur: "背景模糊",
      soft: "柔和",
      heavy: "强烈",
      brightness: "背景亮度",
      dark: "暗",
      bright: "亮",
      saturation: "色彩饱和度",
      muted: "淡",
      vivid: "浓",
      glow: "霓虹光晕",
      clean: "干净",
      dreamy: "梦幻",
      grain: "颗粒噪点",
      filmic: "胶片",
      vignette: "画面暗角",
      open: "开阔",
      focused: "聚焦",
      system: "系统",
      systemHint: "随 Windows 一起，安静地待在桌面",
      autoStart: "开机自动启动",
      autoStartHint: "登录 Windows 后自动启动，使用静默启动器",
      autoStartOn: "已开启",
      autoStartOff: "已关闭",
      trayIcon: "显示托盘图标",
      trayIconHint: "在任务栏右下角显示 VI 图标；关闭后可用 Ctrl+Alt+S 打开设置",
      dataFolder: "数据文件夹",
      dataFolderHint: "日志、壁纸缓存和设置文件都在这里",
      openFolder: "打开",
      reset: "恢复默认",
      saved: "已自动保存",
      saving: "正在保存…",
      saveError: "保存失败",
      autoStartError: "开机自启设置失败",
      close: "完成",
      preview: "实时预览",
      live: "LIVE",
      previewHeadline: "发售倒计时",
      progress: "等待进度",
      previewNote: "预览会跟随滑杆实时变化，并同步到桌面。",
      shortcutHint: "随时打开设置",
      asideNote: "壁纸始终来自你当前的 Windows 桌面。修改壁纸后，倒计时会自动同步新的背景。",
      about: "版本与更新",
      updateChecking: "正在检查更新…",
      updateCheckingHint: "连接 GitHub 发布页面",
      checkNow: "检查更新",
      openRelease: "打开发布页面",
      updateAvailable: "发现新版本",
      updateUpToDate: "已是最新版本",
      updateUnknown: "暂时无法检查更新",
      updateUnconfigured: "开发版本未配置发布源",
      updateAvailableHint: "最新版本为 v{version}，可前往发布页面下载",
      updateUpToDateHint: "当前版本 v{version} 已是最新",
      updateUnknownHint: "网络不可用或仓库暂无可用的 Release",
      updateUnconfiguredHint: "发布前请在 UpdateService.cs 中填写 GitHub 仓库地址",
      updateCheckingAgain: "检查中…"
    },
    en: {
      kicker: "GTA VI · DESKTOP WIDGET",
      title: "Effect settings",
      subtitle: "Tune the desktop countdown to your taste.",
      appearance: "Appearance",
      appearanceHint: "Scale, accent and motion",
      numberScale: "Number size",
      small: "Small",
      large: "Large",
      accent: "Accent color",
      accentHint: "Colour of the neon glow",
      motion: "Motion",
      motionHint: "Intro animation and a minute-change highlight (turn off to save power)",
      background: "Background effects",
      backgroundHint: "Applied live to the desktop layer",
      blur: "Wallpaper blur",
      soft: "Soft",
      heavy: "Heavy",
      brightness: "Brightness",
      dark: "Dark",
      bright: "Bright",
      saturation: "Saturation",
      muted: "Muted",
      vivid: "Vivid",
      glow: "Neon glow",
      clean: "Clean",
      dreamy: "Dreamy",
      grain: "Film grain",
      filmic: "Filmic",
      vignette: "Vignette",
      open: "Open",
      focused: "Focused",
      system: "System",
      systemHint: "Quietly part of Windows",
      autoStart: "Start with Windows",
      autoStartHint: "Launches silently after you sign in",
      autoStartOn: "On",
      autoStartOff: "Off",
      trayIcon: "Show tray icon",
      trayIconHint: "Shows the VI icon in the notification area. If hidden, press Ctrl+Alt+S to reopen settings",
      dataFolder: "Data folder",
      dataFolderHint: "Logs, wallpaper cache and settings live here",
      openFolder: "Open",
      reset: "Reset defaults",
      saved: "Auto-saved",
      saving: "Saving…",
      saveError: "Save failed",
      autoStartError: "Could not update the startup setting",
      close: "Done",
      preview: "Live preview",
      live: "LIVE",
      previewHeadline: "RELEASE COUNTDOWN",
      progress: "WAIT PROGRESS",
      previewNote: "The preview follows the sliders and updates the desktop at the same time.",
      shortcutHint: "Open settings anytime",
      asideNote: "The background is always your current Windows wallpaper. It is refreshed automatically when you change it.",
      about: "Version & updates",
      updateChecking: "Checking for updates…",
      updateCheckingHint: "Contacting the GitHub release page",
      checkNow: "Check now",
      openRelease: "Open releases",
      updateAvailable: "Update available",
      updateUpToDate: "You are up to date",
      updateUnknown: "Update check unavailable",
      updateUnconfigured: "Release source not configured",
      updateAvailableHint: "Latest version is v{version}. Open the release page to download.",
      updateUpToDateHint: "Version v{version} is the latest",
      updateUnknownHint: "No network access or no release published yet",
      updateUnconfiguredHint: "Set the GitHub repository in UpdateService.cs before publishing",
      updateCheckingAgain: "Checking…"
    }
  };

  const params = new URLSearchParams(window.location.search);
  const webview = window.chrome && window.chrome.webview ? window.chrome.webview : null;

  const els = {
    ambientWallpaper: document.getElementById("ambientWallpaper"),
    previewWallpaper: document.getElementById("previewWallpaper"),
    previewStage: document.getElementById("previewStage"),
    previewHeadline: document.getElementById("previewHeadline"),
    previewDays: document.getElementById("previewDays"),
    previewHours: document.getElementById("previewHours"),
    previewMinutes: document.getElementById("previewMinutes"),
    previewFill: document.getElementById("previewFill"),
    previewProgress: document.getElementById("previewProgress"),
    saveState: document.getElementById("saveState"),
    saveStateText: document.getElementById("saveStateText"),
    autoStartToggle: document.getElementById("autoStartToggle"),
    autoStartStatus: document.getElementById("autoStartStatus"),
    trayToggle: document.getElementById("trayToggle"),
    motionToggle: document.getElementById("motionToggle"),
    accentCustom: document.getElementById("accentCustom"),
    languageButtons: Array.from(document.querySelectorAll("[data-lang]")),
    swatches: Array.from(document.querySelectorAll(".swatch")),
    resetButton: document.getElementById("resetButton"),
    closeButton: document.getElementById("closeButton"),
    openDataButton: document.getElementById("openDataButton"),
    versionValue: document.getElementById("versionValue"),
    updateIcon: document.getElementById("updateIcon"),
    updateTitle: document.getElementById("updateTitle"),
    updateDesc: document.getElementById("updateDesc"),
    checkUpdateButton: document.getElementById("checkUpdateButton"),
    releaseButton: document.getElementById("releaseButton")
  };

  const controls = [
    { id: "blur", key: "blur", from: (v) => v, to: (v) => v, format: (v) => `${Math.round(v)} px` },
    { id: "brightness", key: "brightness", from: (v) => v * 100, to: (v) => v / 100, format: (v) => `${Math.round(v)}%` },
    { id: "saturation", key: "saturation", from: (v) => v * 100, to: (v) => v / 100, format: (v) => `${Math.round(v)}%` },
    { id: "glow", key: "glow", from: (v) => v * 100, to: (v) => v / 100, format: (v) => `${Math.round(v)}%` },
    { id: "grain", key: "grain", from: (v) => v * 100, to: (v) => v / 100, format: (v) => `${Math.round(v)}%` },
    { id: "vignette", key: "vignette", from: (v) => v * 100, to: (v) => v / 100, format: (v) => `${Math.round(v)}%` },
    { id: "numberScale", key: "numberScale", from: (v) => v * 100, to: (v) => v / 100, format: (v) => `${Math.round(v)}%` }
  ].map((definition) => ({
    ...definition,
    input: document.getElementById(definition.id),
    output: document.getElementById(`${definition.id}Value`)
  }));

  let settings = { ...DEFAULTS };
  let autoStart = false;
  const state = {
    version: params.get("version") || "",
    update: null
  };
  let statusMode = "saved";
  let saveTimer = 0;
  let saveError = "";

  // Headless preview overrides, useful for screenshot checks.
  if (params.has("lang")) settings.language = params.get("lang");
  for (const key of ["blur", "brightness", "saturation", "glow", "grain", "vignette", "numberScale"]) {
    if (params.has(key)) settings[key] = Number(params.get(key));
  }
  if (params.has("accent")) settings.accent = `#${params.get("accent").replace(/^#/, "")}`;

  function clamp(value, min, max) {
    return Math.max(min, Math.min(max, value));
  }

  function copy(key) {
    const language = settings.language === "en" ? "en" : "zh";
    return I18N[language][key] || key;
  }

  function normalizeAccent(value) {
    return /^#[0-9a-f]{6}$/i.test(String(value)) ? String(value).toLowerCase() : DEFAULTS.accent;
  }

  function hexToRgb(hex) {
    const value = hex.replace("#", "");
    return [
      Number.parseInt(value.slice(0, 2), 16),
      Number.parseInt(value.slice(2, 4), 16),
      Number.parseInt(value.slice(4, 6), 16)
    ];
  }

  function mixWithWhite(hex, ratio) {
    const [r, g, b] = hexToRgb(hex).map((channel) => Math.round(channel + (255 - channel) * ratio));
    return `#${[r, g, b].map((channel) => channel.toString(16).padStart(2, "0")).join("")}`;
  }

  function applyAccent() {
    const root = document.documentElement.style;
    const accent = normalizeAccent(settings.accent);
    settings.accent = accent;
    root.setProperty("--accent", accent);
    root.setProperty("--accent-soft", mixWithWhite(accent, 0.4));
    root.setProperty("--accent-rgb", hexToRgb(accent).join(", "));

    els.swatches.forEach((swatch) => {
      swatch.classList.toggle("selected", swatch.dataset.accent === accent);
    });
    if (els.accentCustom.value.toLowerCase() !== accent) {
      els.accentCustom.value = accent;
    }
  }

  function applyPreview() {
    const stage = els.previewStage;
    stage.style.setProperty("--preview-blur", `${settings.blur}px`);
    stage.style.setProperty("--preview-brightness", settings.brightness.toFixed(3));
    stage.style.setProperty("--preview-saturation", settings.saturation.toFixed(3));
    stage.style.setProperty("--preview-vignette", settings.vignette.toFixed(3));
    stage.style.setProperty("--preview-grain", settings.grain.toFixed(3));
    stage.style.setProperty("--preview-glow", (0.3 * settings.glow).toFixed(3));
    stage.style.setProperty("--preview-glow-alpha", Math.min(1, 0.7 * settings.glow).toFixed(3));
  }

  function updateFill(input) {
    const min = Number(input.min);
    const max = Number(input.max);
    const value = Number(input.value);
    const fill = `${((value - min) / (max - min)) * 100}%`;
    input.style.setProperty("--fill", fill);
  }

  function syncControls() {
    controls.forEach((control) => {
      const displayed = control.from(settings[control.key]);
      control.input.value = displayed;
      control.output.textContent = control.format(displayed);
      updateFill(control.input);
    });

    els.motionToggle.setAttribute("aria-checked", String(settings.motion));
    els.trayToggle.setAttribute("aria-checked", String(settings.showTrayIcon));
    els.autoStartToggle.setAttribute("aria-checked", String(autoStart));

    els.autoStartStatus.textContent = autoStart ? copy("autoStartOn") : copy("autoStartOff");
    els.autoStartStatus.classList.toggle("on", autoStart);
  }

  function applyLanguage() {
    const language = settings.language === "en" ? "en" : "zh";
    document.documentElement.lang = language === "en" ? "en" : "zh-CN";
    document.title = language === "en" ? "GTA VI · Settings" : "GTA VI · 效果设置";

    document.querySelectorAll("[data-i18n]").forEach((element) => {
      const key = element.dataset.i18n;
      element.textContent = copy(key);
    });

    els.languageButtons.forEach((button) => {
      button.classList.toggle("active", button.dataset.lang === language);
    });

    els.previewHeadline.textContent = copy("previewHeadline");
    els.autoStartStatus.textContent = autoStart ? copy("autoStartOn") : copy("autoStartOff");
    renderUpdate();
    updateStatusText();
  }

  // ---------------------------------------------------------------
  // Version and update state
  // ---------------------------------------------------------------
  function formatCheckedAt(value) {
    if (!value) return "";
    const stamp = new Date(value);
    if (Number.isNaN(stamp.getTime())) return "";
    return stamp.toLocaleString(settings.language === "en" ? "en-US" : "zh-CN", {
      month: "2-digit",
      day: "2-digit",
      hour: "2-digit",
      minute: "2-digit"
    });
  }

  function renderUpdate() {
    const version = state.version || "1.2.0";
    const update = state.update || { state: "unknown" };
    const latest = update.latestVersion || "";

    els.versionValue.textContent = version;
    els.updateIcon.classList.remove("is-available", "is-ok", "is-unknown");

    const titleMap = {
      available: "updateAvailable",
      "up-to-date": "updateUpToDate",
      unknown: "updateUnknown",
      unconfigured: "updateUnconfigured"
    };

    let descKey;
    if (update.state === "available") {
      els.updateIcon.textContent = "↑";
      els.updateIcon.classList.add("is-available");
      descKey = "updateAvailableHint";
    } else if (update.state === "up-to-date") {
      els.updateIcon.textContent = "✓";
      els.updateIcon.classList.add("is-ok");
      descKey = "updateUpToDateHint";
    } else if (update.state === "unconfigured") {
      els.updateIcon.textContent = "!";
      els.updateIcon.classList.add("is-unknown");
      descKey = "updateUnconfiguredHint";
    } else {
      els.updateIcon.textContent = "?";
      els.updateIcon.classList.add("is-unknown");
      descKey = "updateUnknownHint";
    }

    els.updateTitle.textContent = copy(titleMap[update.state] || "updateUnknown");

    let description = copy(descKey).replace("{version}", update.state === "available" ? latest || version : version);
    const checkedAt = formatCheckedAt(update.checkedAt);
    if (checkedAt && update.state !== "unconfigured") {
      description += `  ·  ${checkedAt}`;
    }
    els.updateDesc.textContent = description;

    els.releaseButton.disabled = update.state === "unconfigured";
  }

  function updateStatusText() {
    els.saveState.classList.remove("is-saved", "is-saving", "is-error");
    if (statusMode === "saving") {
      els.saveState.classList.add("is-saving");
      els.saveStateText.textContent = copy("saving");
    } else if (statusMode === "error") {
      els.saveState.classList.add("is-error");
      els.saveStateText.textContent = saveError || copy("saveError");
    } else {
      els.saveState.classList.add("is-saved");
      els.saveStateText.textContent = copy("saved");
    }
  }

  function setStatus(mode, error = "") {
    statusMode = mode;
    saveError = error;
    updateStatusText();
  }

  function renderPreviewCountdown() {
    const remaining = Math.max(0, RELEASE_AT - Date.now());
    const wholeSeconds = Math.ceil(remaining / 1000);
    const days = Math.floor(wholeSeconds / 86400);
    const hours = Math.floor((wholeSeconds % 86400) / 3600);
    const minutes = Math.floor((wholeSeconds % 3600) / 60);
    const progress = Math.max(0, Math.min(100, ((Date.now() - WAIT_STARTED_AT) / (RELEASE_AT - WAIT_STARTED_AT)) * 100));

    els.previewDays.textContent = String(days).padStart(2, "0");
    els.previewHours.textContent = String(hours).padStart(2, "0");
    els.previewMinutes.textContent = String(minutes).padStart(2, "0");
    els.previewFill.style.width = `${remaining === 0 ? 100 : progress}%`;
    els.previewProgress.textContent = `${(Math.floor((remaining === 0 ? 100 : progress) * 10) / 10).toFixed(1)}%`;
  }

  function applyAll() {
    applyAccent();
    applyPreview();
    applyLanguage();
    syncControls();
    renderPreviewCountdown();
  }

  function payload() {
    return {
      type: "save",
      settings: { ...settings },
      autoStart
    };
  }

  function send(payloadObject) {
    if (webview) {
      webview.postMessage(payloadObject);
    }
  }

  function saveNow() {
    window.clearTimeout(saveTimer);
    setStatus("saving");
    if (webview) {
      send(payload());
    } else {
      window.setTimeout(() => setStatus("saved"), 260);
    }
  }

  function scheduleSave(immediate = false) {
    window.clearTimeout(saveTimer);
    setStatus("saving");
    if (immediate) {
      saveNow();
    } else {
      saveTimer = window.setTimeout(saveNow, 160);
    }
  }

  // Controls
  controls.forEach((control) => {
    control.input.addEventListener("input", () => {
      const displayed = Number(control.input.value);
      settings[control.key] = control.to(displayed);
      control.output.textContent = control.format(displayed);
      updateFill(control.input);
      applyPreview();
      scheduleSave();
    });
  });

  els.motionToggle.addEventListener("click", () => {
    settings.motion = !settings.motion;
    els.motionToggle.setAttribute("aria-checked", String(settings.motion));
    scheduleSave(true);
  });

  els.trayToggle.addEventListener("click", () => {
    settings.showTrayIcon = !settings.showTrayIcon;
    els.trayToggle.setAttribute("aria-checked", String(settings.showTrayIcon));
    scheduleSave(true);
  });

  els.autoStartToggle.addEventListener("click", () => {
    autoStart = !autoStart;
    syncControls();
    scheduleSave(true);
  });

  els.swatches.forEach((swatch) => {
    swatch.addEventListener("click", () => {
      settings.accent = normalizeAccent(swatch.dataset.accent);
      applyAccent();
      scheduleSave();
    });
  });

  els.accentCustom.addEventListener("input", () => {
    settings.accent = normalizeAccent(els.accentCustom.value);
    applyAccent();
    scheduleSave();
  });

  els.languageButtons.forEach((button) => {
    button.addEventListener("click", () => {
      settings.language = button.dataset.lang === "en" ? "en" : "zh";
      applyLanguage();
      syncControls();
      scheduleSave(true);
    });
  });

  els.resetButton.addEventListener("click", () => {
    settings = {
      ...DEFAULTS,
      language: settings.language,
      showTrayIcon: settings.showTrayIcon
    };
    // Reset appearance only; never silently turn off a system startup entry.
    applyAll();
    scheduleSave(true);
  });

  els.openDataButton.addEventListener("click", () => {
    send({ type: "openDataFolder" });
  });

  els.checkUpdateButton.addEventListener("click", () => {
    els.checkUpdateButton.disabled = true;
    els.updateTitle.textContent = copy("updateCheckingAgain");
    send({ type: "checkUpdate" });
    window.setTimeout(() => {
      els.checkUpdateButton.disabled = false;
    }, 1200);
  });

  els.releaseButton.addEventListener("click", () => {
    send({ type: "openRelease" });
  });

  els.closeButton.addEventListener("click", () => {
    send({ type: "close" });
  });

  window.addEventListener("keydown", (event) => {
    if (event.key === "Escape") {
      send({ type: "close" });
    }
  });

  // WebView host messages
  if (webview) {
    webview.addEventListener("message", (event) => {
      const message = event.data;
      if (!message || !message.type) return;

      if (message.type === "init") {
        settings = { ...DEFAULTS, ...(message.settings || {}) };
        autoStart = Boolean(message.autoStart);
        state.version = message.version || state.version;
        if (message.update) state.update = message.update;
        applyAll();
        renderUpdate();
        setStatus("saved");
      } else if (message.type === "updateState") {
        state.version = message.version || state.version;
        state.update = message.update || { state: "unknown" };
        els.checkUpdateButton.disabled = false;
        renderUpdate();
      } else if (message.type === "saved") {
        if (message.settings) {
          settings = { ...DEFAULTS, ...message.settings };
        }
        autoStart = Boolean(message.autoStart);
        applyAccent();
        applyPreview();
        syncControls();
        if (message.autoStartError) {
          setStatus("error", `${copy("autoStartError")}`);
        } else {
          setStatus("saved");
        }
      }
    });

    webview.postMessage({ type: "ready" });
  } else {
    // Standalone browser preview, used for screenshots and design checks.
    const previewStates = {
      available: { state: "available", latestVersion: "1.3.0", checkedAt: new Date().toISOString() },
      uptodate: { state: "up-to-date", checkedAt: new Date().toISOString() },
      unknown: { state: "unknown", checkedAt: new Date().toISOString() }
    };
    state.version = params.get("version") || "1.2.0";
    state.update = previewStates[params.get("update")] || previewStates.available;
    applyAll();
    renderUpdate();
    setStatus("saved");
  }

  const wallpaperSrc =
    window.location.protocol === "file:" ? "wallpaper-preview.jpg" : "https://gta6.data/wallpaper.jpg";
  els.ambientWallpaper.src = wallpaperSrc;
  els.previewWallpaper.src = wallpaperSrc;

  window.setInterval(renderPreviewCountdown, 1000);
})();
