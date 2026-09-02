"use strict";

const tabs = [...document.querySelectorAll('[role="tab"]')];
const panels = [...document.querySelectorAll('[role="tabpanel"]')];

function activate(tab) {
  tabs.forEach((item) => {
    const selected = item === tab;
    item.setAttribute("aria-selected", String(selected));
    item.tabIndex = selected ? 0 : -1;
  });
  panels.forEach((panel) => {
    const selected = panel.id === `panel-${tab.dataset.panel}`;
    panel.hidden = !selected;
    panel.classList.toggle("active", selected);
  });
}

tabs.forEach((tab, index) => {
  tab.addEventListener("click", () => activate(tab));
  tab.addEventListener("keydown", (event) => {
    if (!["ArrowLeft", "ArrowRight", "Home", "End"].includes(event.key)) return;
    event.preventDefault();
    let next = index;
    if (event.key === "ArrowLeft") next = (index - 1 + tabs.length) % tabs.length;
    if (event.key === "ArrowRight") next = (index + 1) % tabs.length;
    if (event.key === "Home") next = 0;
    if (event.key === "End") next = tabs.length - 1;
    activate(tabs[next]);
    tabs[next].focus();
  });
});

const statusMessage = document.querySelector("#project-status-message");
const bind = (name, value) => {
  document.querySelectorAll(`[data-bind="${name}"]`).forEach((node) => {
    node.textContent = value;
  });
};

const metadata = (name) => document.querySelector(`meta[name="${name}"]`)?.content ?? "";
const fullHash = /^[0-9a-f]{40}$/;
const branchName = /^[A-Za-z0-9._/-]{1,200}$/;
const isoTimestamp = /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d+)?(?:Z|[+-]\d{2}:\d{2})$/;

function validTimestamp(value) {
  return typeof value === "string" && isoTimestamp.test(value) && Number.isFinite(Date.parse(value));
}

function validCounter(value) {
  return Number.isSafeInteger(value) && value >= 0;
}

function validateStatus(status) {
  if (!status || typeof status !== "object" || Array.isArray(status)) throw new Error("invalid-root");
  if (status.schemaVersion !== 2 || status.statusContract !== "riftward-public-status-v2") throw new Error("unsupported-schema");

  const source = status.source;
  if (!source || typeof source !== "object") throw new Error("missing-source");
  if (!fullHash.test(source.commit) || !fullHash.test(source.tree)) throw new Error("invalid-source-hash");
  if (!branchName.test(source.branch)) throw new Error("invalid-source-branch");
  if (!["accepted-main", "candidate-branch"].includes(source.classification)) throw new Error("invalid-source-classification");
  if (!validTimestamp(source.committedAt) || source.dirty !== false) throw new Error("invalid-source-state");
  if (source.commit !== metadata("riftward-source-commit") ||
      source.tree !== metadata("riftward-source-tree") ||
      source.branch !== metadata("riftward-source-branch") ||
      source.classification !== metadata("riftward-source-classification")) {
    throw new Error("html-status-provenance-mismatch");
  }

  const workItems = status.workItems;
  if (!workItems || ![workItems.accepted, workItems.ready, workItems.review].every(validCounter)) throw new Error("invalid-work-items");
  if (!workItems.nextReady || !["none", "single", "multiple"].includes(workItems.nextReady.state)) throw new Error("invalid-next-ready");
  if (!Array.isArray(workItems.nextReady.taskIds) || !workItems.nextReady.taskIds.every((id) => /^T-[0-9]{3,}$/.test(id))) throw new Error("invalid-ready-ids");
  const readyCount = workItems.nextReady.taskIds.length;
  if ((readyCount === 0 && workItems.nextReady.state !== "none") ||
      (readyCount === 1 && workItems.nextReady.state !== "single") ||
      (readyCount > 1 && workItems.nextReady.state !== "multiple")) {
    throw new Error("inconsistent-ready-state");
  }

  const candidate = status.candidate;
  if (!candidate || !["checked-out-candidate", "not-observed"].includes(candidate.state)) throw new Error("invalid-candidate-state");
  if (source.classification === "candidate-branch" && candidate.state !== "checked-out-candidate") throw new Error("missing-candidate-binding");
  if (source.classification === "accepted-main" && candidate.state !== "not-observed") throw new Error("invented-candidate");

  const wip = status.wip;
  if (!wip || !["published", "not-observed"].includes(wip.state)) throw new Error("invalid-wip-state");
  if (wip.classification !== "continuity-snapshot-not-accepted-progress") throw new Error("invalid-wip-classification");
  if (wip.state === "published" &&
      (wip.branch !== "autopilot/live-wip" || !fullHash.test(wip.commit) || !validTimestamp(wip.committedAt))) {
    throw new Error("invalid-wip-provenance");
  }

  const claims = status.claims;
  if (!claims || claims.gameplay !== false || claims.targetHardwareValidated !== false || claims.physicalEdition !== false || claims.twentyFourSevenAutonomy !== false) {
    throw new Error("unsupported-public-claim");
  }
  return status;
}

function dateLabel(value) {
  return new Intl.DateTimeFormat("de-DE", { dateStyle: "medium", timeStyle: "short", timeZone: "UTC" }).format(new Date(value)) + " UTC";
}

function setUnavailable() {
  document.documentElement.dataset.projectStatus = "unavailable";
  statusMessage.dataset.state = "unavailable";
  statusMessage.textContent = "Status nicht verfügbar: Die Statusdatei fehlt, ist ungültig oder widerspricht der an diese Seite gebundenen Provenienz.";
  bind("accepted", "—");
  bind("review", "—");
  bind("ready", "—");
  bind("nextReady", "Nicht verfügbar");
  bind("shortCommit", "—");
  bind("committedAt", "Nicht verfügbar");
  bind("sourceProvenance", "Nicht verfügbar; keine Ableitung aus sichtbaren WIP-Dateien.");
  bind("candidateState", "Nicht verfügbar; kein Kandidat wird angenommen.");
  bind("wipState", "Nicht verfügbar. Kontinuitätssnapshot, kein akzeptierter Fortschritt.");
}

fetch("status.json", { headers: { Accept: "application/json" }, cache: "no-store" })
  .then((response) => response.ok ? response.json() : Promise.reject(new Error(`status-${response.status}`)))
  .then(validateStatus)
  .then((status) => {
    const { source, workItems, candidate, wip } = status;
    bind("accepted", String(workItems.accepted));
    bind("review", String(workItems.review));
    bind("ready", String(workItems.ready));
    bind("nextReady", workItems.nextReady.state === "none" ? "Kein READY-Auftrag" : `${workItems.nextReady.state === "single" ? "Nächster" : "Mehrere"}: ${workItems.nextReady.taskIds.join(", ")}`);
    bind("shortCommit", source.commit.slice(0, 8));
    bind("committedAt", `Commit ${dateLabel(source.committedAt)}`);
    bind("sourceProvenance", `${source.branch} · ${source.classification} · Tree ${source.tree.slice(0, 12)}`);
    bind("candidateState", candidate.state === "checked-out-candidate" ? `Ausgecheckter Kandidatenbranch ${source.branch}; nicht als main akzeptiert.` : "Nicht beobachtet; der Pages-Build besitzt keinen autoritativen Kandidaten-Receipt.");
    bind("wipState", wip.state === "published" ? `autopilot/live-wip · ${wip.commit.slice(0, 12)} · ${dateLabel(wip.committedAt)} · Kontinuitätssnapshot, kein akzeptierter Fortschritt.` : "Nicht beobachtet. Kontinuitätssnapshot, kein akzeptierter Fortschritt.");
    document.querySelector('[data-lamp="source"]')?.classList.add("green");
    if (candidate.state === "checked-out-candidate") document.querySelector('[data-lamp="candidate"]')?.classList.add("amber");
    if (wip.state === "published") document.querySelector('[data-lamp="wip"]')?.classList.add("amber");
    document.documentElement.dataset.projectStatus = "verified";
    statusMessage.dataset.state = "verified";
    statusMessage.textContent = `Status verifiziert: ${source.classification} ${source.commit.slice(0, 12)} ist an diese HTML-Ausgabe gebunden.`;
  })
  .catch(setUnavailable);
