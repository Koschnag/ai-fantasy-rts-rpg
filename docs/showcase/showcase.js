"use strict";

(() => {
  const SHA = /^[a-f0-9]{40}$/;
  const TASK = /^T-\d{3}$/;
  const RFC3339 = /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d+)?(?:Z|[+-]\d{2}:\d{2})$/;
  const OBSERVATION = new Set(["current", "stale", "offline", "unknown"]);
  const CANDIDATE = new Set(["observed", "not-observed", "unavailable"]);
  const CONTINUITY = new Set(["published", "not-observed", "stale", "unavailable"]);
  const ACTIVITY = new Set(["active", "waiting", "blocked", "idle", "offline", "unknown"]);
  const DETAILED_ACTIVITY = new Set(["active", "waiting", "blocked", "idle"]);
  const LIFECYCLE = new Set(["DRAFT", "READY", "IN_PROGRESS", "REVIEW", "BLOCKED", "DONE", "UNKNOWN"]);
  const GATE = new Set(["passed", "failed", "waiting", "blocked", "unknown"]);
  const BLOCKER = new Set(["none", "awaiting-review", "awaiting-preregistered-t042-start-eligibility", "blocked", "unknown"]);
  const ELIGIBILITY = new Set(["eligible", "waiting", "blocked", "unknown"]);
  const PHASE = new Set(["planning", "building", "reviewing", "repairing", "waiting", "unknown"]);
  const ROLE = new Set(["planner", "builder", "reviewer", "repair", "wip", "unknown"]);
  const AUTONOMY = new Set(["human-gated", "bounded-autopilot", "unknown"]);
  const PARENT = new Set(["root", "child", "unknown"]);

  const isRecord = (value) => value !== null && typeof value === "object" && !Array.isArray(value);
  const exactKeys = (value, keys) => isRecord(value) && Object.keys(value).length === keys.length && keys.every((key) => Object.prototype.hasOwnProperty.call(value, key));
  const isSha = (value) => typeof value === "string" && SHA.test(value);
  const isTask = (value) => typeof value === "string" && TASK.test(value);
  const isPublicCommitTime = (value) => typeof value === "string" && RFC3339.test(value) && !Number.isNaN(Date.parse(value));
  const isEnum = (set, value) => typeof value === "string" && set.has(value);
  const isTaskList = (value) => Array.isArray(value) && value.every(isTask) && value.length === new Set(value).size;

  function validateObservation(value) {
    return exactKeys(value, ["state", "basis", "observedAtUtc", "freshForSeconds", "offlineAfterSeconds", "sourceCommit", "sourceTree"])
      && isEnum(OBSERVATION, value.state)
      && value.basis === "trusted-main-and-allowlisted-inputs-v1"
      && value.freshForSeconds === 1800
      && value.offlineAfterSeconds === 21600
      && isPublicCommitTime(value.observedAtUtc)
      && isSha(value.sourceCommit)
      && isSha(value.sourceTree);
  }

  function observationState(value, trustedNow) {
    if (!validateObservation(value) || !Number.isFinite(trustedNow)) return "invalid";
    if (value.state === "unknown") return "unknown";
    const ageSeconds = observationAgeSeconds(value, trustedNow);
    if (!Number.isFinite(ageSeconds) || ageSeconds < 0) return "invalid";
    const derived = ageSeconds <= value.freshForSeconds ? "current" : ageSeconds <= value.offlineAfterSeconds ? "stale" : "offline";
    const rank = {current: 0, stale: 1, offline: 2};
    return rank[value.state] >= rank[derived] ? value.state : derived;
  }

  function observationAgeSeconds(value, trustedNow) {
    return (trustedNow - Date.parse(value.observedAtUtc)) / 1000;
  }

  function trustedHttpTime(response) {
    if (!response || !response.headers || typeof response.headers.get !== "function") return NaN;
    if (typeof response.url !== "string" || response.url.length === 0
        || typeof location === "undefined" || typeof location.origin !== "string" || location.origin.length === 0) return NaN;
    let responseUrl;
    let locationOrigin;
    try {
      responseUrl = new URL(response.url);
      locationOrigin = new URL(location.origin);
    } catch {
      return NaN;
    }
    if (responseUrl.origin !== locationOrigin.origin) return NaN;
    const value = response.headers.get("Date");
    if (typeof value !== "string" || !/^[A-Z][a-z]{2}, \d{2} [A-Z][a-z]{2} \d{4} \d{2}:\d{2}:\d{2} GMT$/.test(value)) return NaN;
    const date = Date.parse(value);
    if (!Number.isFinite(date) || date < Date.UTC(2021, 0, 1)) return NaN;
    const ageValue = response.headers.get("Age");
    if (ageValue !== null && !/^\d{1,8}$/.test(ageValue)) return NaN;
    const age = ageValue === null ? 0 : Number(ageValue);
    return Number.isSafeInteger(age) ? date + age * 1000 : NaN;
  }

  function validateAccepted(value) {
    return exactKeys(value, ["main", "tasks"])
      && exactKeys(value.main, ["branch", "classification", "commit", "tree", "committedAt", "gates"])
      && value.main.branch === "main"
      && value.main.classification === "accepted-main"
      && isSha(value.main.commit)
      && isSha(value.main.tree)
      && isPublicCommitTime(value.main.committedAt)
      && isEnum(new Set(["passed", "blocked", "unknown"]), value.main.gates)
      && exactKeys(value.tasks, ["count", "ids"])
      && Number.isSafeInteger(value.tasks.count) && value.tasks.count >= 0
      && isTaskList(value.tasks.ids) && value.tasks.count === value.tasks.ids.length;
  }

  function validateCandidates(value) {
    return exactKeys(value, ["state", "items"])
      && isEnum(CANDIDATE, value.state)
      && Array.isArray(value.items)
      && ((value.state === "observed") === (value.items.length > 0))
      && value.items.every((item) => exactKeys(item, ["taskId", "lifecycleStatus", "gate", "blocker"])
        && isTask(item.taskId) && isEnum(LIFECYCLE, item.lifecycleStatus)
        && isEnum(GATE, item.gate) && isEnum(BLOCKER, item.blocker));
  }

  function validateContinuity(value) {
    if (!isRecord(value) || !isEnum(CONTINUITY, value.state)) return false;
    if (value.state === "published") {
      return exactKeys(value, ["state", "classification", "commit", "committedAt"])
        && value.classification === "continuity-not-accepted-progress"
        && isSha(value.commit) && isPublicCommitTime(value.committedAt);
    }
    return exactKeys(value, ["state", "classification"])
      && value.classification === "continuity-not-accepted-progress";
  }

  function validateActivity(value, declaredObservation) {
    if (!isRecord(value) || !isEnum(ACTIVITY, value.state)) return false;
    if (!DETAILED_ACTIVITY.has(value.state)) return exactKeys(value, ["state"]);
    return declaredObservation === "current"
      && exactKeys(value, ["state", "taskId", "phase", "role", "lastGate", "blocker", "autonomy", "parentClass"])
      && isTask(value.taskId) && isEnum(PHASE, value.phase) && isEnum(ROLE, value.role)
      && isEnum(GATE, value.lastGate) && isEnum(BLOCKER, value.blocker)
      && isEnum(AUTONOMY, value.autonomy) && isEnum(PARENT, value.parentClass);
  }

  function validateTasks(value) {
    return exactKeys(value, ["current", "ready"])
      && exactKeys(value.current, ["taskId", "lifecycleStatus", "effectiveStartEligibility", "waitingReason", "selectorEnforcement"])
      && isTask(value.current.taskId) && isEnum(LIFECYCLE, value.current.lifecycleStatus)
      && isEnum(ELIGIBILITY, value.current.effectiveStartEligibility)
      && isEnum(BLOCKER, value.current.waitingReason)
      && value.current.selectorEnforcement === "pending"
      && value.current.taskId === "T-053"
      && value.current.lifecycleStatus === "READY"
      && value.current.effectiveStartEligibility === "waiting"
      && value.current.waitingReason === "awaiting-preregistered-t042-start-eligibility"
      && isTaskList(value.ready);
  }

  function validateClaims(value) {
    return exactKeys(value, ["gameplay", "targetHardware", "physicalEdition", "twentyFourSevenAutonomy", "concepts"])
      && value.gameplay === "graybox-only"
      && value.targetHardware === "not-validated"
      && value.physicalEdition === "not-produced"
      && value.twentyFourSevenAutonomy === "not-demonstrated"
      && value.concepts === "not-gameplay";
  }

  function metadataMatches(status) {
    const read = (name) => document.querySelector(`meta[name="${name}"]`)?.getAttribute("content") || "";
    const metaCommit = read("riftward-source-commit");
    const metaTree = read("riftward-source-tree");
    const metaBranch = read("riftward-source-branch");
    const metaClassification = read("riftward-source-classification");
    return isSha(metaCommit) && metaCommit === status.accepted.main.commit
      && isSha(metaTree) && metaTree === status.accepted.main.tree
      && metaBranch === status.accepted.main.branch
      && metaClassification === status.accepted.main.classification;
  }

  function validateStatus(value) {
    return exactKeys(value, ["schemaVersion", "statusContract", "observation", "accepted", "candidates", "continuity", "activity", "tasks", "claims"])
      && value.schemaVersion === 3
      && value.statusContract === "riftward-public-status-v3"
      && validateObservation(value.observation)
      && validateAccepted(value.accepted)
      && validateCandidates(value.candidates)
      && validateContinuity(value.continuity)
      && validateActivity(value.activity, value.observation.state)
      && validateTasks(value.tasks)
      && validateClaims(value.claims)
      && value.observation.sourceCommit === value.accepted.main.commit
      && value.observation.sourceTree === value.accepted.main.tree
      && metadataMatches(value);
  }

  const set = (name, text) => document.querySelectorAll(`[data-bind="${name}"]`).forEach((node) => { node.textContent = text; });
  const setNotice = (state, text) => {
    const node = document.getElementById("project-status-message");
    if (node) { node.dataset.state = state; node.textContent = text; }
  };
  const short = (sha) => sha.slice(0, 12);
  const title = (value) => ({
    "not-observed": "NICHT BEOBACHTET", unavailable: "UNVERFÜGBAR", stale: "VERALTET",
    current: "AKTUELL", offline: "OFFLINE", active: "AKTIV", idle: "RUHEND",
    observed: "BEOBACHTET", published: "VERÖFFENTLICHT",
    eligible: "STARTBERECHTIGT", waiting: "WARTET", blocked: "BLOCKIERT", unknown: "UNBEKANNT",
    passed: "BESTANDEN", failed: "FEHLGESCHLAGEN", "human-gated": "MENSCHLICH GEGATED",
    "bounded-autopilot": "BEGRENZTER AUTOPILOT", none: "KEIN BLOCKER"
  }[value] || String(value).replaceAll("-", " ").toUpperCase());
  const publicDate = (value) => new Intl.DateTimeFormat("de-DE", { dateStyle: "medium", timeZone: "UTC" }).format(new Date(value));
  const observationAge = (value) => {
    let seconds = Math.ceil(value);
    const days = Math.floor(seconds / 86400); seconds %= 86400;
    const hours = Math.floor(seconds / 3600); seconds %= 3600;
    const minutes = Math.floor(seconds / 60); seconds %= 60;
    const parts = [];
    if (days > 0) parts.push(`${days} T`);
    if (hours > 0) parts.push(`${hours} STD`);
    if (minutes > 0) parts.push(`${minutes} MIN`);
    if (seconds > 0 || parts.length === 0) parts.push(`${seconds} SEK`);
    return parts.join(" ");
  };

  function replaceList(name, items) {
    document.querySelectorAll(`[data-bind="${name}"]`).forEach((list) => {
      list.replaceChildren(...items.map((text) => { const item = document.createElement("li"); item.textContent = text; return item; }));
    });
  }

  function maskVolatile() {
    ["current-task", "current-gate", "autonomy", "candidate-summary", "wip-summary", "activity-summary"].forEach((name) => set(name, "UNVERFÜGBAR"));
    set("effective-eligibility", "Effektiver Start: unbekannt");
    set("current-blocker", "Blocker: unbekannt");
    set("wip-detail", "WIP-Kontinuität: nicht verfügbar.");
    set("activity-detail", "Aktivität: nicht verfügbar.");
    replaceList("candidates", ["Kandidaten: nicht verfügbar."]);
    replaceList("claims", ["Claims: nicht verfügbar."]);
  }

  function unavailable(reason, state = "unavailable") {
    ["main-short-commit", "main-commit", "main-tree", "observation", "accepted-summary"].forEach((name) => set(name, "UNVERFÜGBAR"));
    if (OBSERVATION.has(state)) set("observation", title(state));
    set("main-committed-at", "Öffentliche Commitzeit: unbekannt");
    maskVolatile();
    setNotice(state, reason);
  }

  function renderHistorical(status, state, ageSeconds) {
    const age = observationAge(ageSeconds);
    set("main-short-commit", short(status.accepted.main.commit));
    set("main-commit", status.accepted.main.commit);
    set("main-tree", status.accepted.main.tree);
    set("main-committed-at", `Zuletzt beobachteter öffentlicher Commitzeitpunkt: ${status.accepted.main.committedAt}`);
    set("observation", `${title(state)} · ${age} ALT · HTTP-VERTRAUENSZEIT`);
    set("accepted-summary", `${status.accepted.tasks.count} ZULETZT BEOBACHTETE AKZEPTIERTE TASKS`);
    maskVolatile();
    setNotice(state, `${title(state)}: letzte validierte akzeptierte main-Provenienz, ${age} alt nach gleichoriginiger HTTP-Vertrauenszeit. Volatile Werte sind nicht verfügbar.`);
  }

  function render(status) {
    const { observation, accepted, candidates, continuity, activity, tasks, claims } = status;
    set("main-short-commit", short(accepted.main.commit));
    set("main-commit", accepted.main.commit);
    set("main-tree", accepted.main.tree);
    set("main-committed-at", `Öffentliche Commitzeit: ${publicDate(accepted.main.committedAt)} UTC`);
    set("observation", "AKTUELL · ≤ 30 MIN");
    set("current-task", `${tasks.current.taskId} · ${title(tasks.current.lifecycleStatus)}`);
    set("effective-eligibility", `Effektiver Start: ${title(tasks.current.effectiveStartEligibility)} · Selektor: NICHT NACHGEWIESEN`);
    set("current-gate", title(accepted.main.gates));
    set("current-blocker", `Blocker: ${title(tasks.current.waitingReason)}`);
    set("autonomy", DETAILED_ACTIVITY.has(activity.state) ? title(activity.autonomy) : "NICHT BEOBACHTET");
    set("accepted-summary", `${accepted.tasks.count} AKZEPTIERTE TASKS`);
    set("candidate-summary", candidates.state === "observed" ? `${candidates.items.length} OFFENE KANDIDATEN` : title(candidates.state));
    replaceList("candidates", candidates.state === "observed" && candidates.items.length > 0
      ? candidates.items.map((item) => `${item.taskId}: ${title(item.lifecycleStatus)} · ${title(item.gate)} · ${title(item.blocker)}`)
      : [`Kandidaten: ${title(candidates.state)}.`]);
    set("wip-summary", title(continuity.state));
    set("wip-detail", continuity.state === "published"
      ? `Öffentliche Kontinuität veröffentlicht (${publicDate(continuity.committedAt)} UTC). Kein akzeptierter Fortschritt.`
      : "Keine WIP-Kontinuität als Akzeptanz interpretieren.");
    set("activity-summary", title(activity.state));
    set("activity-detail", DETAILED_ACTIVITY.has(activity.state)
      ? `${activity.taskId}: ${title(activity.phase)} · ${title(activity.role)} · Gate ${title(activity.lastGate)} · ${title(activity.parentClass)}.`
      : "Keine frische, begrenzte Aktivitätsbeobachtung verfügbar.");
    replaceList("claims", [
      "Spielstand: ausschließlich akzeptierte interaktive Graybox.",
      "Fertiges oder repräsentatives Spiel: nicht belegt.",
      "Zielhardware: nicht validiert.",
      "Physische Ausgabe: nicht produziert.",
      "24/7-Autonomie: nicht nachgewiesen.",
      "Konzeptbilder: kein Gameplay."
    ]);
    setNotice("current", "Aktuell beobachtet: exakter akzeptierter main-Baum, höchstens 30 Minuten alt. Kandidaten, WIP und Aktivität sind getrennte Aussagen.");
  }

  function installTabs() {
    const tabs = [...document.querySelectorAll('[role="tab"]')];
    tabs.forEach((tab, index) => tab.addEventListener("keydown", (event) => {
      if (!["ArrowLeft", "ArrowRight", "Home", "End"].includes(event.key)) return;
      event.preventDefault();
      const next = event.key === "Home" ? 0 : event.key === "End" ? tabs.length - 1 : (index + (event.key === "ArrowRight" ? 1 : -1) + tabs.length) % tabs.length;
      tabs[next].focus();
      tabs[next].click();
    }));
    tabs.forEach((tab) => tab.addEventListener("click", () => {
      const panelId = `panel-${tab.dataset.panel}`;
      tabs.forEach((item) => { const active = item === tab; item.setAttribute("aria-selected", String(active)); item.tabIndex = active ? 0 : -1; });
      document.querySelectorAll(".method-panel").forEach((panel) => { const active = panel.id === panelId; panel.hidden = !active; panel.classList.toggle("active", active); });
    }));
  }

  installTabs();
  fetch("status.json", { cache: "no-store", credentials: "same-origin", redirect: "error" })
    .then(async (response) => {
      if (!response.ok) throw new Error("status response unavailable");
      const trustedNow = trustedHttpTime(response);
      if (!Number.isFinite(trustedNow)) throw new Error("trusted HTTP time unavailable");
      return {status: await response.json(), trustedNow};
    })
    .then(({status, trustedNow}) => {
      if (!validateStatus(status)) throw new Error("status contract invalid");
      const freshness = observationState(status.observation, trustedNow);
      if (freshness === "invalid") {
        unavailable("Status nicht verfügbar: die öffentliche Beobachtungszeit ist unbekannt oder ungültig.");
        return;
      }
      if (freshness === "unknown") {
        unavailable("Status unbekannt: die öffentliche Beobachtung enthält keine belastbare Freshness-Aussage.", "unknown");
        return;
      }
      if (freshness !== "current") {
        renderHistorical(status, freshness, observationAgeSeconds(status.observation, trustedNow));
        return;
      }
      render(status);
    })
    .catch(() => unavailable("Status nicht verfügbar: Vertrag, Provenienz oder öffentliche Statusdatei konnte nicht geprüft werden."));
})();
