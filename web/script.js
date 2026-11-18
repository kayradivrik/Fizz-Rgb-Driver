const KEYS = [
  { id: "esc", label: "ESC", row: 0, w: 1.0, mod: true },
  { id: "1", label: "1", row: 0, w: 1.0 },
  { id: "2", label: "2", row: 0, w: 1.0 },
  { id: "3", label: "3", row: 0, w: 1.0 },
  { id: "4", label: "4", row: 0, w: 1.0 },
  { id: "5", label: "5", row: 0, w: 1.0 },
  { id: "6", label: "6", row: 0, w: 1.0 },
  { id: "7", label: "7", row: 0, w: 1.0 },
  { id: "8", label: "8", row: 0, w: 1.0 },
  { id: "9", label: "9", row: 0, w: 1.0 },
  { id: "0", label: "0", row: 0, w: 1.0 },
  { id: "minus", label: "-", row: 0, w: 1.0 },
  { id: "equal", label: "=", row: 0, w: 1.0 },
  { id: "backspace", label: "BKSP", row: 0, w: 2.0, mod: true },

  { id: "tab", label: "TAB", row: 1, w: 1.5, mod: true },
  { id: "q", label: "Q", row: 1, w: 1.0 },
  { id: "w", label: "W", row: 1, w: 1.0 },
  { id: "e", label: "E", row: 1, w: 1.0 },
  { id: "r", label: "R", row: 1, w: 1.0 },
  { id: "t", label: "T", row: 1, w: 1.0 },
  { id: "y", label: "Y", row: 1, w: 1.0 },
  { id: "u", label: "U", row: 1, w: 1.0 },
  { id: "i", label: "I", row: 1, w: 1.0 },
  { id: "o", label: "O", row: 1, w: 1.0 },
  { id: "p", label: "P", row: 1, w: 1.0 },
  { id: "lbracket", label: "[", row: 1, w: 1.0 },
  { id: "rbracket", label: "]", row: 1, w: 1.0 },
  { id: "backslash", label: "\\", row: 1, w: 1.5, mod: true },

  { id: "caps", label: "CAPS", row: 2, w: 1.75, mod: true },
  { id: "a", label: "A", row: 2, w: 1.0 },
  { id: "s", label: "S", row: 2, w: 1.0 },
  { id: "d", label: "D", row: 2, w: 1.0 },
  { id: "f", label: "F", row: 2, w: 1.0 },
  { id: "g", label: "G", row: 2, w: 1.0 },
  { id: "h", label: "H", row: 2, w: 1.0 },
  { id: "j", label: "J", row: 2, w: 1.0 },
  { id: "k", label: "K", row: 2, w: 1.0 },
  { id: "l", label: "L", row: 2, w: 1.0 },
  { id: "semicolon", label: ";", row: 2, w: 1.0 },
  { id: "quote", label: "'", row: 2, w: 1.0 },
  { id: "enter", label: "ENTER", row: 2, w: 2.25, mod: true },

  { id: "lshift", label: "SHIFT", row: 3, w: 2.25, mod: true },
  { id: "z", label: "Z", row: 3, w: 1.0 },
  { id: "x", label: "X", row: 3, w: 1.0 },
  { id: "c", label: "C", row: 3, w: 1.0 },
  { id: "v", label: "V", row: 3, w: 1.0 },
  { id: "b", label: "B", row: 3, w: 1.0 },
  { id: "n", label: "N", row: 3, w: 1.0 },
  { id: "m", label: "M", row: 3, w: 1.0 },
  { id: "comma", label: ",", row: 3, w: 1.0 },
  { id: "period", label: ".", row: 3, w: 1.0 },
  { id: "slash", label: "/", row: 3, w: 1.0 },
  { id: "rshift", label: "SHIFT", row: 3, w: 2.75, mod: true },

  { id: "lctrl", label: "CTRL", row: 4, w: 1.25, mod: true },
  { id: "win", label: "WIN", row: 4, w: 1.25, mod: true },
  { id: "lalt", label: "ALT", row: 4, w: 1.25, mod: true },
  { id: "space", label: "", row: 4, w: 6.25 },
  { id: "ralt", label: "ALT", row: 4, w: 1.25, mod: true },
  { id: "fn", label: "FN", row: 4, w: 1.25, mod: true },
  { id: "menu", label: "MENU", row: 4, w: 1.25, mod: true },
  { id: "rctrl", label: "CTRL", row: 4, w: 1.25, mod: true },
];

const KEY_GROUPS = {
  wasd: ["w", "a", "s", "d"],
  qwer: ["q", "w", "e", "r", "d", "f"],
  numbers: ["1", "2", "3", "4", "5", "6", "7", "8", "9", "0"],
  modifiers: ["esc", "tab", "caps", "lshift", "lctrl", "win", "lalt", "ralt", "fn", "menu", "rctrl", "rshift", "enter", "backspace", "backslash"],
  all: KEYS.map(k => k.id)
};

let activeBrushColor = "#e62e2d";
let isMouseDown = false;
let currentTab = "tab-effects";
let currentMode = "rainbow_wave";
let currentDirection = "right";
let livePreviewEnabled = true;

// DOM Cache for zero-reflow updates
const glowMap = {};
const ledBarMap = {};
const legendMap = {};
const keycapMap = {};
const lastColorKey = {};
let keyColors = {};
KEYS.forEach(k => { keyColors[k.id] = [230, 46, 45]; });

// Debounce timers
let paramTimer = null;
let pendingPaintBatch = {};
let paintBatchTimer = null;
let toastTimer = null;

// Macro Editor state
let currentMacroSteps = [
  { type: "key_press", value: "3" },
  { type: "delay", value: 45 },
  { type: "key_press", value: "1" }
];
let isBindingTrigger = false;
let isRecordingMacro = false;

function showToast(msg) {
  const t = document.getElementById("status-toast");
  if (!t) return;
  t.textContent = msg;
  t.classList.add("visible");
  if (toastTimer) clearTimeout(toastTimer);
  toastTimer = setTimeout(() => t.classList.remove("visible"), 2200);
}

function renderKeyboard() {
  const container = document.getElementById("keyboard-layout");
  container.innerHTML = "";

  for (let r = 0; r < 5; r++) {
    const rowDiv = document.createElement("div");
    rowDiv.className = "plate-row";

    KEYS.filter(k => k.row === r).forEach(k => {
      const keycap = document.createElement("div");
      keycap.className = "keycap" + (k.mod ? " key-mod" : "") + (k.id === "esc" ? " key-esc" : "");
      keycap.id = `key-${k.id}`;
      keycap.style.flex = `${k.w} 0 0`;

      const glow = document.createElement("div");
      glow.className = "key-glow";
      keycap.appendChild(glow);
      glowMap[k.id] = glow;

      const ledBar = document.createElement("div");
      ledBar.className = "key-led-bar";
      keycap.appendChild(ledBar);
      ledBarMap[k.id] = ledBar;

      const label = document.createElement("span");
      label.className = "key-legend";
      label.textContent = k.label;
      keycap.appendChild(label);
      legendMap[k.id] = label;
      keycapMap[k.id] = keycap;

      keycap.addEventListener("mousedown", (e) => {
        e.preventDefault();
        if (currentTab !== "tab-paint") {
          document.querySelector('[data-tab="tab-paint"]').click();
        }
        queuePaintKey(k.id, activeBrushColor);
      });

      keycap.addEventListener("mouseenter", () => {
        if (currentTab === "tab-paint" && isMouseDown) {
          queuePaintKey(k.id, activeBrushColor);
        }
      });

      rowDiv.appendChild(keycap);
    });

    container.appendChild(rowDiv);
  }
}

function hexToRgb(hex) {
  const c = parseInt(hex.replace("#", ""), 16);
  return [(c >> 16) & 255, (c >> 8) & 255, c & 255];
}

function updateKeyVisual(keyId, r, g, b) {
  const packed = (r << 16) | (g << 8) | b;
  if (lastColorKey[keyId] === packed) return;
  lastColorKey[keyId] = packed;

  const glow = glowMap[keyId];
  const ledBar = ledBarMap[keyId];
  const legend = legendMap[keyId];
  const keycap = keycapMap[keyId];
  if (!glow) return;

  if (packed === 0) {
    glow.style.backgroundColor = "transparent";
    ledBar.style.backgroundColor = "transparent";
    ledBar.style.boxShadow = "none";
    legend.style.color = "#4b5262";
    legend.style.textShadow = "none";
    keycap.style.borderColor = "#1d212a";
  } else {
    const colorStr = `rgb(${r}, ${g}, ${b})`;
    glow.style.backgroundColor = colorStr;
    ledBar.style.backgroundColor = colorStr;
    ledBar.style.boxShadow = `0 0 8px ${colorStr}`;
    legend.style.color = `rgb(${Math.min(255, r + 65)}, ${Math.min(255, g + 65)}, ${Math.min(255, b + 65)})`;
    legend.style.textShadow = `0 0 7px rgba(${r}, ${g}, ${b}, 0.85)`;
    keycap.style.borderColor = `rgba(${r}, ${g}, ${b}, 0.55)`;
  }
}

function queuePaintKey(keyId, hexColor) {
  const rgb = hexToRgb(hexColor);
  keyColors[keyId] = rgb;
  updateKeyVisual(keyId, rgb[0], rgb[1], rgb[2]);
  pendingPaintBatch[keyId] = rgb;

  if (!paintBatchTimer) {
    paintBatchTimer = setTimeout(() => {
      const batch = pendingPaintBatch;
      pendingPaintBatch = {};
      paintBatchTimer = null;
      fetch("/api/paint_batch", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ keys: batch })
      }).catch(() => {});
    }, 35);
  }
}

document.addEventListener("mousedown", () => { isMouseDown = true; });
document.addEventListener("mouseup", () => { isMouseDown = false; });

// Quick Group Selection Buttons
document.querySelectorAll(".qgroup-btn").forEach(btn => {
  btn.addEventListener("click", () => {
    const grp = btn.dataset.group;
    const ids = KEY_GROUPS[grp] || [];
    if (currentTab !== "tab-paint") {
      document.querySelector('[data-tab="tab-paint"]').click();
    }
    ids.forEach(id => queuePaintKey(id, activeBrushColor));
    showToast(`GRUP BOYANDI: ${grp.toUpperCase()} (${ids.length} TUŞ)`);
  });
});

// Navigation Tabs
document.querySelectorAll(".nav-tab-btn").forEach(btn => {
  btn.addEventListener("click", () => {
    document.querySelectorAll(".nav-tab-btn").forEach(b => b.classList.remove("active"));
    document.querySelectorAll(".tab-content").forEach(p => p.classList.remove("active"));

    btn.classList.add("active");
    const tabId = btn.dataset.tab;
    document.getElementById(tabId).classList.add("active");
    currentTab = tabId;

    if (tabId === "tab-paint") {
      document.getElementById("active-mode-readout").textContent = "MOD: ÖZEL TUŞ HARİTASI";
      fetch("/api/set_mode", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ mode: "per_key" })
      });
      Object.entries(keyColors).forEach(([kid, rgb]) => {
        updateKeyVisual(kid, rgb[0], rgb[1], rgb[2]);
      });
    } else if (tabId === "tab-macros") {
      loadMacros();
    } else if (tabId === "tab-settings") {
      loadSettings();
    }
  });
});

// Category Filter Chips
document.querySelectorAll(".chip").forEach(chip => {
  chip.addEventListener("click", () => {
    document.querySelectorAll(".chip").forEach(c => c.classList.remove("active"));
    chip.classList.add("active");
    const filter = chip.dataset.filter;
    document.querySelectorAll("#effects-grid .card-item").forEach(card => {
      card.style.display = (filter === "all" || card.dataset.category === filter) ? "flex" : "none";
    });
  });
});

// Search
document.getElementById("effect-search").addEventListener("input", (e) => {
  const query = e.target.value.toLowerCase().trim();
  document.querySelectorAll("#effects-grid .card-item").forEach(card => {
    const title = card.querySelector(".card-name").textContent.toLowerCase();
    const desc = card.querySelector(".card-desc").textContent.toLowerCase();
    card.style.display = (title.includes(query) || desc.includes(query)) ? "flex" : "none";
  });
});

// Software Effects Selection
document.querySelectorAll("#effects-grid .card-item").forEach(card => {
  card.addEventListener("click", () => {
    document.querySelectorAll("#effects-grid .card-item").forEach(c => c.classList.remove("active"));
    card.classList.add("active");
    currentMode = card.dataset.effect;
    const title = card.querySelector(".card-name").textContent.toUpperCase();
    document.getElementById("active-mode-readout").textContent = `MOD: ${title}`;
    sendSetMode(currentMode);
  });
});

function sendSetMode(eff) {
  const b = parseFloat(document.getElementById("slider-brightness").value) / 100.0;
  const spd = parseFloat(document.getElementById("slider-speed").value) / 100.0;
  const sens = parseFloat(document.getElementById("slider-audio-sens").value) / 10.0;
  const pCol = hexToRgb(document.getElementById("picker-primary").value);
  const sCol = hexToRgb(document.getElementById("picker-secondary").value);
  const tCol = hexToRgb(document.getElementById("picker-tertiary").value);

  fetch("/api/set_mode", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({
      mode: eff,
      brightness: b,
      speed: spd,
      audio_sensitivity: sens,
      direction: currentDirection,
      primary_color: pCol,
      secondary_color: sCol,
      tertiary_color: tCol
    })
  });
}

// Firmware Effects
document.querySelectorAll("[data-fw]").forEach(card => {
  card.addEventListener("click", () => {
    document.querySelectorAll("[data-fw]").forEach(c => c.classList.remove("active"));
    card.classList.add("active");
    const title = card.querySelector(".card-name").textContent.toUpperCase();
    document.getElementById("active-mode-readout").textContent = `MCU ROM: ${title}`;
    fetch("/api/firmware_effect", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        effect: card.dataset.fw,
        speed: parseInt(document.getElementById("slider-fw-speed").value),
        brightness: parseInt(document.getElementById("slider-fw-brightness").value),
        color: hexToRgb(document.getElementById("picker-fw-color").value)
      })
    }).then(() => showToast(`MCU ROM YAZILDI: ${title}`));
  });
});

document.getElementById("slider-fw-speed").addEventListener("input", (e) => {
  document.getElementById("label-fw-speed").textContent = e.target.value;
});
document.getElementById("slider-fw-brightness").addEventListener("input", (e) => {
  document.getElementById("label-fw-brightness").textContent = e.target.value;
});
document.getElementById("picker-fw-color").addEventListener("input", (e) => {
  document.getElementById("hex-fw-color").textContent = e.target.value.toUpperCase();
});

// Debounced Sliders & Pickers
document.getElementById("slider-brightness").addEventListener("input", (e) => {
  document.getElementById("label-brightness").textContent = `${e.target.value}%`;
  scheduleSendParams();
});

document.getElementById("slider-speed").addEventListener("input", (e) => {
  document.getElementById("label-speed").textContent = `${(parseFloat(e.target.value) / 100.0).toFixed(1)}x`;
  scheduleSendParams();
});

document.getElementById("slider-audio-sens").addEventListener("input", (e) => {
  document.getElementById("label-audio-sens").textContent = `${(parseFloat(e.target.value) / 10.0).toFixed(1)}x`;
  scheduleSendParams();
});

document.querySelectorAll(".dir-btn").forEach(btn => {
  btn.addEventListener("click", () => {
    document.querySelectorAll(".dir-btn").forEach(b => b.classList.remove("active"));
    btn.classList.add("active");
    currentDirection = btn.dataset.dir;
    scheduleSendParams();
  });
});

document.getElementById("picker-primary").addEventListener("input", (e) => {
  document.getElementById("hex-primary").textContent = e.target.value.toUpperCase();
  scheduleSendParams();
});
document.getElementById("picker-secondary").addEventListener("input", (e) => {
  document.getElementById("hex-secondary").textContent = e.target.value.toUpperCase();
  scheduleSendParams();
});
document.getElementById("picker-tertiary").addEventListener("input", (e) => {
  document.getElementById("hex-tertiary").textContent = e.target.value.toUpperCase();
  scheduleSendParams();
});

function scheduleSendParams() {
  if (paramTimer) return;
  paramTimer = setTimeout(() => {
    paramTimer = null;
    fetch("/api/update_params", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        brightness: parseFloat(document.getElementById("slider-brightness").value) / 100.0,
        speed: parseFloat(document.getElementById("slider-speed").value) / 100.0,
        audio_sensitivity: parseFloat(document.getElementById("slider-audio-sens").value) / 10.0,
        direction: currentDirection,
        primary_color: hexToRgb(document.getElementById("picker-primary").value),
        secondary_color: hexToRgb(document.getElementById("picker-secondary").value),
        tertiary_color: hexToRgb(document.getElementById("picker-tertiary").value)
      })
    }).catch(() => {});
  }, 40);
}

// Paint Swatches & Actions
document.querySelectorAll(".swatch-circle").forEach(swatch => {
  swatch.addEventListener("click", () => {
    document.querySelectorAll(".swatch-circle").forEach(s => s.classList.remove("active"));
    swatch.classList.add("active");
    activeBrushColor = swatch.dataset.color;
    if (activeBrushColor !== "#000000") {
      document.getElementById("picker-brush").value = activeBrushColor;
    }
  });
});

document.getElementById("picker-brush").addEventListener("input", (e) => {
  activeBrushColor = e.target.value;
  document.querySelectorAll(".swatch-circle").forEach(s => s.classList.remove("active"));
});

document.getElementById("btn-paint-fill").addEventListener("click", () => {
  const rgb = hexToRgb(activeBrushColor);
  KEYS.forEach(k => {
    keyColors[k.id] = rgb;
    updateKeyVisual(k.id, rgb[0], rgb[1], rgb[2]);
  });
  fetch("/api/fill_all", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ rgb: rgb })
  });
  showToast("TÜM ANAHTARLAR DOLDURULDU");
});

document.getElementById("btn-paint-clear").addEventListener("click", () => {
  KEYS.forEach(k => {
    keyColors[k.id] = [0, 0, 0];
    updateKeyVisual(k.id, 0, 0, 0);
  });
  fetch("/api/fill_all", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ rgb: [0, 0, 0] })
  });
  showToast("TÜM ANAHTAR LED'LERİ KAPATILDI");
});

document.querySelectorAll(".preset-tile").forEach(card => {
  card.addEventListener("click", () => {
    const presetName = card.dataset.preset;
    fetch(`/api/load_profile?name=${presetName}`)
      .then(res => res.json())
      .then(data => {
        if (data.colors) {
          keyColors = data.colors;
          Object.entries(keyColors).forEach(([kid, rgb]) => {
            updateKeyVisual(kid, rgb[0], rgb[1], rgb[2]);
          });
          document.getElementById("active-mode-readout").textContent = `PROFİL: ${presetName.toUpperCase()}`;
          showToast(`PROFİL YÜKLENDİ: ${presetName.toUpperCase()}`);
        }
      });
  });
});

document.getElementById("btn-paint-save").addEventListener("click", () => {
  const nameInput = document.getElementById("input-profile-name");
  const name = (nameInput.value || "Ozel_Profil").trim();
  fetch("/api/save_profile", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ name: name, colors: keyColors })
  }).then(() => showToast(`PROFİL KAYDEDİLDİ: ${name.toUpperCase()}`));
});

// ---------------- MACRO STUDIO ----------------
function loadMacros() {
  fetch("/api/macros")
    .then(r => r.json())
    .then(data => {
      document.getElementById("toggle-global-macros").checked = !!data.global_enabled;
      const list = data.macros || [];
      const countEl = document.getElementById("nav-macro-count");
      if (countEl) countEl.textContent = String(list.length).padStart(2, "0");
      renderMacroList(list);
    });
}

function renderMacroList(macros) {
  const container = document.getElementById("macro-list-container");
  container.innerHTML = "";
  const countEl = document.getElementById("nav-macro-count");
  if (countEl) countEl.textContent = String(macros.length).padStart(2, "0");

  if (macros.length === 0) {
    container.innerHTML = `<div style="color: var(--text-muted); font-size: 11.5px; padding: 10px;">Kayıtlı makro bulunmuyor. Sağ panelden yeni makro oluşturun.</div>`;
    return;
  }

  macros.forEach(m => {
    const row = document.createElement("div");
    row.className = "macro-list-item";

    const modeLabels = { once: "TEK SEFER", repeat_n: `${m.repeat_count || 1}X TEKRAR`, toggle_loop: "DÖNGÜ (TOGGLE)" };
    row.innerHTML = `
      <div style="display: flex; align-items: center; gap: 10px;">
        <span class="macro-trigger-badge">${(m.trigger || "?").toUpperCase()}</span>
        <div>
          <div style="font-size: 12.5px; font-weight: 700;">${m.name}</div>
          <div style="font-family: var(--font-mono); font-size: 10px; color: var(--text-secondary); margin-top: 2px;">${modeLabels[m.repeat_mode] || "TEK SEFER"} // ${(m.steps || []).length} ADIM</div>
        </div>
      </div>
      <div style="display: flex; align-items: center; gap: 6px;">
        <button class="btn-act btn-edit-m" style="padding: 4px 8px;">DÜZENLE</button>
        <button class="btn-act danger btn-del-m" style="padding: 4px 8px;">SİL</button>
        <label class="switch">
          <input type="checkbox" class="toggle-m" ${m.enabled ? "checked" : ""}>
          <span class="slider-toggle"></span>
        </label>
      </div>
    `;

    row.querySelector(".toggle-m").addEventListener("change", (e) => {
      fetch("/api/macro_toggle", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ id: m.id, enabled: e.target.checked })
      });
    });

    row.querySelector(".btn-del-m").addEventListener("click", () => {
      fetch("/api/macro_delete", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ id: m.id })
      }).then(r => r.json()).then(d => {
        renderMacroList(d.macros || []);
        showToast("MAKRO SİLİNDİ");
      });
    });

    row.querySelector(".btn-edit-m").addEventListener("click", () => {
      document.getElementById("macro-edit-id").value = m.id;
      document.getElementById("macro-name").value = m.name;
      document.getElementById("macro-trigger").value = m.trigger;
      document.getElementById("macro-repeat-mode").value = m.repeat_mode || "once";
      document.getElementById("macro-repeat-count").value = m.repeat_count || 1;
      currentMacroSteps = [...(m.steps || [])];
      renderMacroSteps();
    });

    container.appendChild(row);
  });
}

function renderMacroSteps() {
  const list = document.getElementById("macro-steps-list");
  list.innerHTML = "";
  if (currentMacroSteps.length === 0) {
    list.innerHTML = `<div style="color: var(--text-muted); font-family: var(--font-mono); font-size: 11px; text-align: center; padding: 18px;">ADIM LİSTESİ BOŞ // YUKARIDAN ADIM EKLEYİN VEYA CANLI KAYIT BAŞLATIN</div>`;
    return;
  }

  currentMacroSteps.forEach((s, idx) => {
    const item = document.createElement("div");
    item.className = "step-row";
    let tag = "";
    let detail = "";
    if (s.type === "key_press") { tag = "[TUŞ VURUŞU]"; detail = String(s.value).toUpperCase(); }
    else if (s.type === "key_down") { tag = "[TUŞ BASILI]"; detail = String(s.value).toUpperCase(); }
    else if (s.type === "key_up") { tag = "[TUŞ BIRAK]"; detail = String(s.value).toUpperCase(); }
    else if (s.type === "delay") { tag = "[GECİKME]"; detail = `${s.value} ms`; }
    else if (s.type === "mouse_click") { tag = "[FARE TIKI]"; detail = String(s.value).toUpperCase(); }
    else if (s.type === "text") { tag = "[METİN DİZİSİ]"; detail = `"${s.value}"`; }
    else { tag = `[${s.type.toUpperCase()}]`; detail = String(s.value); }

    const idxStr = String(idx + 1).padStart(2, "0");
    item.innerHTML = `
      <div><span style="color: var(--text-muted); margin-right: 8px;">${idxStr}</span><span class="step-type-tag">${tag}</span><span>${detail}</span></div>
      <button class="btn-act danger" style="padding: 2px 7px; font-size: 10px;">SİL</button>
    `;
    item.querySelector("button").addEventListener("click", () => {
      currentMacroSteps.splice(idx, 1);
      renderMacroSteps();
    });
    list.appendChild(item);
  });
}

document.getElementById("btn-new-macro").addEventListener("click", () => {
  document.getElementById("macro-edit-id").value = "";
  document.getElementById("macro-name").value = "Yeni Özel Makro";
  document.getElementById("macro-trigger").value = "f6";
  currentMacroSteps = [];
  renderMacroSteps();
});

document.getElementById("btn-bind-trigger").addEventListener("click", (e) => {
  isBindingTrigger = true;
  e.target.textContent = "TUŞA BASIN...";
});

document.addEventListener("keydown", (e) => {
  if (isBindingTrigger) {
    e.preventDefault();
    isBindingTrigger = false;
    let keyName = e.key.toLowerCase();
    if (keyName === " ") keyName = "space";
    else if (keyName === "escape") keyName = "esc";
    else if (keyName === "control") keyName = "ctrl";
    document.getElementById("macro-trigger").value = keyName;
    document.getElementById("btn-bind-trigger").textContent = "TUŞ ATA";
  }
});

// Inline Macro Step Composer (Zero Browser Prompts)
document.getElementById("btn-add-keypress").addEventListener("click", () => {
  const k = (document.getElementById("step-key-input").value || "q").trim().toLowerCase();
  if (k) {
    currentMacroSteps.push({ type: "key_press", value: k });
    renderMacroSteps();
  }
});

document.getElementById("btn-add-delay").addEventListener("click", () => {
  const ms = parseInt(document.getElementById("step-delay-input").value) || 40;
  currentMacroSteps.push({ type: "delay", value: Math.max(1, ms) });
  renderMacroSteps();
});

document.getElementById("btn-add-mouse").addEventListener("click", () => {
  const btn = document.getElementById("step-mouse-input").value || "left";
  currentMacroSteps.push({ type: "mouse_click", value: btn });
  renderMacroSteps();
});

document.getElementById("btn-add-text").addEventListener("click", () => {
  const txt = document.getElementById("step-text-input").value;
  if (txt) {
    currentMacroSteps.push({ type: "text", value: txt });
    renderMacroSteps();
  }
});

document.getElementById("btn-clear-steps").addEventListener("click", () => {
  currentMacroSteps = [];
  renderMacroSteps();
});

document.getElementById("btn-record-macro").addEventListener("click", (e) => {
  const btn = e.target;
  if (!isRecordingMacro) {
    isRecordingMacro = true;
    btn.textContent = "KAYDI DURDUR";
    showToast("CANLI TUŞ KAYDI BAŞLADI");
    fetch("/api/macro_record_start", { method: "POST", headers: { "Content-Type": "application/json" }, body: "{}" });
  } else {
    isRecordingMacro = false;
    btn.textContent = "CANLI KAYIT BAŞLAT";
    fetch("/api/macro_record_stop", { method: "POST", headers: { "Content-Type": "application/json" }, body: "{}" })
      .then(r => r.json())
      .then(d => {
        if (d.steps && d.steps.length > 0) {
          currentMacroSteps = d.steps;
          renderMacroSteps();
          showToast(`${d.steps.length} ADIM KAYDEDİLDİ`);
        }
      });
  }
});

document.getElementById("btn-save-macro").addEventListener("click", () => {
  const payload = {
    id: document.getElementById("macro-edit-id").value || undefined,
    name: document.getElementById("macro-name").value || "Özel Makro",
    trigger: document.getElementById("macro-trigger").value || "f6",
    repeat_mode: document.getElementById("macro-repeat-mode").value,
    repeat_count: parseInt(document.getElementById("macro-repeat-count").value) || 1,
    enabled: true,
    steps: currentMacroSteps
  };

  fetch("/api/macro_save", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(payload)
  })
    .then(r => r.json())
    .then(d => {
      renderMacroList(d.macros || []);
      showToast(`MAKRO ETKİNLEŞTİRİLDİ: ${payload.name.toUpperCase()}`);
    });
});

document.getElementById("toggle-global-macros").addEventListener("change", (e) => {
  fetch("/api/macro_toggle", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ global_enabled: e.target.checked })
  });
});

// ---------------- SETTINGS & PERFORMANCE ----------------
function loadSettings() {
  fetch("/api/settings")
    .then(r => r.json())
    .then(data => {
      const s = data.settings || {};
      document.getElementById("set-eco-mode").checked = s.eco_mode !== false;
      document.getElementById("set-target-fps").value = String(s.target_fps || 35);
      document.getElementById("set-live-preview").checked = s.ui_live_preview !== false;
      document.getElementById("set-autostart").checked = !!s.autostart;
      document.getElementById("set-idle-timeout").value = String(s.idle_timeout_minutes ?? 15);
      document.getElementById("set-tray").checked = s.minimize_to_tray !== false;
      livePreviewEnabled = s.ui_live_preview !== false;

      if (data.ram_mb !== undefined) {
        document.getElementById("settings-ram-readout").textContent = `${data.ram_mb} MB`;
        document.getElementById("header-ram").textContent = `${data.ram_mb} MB`;
      }
    });
}

function saveSettingsUpdate(patch) {
  if ("ui_live_preview" in patch) {
    livePreviewEnabled = patch.ui_live_preview;
    document.getElementById("preview-badge").textContent = livePreviewEnabled ? "GERÇEK ZAMANLI" : "ÖNİZLEME KAPALI";
  }
  fetch("/api/settings", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(patch)
  })
    .then(r => r.json())
    .then(d => {
      if (d.ram_mb !== undefined) {
        document.getElementById("settings-ram-readout").textContent = `${d.ram_mb} MB`;
        document.getElementById("header-ram").textContent = `${d.ram_mb} MB`;
      }
      showToast("AYARLAR GÜNCELLENDİ");
    });
}

document.getElementById("set-eco-mode").addEventListener("change", e => saveSettingsUpdate({ eco_mode: e.target.checked }));
document.getElementById("set-target-fps").addEventListener("change", e => saveSettingsUpdate({ target_fps: parseInt(e.target.value) }));
document.getElementById("set-live-preview").addEventListener("change", e => saveSettingsUpdate({ ui_live_preview: e.target.checked }));
document.getElementById("set-autostart").addEventListener("change", e => saveSettingsUpdate({ autostart: e.target.checked }));
document.getElementById("set-idle-timeout").addEventListener("change", e => saveSettingsUpdate({ idle_timeout_minutes: parseInt(e.target.value) }));
document.getElementById("set-tray").addEventListener("change", e => saveSettingsUpdate({ minimize_to_tray: e.target.checked }));

function triggerMemoryTrim() {
  fetch("/api/trim_memory", { method: "POST", headers: { "Content-Type": "application/json" }, body: "{}" })
    .then(r => r.json())
    .then(d => {
      if (d.ram_mb !== undefined) {
        document.getElementById("settings-ram-readout").textContent = `${d.ram_mb} MB`;
        document.getElementById("header-ram").textContent = `${d.ram_mb} MB`;
        showToast(`BELLEK TEMİZLENDİ // ${d.ram_mb} MB`);
      }
    });
}

document.getElementById("btn-trim-ram").addEventListener("click", triggerMemoryTrim);
document.getElementById("btn-quick-trim").addEventListener("click", triggerMemoryTrim);

// ---------------- LIVE PREVIEW LOOP ----------------
function syncLiveVisualizer() {
  if (!document.hidden && livePreviewEnabled && currentTab === "tab-effects") {
    fetch("/api/get_live_colors")
      .then(r => r.json())
      .then(data => {
        if (data.colors) {
          const cols = data.colors;
          for (let i = 0; i < KEYS.length; i++) {
            const kid = KEYS[i].id;
            const rgb = cols[kid];
            if (rgb) updateKeyVisual(kid, rgb[0], rgb[1], rgb[2]);
          }
        }
        if (data.ram_mb !== undefined) {
          document.getElementById("header-ram").textContent = `${data.ram_mb} MB`;
        }
        if (data.fps !== undefined) {
          document.getElementById("header-fps").textContent = Math.round(data.fps);
        }
      })
      .catch(() => {});
  }
  setTimeout(syncLiveVisualizer, livePreviewEnabled ? 120 : 1000);
}

renderKeyboard();
renderMacroSteps();
loadMacros();
loadSettings();
syncLiveVisualizer();
