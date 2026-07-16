const state = {
  campaign: null,
  selectedAmount: 0,
  token: localStorage.getItem("adminToken"),
  metricValues: {
    raisedAmount: 0,
    goalAmount: 0,
    remainingAmount: 0,
    percent: 0
  }
};

const $ = (selector) => document.querySelector(selector);
const money = (value) => Number(value || 0).toLocaleString("pt-BR", { style: "currency", currency: "BRL" });
const percent = (value) => `${Number(value || 0).toLocaleString("pt-BR", { minimumFractionDigits: 2, maximumFractionDigits: 2 })}%`;

async function api(path, options = {}) {
  const headers = options.headers || {};
  if (state.token) headers.Authorization = `Bearer ${state.token}`;
  const response = await fetch(path, { ...options, headers });
  if (!response.ok) {
    let message = "Não foi possível concluir a operação.";
    try { message = (await response.json()).message || message; } catch { }
    throw new Error(message);
  }
  if (response.status === 204) return null;
  return response.json();
}

async function loadCampaign() {
  state.campaign = await api("/api/campaign-current");
  $("#campaignId").value = state.campaign.id;
  $("#brandName").textContent = compactBrandName(state.campaign.name);
  $("#campaignName").textContent = state.campaign.name;
  $("#campaignTitle").innerHTML = formatHeroTitle(state.campaign.title);
  $("#campaignDescription").textContent = state.campaign.description;
  $("#bibleQuote").textContent = `${state.campaign.bibleReference} · "${state.campaign.bibleText}"`;
  $(".hero").style.backgroundImage = `url("${state.campaign.heroImageUrl}")`;
  $("#purposeHeading").textContent = state.campaign.purposeHeading;
  $("#purposeText").textContent = state.campaign.purpose;
  $("#contributionHeading").textContent = state.campaign.contributionHeading;
  updateCampaignMetrics(state.campaign);
  $("#campaignStatus").textContent = `Campanha ${state.campaign.status}`;
  const campaignOpen = state.campaign.status === "Ativa";
  $("#openContribution").disabled = !campaignOpen;
  $("#openWallContribution").disabled = !campaignOpen;
  $("#openContribution").textContent = campaignOpen ? "Já fiz minha contribuição" : "Campanha não está ativa";
  $("#pixKey").value = state.campaign.pixKey;
  $("#pixQr").src = state.campaign.pixQrCodeUrl;
  $("#pastorVideoTitle").textContent = state.campaign.pastorVideoTitle;
  $("#pastorVideoSubtitle").textContent = state.campaign.pastorVideoSubtitle;
  renderCampaignVideo(state.campaign.videoUrl);
  renderPillars();
  renderQuickAmounts();
  renderWall();
}

async function refreshCampaignProgress() {
  if (document.hidden || !state.campaign) return;
  const campaign = await api("/api/campaign-current");
  state.campaign = campaign;
  updateCampaignMetrics(campaign);
  renderWall();
}

function updateCampaignMetrics(campaign) {
  animateMoney("#raisedAmount", state.metricValues.raisedAmount, campaign.raisedAmount);
  animateMoney("#goalAmount", state.metricValues.goalAmount, campaign.goalAmount);
  animateMoney("#remainingAmount", state.metricValues.remainingAmount, campaign.remainingAmount);
  animatePercent("#progressBubble", state.metricValues.percent, campaign.percent);
  $("#progressBar").style.width = `${Math.min(100, Number(campaign.percent || 0))}%`;
  state.metricValues.raisedAmount = Number(campaign.raisedAmount || 0);
  state.metricValues.goalAmount = Number(campaign.goalAmount || 0);
  state.metricValues.remainingAmount = Number(campaign.remainingAmount || 0);
  state.metricValues.percent = Number(campaign.percent || 0);
}

function animateMoney(selector, from, to) {
  animateValue(selector, from, to, 700, money);
}

function animatePercent(selector, from, to) {
  animateValue(selector, from, to, 700, percent);
}

function animateValue(selector, from, to, duration, formatter) {
  const element = $(selector);
  const start = Number(from || 0);
  const end = Number(to || 0);
  const startedAt = performance.now();
  const delta = end - start;
  if (!delta) {
    element.textContent = formatter(end);
    return;
  }
  const tick = (now) => {
    const progress = Math.min(1, (now - startedAt) / duration);
    const eased = 1 - Math.pow(1 - progress, 3);
    element.textContent = formatter(start + delta * eased);
    if (progress < 1) requestAnimationFrame(tick);
  };
  requestAnimationFrame(tick);
}

function compactBrandName(value) {
  return toTitleCase(String(value || "").replace(/^projeto\s+/i, ""));
}

function toTitleCase(value) {
  return String(value || "").toLocaleLowerCase("pt-BR").replace(/(^|\s)\S/g, (letter) => letter.toLocaleUpperCase("pt-BR"));
}

function formatHeroTitle(value) {
  const safe = escapeHtml(value || "");
  const marker = "E nós";
  const index = safe.toLocaleLowerCase("pt-BR").indexOf(marker.toLocaleLowerCase("pt-BR"));
  if (index < 0) return safe;
  return `${safe.slice(0, index)}<span class="accent">${safe.slice(index)}</span>`;
}

function renderPillars() {
  const list = $("#pillarsList");
  list.innerHTML = "";
  for (const [index, pillar] of (state.campaign.pillars || []).entries()) {
    const node = document.createElement("article");
    node.innerHTML = `<span>${String(index + 1).padStart(2, "0")}</span><strong>${escapeHtml(pillar)}</strong>`;
    list.appendChild(node);
  }
}

function renderQuickAmounts() {
  const values = state.campaign.quickAmounts?.length ? state.campaign.quickAmounts : [50, 100, 250, 500];
  const list = $("#quickValues");
  list.innerHTML = "";
  state.selectedAmount = Number(values[0] || 0);
  for (const [index, value] of values.entries()) {
    const button = document.createElement("button");
    button.type = "button";
    button.dataset.value = value;
    button.textContent = money(value).replace(",00", "");
    button.classList.toggle("active", index === 0);
    list.appendChild(button);
  }
  const other = document.createElement("button");
  other.type = "button";
  other.className = "other-amount";
  other.textContent = "Outro valor";
  other.addEventListener("click", () => {
    document.querySelectorAll("#quickValues button").forEach((item) => item.classList.remove("active"));
    $("#customAmount").focus();
  });
  list.appendChild(other);
}

function normalizeVideoUrl(url) {
  if (!url) return "";
  if (url.includes("youtube.com/embed/")) return url;
  const watch = url.match(/[?&]v=([^&]+)/);
  if (watch) return `https://www.youtube.com/embed/${watch[1]}`;
  const short = url.match(/youtu\.be\/([^?]+)/);
  if (short) return `https://www.youtube.com/embed/${short[1]}`;
  return url;
}

function renderCampaignVideo(url) {
  const videoUrl = String(url || "").trim();
  const iframe = $("#pastorVideo");
  const video = $("#pastorVideoFile");
  iframe.classList.add("hidden");
  video.classList.add("hidden");
  iframe.removeAttribute("src");
  video.removeAttribute("src");
  $(".video-frame").classList.toggle("hidden", !videoUrl);
  if (!videoUrl) return;

  if (isUploadedVideo(videoUrl)) {
    video.src = videoUrl;
    video.classList.remove("hidden");
    return;
  }

  iframe.src = normalizeVideoUrl(videoUrl);
  iframe.classList.remove("hidden");
}

function isUploadedVideo(url) {
  return /\.(mp4|webm|mov|m4v)(\?.*)?$/i.test(url) || url.startsWith("/uploads/");
}

function renderWall() {
  const wall = $("#wallList");
  wall.innerHTML = "";
  if (!state.campaign.wall.length) {
    wall.innerHTML = `<p>Ainda não há contribuições aprovadas no mural.</p>`;
    return;
  }
  for (const item of state.campaign.wall) {
    const node = document.createElement("article");
    node.className = "wall-item";
    node.innerHTML = `
      ${item.imageUrl ? `<img class="wall-photo" src="${escapeAttr(item.imageUrl)}" alt="${escapeAttr(item.displayName)}">` : `<div class="wall-avatar">${getInitials(item.displayName)}</div>`}
      <strong class="wall-name">${escapeHtml(item.displayName)}</strong>
      <strong class="wall-amount">${money(item.amount)}</strong>
      ${item.message ? `<p>${escapeHtml(item.message)}</p>` : ""}
      <span>${relativeTime(item.approvedAt)}</span>`;
    wall.appendChild(node);
  }
}

function getInitials(value) {
  const text = String(value || "CA").trim();
  if (text.toLocaleLowerCase("pt-BR").includes("anônima")) return "♡";
  return text.split(/\s+/).slice(0, 2).map((part) => part[0]).join("").toLocaleUpperCase("pt-BR");
}

function relativeTime(date) {
  const diff = Math.max(1, Math.round((Date.now() - new Date(date).getTime()) / 60000));
  if (diff < 60) return `Há ${diff} min`;
  const hours = Math.round(diff / 60);
  if (hours < 24) return `Há ${hours} hora${hours > 1 ? "s" : ""}`;
  const days = Math.round(hours / 24);
  return `Há ${days} dia${days > 1 ? "s" : ""}`;
}

function escapeHtml(value) {
  return String(value ?? "").replace(/[&<>"']/g, (char) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#039;" }[char]));
}

$("#quickValues").addEventListener("click", (event) => {
  const button = event.target.closest("button[data-value]");
  if (!button) return;
  state.selectedAmount = Number(button.dataset.value);
  $("#customAmount").value = "";
  document.querySelectorAll("#quickValues button").forEach((item) => item.classList.toggle("active", item === button));
});

$("#customAmount").addEventListener("input", (event) => {
  state.selectedAmount = Number(event.target.value || 0);
  document.querySelectorAll("#quickValues button").forEach((item) => item.classList.remove("active"));
});

$("#copyPix").addEventListener("click", async () => {
  const pixKey = $("#pixKey").value;
  if (!pixKey) return;
  try {
    await navigator.clipboard.writeText(pixKey);
  } catch {
    $("#pixKey").select();
    document.execCommand("copy");
  }
  $("#copyFeedback").textContent = "Chave PIX copiada com sucesso.";
  setTimeout(() => $("#copyFeedback").textContent = "", 2500);
});

$("#openContribution").addEventListener("click", () => {
  $("#contributionAmount").value = state.selectedAmount || "";
  syncWallExtras();
  $("#contributionDialog").showModal();
});

$("#openWallContribution").addEventListener("click", () => {
  $("#contributionAmount").value = state.selectedAmount || "";
  $("#contributionForm").showOnWall.checked = true;
  syncWallExtras();
  $("#contributionDialog").showModal();
});

$("#closeContribution").addEventListener("click", () => $("#contributionDialog").close());

$("#contributionForm").showOnWall.addEventListener("change", syncWallExtras);

$("#anonymousCheck").addEventListener("change", (event) => {
  $("#contributorName").required = !event.target.checked;
});

function syncWallExtras() {
  const enabled = $("#contributionForm").showOnWall.checked;
  document.querySelectorAll(".wall-extra").forEach((label) => {
    label.classList.toggle("hidden", !enabled);
    label.querySelectorAll("input, textarea").forEach((input) => {
      input.disabled = !enabled;
      if (!enabled) input.value = "";
    });
  });
}

$("#contributionForm").addEventListener("submit", async (event) => {
  event.preventDefault();
  const form = event.currentTarget;
  const data = new FormData(form);
  data.set("isAnonymous", form.isAnonymous.checked);
  data.set("showOnWall", form.showOnWall.checked);
  data.set("amount", Number(data.get("amount")).toString());
  try {
    await fetch("/api/contribution", { method: "POST", body: data }).then(async (response) => {
      if (!response.ok) throw new Error((await response.json()).message);
    });
    $("#contributionMessage").textContent = "Comprovante enviado. A contribuição ficará pendente até aprovação.";
    form.reset();
    syncWallExtras();
    setTimeout(() => $("#contributionDialog").close(), 1800);
  } catch (error) {
    $("#contributionMessage").textContent = error.message;
  }
});

$("#loginForm").addEventListener("submit", async (event) => {
  event.preventDefault();
  const data = Object.fromEntries(new FormData(event.currentTarget));
  try {
    const result = await api("/api/admin/auth/login", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(data)
    });
    state.token = result.token;
    localStorage.setItem("adminToken", state.token);
    $("#loginMessage").textContent = "";
    $("#loginForm").classList.add("hidden");
    $("#adminArea").classList.remove("hidden");
    await loadAdmin();
  } catch {
    $("#loginMessage").textContent = "Login inválido.";
  }
});

$("#logoutBtn").addEventListener("click", () => {
  localStorage.removeItem("adminToken");
  state.token = null;
  $("#adminArea").classList.add("hidden");
  $("#loginForm").classList.remove("hidden");
});

$("#refreshAdmin").addEventListener("click", loadAdmin);
$("#statusFilter").addEventListener("change", loadContributions);
$("#searchContribution").addEventListener("input", debounce(loadContributions, 300));
document.querySelectorAll("[data-open-admin]").forEach((link) => {
  link.addEventListener("click", (event) => {
    event.preventDefault();
    history.replaceState(null, "", "#admin");
    showAdminPanel();
  });
});
window.addEventListener("hashchange", () => {
  if (location.hash === "#admin") showAdminPanel();
});

async function showAdminPanel() {
  $("#admin").classList.remove("hidden");
  if (state.token) {
    await loadAdmin();
  } else {
    $("#adminArea").classList.add("hidden");
    $("#loginForm").classList.remove("hidden");
  }
  $("#admin").scrollIntoView({ behavior: "smooth", block: "start" });
}

async function loadAdmin() {
  if (!state.token) return;
  $("#admin").classList.remove("hidden");
  $("#loginForm").classList.add("hidden");
  $("#adminArea").classList.remove("hidden");
  await Promise.all([loadDashboard(), loadContributions(), loadCampaignForm()]);
}

async function loadDashboard() {
  const dashboard = await api("/api/admin/dashboard");
  $("#dashboard").innerHTML = [
    ["Meta", money(dashboard.goalAmount)],
    ["Arrecadado", money(dashboard.raisedAmount)],
    ["Faltam", money(dashboard.remainingAmount)],
    ["Progresso", percent(dashboard.percent)],
    ["Aprovadas", dashboard.approvedContributions],
    ["Pendentes", dashboard.pendingContributions]
  ].map(([label, value]) => `<article class="dashboard-card"><span>${label}</span><strong>${value}</strong></article>`).join("");
}

async function loadContributions() {
  if (!state.token) return;
  const status = $("#statusFilter").value;
  const search = $("#searchContribution").value;
  const params = new URLSearchParams();
  if (status) params.set("status", status);
  if (search) params.set("search", search);
  const items = await api(`/api/admin/contribution?${params}`);
  const table = $("#contributionTable");
  table.innerHTML = items.length ? "" : "<p>Nenhuma contribuição encontrada.</p>";
  for (const item of items) {
    const row = document.createElement("article");
    row.className = "contribution-row";
    const wallReview = item.showOnWall
      ? `<div class="wall-review">
          <small><strong>Mural:</strong> revise a mensagem, a foto e o comprovante antes de aprovar.</small>
          ${item.wallMessage ? `<p>${escapeHtml(item.wallMessage)}</p>` : "<small>Sem mensagem para o mural.</small>"}
          ${item.wallImageOriginalName ? `<button data-wall-image="${item.id}">Ver foto do mural</button>` : "<small>Sem foto para o mural.</small>"}
        </div>`
      : "<small>Não deseja aparecer no mural.</small>";
    row.innerHTML = `
      <div>
        <strong>${escapeHtml(item.isAnonymous ? "Contribuição Anônima" : item.name || "Sem nome")} · ${money(item.amount)}</strong>
        <small>${escapeHtml(item.phone)} · ${item.status} · ${new Date(item.createdAt).toLocaleString("pt-BR")}</small>
        ${item.rejectionReason ? `<small>Motivo: ${escapeHtml(item.rejectionReason)}</small>` : ""}
        ${wallReview}
      </div>
      <div class="row-actions">
        <button data-receipt="${item.id}">Comprovante</button>
        <button class="ok" data-approve="${item.id}">Aprovar e publicar</button>
        <button class="danger" data-reject="${item.id}">Rejeitar</button>
      </div>`;
    table.appendChild(row);
  }
}

$("#contributionTable").addEventListener("click", async (event) => {
  const receipt = event.target.closest("[data-receipt]");
  const wallImage = event.target.closest("[data-wall-image]");
  const approve = event.target.closest("[data-approve]");
  const reject = event.target.closest("[data-reject]");
  try {
    if (receipt) await openReceipt(receipt.dataset.receipt);
    if (wallImage) await openProtectedFile(`/api/admin/contribution/${wallImage.dataset.wallImage}/wall-image`, "Não foi possível abrir a foto do mural.");
    if (approve) {
      const confirmed = confirm("Confirma que o comprovante procede e que a mensagem/foto podem ser publicadas no mural?");
      if (!confirmed) return;
      await api(`/api/admin/contribution/${approve.dataset.approve}/approve`, { method: "PUT" });
    }
    if (reject) {
      const reason = prompt("Motivo interno da rejeição:");
      await api(`/api/admin/contribution/${reject.dataset.reject}/reject`, {
        method: "PUT",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ reason })
      });
    }
    if (approve || reject) {
      await Promise.all([loadCampaign(), loadAdmin()]);
    }
  } catch (error) {
    alert(error.message);
  }
});

async function openReceipt(id) {
  await openProtectedFile(`/api/admin/contribution/${id}/receipt`, "Não foi possível abrir o comprovante.");
}

async function openProtectedFile(path, errorMessage) {
  const response = await fetch(path, {
    headers: { Authorization: `Bearer ${state.token}` }
  });
  if (!response.ok) throw new Error(errorMessage);
  const blob = await response.blob();
  const url = URL.createObjectURL(blob);
  window.open(url, "_blank", "noopener");
  setTimeout(() => URL.revokeObjectURL(url), 60000);
}

async function loadCampaignForm() {
  const campaign = await api("/api/admin/campaign");
  $("#campaignForm").innerHTML = `
    <label>Nome <input name="name" value="${escapeAttr(campaign.name)}" required></label>
    <label>Slug <input name="slug" value="${escapeAttr(campaign.slug)}" required></label>
    <label>Meta financeira <input name="goalAmount" type="number" min="1" step="0.01" value="${campaign.goalAmount}" required></label>
    <label>Título principal <textarea name="title" required>${escapeHtml(campaign.title)}</textarea></label>
    <label>Texto principal <textarea name="description" required>${escapeHtml(campaign.description)}</textarea></label>
    <label>Referência bíblica <input name="bibleReference" value="${escapeAttr(campaign.bibleReference)}" required></label>
    <label>Texto bíblico <textarea name="bibleText" required>${escapeHtml(campaign.bibleText)}</textarea></label>
    <label>URL da imagem principal <input name="heroImageUrl" value="${escapeAttr(campaign.heroImageUrl)}" required></label>
    <label>Enviar nova imagem principal <input name="heroFile" type="file" accept=".jpg,.jpeg,.png,.webp"><span class="asset-help">Opcional. Ao enviar, a URL acima será atualizada automaticamente.</span></label>
    <label>URL da imagem do propósito <input name="purposeImageUrl" value="${escapeAttr(campaign.purposeImageUrl || campaign.heroImageUrl)}" required></label>
    <label>Título da seção propósito <textarea name="purposeHeading" required>${escapeHtml(campaign.purposeHeading)}</textarea></label>
    <label>Texto do propósito <textarea name="purpose" required>${escapeHtml(campaign.purpose)}</textarea></label>
    <label>Pilares, um por linha <textarea name="pillars">${escapeHtml((campaign.pillars || []).join("\n"))}</textarea></label>
    <label>Título da seção de contribuição <textarea name="contributionHeading" required>${escapeHtml(campaign.contributionHeading)}</textarea></label>
    <label>Valores rápidos, um por linha <textarea name="quickAmounts">${escapeHtml((campaign.quickAmounts || []).join("\n"))}</textarea></label>
    <label>Chave PIX <input name="pixKey" value="${escapeAttr(campaign.pixKey)}" required></label>
    <label>URL do QR Code PIX <input name="pixQrCodeUrl" value="${escapeAttr(campaign.pixQrCodeUrl)}" required></label>
    <label>Enviar novo QR Code PIX <input name="pixFile" type="file" accept=".jpg,.jpeg,.png,.webp"><span class="asset-help">Opcional. Use a imagem exportada pelo banco ou gerador PIX.</span></label>
    <label>Título do vídeo <input name="pastorVideoTitle" value="${escapeAttr(campaign.pastorVideoTitle)}" required></label>
    <label>Subtítulo do vídeo <input name="pastorVideoSubtitle" value="${escapeAttr(campaign.pastorVideoSubtitle)}" required></label>
    <label>Arquivo/URL do vídeo
      <span class="file-picker-row">
        <input name="videoUrl" value="${escapeAttr(campaign.videoUrl)}" placeholder="/uploads/campaign/video.mp4">
        <button type="button" class="file-pick-btn" data-pick-file="videoFile">Escolher arquivo</button>
      </span>
      <input class="visually-hidden-file" name="videoFile" type="file" accept=".mp4,.webm,.mov,.m4v,video/mp4,video/webm,video/quicktime">
      <span class="asset-help" id="videoFileName">Opcional. Ao enviar, o vídeo substitui a URL acima. Tamanho máximo: 200 MB.</span>
    </label>
    <label>Status <select name="status">
      ${["Ativa", "Finalizada", "Inativa"].map((status) => `<option ${campaign.status === status ? "selected" : ""}>${status}</option>`).join("")}
    </select></label>
    <button class="primary-btn">Salvar campanha</button>
    <p class="feedback" id="campaignFormMessage"></p>`;
  $("#campaignForm").dataset.id = campaign.id;
}

$("#campaignForm").addEventListener("submit", async (event) => {
  event.preventDefault();
  const form = event.currentTarget;
  const saveButton = form.querySelector("button");
  const message = $("#campaignFormMessage");
  const formData = new FormData(form);
  const heroFile = form.heroFile.files[0];
  const pixFile = form.pixFile.files[0];
  const videoFile = form.videoFile.files[0];
  formData.delete("heroFile");
  formData.delete("pixFile");
  formData.delete("videoFile");
  const body = Object.fromEntries(formData);
  body.id = form.dataset.id;
  body.goalAmount = Number(body.goalAmount);
  body.pillars = body.pillars.split(/\r?\n/).map((item) => item.trim()).filter(Boolean);
  body.quickAmounts = body.quickAmounts.split(/\r?\n|,/).map((item) => Number(String(item).replace(",", "."))).filter((item) => item > 0);
  saveButton.disabled = true;
  message.textContent = "Salvando...";
  try {
    await api(`/api/admin/campaign/${form.dataset.id}`, {
      method: "PUT",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body)
    });
    if (heroFile) await uploadCampaignAsset(form.dataset.id, "hero", heroFile);
    if (pixFile) await uploadCampaignAsset(form.dataset.id, "pix", pixFile);
    if (videoFile) await uploadCampaignAsset(form.dataset.id, "video", videoFile);
    await Promise.all([loadCampaign(), loadDashboard(), loadCampaignForm()]);
    $("#campaignFormMessage").textContent = "Campanha salva com sucesso.";
  } catch (error) {
    message.textContent = error.message;
  } finally {
    saveButton.disabled = false;
  }
});

$("#campaignForm").addEventListener("click", (event) => {
  const pickButton = event.target.closest("[data-pick-file]");
  if (!pickButton) return;
  const input = document.forms.campaignForm?.elements[pickButton.dataset.pickFile];
  input?.click();
});

$("#campaignForm").addEventListener("change", (event) => {
  if (event.target.name !== "videoFile") return;
  const file = event.target.files?.[0];
  $("#videoFileName").textContent = file
    ? `Arquivo selecionado: ${file.name}`
    : "Opcional. Ao enviar, o vídeo substitui a URL acima. Tamanho máximo: 200 MB.";
});

async function uploadCampaignAsset(campaignId, kind, file) {
  const data = new FormData();
  data.set("kind", kind);
  data.set("file", file);
  await api(`/api/admin/campaign/${campaignId}/asset`, { method: "POST", body: data });
}

function escapeAttr(value) {
  return escapeHtml(value).replace(/"/g, "&quot;");
}

function debounce(fn, wait) {
  let timeout;
  return (...args) => {
    clearTimeout(timeout);
    timeout = setTimeout(() => fn(...args), wait);
  };
}

syncWallExtras();

loadCampaign().then(() => {
  if (location.hash === "#admin") showAdminPanel();
  setInterval(() => refreshCampaignProgress().catch((error) => console.error(error)), 30000);
}).catch((error) => console.error(error));
