#!/usr/bin/env python3
"""Read and normalize allowlisted public GitHub state without executing foreign refs."""

from __future__ import annotations

import argparse
import base64
from datetime import datetime, timezone
import hashlib
import json
import os
from pathlib import Path
import re
import sys
from urllib.error import HTTPError, URLError
from urllib.parse import quote
from urllib.request import Request, urlopen


REPOSITORY_ID = "1333151301"
REPOSITORY = "Koschnag/ai-fantasy-rts-rpg"
API_ROOT = f"https://api.github.com/repos/{REPOSITORY}"
SIDECAR = ".ai/public-status-v3.json"
WORKFLOW_ID = "333602498"
WORKFLOW_PATH = ".github/workflows/verify.yml"
WORKFLOW_BLOB = "43f93d02014752b6162a923215ff7634905db165"
SHA = re.compile(r"^[0-9a-f]{40}$")
TASK = re.compile(r"^T-[0-9]{3}$")
LIFECYCLE = {"DRAFT", "READY", "IN_PROGRESS", "REVIEW", "BLOCKED", "DONE", "UNKNOWN"}
GATES = {"passed", "failed", "waiting", "blocked", "unknown"}
BLOCKERS = {"none", "awaiting-review", "awaiting-preregistered-t042-start-eligibility", "blocked", "unknown"}
PHASES = {"planning", "building", "reviewing", "repairing", "waiting", "unknown"}
ROLES = {"planner", "builder", "reviewer", "repair", "wip", "unknown"}
AUTONOMY = {"human-gated", "bounded-autopilot", "unknown"}
PARENTS = {"root", "child", "unknown"}
ACTIVITY = {"active", "waiting", "blocked", "idle"}
ROLE_IDENTITIES = {
    "planner": ("Riftward Planner Autopilot", "riftward-planner-autopilot@users.noreply.github.com"),
    "builder": ("Riftward Builder Autopilot", "riftward-builder-autopilot@users.noreply.github.com"),
    "reviewer": ("Riftward Reviewer Autopilot", "riftward-reviewer-autopilot@users.noreply.github.com"),
    "repair": ("Riftward Repair Autopilot", "riftward-repair-autopilot@users.noreply.github.com"),
    "wip": ("Riftward WIP Autopilot", "riftward-wip-autopilot@users.noreply.github.com"),
    "project-lead": ("Koschnag", "35305653+Koschnag@users.noreply.github.com"),
}


class ObservationError(RuntimeError):
    pass


def duplicate_safe(pairs: list[tuple[str, object]]) -> dict[str, object]:
    result: dict[str, object] = {}
    for key, value in pairs:
        if key in result:
            raise ObservationError("duplicate JSON key in untrusted input")
        result[key] = value
    return result


def exact(value: object, fields: set[str]) -> bool:
    return isinstance(value, dict) and set(value) == fields


def timestamp(value: str) -> datetime:
    try:
        parsed = datetime.fromisoformat(value.replace("Z", "+00:00"))
    except ValueError as exc:
        raise ObservationError("invalid public observation timestamp") from exc
    if parsed.tzinfo is None:
        raise ObservationError("public observation timestamp has no timezone")
    return parsed.astimezone(timezone.utc)


class GitHub:
    def __init__(self, token: str | None):
        self.token = token

    def get(self, endpoint: str) -> object:
        headers = {"Accept": "application/vnd.github+json", "User-Agent": "Riftward-Public-Observer/1", "X-GitHub-Api-Version": "2022-11-28"}
        if self.token:
            headers["Authorization"] = f"Bearer {self.token}"
        request = Request(API_ROOT if endpoint == "" else f"{API_ROOT}/{endpoint}", headers=headers)
        try:
            with urlopen(request, timeout=15) as response:
                payload = response.read(1_000_001)
        except (HTTPError, URLError, TimeoutError) as exc:
            raise ObservationError("GitHub public observation unavailable") from exc
        if len(payload) > 1_000_000:
            raise ObservationError("GitHub response exceeds public observation limit")
        try:
            return json.loads(payload.decode("utf-8"), object_pairs_hook=duplicate_safe)
        except (UnicodeDecodeError, json.JSONDecodeError) as exc:
            raise ObservationError("invalid GitHub JSON response") from exc


def trailers(message: object) -> dict[str, str] | None:
    if not isinstance(message, str):
        return None
    result: dict[str, str] = {}
    for line in message.splitlines():
        match = re.fullmatch(r"([A-Za-z][A-Za-z-]*): (.+)", line)
        if match:
            if match.group(1) in result:
                return None
            result[match.group(1)] = match.group(2)
    return result


def git_blob_oid(payload: bytes) -> str:
    return hashlib.sha1(f"blob {len(payload)}\0".encode("ascii") + payload).hexdigest()


def read_sidecar(client: GitHub, commit: str, commit_record: dict[str, object]) -> dict[str, object] | None:
    try:
        raw = client.get(f"contents/{quote(SIDECAR, safe='/')}?ref={commit}")
    except ObservationError:
        return None
    if not isinstance(raw, dict) or raw.get("type") != "file" or raw.get("path") != SIDECAR or raw.get("encoding") != "base64" or not isinstance(raw.get("content"), str) or not isinstance(raw.get("size"), int) or isinstance(raw.get("size"), bool) or raw["size"] > 16384:
        return None
    try:
        encoded = "".join(raw["content"].split())
        payload = base64.b64decode(encoded, validate=True)
        if len(payload) != raw["size"]:
            return None
        value = json.loads(payload.decode("utf-8"), object_pairs_hook=duplicate_safe)
    except (ValueError, UnicodeDecodeError, json.JSONDecodeError, ObservationError):
        return None
    fields = {"schemaVersion", "candidate", "activity"}
    if not exact(value, fields) or value["schemaVersion"] != 1:
        return None
    candidate = value["candidate"]
    if not exact(candidate, {"taskId", "lifecycleStatus", "blocker"}) or not isinstance(candidate["taskId"], str) or not TASK.fullmatch(candidate["taskId"]) or candidate["lifecycleStatus"] not in LIFECYCLE or candidate["blocker"] not in BLOCKERS:
        return None
    activity = value["activity"]
    activity_fields = {"state", "taskId", "phase", "role", "lastGate", "blocker", "autonomy", "parentClass"}
    if not exact(activity, activity_fields) or activity["state"] not in ACTIVITY or activity["taskId"] != candidate["taskId"] or activity["phase"] not in PHASES or activity["role"] not in ROLES or activity["lastGate"] not in GATES or activity["blocker"] not in BLOCKERS or activity["autonomy"] not in AUTONOMY or activity["parentClass"] not in PARENTS:
        return None
    leaf_oid = git_blob_oid(payload)
    commit_trailers = trailers(commit_record.get("message"))
    author = commit_record.get("author")
    committer = commit_record.get("committer")
    parents = commit_record.get("parents")
    if commit_record.get("sha") != commit or commit_trailers is None or not isinstance(author, dict) or not isinstance(committer, dict) or not isinstance(parents, list) or len(parents) != 1 or not isinstance(parents[0], dict):
        return None
    role = commit_trailers.get("Agent-Role")
    identity = ROLE_IDENTITIES.get(str(role))
    source_commit = commit_trailers.get("Source-Commit")
    source_tree = commit_trailers.get("Source-Tree")
    if identity is None or author.get("name") != identity[0] or author.get("email") != identity[1] or committer.get("name") != identity[0] or committer.get("email") != identity[1] or commit_trailers.get("Task-ID") != candidate["taskId"] or commit_trailers.get("Public-Status-Blob") != leaf_oid or raw.get("sha") != leaf_oid or not isinstance(source_commit, str) or not SHA.fullmatch(source_commit) or not isinstance(source_tree, str) or not SHA.fullmatch(source_tree) or parents[0].get("sha") != source_commit:
        return None
    try:
        source = client.get(f"git/commits/{source_commit}")
    except ObservationError:
        return None
    if not isinstance(source, dict) or not isinstance(source.get("tree"), dict) or source["tree"].get("sha") != source_tree:
        return None
    return value


def repository_matches(value: object) -> bool:
    return isinstance(value, dict) and str(value.get("id")) == REPOSITORY_ID and value.get("full_name") == REPOSITORY


def workflow_blob_matches(client: GitHub, commit: str) -> bool:
    value = client.get(f"contents/{quote(WORKFLOW_PATH, safe='/')}?ref={commit}")
    return isinstance(value, dict) and value.get("type") == "file" and value.get("path") == WORKFLOW_PATH and value.get("sha") == WORKFLOW_BLOB


def gate_for(client: GitHub, head_sha: str, base_sha: str, pull_number: int, current_main_sha: str) -> str:
    workflow = client.get(f"actions/workflows/{WORKFLOW_ID}")
    if not isinstance(workflow, dict) or str(workflow.get("id")) != WORKFLOW_ID or workflow.get("path") != WORKFLOW_PATH:
        return "unknown"
    if not workflow_blob_matches(client, current_main_sha) or not workflow_blob_matches(client, head_sha):
        return "unknown"
    value = client.get(f"commits/{head_sha}/check-runs?per_page=100")
    if not isinstance(value, dict) or not isinstance(value.get("check_runs"), list) or value.get("total_count") != len(value["check_runs"]):
        return "unknown"
    matches = [item for item in value["check_runs"] if isinstance(item, dict) and item.get("name") == "Repository gates" and item.get("head_sha") == head_sha and isinstance(item.get("app"), dict) and str(item["app"].get("id")) == "15368" and item["app"].get("slug") == "github-actions"]
    if len(matches) != 1:
        return "unknown"
    check = matches[0]
    check_id = check.get("id")
    check_suite = check.get("check_suite")
    if not isinstance(check_id, int) or isinstance(check_id, bool) or not isinstance(check_suite, dict) or not isinstance(check_suite.get("id"), int):
        return "unknown"
    runs = client.get(f"actions/runs?head_sha={head_sha}&event=pull_request&per_page=100")
    if not isinstance(runs, dict) or not isinstance(runs.get("workflow_runs"), list) or runs.get("total_count") != len(runs["workflow_runs"]):
        return "unknown"
    bindings: list[tuple[dict[str, object], dict[str, object]]] = []
    for run in runs["workflow_runs"]:
        if not isinstance(run, dict) or str(run.get("workflow_id")) != WORKFLOW_ID or run.get("path") != WORKFLOW_PATH or str(run.get("check_suite_id")) != str(check_suite["id"]) or run.get("event") != "pull_request" or run.get("head_sha") != head_sha or not repository_matches(run.get("repository")) or not isinstance(run.get("run_attempt"), int) or not isinstance(run.get("id"), int):
            continue
        pull_requests = run.get("pull_requests")
        if not isinstance(pull_requests, list) or len(pull_requests) != 1 or not isinstance(pull_requests[0], dict):
            continue
        run_pr = pull_requests[0]
        if run_pr.get("number") != pull_number or not isinstance(run_pr.get("base"), dict) or not isinstance(run_pr.get("head"), dict) or run_pr["base"].get("sha") != base_sha or run_pr["head"].get("sha") != head_sha:
            continue
        jobs = client.get(f"actions/runs/{run['id']}/attempts/{run['run_attempt']}/jobs?per_page=100")
        job_list = jobs.get("jobs") if isinstance(jobs, dict) else None
        if not isinstance(job_list, list) or jobs.get("total_count") != len(job_list):
            continue
        exact_jobs = [job for job in job_list if isinstance(job, dict) and job.get("id") == check_id]
        if len(exact_jobs) == 1:
            bindings.append((run, exact_jobs[0]))
    if len(bindings) != 1:
        return "unknown"
    run, job = bindings[0]
    suite = client.get(f"check-suites/{check_suite['id']}")
    exact_check = client.get(f"check-runs/{check_id}")
    app = {"id": "15368", "slug": "github-actions"}
    if not isinstance(suite, dict) or str(suite.get("id")) != str(check_suite["id"]) or suite.get("head_sha") != head_sha or not isinstance(suite.get("app"), dict) or str(suite["app"].get("id")) != app["id"] or suite["app"].get("slug") != app["slug"]:
        return "unknown"
    if not isinstance(exact_check, dict) or exact_check.get("id") != check_id or exact_check.get("name") != "Repository gates" or exact_check.get("head_sha") != head_sha or not isinstance(exact_check.get("check_suite"), dict) or exact_check["check_suite"].get("id") != check_suite["id"] or not isinstance(exact_check.get("app"), dict) or str(exact_check["app"].get("id")) != app["id"] or exact_check["app"].get("slug") != app["slug"]:
        return "unknown"
    if job.get("name") != "Repository gates" or job.get("head_sha") != head_sha or job.get("run_attempt") != run["run_attempt"]:
        return "unknown"
    states = (run.get("conclusion"), job.get("conclusion"), suite.get("conclusion"), exact_check.get("conclusion"))
    if check.get("status") != "completed" or exact_check.get("status") != "completed" or any(state is None for state in states):
        return "waiting"
    return "passed" if states == ("success", "success", "success", "success") else "failed" if any(state in {"failure", "cancelled", "timed_out", "action_required"} for state in states) else "unknown"


def main_gate(client: GitHub, main_sha: str, main_tree: str) -> str:
    pulls = client.get("pulls?state=closed&base=main&per_page=100&sort=updated&direction=desc")
    if not isinstance(pulls, list):
        raise ObservationError("invalid merged pull request response")
    matches: list[dict[str, object]] = []
    for pull in pulls:
        if not isinstance(pull, dict) or pull.get("state") != "closed" or not isinstance(pull.get("merged_at"), str) or pull.get("merge_commit_sha") != main_sha:
            continue
        base = pull.get("base")
        head = pull.get("head")
        if not isinstance(base, dict) or not isinstance(head, dict) or base.get("ref") != "main" or not isinstance(base.get("repo"), dict) or not isinstance(head.get("repo"), dict):
            continue
        if str(base["repo"].get("id")) != REPOSITORY_ID or str(head["repo"].get("id")) != REPOSITORY_ID:
            continue
        matches.append(pull)
    if len(matches) != 1:
        return "unknown"
    head_sha = matches[0]["head"].get("sha")
    if not isinstance(head_sha, str) or not SHA.fullmatch(head_sha):
        return "unknown"
    head_commit = client.get(f"git/commits/{head_sha}")
    head_tree = head_commit.get("tree", {}).get("sha") if isinstance(head_commit, dict) and isinstance(head_commit.get("tree"), dict) else None
    if head_tree != main_tree:
        return "unknown"
    base_sha = matches[0]["base"].get("sha")
    pull_number = matches[0].get("number")
    if not isinstance(base_sha, str) or not SHA.fullmatch(base_sha) or not isinstance(pull_number, int) or isinstance(pull_number, bool):
        return "unknown"
    gate = gate_for(client, head_sha, base_sha, pull_number, main_sha)
    return "passed" if gate == "passed" else "blocked" if gate == "failed" else "unknown"


def observe(client: GitHub, main_sha: str, main_tree: str, observed_at: str) -> dict[str, object]:
    repository = client.get("")
    if not isinstance(repository, dict) or str(repository.get("id")) != REPOSITORY_ID or repository.get("full_name") != REPOSITORY or repository.get("default_branch") != "main":
        raise ObservationError("repository identity mismatch")
    branch = client.get("branches/main")
    commit = branch.get("commit") if isinstance(branch, dict) else None
    if not isinstance(commit, dict) or commit.get("sha") != main_sha:
        raise ObservationError("main ref changed during observation")
    commit_value = client.get(f"git/commits/{main_sha}")
    if not isinstance(commit_value, dict) or not isinstance(commit_value.get("tree"), dict) or commit_value["tree"].get("sha") != main_tree:
        raise ObservationError("main tree mismatch")
    accepted_gate = main_gate(client, main_sha, main_tree)

    candidate_items: list[dict[str, object]] = []
    candidate_invalid = False
    pulls = client.get("pulls?state=open&base=main&per_page=100&sort=created&direction=asc")
    if not isinstance(pulls, list) or len(pulls) >= 100:
        raise ObservationError("invalid pull request response")
    for pull in pulls:
        if not isinstance(pull, dict) or not isinstance(pull.get("head"), dict) or not isinstance(pull.get("base"), dict):
            candidate_invalid = True
            continue
        head = pull["head"]
        base = pull["base"]
        if not isinstance(head.get("repo"), dict) or not isinstance(base.get("repo"), dict) or str(head["repo"].get("id")) != REPOSITORY_ID or str(base["repo"].get("id")) != REPOSITORY_ID or base.get("ref") != "main":
            candidate_invalid = True
            continue
        head_sha = head.get("sha")
        base_sha = base.get("sha")
        number = pull.get("number")
        if not isinstance(number, int) or isinstance(number, bool) or number < 1 or not isinstance(head_sha, str) or not SHA.fullmatch(head_sha) or not isinstance(base_sha, str) or not SHA.fullmatch(base_sha):
            candidate_invalid = True
            continue
        head_commit = client.get(f"git/commits/{head_sha}")
        head_tree = head_commit.get("tree", {}).get("sha") if isinstance(head_commit, dict) and isinstance(head_commit.get("tree"), dict) else None
        if not isinstance(head_tree, str) or not SHA.fullmatch(head_tree):
            candidate_invalid = True
            continue
        sidecar = read_sidecar(client, head_sha, head_commit)
        if sidecar is None:
            candidate_invalid = True
            continue
        current_pull = client.get(f"pulls/{number}")
        current_head = current_pull.get("head") if isinstance(current_pull, dict) else None
        current_base = current_pull.get("base") if isinstance(current_pull, dict) else None
        if not isinstance(current_pull, dict) or current_pull.get("state") != "open" or not isinstance(current_head, dict) or current_head.get("sha") != head_sha or not isinstance(current_head.get("repo"), dict) or str(current_head["repo"].get("id")) != REPOSITORY_ID or not isinstance(current_base, dict) or current_base.get("ref") != "main" or current_base.get("sha") != base_sha or not isinstance(current_base.get("repo"), dict) or str(current_base["repo"].get("id")) != REPOSITORY_ID:
            candidate_invalid = True
            continue
        candidate_items.append({**sidecar["candidate"], "gate": gate_for(client, head_sha, base_sha, number, main_sha)})
    candidate_items.sort(key=lambda item: str(item["taskId"]))
    if len({item["taskId"] for item in candidate_items}) != len(candidate_items):
        candidate_invalid = True
        candidate_items = []
    candidates = {"state": "observed", "items": candidate_items} if candidate_items and not candidate_invalid else {"state": "unavailable", "items": []} if pulls else {"state": "not-observed", "items": []}

    continuity: dict[str, object] = {"state": "not-observed", "classification": "continuity-not-accepted-progress"}
    activity: dict[str, object] = {"state": "unknown"}
    try:
        wip_ref = client.get("git/ref/heads/autopilot/live-wip")
        wip_sha = wip_ref.get("object", {}).get("sha") if isinstance(wip_ref, dict) and isinstance(wip_ref.get("object"), dict) else None
        if not isinstance(wip_sha, str) or not SHA.fullmatch(wip_sha):
            raise ObservationError("invalid WIP ref")
        wip_commit = client.get(f"git/commits/{wip_sha}")
        wip_tree = wip_commit.get("tree", {}).get("sha") if isinstance(wip_commit, dict) and isinstance(wip_commit.get("tree"), dict) else None
        public_time = wip_commit.get("committer", {}).get("date") if isinstance(wip_commit, dict) and isinstance(wip_commit.get("committer"), dict) else None
        if not isinstance(wip_tree, str) or not SHA.fullmatch(wip_tree) or not isinstance(public_time, str):
            raise ObservationError("invalid WIP commit")
        sidecar = read_sidecar(client, wip_sha, wip_commit)
        age = (timestamp(observed_at) - timestamp(public_time)).total_seconds()
        current_wip_ref = client.get("git/ref/heads/autopilot/live-wip")
        current_wip_sha = current_wip_ref.get("object", {}).get("sha") if isinstance(current_wip_ref, dict) and isinstance(current_wip_ref.get("object"), dict) else None
        if current_wip_sha != wip_sha:
            raise ObservationError("WIP ref changed during observation")
        if age < 0 or sidecar is None:
            continuity = {"state": "unavailable", "classification": "continuity-not-accepted-progress"}
        elif age <= 1800:
            continuity = {"state": "published", "classification": "continuity-not-accepted-progress", "commit": wip_sha, "committedAt": public_time}
            activity = dict(sidecar["activity"])
        else:
            continuity = {"state": "stale", "classification": "continuity-not-accepted-progress"}
            activity = {"state": "unknown"}
    except ObservationError:
        continuity = {"state": "unavailable", "classification": "continuity-not-accepted-progress"}
        activity = {"state": "unknown"}

    return {"schemaVersion": 1, "repository": {"id": REPOSITORY_ID, "name": REPOSITORY}, "main": {"commit": main_sha, "tree": main_tree, "gates": accepted_gate}, "candidates": candidates, "continuity": continuity, "activity": activity}


def atomic_json(path: Path, value: object) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_name(f".{path.name}.{os.getpid()}.tmp")
    temporary.write_text(json.dumps(value, ensure_ascii=False, indent=2, sort_keys=True) + "\n", encoding="utf-8", newline="\n")
    os.replace(temporary, path)


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--main-sha", required=True)
    parser.add_argument("--main-tree", required=True)
    parser.add_argument("--observed-at", required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    try:
        if not SHA.fullmatch(args.main_sha) or not SHA.fullmatch(args.main_tree):
            raise ObservationError("invalid trusted main identity")
        observed_at = timestamp(args.observed_at).isoformat(timespec="seconds").replace("+00:00", "Z")
        atomic_json(args.output.resolve(), observe(GitHub(os.environ.get("GITHUB_TOKEN")), args.main_sha, args.main_tree, observed_at))
    except (OSError, ValueError, json.JSONDecodeError, ObservationError) as exc:
        print(f"GitHub-Beobachtung abgelehnt: {exc}", file=sys.stderr)
        return 2
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
