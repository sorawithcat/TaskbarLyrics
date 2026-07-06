const sourceNames = {
  QQMusic: "QQ音乐",
  Netease: "网易云音乐",
  Kugou: "酷狗音乐",
  Spotify: "Spotify"
};

const sourceIcons = {
  QQMusic: "../../Assets/PlayerIcons/QQ音乐.png",
  Netease: "../../Assets/PlayerIcons/网易云音乐.png",
  Kugou: "../../Assets/PlayerIcons/酷狗音乐.png",
  Spotify: "../../Assets/PlayerIcons/spotify.png"
};

const presetForegroundColors = {
  Dark: "#FF111827",
  Light: "#FFFFFFFF"
};

let state = null;
let fonts = [];
let draggedSource = null;
let updateReleaseUrl = "";

const bridge = {
  post(message) {
    window.chrome?.webview?.postMessage(JSON.stringify(message));
  }
};

function updateSetting(key, value) {
  if (!state) return;
  value = normalizeSettingValue(key, value);
  state[key] = value;
  if (key === "foregroundColorMode") updateForegroundMode(value);
  if (key === "foregroundColor") {
    state.foregroundColorMode = "Custom";
    updateSwatch(value);
    updateColorModeControl();
  }
  if (key === "enableSpectrum") updateSpectrumChildren();
  updateDimensionSteppers(key);
  updateRangeControls();
  animateSettingFeedback(key);
  bridge.post({ type: "update", key, value });
}

function normalizeSettingValue(key, value) {
  if (!state) return value;
  const stepper = document.querySelector(`.stepper[data-key="${key}"]`);
  const rangeControl = document.querySelector(`.range-control [data-key="${key}"]`);
  if (!stepper && !rangeControl) return value;
  const range = stepper ? getStepperRange(stepper) : getRangeControlRange(rangeControl);
  return clamp(Number(value), range.min, range.max);
}

function animateSettingFeedback(key) {
  const control = document.querySelector(`[data-key="${key}"]`);
  const row = control?.closest(".player, .row, .form-row");
  if (!row) return;

  row.classList.remove("setting-updated");
  row.offsetWidth;
  row.classList.add("setting-updated");
}

function setState(nextState, fontList = fonts) {
  state = nextState;
  fonts = fontList;
  renderFonts();
  renderControls();
  renderOrder();
  renderAbout();
}

function renderFonts() {
  const select = document.querySelector('select[data-key="fontFamily"]');
  if (!select || select.options.length > 0) return;

  for (const font of fonts) {
    const option = document.createElement("option");
    option.value = typeof font === "string" ? font : font.value;
    option.textContent = typeof font === "string" ? font : font.label;
    select.appendChild(option);
  }
}

function renderControls() {
  if (!state) return;

  for (const input of document.querySelectorAll("[data-key]")) {
    const key = input.dataset.key;
    if (!(key in state)) continue;

    if (input.type === "checkbox") {
      input.checked = Boolean(state[key]);
    } else if (input.tagName === "TEXTAREA" && Array.isArray(state[key])) {
      input.value = state[key].join("\n");
    } else {
      input.value = state[key] ?? "";
    }
  }

  updateSwatch(state.foregroundColor);
  updateColorModeControl();
  updateSpectrumChildren();
  updateDimensionSteppers();
  updateRangeControls();
}

function updateSpectrumChildren() {
  const enabled = Boolean(state?.enableSpectrum);
  document.querySelectorAll(".spectrum-child").forEach((row) => {
    row.classList.toggle("disabled", !enabled);
    row.querySelectorAll("input, select, textarea, button").forEach((control) => {
      control.disabled = !enabled;
    });
  });
}

function getStepperRange(stepper) {
  const safeKey = stepper.dataset.safeKey;
  const useSafeRange = safeKey ? Boolean(state?.[safeKey]) : false;
  const min = Number(useSafeRange ? stepper.dataset.safeMin : stepper.dataset.extendedMin ?? stepper.dataset.min);
  const max = Number(useSafeRange ? stepper.dataset.safeMax : stepper.dataset.extendedMax ?? stepper.dataset.max);
  return {
    min: Number.isFinite(min) ? min : Number(stepper.dataset.min),
    max: Number.isFinite(max) ? max : Number(stepper.dataset.max)
  };
}

function getRangeControlRange(input) {
  return {
    min: Number(input.min),
    max: Number(input.max)
  };
}

function updateRangeValue(input) {
  const output = document.querySelector(`[data-value-for="${input.dataset.key}"]`);
  if (!output) return;
  output.textContent = `${input.value} px`;
}

function getCoverCornerRadiusMax() {
  const coverSize = Number(state?.coverSize);
  return Math.max(0, Math.floor((Number.isFinite(coverSize) ? coverSize : 40) / 2));
}

function updateRangeControls(changedKey = "") {
  document.querySelectorAll('.range-control input[type="range"][data-key]').forEach((input) => {
    const key = input.dataset.key;
    if (changedKey && key !== changedKey && !(key === "coverCornerRadius" && changedKey === "coverSize")) return;

    if (key === "coverCornerRadius") {
      input.max = String(getCoverCornerRadiusMax());
    }

    const min = Number(input.min);
    const max = Number(input.max);
    const current = Number(input.value);
    const next = clamp(Number.isFinite(current) ? current : Number(state?.[key] ?? min), min, max);
    input.value = String(next);
    if (state && key in state && Number(state[key]) !== next) {
      state[key] = next;
      bridge.post({ type: "update", key, value: next });
    }

    updateRangeValue(input);
  });
}

function updateDimensionSteppers(changedKey = "") {
  if (!state) return;
  document.querySelectorAll(".stepper[data-safe-key]").forEach((stepper) => {
    const key = stepper.dataset.key;
    const safeKey = stepper.dataset.safeKey;
    if (changedKey && changedKey !== key && changedKey !== safeKey) return;

    const range = getStepperRange(stepper);
    stepper.dataset.min = String(range.min);
    stepper.dataset.max = String(range.max);

    if (!(key in state)) return;
    const decimals = Number(stepper.dataset.decimals);
    const next = clamp(Number(state[key]), range.min, range.max);
    if (next !== Number(state[key])) {
      state[key] = next;
      bridge.post({ type: "update", key, value: next });
    }

    const input = stepper.querySelector("input");
    if (input) input.value = formatNumber(Number(state[key]), decimals);
  });
}

function updateSwatch(color) {
  const swatch = document.getElementById("colorSwatch");
  if (!swatch) return;
  const normalized = normalizeCssColor(color);
  swatch.style.background = normalized;
  const value = document.getElementById("colorValue");
  if (value) value.textContent = color ?? "";
  swatch.animate(
    [{ transform: "scale(1)" }, { transform: "scale(1.14)" }, { transform: "scale(1)" }],
    { duration: 160, easing: "ease-out" }
  );
}

function updateForegroundMode(mode) {
  if (mode in presetForegroundColors) {
    state.foregroundColor = presetForegroundColors[mode];
    updateSwatch(state.foregroundColor);
  }
  updateColorModeControl();
}

function updateColorModeControl() {
  const picker = document.getElementById("colorPicker");
  if (!picker || !state) return;
  picker.disabled = state.foregroundColorMode !== "Custom";
  picker.title = picker.disabled ? "选择自定义后可设置颜色" : "选择自定义颜色";
}

function normalizeCssColor(color) {
  if (typeof color !== "string") return "#fff";
  if (/^#[0-9a-fA-F]{8}$/.test(color)) {
    return `#${color.slice(3)}`;
  }
  return color;
}

function renderOrder() {
  const order = document.getElementById("sourceOrder");
  if (!order || !state) return;

  order.innerHTML = "";
  for (const source of state.sourceRecognitionOrder ?? []) {
    const item = document.createElement("div");
    item.className = "order-item";
    item.draggable = true;
    item.dataset.source = source;
    item.innerHTML = `<span class="handle">⋮⋮</span><img class="order-icon" src="${sourceIcons[source] ?? ""}" alt=""><strong>${sourceNames[source] ?? source}</strong>`;
    item.addEventListener("dragstart", onOrderDragStart);
    item.addEventListener("dragover", onOrderDragOver);
    item.addEventListener("drop", onOrderDrop);
    item.addEventListener("dragend", onOrderDragEnd);
    order.appendChild(item);
  }
}

function renderAbout() {
  if (!state) return;

  const version = document.getElementById("appVersion");
  if (version) version.textContent = `当前版本 ${state.appVersion || "--"}`;
}

function setUpdateStatus(payload) {
  const status = document.getElementById("updateStatus");
  const checkButton = document.getElementById("checkUpdateButton");
  const releaseButton = document.getElementById("openReleaseButton");
  const stateName = payload?.state || "";

  if (status) {
    status.textContent = payload?.message || "从 GitHub Releases 检查是否有新版本。";
    status.dataset.state = stateName;
  }

  if (checkButton) {
    checkButton.disabled = stateName === "checking";
    checkButton.textContent = stateName === "checking" ? "检查中" : "检查更新";
  }

  updateReleaseUrl = payload?.url || "";
  releaseButton?.classList.toggle("hidden", stateName !== "available");
}

function onOrderDragStart(event) {
  draggedSource = event.currentTarget.dataset.source;
  event.currentTarget.classList.add("dragging");
  event.dataTransfer.effectAllowed = "move";
}

function onOrderDragOver(event) {
  event.preventDefault();
  event.dataTransfer.dropEffect = "move";
}

function onOrderDrop(event) {
  event.preventDefault();
  const targetSource = event.currentTarget.dataset.source;
  if (!state || !draggedSource || draggedSource === targetSource) return;

  const order = [...state.sourceRecognitionOrder];
  const from = order.indexOf(draggedSource);
  const to = order.indexOf(targetSource);
  if (from < 0 || to < 0) return;

  order.splice(from, 1);
  order.splice(to, 0, draggedSource);
  state.sourceRecognitionOrder = order;
  renderOrder();
  bridge.post({ type: "reorderSources", value: order });
}

function onOrderDragEnd(event) {
  event.currentTarget.classList.remove("dragging");
  draggedSource = null;
}

function clamp(value, min, max) {
  return Math.min(max, Math.max(min, value));
}

function formatNumber(value, decimals) {
  if (decimals <= 0) return String(Math.round(value));
  return Number(value.toFixed(decimals)).toString();
}

function setupEvents() {
  document.querySelectorAll("input, select, textarea").forEach((element) => {
    element.addEventListener("change", () => {
      const key = element.dataset.key;
      if (!key) return;

      const value = element.type === "checkbox"
        ? element.checked
        : parseInputValue(element);
      updateSetting(key, value);
    });
  });

  document.querySelectorAll(".stepper").forEach((stepper) => {
    stepper.addEventListener("click", (event) => {
      const button = event.target.closest("button[data-step]");
      if (!button) return;

      const key = stepper.dataset.key;
      const input = stepper.querySelector("input");
      const min = Number(stepper.dataset.min);
      const max = Number(stepper.dataset.max);
      const step = Number(stepper.dataset.step);
      const decimals = Number(stepper.dataset.decimals);
      const direction = Number(button.dataset.step);
      const current = Number.parseFloat(input.value || state?.[key] || min);
      const next = clamp(current + direction * step, min, max);
      input.value = formatNumber(next, decimals);
      updateSetting(key, Number(input.value));
      input.animate(
        [{ transform: "scale(1)" }, { transform: "scale(1.035)" }, { transform: "scale(1)" }],
        { duration: 150, easing: "ease-out" }
      );
    });
  });

  document.querySelectorAll('.range-control input[type="range"][data-key]').forEach((input) => {
    input.addEventListener("input", () => {
      updateRangeValue(input);
      updateSetting(input.dataset.key, Number(input.value));
    });
  });

  document.querySelector('select[data-key="foregroundColorMode"]')?.addEventListener("change", (event) => {
    if (event.currentTarget.value === "Custom") {
      bridge.post({ type: "pickColor" });
    }
  });

  document.getElementById("colorPicker")?.addEventListener("click", () => {
    if (state?.foregroundColorMode !== "Custom") return;
    bridge.post({ type: "pickColor" });
  });

  document.getElementById("resetButton")?.addEventListener("click", () => {
    bridge.post({ type: "resetDefaults" });
  });

  document.getElementById("clearCacheButton")?.addEventListener("click", () => {
    bridge.post({ type: "clearCache" });
  });

  document.getElementById("spectrumTuningButton")?.addEventListener("click", () => {
    bridge.post({ type: "openSpectrumTuning" });
  });

  document.getElementById("checkUpdateButton")?.addEventListener("click", () => {
    bridge.post({ type: "checkForUpdates" });
  });

  document.getElementById("openReleaseButton")?.addEventListener("click", () => {
    if (!updateReleaseUrl) return;
    bridge.post({ type: "openExternalLink", value: updateReleaseUrl });
  });

  document.getElementById("repositoryButton")?.addEventListener("click", () => {
    if (!state?.repositoryUrl) return;
    bridge.post({ type: "openExternalLink", value: state.repositoryUrl });
  });

  document.getElementById("sidebarToggle")?.addEventListener("click", () => {
    const windowElement = document.querySelector(".window");
    const toggle = document.getElementById("sidebarToggle");
    windowElement?.classList.toggle("collapsed");
    toggle?.animate(
      [{ transform: "scale(1)" }, { transform: "scale(.92)" }, { transform: "scale(1)" }],
      { duration: 170, easing: "ease-out" }
    );
  });

  document.querySelectorAll(".nav-item").forEach((button) => {
    button.addEventListener("click", () => {
      document.querySelectorAll(".nav-item").forEach((item) => item.classList.remove("active"));
      button.classList.add("active");
      const target = document.getElementById(button.dataset.target);
      target?.scrollIntoView({ behavior: "smooth", block: "start" });
      target?.animate(
        [
          { transform: "translateY(0)", borderColor: "rgba(120, 160, 255, .13)" },
          { transform: "translateY(-2px)", borderColor: "rgba(107, 145, 255, .42)" },
          { transform: "translateY(0)", borderColor: "rgba(120, 160, 255, .13)" }
        ],
        { duration: 280, easing: "ease-out" }
      );
    });
  });

  document.getElementById("content")?.addEventListener("scroll", updateActiveNav);
}

function parseInputValue(element) {
  const key = element.dataset.key;
  if (key === "localMusicFolders") {
    return element.value
      .split(/\r?\n/)
      .map((value) => value.trim().replace(/^"|"$/g, ""))
      .filter(Boolean);
  }

  if (["fontSize", "coverSize", "coverGap", "coverCornerRadius", "backgroundOpacity", "windowWidth", "xOffset", "yOffset"].includes(key)) {
    return Number(element.value);
  }
  return element.value;
}

function updateActiveNav() {
  const content = document.getElementById("content");
  const sections = [...document.querySelectorAll("section.card")];
  if (sections.length === 0) return;

  const active = content && content.scrollTop + content.clientHeight >= content.scrollHeight - 8
    ? sections[sections.length - 1]
    : sections
    .map((section) => ({ id: section.id, distance: Math.abs(section.getBoundingClientRect().top - 110) }))
    .sort((a, b) => a.distance - b.distance)[0];
  if (!active) return;

  document.querySelectorAll(".nav-item").forEach((item) => {
    item.classList.toggle("active", item.dataset.target === (active.id ?? active));
  });
}

window.settingsApp = { setState, setUpdateStatus };

setupEvents();
bridge.post({ type: "ready" });
