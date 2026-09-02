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
const earliestTrustedTimestamp = Date.parse("2021-01-01T00:00:00Z");
const freshnessMaxAgeSeconds = 7 * 24 * 60 * 60;
const trustedCurrentTime = () => {
  const injected = globalThis.__RIFTWARD_TRUSTED_NOW__;
  return Number.isFinite(injected) ? injected : Date.now();
};

function exactFields(value, fields) {
  return value && typeof value === "object" && !Array.isArray(value) && Object.keys(value).length === fields.length && fields.every((field) => Object.hasOwn(value, field));
}

function timestamp(value) {
  if (typeof value !== "string" || !isoTimestamp.test(value)) return null;
  const parsed = Date.parse(value);
  return Number.isFinite(parsed) && parsed >= earliestTrustedTimestamp ? parsed : null;
}

function validCounter(value) {
  return Number.isSafeInteger(value) && value >= 0;
}

function validateStatus(status) {
  if (!exactFields(status, ["schemaVersion", "statusContract", "generatedAt", "freshness", "source", "workItems", "candidate", "wip", "claims"])) throw new Error("invalid-root");
  if (status.schemaVersion !== 2 || status.statusContract !== "riftward-public-status-v2") throw new Error("unsupported-schema");
  if (timestamp(status.generatedAt) === null) throw new Error("invalid-generated-at");

  const source = status.source;
  if (!exactFields(source, ["branch", "classification", "commit", "tree", "committedAt", "dirty"])) throw new Error("missing-source");
  if (!fullHash.test(source.commit) || !fullHash.test(source.tree)) throw new Error("invalid-source-hash");
  if (!branchName.test(source.branch) || source.branch.startsWith("/") || source.branch.includes("..")) throw new Error("invalid-source-branch");
  if (!["accepted-main", "candidate-branch"].includes(source.classification)) throw new Error("invalid-source-classification");
  if (timestamp(source.committedAt) === null || source.dirty !== false) throw new Error("invalid-source-state");
  const freshness = status.freshness;
  if (!exactFields(freshness, ["basis", "sourceCommit", "trustedBuildAt", "maxAgeSeconds"]) || freshness.basis !== "source-commit-time" || freshness.sourceCommit !== source.commit || freshness.maxAgeSeconds !== freshnessMaxAgeSeconds || timestamp(freshness.trustedBuildAt) === null) throw new Error("invalid-freshness-binding");
  const ageSeconds = (timestamp(freshness.trustedBuildAt) - timestamp(source.committedAt)) / 1000;
  const currentAgeSeconds = (trustedCurrentTime() - timestamp(freshness.trustedBuildAt)) / 1000;
  if (status.generatedAt !== source.committedAt || ageSeconds < 0 || ageSeconds > freshness.maxAgeSeconds || currentAgeSeconds < 0 || currentAgeSeconds > freshness.maxAgeSeconds) throw new Error("invalid-freshness-age");
  if (source.commit !== metadata("riftward-source-commit") ||
      source.tree !== metadata("riftward-source-tree") ||
      source.branch !== metadata("riftward-source-branch") ||
      source.classification !== metadata("riftward-source-classification")) {
    throw new Error("html-status-provenance-mismatch");
  }

  const workItems = status.workItems;
  if (!exactFields(workItems, ["accepted", "ready", "review", "acceptedTaskIds", "reviewTaskIds", "nextReady"]) || ![workItems.accepted, workItems.ready, workItems.review].every(validCounter)) throw new Error("invalid-work-items");
  for (const [name, count] of [["acceptedTaskIds", workItems.accepted], ["reviewTaskIds", workItems.review]]) {
    if (!Array.isArray(workItems[name]) || new Set(workItems[name]).size !== workItems[name].length || workItems[name].length !== count || !workItems[name].every((id) => /^T-[0-9]{3,}$/.test(id))) throw new Error(`invalid-${name}`);
  }
  if (!exactFields(workItems.nextReady, ["state", "taskIds"]) || !["none", "single", "multiple"].includes(workItems.nextReady.state)) throw new Error("invalid-next-ready");
  if (!Array.isArray(workItems.nextReady.taskIds) || new Set(workItems.nextReady.taskIds).size !== workItems.nextReady.taskIds.length || !workItems.nextReady.taskIds.every((id) => /^T-[0-9]{3,}$/.test(id))) throw new Error("invalid-ready-ids");
  const readyCount = workItems.nextReady.taskIds.length;
  if (workItems.ready !== readyCount || (readyCount === 0 && workItems.nextReady.state !== "none") ||
      (readyCount === 1 && workItems.nextReady.state !== "single") ||
      (readyCount > 1 && workItems.nextReady.state !== "multiple")) {
    throw new Error("inconsistent-ready-state");
  }

  const candidate = status.candidate;
  if (!exactFields(candidate, ["state", "reason"]) || !["checked-out-candidate", "not-observed"].includes(candidate.state) || typeof candidate.reason !== "string" || !candidate.reason) throw new Error("invalid-candidate-state");
  if ((source.branch === "main") !== (source.classification === "accepted-main")) throw new Error("invalid-source-relation");
  if (source.classification === "candidate-branch" && candidate.state !== "checked-out-candidate") throw new Error("missing-candidate-binding");
  if (source.classification === "accepted-main" && candidate.state !== "not-observed") throw new Error("invented-candidate");

  const wip = status.wip;
  if (!wip || !["published", "not-observed"].includes(wip.state)) throw new Error("invalid-wip-state");
  if (wip.classification !== "continuity-snapshot-not-accepted-progress") throw new Error("invalid-wip-classification");
  if (!exactFields(wip.provenance, ["observed", "source", "reason"]) || wip.provenance.source !== "public-remote-ref" || typeof wip.provenance.reason !== "string" || typeof wip.provenance.observed !== "boolean" || !wip.provenance.reason) throw new Error("invalid-wip-provenance");
  if (wip.state === "published" &&
      (!exactFields(wip, ["state", "classification", "branch", "commit", "committedAt", "provenance"]) || wip.branch !== "autopilot/live-wip" || !fullHash.test(wip.commit) || timestamp(wip.committedAt) === null || wip.provenance.observed !== true)) {
    throw new Error("invalid-wip-provenance");
  }
  if (wip.state === "not-observed" && (!exactFields(wip, ["state", "classification", "provenance"]) || wip.provenance.observed !== false)) throw new Error("invalid-wip-provenance");

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
