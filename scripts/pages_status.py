#!/usr/bin/env python3
"""Build deterministic public status JSON and dependency-free SVG badges."""

from __future__ import annotations

import argparse
from datetime import datetime, timezone
from html import escape
import json
import os
from pathlib import Path
import re
import subprocess
import sys
import tempfile

from task_eligibility import EligibilityError, evaluate
from validate_reconciliation import RECONCILIATION_PATH, ReconciliationError, load_json as load_reconciliation, validate as validate_reconciliation


SHA = re.compile(r"^[0-9a-f]{40}$")
TASK = re.compile(r"^T-[0-9]{3}$")
BACKLOG_ROW = re.compile(r"^\|\s*(T-[0-9]{3})\s*\|")
PUBLIC_STATUSES = {"DONE", "READY", "REVIEW", "DRAFT", "BLOCKED", "CANCELLED", "RUNNING"}
LIFECYCLE = {"DRAFT", "READY", "IN_PROGRESS", "REVIEW", "BLOCKED", "DONE", "UNKNOWN"}
GATES = {"passed", "failed", "waiting", "blocked", "unknown"}
BLOCKERS = {"none", "awaiting-review", "awaiting-preregistered-t042-start-eligibility", "blocked", "unknown"}
PHASES = {"planning", "building", "reviewing", "repairing", "waiting", "unknown"}
ROLES = {"planner", "builder", "reviewer", "repair", "wip", "unknown"}
AUTONOMY = {"human-gated", "bounded-autopilot", "unknown"}
PARENTS = {"root", "child", "unknown"}
EARLIEST = datetime(2021, 1, 1, tzinfo=timezone.utc)
ROOT_FIELDS = {"schemaVersion", "repository", "main", "candidates", "continuity", "activity"}


class StatusError(RuntimeError):
    pass


def git(root: Path, *args: str) -> str:
    result = subprocess.run(["git", "-C", str(root), *args], check=False, stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True, encoding="utf-8")
    if result.returncode != 0:
        raise StatusError(f"git {' '.join(args)} failed")
    return result.stdout.strip()


def canonical_time(value: str, label: str) -> str:
    try:
        parsed = datetime.fromisoformat(value.replace("Z", "+00:00"))
    except ValueError as exc:
        raise StatusError(f"invalid {label}") from exc
    if parsed.tzinfo is None:
        raise StatusError(f"{label} has no timezone")
    parsed = parsed.astimezone(timezone.utc)
    if parsed < EARLIEST:
        raise StatusError(f"{label} predates public epoch")
    return parsed.isoformat(timespec="seconds").replace("+00:00", "Z")


def parse_backlog(path: Path) -> dict[str, list[str]]:
    statuses = {name: [] for name in PUBLIC_STATUSES}
    seen: set[str] = set()
    for line in path.read_text(encoding="utf-8").splitlines():
        match = BACKLOG_ROW.match(line)
        if not match:
            continue
        columns = [column.strip() for column in line.strip().strip("|").split("|")]
        if len(columns) != 7 or columns[0] in seen or columns[6] not in PUBLIC_STATUSES:
            raise StatusError("malformed or duplicate BACKLOG row")
        seen.add(columns[0])
        statuses[columns[6]].append(columns[0])
    if not seen:
        raise StatusError("BACKLOG contains no task rows")
    return statuses


def clean_source(root: Path) -> None:
    if git(root, "status", "--porcelain=v1", "--untracked-files=all"):
        raise StatusError("public status inputs are not a clean committed tree")


def validate_verdict(root: Path, value: object, reconciliation: dict[str, object], commit: str, tree: str) -> tuple[set[str], set[str]]:
    fields = {"schemaVersion", "contract", "mainCommit", "mainTree", "reconciliationBlobOid", "historicalProjectionSha256", "audit", "recordedTaskIds", "doneEligibleTaskIds"}
    if not isinstance(value, dict) or set(value) != fields or value.get("schemaVersion") != 1 or value.get("contract") != "riftward-reconciliation-verdict-v1" or value.get("mainCommit") != commit or value.get("mainTree") != tree:
        raise StatusError("invalid reconciliation verdict identity")
    if value.get("reconciliationBlobOid") != git(root, "rev-parse", f"HEAD:{RECONCILIATION_PATH}") or value.get("historicalProjectionSha256") != reconciliation["historicalProjectionSha256"]:
        raise StatusError("reconciliation verdict blob/digest mismatch")
    recorded = value.get("recordedTaskIds")
    eligible = value.get("doneEligibleTaskIds")
    if not isinstance(recorded, list) or not isinstance(eligible, list) or recorded != reconciliation["recordedTaskIds"] or eligible != reconciliation["doneEligibleTaskIds"] or len(recorded) != len(set(recorded)) or len(eligible) != len(set(eligible)):
        raise StatusError("reconciliation verdict task set mismatch")
    audit = value.get("audit")
    if reconciliation["auditState"] == "pending":
        if audit != {"state": "pending"} or eligible:
            raise StatusError("pending reconciliation verdict grants DONE eligibility")
    else:
        manifest_audit = reconciliation["manifestAudit"]
        if not isinstance(manifest_audit, dict) or audit != {"state": "passed", "evidenceBlobOid": manifest_audit.get("evidenceBlobOid")}:
            raise StatusError("passed reconciliation verdict lacks audit binding")
    return set(recorded), set(eligible)


def task_manifest_status(root: Path, task_id: str) -> str:
    matches = list((root / ".ai/tasks").glob(f"{task_id}-*.json"))
    if len(matches) != 1:
        raise StatusError(f"task manifest cardinality mismatch for {task_id}")
    value = json.loads(matches[0].read_text(encoding="utf-8"))
    if not isinstance(value, dict) or value.get("id") != task_id or not isinstance(value.get("status"), str):
        raise StatusError(f"task manifest identity mismatch for {task_id}")
    return str(value["status"])


def accepted_task_ids(root: Path, done: set[str], recorded: set[str], eligible: set[str]) -> list[str]:
    if (done & recorded) != eligible or any(task_manifest_status(root, task_id) != "accepted" for task_id in eligible):
        raise StatusError("historical DONE state is not backed by the live audit verdict")
    return sorted((done - recorded) | eligible)


def validate_observation(value: object, commit: str, tree: str) -> dict[str, object]:
    if not isinstance(value, dict) or set(value) != ROOT_FIELDS or value.get("schemaVersion") != 1:
        raise StatusError("invalid normalized GitHub observation")
    main = value.get("main")
    if value.get("repository") != {"id": "1333151301", "name": "Koschnag/ai-fantasy-rts-rpg"} or not isinstance(main, dict) or set(main) != {"commit", "tree", "gates"} or main.get("commit") != commit or main.get("tree") != tree or main.get("gates") not in {"passed", "blocked", "unknown"}:
        raise StatusError("GitHub observation identity mismatch")
    candidates = value.get("candidates")
    if not isinstance(candidates, dict) or set(candidates) != {"state", "items"} or candidates.get("state") not in {"observed", "not-observed", "unavailable"} or not isinstance(candidates.get("items"), list):
        raise StatusError("invalid candidate observation")
    candidate_fields = {"taskId", "lifecycleStatus", "gate", "blocker"}
    for item in candidates["items"]:
        if not isinstance(item, dict) or set(item) != candidate_fields or not isinstance(item.get("taskId"), str) or not TASK.fullmatch(item["taskId"]):
            raise StatusError("invalid candidate item")
        if item["lifecycleStatus"] not in LIFECYCLE or item["gate"] not in GATES or item["blocker"] not in BLOCKERS:
            raise StatusError("invalid candidate item enum")
    if (candidates["state"] == "observed") != bool(candidates["items"]):
        raise StatusError("candidate state/items relation mismatch")
    continuity = value.get("continuity")
    if not isinstance(continuity, dict) or continuity.get("state") not in {"published", "not-observed", "stale", "unavailable"} or continuity.get("classification") != "continuity-not-accepted-progress":
        raise StatusError("invalid continuity observation")
    if continuity["state"] == "published":
        if set(continuity) != {"state", "classification", "commit", "committedAt"} or not isinstance(continuity["commit"], str) or not SHA.fullmatch(continuity["commit"]):
            raise StatusError("invalid published continuity")
        canonical_time(str(continuity["committedAt"]), "continuity commit time")
    elif set(continuity) != {"state", "classification"}:
        raise StatusError("unpublished continuity contains details")
    activity = value.get("activity")
    if not isinstance(activity, dict) or activity.get("state") not in {"active", "waiting", "blocked", "idle", "offline", "unknown"}:
        raise StatusError("invalid activity observation")
    activity_fields = {"state", "taskId", "phase", "role", "lastGate", "blocker", "autonomy", "parentClass"}
    if activity["state"] in {"active", "waiting", "blocked", "idle"}:
        if set(activity) != activity_fields or not isinstance(activity["taskId"], str) or not TASK.fullmatch(activity["taskId"]):
            raise StatusError("invalid activity details")
        if activity["phase"] not in PHASES or activity["role"] not in ROLES or activity["lastGate"] not in GATES or activity["blocker"] not in BLOCKERS or activity["autonomy"] not in AUTONOMY or activity["parentClass"] not in PARENTS:
            raise StatusError("invalid activity enum")
    elif set(activity) != {"state"}:
        raise StatusError("offline/unknown activity contains details")
    return value


def badge(label: str, value: str, color: str) -> str:
    label_text, value_text = escape(label), escape(value)
    left, right = 78, 158
    return (
        '<svg xmlns="http://www.w3.org/2000/svg" role="img" aria-label="'
        + escape(f"{label}: {value}", quote=True)
        + f'" width="{left + right}" height="20" viewBox="0 0 {left + right} 20">'
        + '<rect width="236" height="20" rx="3" fill="#252b36"/>'
        + f'<rect x="{left}" width="{right}" height="20" rx="3" fill="{color}"/>'
        + f'<path d="M{left} 0h4v20h-4z" fill="{color}"/>'
        + '<g fill="#fff" text-anchor="middle" font-family="Verdana,DejaVu Sans,sans-serif" font-size="11">'
        + f'<text x="{left / 2}" y="14">{label_text}</text><text x="{left + right / 2}" y="14">{value_text}</text></g></svg>\n'
    )


def atomic_text(path: Path, payload: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    handle, temporary = tempfile.mkstemp(prefix=f".{path.name}.", dir=path.parent)
    try:
        with os.fdopen(handle, "w", encoding="utf-8", newline="\n") as stream:
            stream.write(payload)
            stream.flush()
            os.fsync(stream.fileno())
        os.replace(temporary, path)
    finally:
        if os.path.exists(temporary):
            os.unlink(temporary)


def generate(root: Path, observed_at: str, observation_file: Path, reconciliation_file: Path, verdict_file: Path) -> dict[str, object]:
    clean_source(root)
    commit = git(root, "rev-parse", "HEAD")
    tree = git(root, "rev-parse", "HEAD^{tree}")
    if not SHA.fullmatch(commit) or not SHA.fullmatch(tree):
        raise StatusError("invalid main identity")
    observed = canonical_time(observed_at, "public observation time")
    normalized = validate_observation(json.loads(observation_file.read_text(encoding="utf-8")), commit, tree)
    reconciliation_manifest = load_reconciliation(reconciliation_file)
    reconciled = validate_reconciliation(reconciliation_manifest, root)
    recorded, eligible = validate_verdict(root, load_reconciliation(verdict_file), reconciled, commit, tree)
    statuses = parse_backlog(root / "BACKLOG.md")
    done = set(statuses["DONE"])
    accepted_ids = accepted_task_ids(root, done, recorded, eligible)
    ready_ids = sorted(statuses["READY"])
    current = evaluate(root, "T-053")
    committed_at = canonical_time(git(root, "show", "-s", "--format=%cI", "HEAD"), "public commit time")
    return {
        "schemaVersion": 3,
        "statusContract": "riftward-public-status-v3",
        "observation": {"state": "current", "basis": "trusted-main-and-allowlisted-inputs-v1", "observedAtUtc": observed, "freshForSeconds": 1800, "offlineAfterSeconds": 21600, "sourceCommit": commit, "sourceTree": tree},
        "accepted": {"main": {"branch": "main", "classification": "accepted-main", "commit": commit, "tree": tree, "committedAt": committed_at, "gates": normalized["main"]["gates"]}, "tasks": {"count": len(accepted_ids), "ids": accepted_ids}},
        "candidates": normalized["candidates"],
        "continuity": normalized["continuity"],
        "activity": normalized["activity"],
        "tasks": {"current": current, "ready": ready_ids},
        "claims": {"gameplay": "graybox-only", "targetHardware": "not-validated", "physicalEdition": "not-produced", "twentyFourSevenAutonomy": "not-demonstrated", "concepts": "not-gameplay"},
    }


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--root", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--status-svg", type=Path, required=True)
    parser.add_argument("--task-svg", type=Path, required=True)
    parser.add_argument("--observed-at", required=True)
    parser.add_argument("--github-observation", type=Path, required=True)
    parser.add_argument("--reconciliation", type=Path, required=True)
    parser.add_argument("--reconciliation-verdict", type=Path, required=True)
    args = parser.parse_args()
    try:
        status = generate(args.root.resolve(strict=True), args.observed_at, args.github_observation.resolve(strict=True), args.reconciliation.resolve(strict=True), args.reconciliation_verdict.resolve(strict=True))
        payload = json.dumps(status, ensure_ascii=False, indent=2, sort_keys=True) + "\n"
        state = status["observation"]["state"]
        colors = {"current": "#16794b", "stale": "#9a6700", "offline": "#b42318", "unknown": "#57606a"}
        current = status["tasks"]["current"]
        atomic_text(args.output.resolve(), payload)
        atomic_text(args.status_svg.resolve(), badge("Riftward", f"{state} · {len(status['accepted']['tasks']['ids'])} accepted", colors[state]))
        atomic_text(args.task_svg.resolve(), badge(current["taskId"], f"{current['lifecycleStatus']} · {current['effectiveStartEligibility']}", "#9a6700" if current["effectiveStartEligibility"] == "waiting" else "#16794b"))
    except (OSError, ValueError, json.JSONDecodeError, StatusError, EligibilityError, ReconciliationError) as exc:
        print(f"Pages-Status abgelehnt: {exc}", file=sys.stderr)
        return 2
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
