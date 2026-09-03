#!/usr/bin/env python3
"""Validate immutable public promotion receipts without trusting check names alone."""

from __future__ import annotations

import argparse
import base64
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import sys
from urllib.error import HTTPError, URLError
from urllib.parse import quote
from urllib.request import Request, urlopen


SHA = re.compile(r"^[0-9a-f]{40}$")
DECIMAL_ID = re.compile(r"^[1-9][0-9]{0,19}$")
TASK = re.compile(r"^T-[0-9]{3}$")
EXPECTED_TASKS = {"T-034", "T-035", "T-036", "T-037", "T-038", "T-039", "T-052"}
REPOSITORY = {"id": "1333151301", "name": "Koschnag/ai-fantasy-rts-rpg"}
WORKFLOW = {"id": "333602498", "path": ".github/workflows/verify.yml", "trustedBlobOid": "43f93d02014752b6162a923215ff7634905db165"}
CHECK = {"appId": "15368", "appSlug": "github-actions", "name": "Repository gates"}
EXPECTED_HISTORICAL_SHA256 = "2486631c7448933fe24294638dae55c5de781623345a6d28172ea2549e222a12"
HISTORICAL_FIELDS = ("repository", "workflow", "check", "receipts", "disclosures")
RECONCILIATION_PATH = "docs/showcase/reconciliation.json"
AUDIT_PATH = ".ai/audits/T-054-historical-reconciliation-audit.json"
AUDIT_BLOB_PATHS = (
    "scripts/validate_reconciliation.py",
    "docs/showcase/reconciliation.schema.json",
    "docs/showcase/reconciliation.json",
    "scripts/pages_status.py",
    "scripts/test-reconciliation.py",
)
AUDIT_TASKS = ["T-034", "T-035", "T-036", "T-037", "T-038", "T-039", "T-052"]
BUILDER = {"name": "Riftward Builder Autopilot", "email": "riftward-builder-autopilot@users.noreply.github.com"}
REVIEWER = {"name": "Riftward Reviewer Autopilot", "email": "riftward-reviewer-autopilot@users.noreply.github.com"}
API_ROOT = f"https://api.github.com/repos/{REPOSITORY['name']}"
MAX_RESPONSE_BYTES = 1_000_000


class ReconciliationError(RuntimeError):
    pass


def reject_duplicate_keys(pairs: list[tuple[str, object]]) -> dict[str, object]:
    result: dict[str, object] = {}
    for key, value in pairs:
        if key in result:
            raise ReconciliationError(f"duplicate JSON key: {key}")
        result[key] = value
    return result


def load_json(path: Path) -> object:
    return json.loads(path.read_text(encoding="utf-8"), object_pairs_hook=reject_duplicate_keys)


class GitHub:
    """Bounded read-only REST client for independently repeated evidence checks."""

    def __init__(self, token: str):
        if not token:
            raise ReconciliationError("GITHUB_TOKEN is required for live validation")
        self.token = token
        self.cache: dict[str, object] = {}

    def get(self, endpoint: str) -> object:
        if endpoint in self.cache:
            return self.cache[endpoint]
        request = Request(
            API_ROOT if endpoint == "" else f"{API_ROOT}/{endpoint}",
            headers={
                "Accept": "application/vnd.github+json",
                "Authorization": f"Bearer {self.token}",
                "User-Agent": "Riftward-Reconciliation/1",
                "X-GitHub-Api-Version": "2022-11-28",
            },
        )
        try:
            with urlopen(request, timeout=15) as response:
                payload = response.read(MAX_RESPONSE_BYTES + 1)
        except (HTTPError, URLError, TimeoutError) as exc:
            raise ReconciliationError("GitHub reconciliation API unavailable") from exc
        if len(payload) > MAX_RESPONSE_BYTES:
            raise ReconciliationError("GitHub reconciliation response exceeds limit")
        try:
            value = json.loads(payload.decode("utf-8"), object_pairs_hook=reject_duplicate_keys)
        except (UnicodeDecodeError, json.JSONDecodeError) as exc:
            raise ReconciliationError("invalid GitHub reconciliation response") from exc
        self.cache[endpoint] = value
        return value


def exact(value: object, keys: set[str], label: str) -> dict[str, object]:
    if not isinstance(value, dict) or set(value) != keys:
        raise ReconciliationError(f"{label} has an invalid field set")
    return value


def git(root: Path, *args: str) -> str:
    result = subprocess.run(["git", "-C", str(root), *args], text=True, encoding="utf-8", stdout=subprocess.PIPE, stderr=subprocess.PIPE, check=False)
    if result.returncode != 0:
        raise ReconciliationError(f"git {' '.join(args)} failed")
    return result.stdout.strip()


def blob_at(root: Path, commit: str, path: str) -> str:
    line = git(root, "ls-tree", commit, "--", path)
    parts = line.split()
    if len(parts) < 4 or parts[1] != "blob":
        raise ReconciliationError(f"missing required blob at {path}")
    return parts[2]


def record(value: object, label: str) -> dict[str, object]:
    if not isinstance(value, dict):
        raise ReconciliationError(f"invalid {label} response")
    return value


def repository_matches(value: object) -> bool:
    return isinstance(value, dict) and str(value.get("id")) == REPOSITORY["id"] and value.get("full_name") == REPOSITORY["name"]


def app_matches(value: object) -> bool:
    return isinstance(value, dict) and str(value.get("id")) == CHECK["appId"] and value.get("slug") == CHECK["appSlug"]


def content_oid(client: object, commit: str, path: str) -> str:
    endpoint = f"contents/{quote(path, safe='/')}?ref={commit}"
    value = record(client.get(endpoint), "content")
    if value.get("type") != "file" or value.get("path") != path or not isinstance(value.get("sha"), str) or not SHA.fullmatch(str(value["sha"])):
        raise ReconciliationError("GitHub content identity mismatch")
    return str(value["sha"])


def content_json(client: object, commit: str, path: str, expected_oid: str) -> object:
    endpoint = f"contents/{quote(path, safe='/')}?ref={commit}"
    value = record(client.get(endpoint), "JSON content")
    if value.get("type") != "file" or value.get("path") != path or value.get("sha") != expected_oid or value.get("encoding") != "base64" or not isinstance(value.get("content"), str) or not isinstance(value.get("size"), int) or value["size"] > 65_536:
        raise ReconciliationError("GitHub audit content identity mismatch")
    try:
        payload = base64.b64decode("".join(value["content"].split()), validate=True)
        if len(payload) != value["size"]:
            raise ReconciliationError("GitHub audit content size mismatch")
        return json.loads(payload.decode("utf-8"), object_pairs_hook=reject_duplicate_keys)
    except (ValueError, UnicodeDecodeError, json.JSONDecodeError) as exc:
        raise ReconciliationError("invalid GitHub audit evidence JSON") from exc


def projection_digest(manifest: dict[str, object]) -> str:
    projection = {field: manifest[field] for field in HISTORICAL_FIELDS}
    canonical = json.dumps(projection, ensure_ascii=True, separators=(",", ":"), sort_keys=True).encode("utf-8")
    return hashlib.sha256(canonical).hexdigest()


def commit_trailers(message: object) -> dict[str, str]:
    if not isinstance(message, str):
        raise ReconciliationError("commit message missing")
    result: dict[str, str] = {}
    for line in message.splitlines():
        match = re.fullmatch(r"([A-Za-z][A-Za-z-]*): (.+)", line)
        if match:
            if match.group(1) in result:
                raise ReconciliationError("duplicate commit trailer")
            result[match.group(1)] = match.group(2)
    return result


def commit_identity(commit: dict[str, object], expected: dict[str, str]) -> bool:
    author = commit.get("author")
    committer = commit.get("committer")
    return isinstance(author, dict) and isinstance(committer, dict) and author.get("name") == expected["name"] and author.get("email") == expected["email"] and committer.get("name") == expected["name"] and committer.get("email") == expected["email"]


def api_commit(client: object, sha: str) -> dict[str, object]:
    value = record(client.get(f"git/commits/{sha}"), "Git commit")
    if value.get("sha") != sha or not isinstance(value.get("tree"), dict) or not SHA.fullmatch(str(value["tree"].get("sha", ""))) or not isinstance(value.get("parents"), list):
        raise ReconciliationError("Git commit identity mismatch")
    return value


def parent_shas(commit: dict[str, object]) -> list[str]:
    parents = commit.get("parents")
    if not isinstance(parents, list) or any(not isinstance(item, dict) or not isinstance(item.get("sha"), str) for item in parents):
        raise ReconciliationError("Git commit parent relation malformed")
    return [str(item["sha"]) for item in parents]


def validate_live(manifest: object, client: object) -> None:
    top = record(manifest, "manifest")
    repository = record(client.get(""), "repository")
    if not repository_matches(repository) or repository.get("default_branch") != "main":
        raise ReconciliationError("live repository identity mismatch")
    workflow = record(client.get(f"actions/workflows/{WORKFLOW['id']}"), "workflow")
    if str(workflow.get("id")) != WORKFLOW["id"] or workflow.get("path") != WORKFLOW["path"]:
        raise ReconciliationError("live workflow identity mismatch")

    receipts = top.get("receipts")
    if not isinstance(receipts, list):
        raise ReconciliationError("live receipt list missing")
    for raw in receipts:
        item = record(raw, "receipt")
        task_id = str(item["taskId"])
        pr = record(client.get(f"pulls/{item['pullRequestNumber']}"), "pull request")
        base = record(pr.get("base"), "pull request base")
        head = record(pr.get("head"), "pull request head")
        if (
            pr.get("number") != item["pullRequestNumber"]
            or pr.get("merged") is not True
            or pr.get("state") != "closed"
            or pr.get("merge_commit_sha") != item["resultSha"]
            or not repository_matches(base.get("repo"))
            or not repository_matches(head.get("repo"))
            or base.get("ref") != "main"
            or base.get("sha") != item["baseSha"]
            or head.get("sha") != item["headSha"]
        ):
            raise ReconciliationError(f"{task_id} live pull request relation mismatch")

        run = record(client.get(f"actions/runs/{item['runId']}"), "workflow run")
        run_prs = run.get("pull_requests")
        if (
            str(run.get("id")) != item["runId"]
            or str(run.get("workflow_id")) != WORKFLOW["id"]
            or str(run.get("check_suite_id")) != item["checkSuiteId"]
            or run.get("path") != WORKFLOW["path"]
            or run.get("run_attempt") != item["runAttempt"]
            or run.get("event") != item["event"]
            or run.get("head_sha") != item["headSha"]
            or run.get("conclusion") != item["outcome"]
            or not repository_matches(run.get("repository"))
            or not isinstance(run_prs, list)
            or len(run_prs) > 1
        ):
            raise ReconciliationError(f"{task_id} live workflow run mismatch")
        # GitHub may later return an empty pull_requests array for historical
        # Actions runs. The immutable suite relation plus the exact PR, head,
        # base, workflow and event remain binding. If the optional relation is
        # present, validate it strictly rather than treating it as authority.
        if run_prs:
            run_pr = record(run_prs[0], "workflow run pull request")
            run_base = record(run_pr.get("base"), "workflow run base")
            run_head = record(run_pr.get("head"), "workflow run head")
            if run_pr.get("number") != item["pullRequestNumber"] or run_base.get("sha") != item["baseSha"] or run_head.get("sha") != item["headSha"]:
                raise ReconciliationError(f"{task_id} live run/PR relation mismatch")

        jobs = record(client.get(f"actions/runs/{item['runId']}/attempts/{item['runAttempt']}/jobs?per_page=100"), "attempt jobs")
        job_list = jobs.get("jobs")
        if not isinstance(job_list, list) or jobs.get("total_count") != len(job_list):
            raise ReconciliationError(f"{task_id} invalid attempt jobs response")
        matching_jobs = [job for job in job_list if isinstance(job, dict) and str(job.get("id")) == item["checkRunId"]]
        if len(matching_jobs) != 1:
            raise ReconciliationError(f"{task_id} exact attempt job missing")
        job = matching_jobs[0]
        if job.get("name") != CHECK["name"] or job.get("head_sha") != item["headSha"] or job.get("run_attempt") != item["runAttempt"] or job.get("conclusion") != item["outcome"]:
            raise ReconciliationError(f"{task_id} attempt job relation mismatch")

        suite = record(client.get(f"check-suites/{item['checkSuiteId']}"), "check suite")
        if str(suite.get("id")) != item["checkSuiteId"] or suite.get("head_sha") != item["headSha"] or suite.get("conclusion") != item["outcome"] or not app_matches(suite.get("app")):
            raise ReconciliationError(f"{task_id} check suite relation mismatch")
        check_run = record(client.get(f"check-runs/{item['checkRunId']}"), "check run")
        check_suite = record(check_run.get("check_suite"), "check run suite")
        if (
            str(check_run.get("id")) != item["checkRunId"]
            or check_run.get("name") != CHECK["name"]
            or check_run.get("head_sha") != item["headSha"]
            or check_run.get("status") != "completed"
            or check_run.get("conclusion") != item["outcome"]
            or str(check_suite.get("id")) != item["checkSuiteId"]
            or not app_matches(check_run.get("app"))
        ):
            raise ReconciliationError(f"{task_id} check run relation mismatch")

        for commit in (str(item["baseSha"]), str(item["headSha"]), str(item["resultSha"])):
            if content_oid(client, commit, WORKFLOW["path"]) != WORKFLOW["trustedBlobOid"]:
                raise ReconciliationError(f"{task_id} live workflow blob mismatch")
        if content_oid(client, str(item["resultSha"]), str(item["taskManifestPath"])) != item["taskManifestBlobOid"]:
            raise ReconciliationError(f"{task_id} live task blob mismatch")
        if content_oid(client, str(item["resultSha"]), str(item["reviewEvidencePath"])) != item["reviewEvidenceBlobOid"]:
            raise ReconciliationError(f"{task_id} live review blob mismatch")


def validate(manifest: object, root: Path | None) -> dict[str, object]:
    top = exact(manifest, {"schemaVersion", "contract", "repository", "workflow", "check", "receipts", "disclosures", "historicalProjectionSha256", "audit"}, "manifest")
    if top["schemaVersion"] != 2 or top["contract"] != "riftward-promotion-reconciliation-v2":
        raise ReconciliationError("unsupported reconciliation contract")
    digest = projection_digest(top)
    if digest != EXPECTED_HISTORICAL_SHA256 or top["historicalProjectionSha256"] != digest:
        raise ReconciliationError("immutable historical projection differs from the reviewed receipts")
    if top["repository"] != REPOSITORY or top["workflow"] != WORKFLOW or top["check"] != CHECK:
        raise ReconciliationError("trusted repository/workflow/check identity mismatch")
    receipts = top["receipts"]
    if not isinstance(receipts, list) or len(receipts) != 7:
        raise ReconciliationError("exactly seven receipts are required")
    receipt_keys = {
        "taskId", "pullRequestNumber", "baseRepository", "headRepository", "baseRef", "baseSha", "headSha", "mergeSha", "resultSha", "resultTree",
        "runId", "runAttempt", "event", "checkSuiteId", "checkRunId", "outcome", "taskManifestPath", "taskManifestBlobOid", "reviewEvidencePath",
        "reviewEvidenceBlobOid", "reviewEvidenceClass", "roleSeparation"
    }
    seen: set[str] = set()
    for raw in receipts:
        item = exact(raw, receipt_keys, "receipt")
        task_id = item["taskId"]
        if not isinstance(task_id, str) or not TASK.fullmatch(task_id) or task_id in seen:
            raise ReconciliationError("invalid or duplicate task receipt")
        seen.add(task_id)
        if item["baseRepository"] != REPOSITORY or item["headRepository"] != REPOSITORY:
            raise ReconciliationError(f"{task_id} repository relation mismatch")
        if item["baseRef"] != "main" or item["event"] != "pull_request" or item["runAttempt"] != 1 or item["outcome"] != "success":
            raise ReconciliationError(f"{task_id} event/outcome relation mismatch")
        for name in ("baseSha", "headSha", "mergeSha", "resultSha", "resultTree", "taskManifestBlobOid", "reviewEvidenceBlobOid"):
            if not isinstance(item[name], str) or not SHA.fullmatch(item[name]):
                raise ReconciliationError(f"{task_id} invalid {name}")
        for name in ("runId", "checkSuiteId", "checkRunId"):
            if not isinstance(item[name], str) or not DECIMAL_ID.fullmatch(item[name]):
                raise ReconciliationError(f"{task_id} invalid {name}")
        if item["mergeSha"] != item["resultSha"]:
            raise ReconciliationError(f"{task_id} merge/result mismatch")
        if re.fullmatch(rf"\.ai/tasks/{re.escape(task_id)}-[a-z0-9-]+\.json", str(item["taskManifestPath"])) is None:
            raise ReconciliationError(f"{task_id} task path mismatch")
        if not str(item["reviewEvidencePath"]).startswith(f"docs/abnahme/{task_id}-") or not str(item["reviewEvidencePath"]).endswith(".md"):
            raise ReconciliationError(f"{task_id} review path mismatch")
        if item["reviewEvidenceClass"] != "retrospective-public-record" or item["roleSeparation"] != "not-publicly-proven":
            raise ReconciliationError(f"{task_id} overstates historical review provenance")
        if root is not None:
            commit_line = git(root, "cat-file", "-p", str(item["resultSha"]))
            tree_lines = [line[5:] for line in commit_line.splitlines() if line.startswith("tree ")]
            parent_lines = [line[7:] for line in commit_line.splitlines() if line.startswith("parent ")]
            if tree_lines != [item["resultTree"]] or parent_lines != [item["baseSha"]]:
                raise ReconciliationError(f"{task_id} result tree/parent mismatch")
            if blob_at(root, str(item["resultSha"]), str(item["taskManifestPath"])) != item["taskManifestBlobOid"]:
                raise ReconciliationError(f"{task_id} task blob mismatch")
            if blob_at(root, str(item["resultSha"]), str(item["reviewEvidencePath"])) != item["reviewEvidenceBlobOid"]:
                raise ReconciliationError(f"{task_id} review blob mismatch")
            for commit in (str(item["baseSha"]), str(item["headSha"]), str(item["resultSha"])):
                if blob_at(root, commit, WORKFLOW["path"]) != WORKFLOW["trustedBlobOid"]:
                    raise ReconciliationError(f"{task_id} trusted workflow blob mismatch")
            relation = subprocess.run(["git", "-C", str(root), "merge-base", "--is-ancestor", str(item["resultSha"]), "HEAD"], check=False)
            if relation.returncode != 0:
                raise ReconciliationError(f"{task_id} result is not accepted on HEAD")
    if seen != EXPECTED_TASKS:
        raise ReconciliationError("reconciled task set mismatch")
    disclosures = top["disclosures"]
    if disclosures != [{"outcome": "failure", "relation": "subsequent-main-push-not-promotion-evidence", "runId": "33283226720", "taskId": "T-037"}]:
        raise ReconciliationError("required subsequent T-037 failure disclosure is missing")
    audit = top["audit"]
    if not isinstance(audit, dict) or audit.get("state") not in {"pending", "passed"}:
        raise ReconciliationError("invalid retrospective audit state")
    if audit["state"] == "pending":
        if audit != {"state": "pending", "doneEligibleTaskIds": []}:
            raise ReconciliationError("pending audit must have no DONE eligibility")
        eligible: list[str] = []
    else:
        if set(audit) != {"state", "pullRequestNumber", "evidencePath", "evidenceBlobOid", "doneEligibleTaskIds"} or not isinstance(audit["pullRequestNumber"], int) or isinstance(audit["pullRequestNumber"], bool) or audit["pullRequestNumber"] < 1 or audit["evidencePath"] != AUDIT_PATH or not isinstance(audit["evidenceBlobOid"], str) or not SHA.fullmatch(audit["evidenceBlobOid"]) or audit["doneEligibleTaskIds"] != AUDIT_TASKS:
            raise ReconciliationError("invalid passed retrospective audit")
        eligible = list(AUDIT_TASKS)
    return {"recordedTaskIds": sorted(seen), "doneEligibleTaskIds": eligible, "auditState": audit["state"], "historicalProjectionSha256": digest, "manifestAudit": audit}


def validate_review_gate(client: object, head_sha: str, base_sha: str, pull_number: int) -> None:
    checks_response = record(client.get(f"commits/{head_sha}/check-runs?per_page=100"), "audit checks")
    checks = checks_response.get("check_runs")
    if not isinstance(checks, list) or checks_response.get("total_count") != len(checks):
        raise ReconciliationError("invalid audit checks response")
    matches = [item for item in checks if isinstance(item, dict) and item.get("name") == CHECK["name"] and item.get("head_sha") == head_sha and app_matches(item.get("app"))]
    if len(matches) != 1 or not isinstance(matches[0].get("id"), int) or not isinstance(matches[0].get("check_suite"), dict) or not isinstance(matches[0]["check_suite"].get("id"), int):
        raise ReconciliationError("exact audit check is missing or ambiguous")
    check_id = matches[0]["id"]
    suite_id = matches[0]["check_suite"]["id"]
    runs_response = record(client.get(f"actions/runs?head_sha={head_sha}&event=pull_request&per_page=100"), "audit runs")
    runs = runs_response.get("workflow_runs")
    if not isinstance(runs, list) or runs_response.get("total_count") != len(runs):
        raise ReconciliationError("invalid audit runs response")
    bindings: list[tuple[dict[str, object], dict[str, object]]] = []
    for run in runs:
        if not isinstance(run, dict) or str(run.get("workflow_id")) != WORKFLOW["id"] or run.get("path") != WORKFLOW["path"] or str(run.get("check_suite_id")) != str(suite_id) or run.get("event") != "pull_request" or run.get("head_sha") != head_sha or not repository_matches(run.get("repository")) or not isinstance(run.get("id"), int) or not isinstance(run.get("run_attempt"), int):
            continue
        prs = run.get("pull_requests")
        if not isinstance(prs, list) or len(prs) != 1 or not isinstance(prs[0], dict) or prs[0].get("number") != pull_number or not isinstance(prs[0].get("base"), dict) or not isinstance(prs[0].get("head"), dict) or prs[0]["base"].get("sha") != base_sha or prs[0]["head"].get("sha") != head_sha:
            continue
        jobs_response = record(client.get(f"actions/runs/{run['id']}/attempts/{run['run_attempt']}/jobs?per_page=100"), "audit jobs")
        jobs = jobs_response.get("jobs")
        if not isinstance(jobs, list) or jobs_response.get("total_count") != len(jobs):
            continue
        exact_jobs = [job for job in jobs if isinstance(job, dict) and job.get("id") == check_id]
        if len(exact_jobs) == 1:
            bindings.append((run, exact_jobs[0]))
    if len(bindings) != 1:
        raise ReconciliationError("audit workflow/attempt/job binding mismatch")
    run, job = bindings[0]
    suite = record(client.get(f"check-suites/{suite_id}"), "audit suite")
    check = record(client.get(f"check-runs/{check_id}"), "audit check")
    if job.get("name") != CHECK["name"] or job.get("head_sha") != head_sha or job.get("run_attempt") != run["run_attempt"] or job.get("conclusion") != "success":
        raise ReconciliationError("audit job did not pass")
    if str(suite.get("id")) != str(suite_id) or suite.get("head_sha") != head_sha or suite.get("conclusion") != "success" or not app_matches(suite.get("app")):
        raise ReconciliationError("audit suite did not pass")
    check_suite = check.get("check_suite")
    if str(check.get("id")) != str(check_id) or check.get("name") != CHECK["name"] or check.get("head_sha") != head_sha or check.get("status") != "completed" or check.get("conclusion") != "success" or not isinstance(check_suite, dict) or str(check_suite.get("id")) != str(suite_id) or not app_matches(check.get("app")):
        raise ReconciliationError("audit check did not pass")


def allowed_review_path(path: object) -> bool:
    if not isinstance(path, str):
        return False
    exact_paths = {
        AUDIT_PATH,
        RECONCILIATION_PATH,
        "BACKLOG.md",
        ".ai/tasks/T-054-public-project-cockpit.json",
        "docs/abnahme/T-054-public-project-cockpit.md",
        "docs/showcase/status.schema.json",
        "docs/showcase/README.md",
        "docs/communication/SHOWCASE_SYSTEM.md",
        ".ai/public-status-v3.json",
    }
    return path in exact_paths or re.fullmatch(r"\.ai/tasks/T-(034|035|036|037|038|039|052)-[a-z0-9-]+\.json", path) is not None


def validate_audit(manifest: dict[str, object], client: object, main_sha: str, main_tree: str, reconciliation_oid: str) -> str:
    audit = record(manifest["audit"], "audit")
    if audit["state"] == "pending":
        return "none"
    current_main = api_commit(client, main_sha)
    if current_main["tree"].get("sha") != main_tree:
        raise ReconciliationError("audit current main tree mismatch")
    pr = record(client.get(f"pulls/{audit['pullRequestNumber']}"), "audit pull request")
    base = record(pr.get("base"), "audit base")
    head = record(pr.get("head"), "audit head")
    if pr.get("number") != audit["pullRequestNumber"] or pr.get("state") != "closed" or pr.get("merged") is not True or not repository_matches(base.get("repo")) or not repository_matches(head.get("repo")) or base.get("ref") != "main":
        raise ReconciliationError("audit pull request relation mismatch")
    parent_sha, review_sha, result_sha = base.get("sha"), head.get("sha"), pr.get("merge_commit_sha")
    if not all(isinstance(value, str) and SHA.fullmatch(value) for value in (parent_sha, review_sha, result_sha)):
        raise ReconciliationError("audit commit relation is malformed")
    if result_sha == review_sha or result_sha == parent_sha:
        raise ReconciliationError("audit result is not a distinct squash result")

    evidence = content_json(client, review_sha, str(audit["evidencePath"]), str(audit["evidenceBlobOid"]))
    evidence_keys = {"schemaVersion", "contract", "reviewedBuilderCommit", "reviewedBuilderTree", "historicalProjectionSha256", "builderTreeBlobOids", "coveredTaskIds", "criteria", "historicalRoleSeparation", "currentAuditSeparation", "identityAssurance"}
    evidence = exact(evidence, evidence_keys, "audit evidence")
    if evidence["schemaVersion"] != 1 or evidence["contract"] != "riftward-historical-reconciliation-audit-v1" or evidence["historicalProjectionSha256"] != EXPECTED_HISTORICAL_SHA256 or evidence["coveredTaskIds"] != AUDIT_TASKS or evidence["criteria"] != "PASS" or evidence["historicalRoleSeparation"] != "not-publicly-proven" or evidence["currentAuditSeparation"] != "builder-separated-agent-audit" or evidence["identityAssurance"] != "role-declaration-not-personhood-proof":
        raise ReconciliationError("audit evidence claim mismatch")
    builder_sha, builder_tree = evidence["reviewedBuilderCommit"], evidence["reviewedBuilderTree"]
    if not isinstance(builder_sha, str) or not SHA.fullmatch(builder_sha) or not isinstance(builder_tree, str) or not SHA.fullmatch(builder_tree):
        raise ReconciliationError("audit builder identity malformed")
    blob_oids = exact(evidence["builderTreeBlobOids"], set(AUDIT_BLOB_PATHS), "builder-tree blobs")
    if any(not isinstance(value, str) or not SHA.fullmatch(value) for value in blob_oids.values()):
        raise ReconciliationError("audit builder blob is malformed")

    parent = api_commit(client, parent_sha)
    builder = api_commit(client, builder_sha)
    reviewer = api_commit(client, review_sha)
    result = api_commit(client, result_sha)
    parent_tree = str(parent["tree"]["sha"])
    reviewer_tree = str(reviewer["tree"]["sha"])
    if builder["tree"].get("sha") != builder_tree or parent_shas(builder) != [parent_sha] or parent_shas(reviewer) != [builder_sha] or parent_shas(result) != [parent_sha] or result["tree"].get("sha") != reviewer_tree:
        raise ReconciliationError("audit parent/tree/squash relation mismatch")
    if not commit_identity(builder, BUILDER) or not commit_identity(reviewer, REVIEWER):
        raise ReconciliationError("audit role identity mismatch")
    builder_trailers = commit_trailers(builder.get("message"))
    reviewer_trailers = commit_trailers(reviewer.get("message"))
    expected_builder = {"Agent-Role": "builder", "Task-ID": "T-054", "Source-Commit": parent_sha, "Source-Tree": parent_tree}
    expected_reviewer = {"Agent-Role": "reviewer", "Task-ID": "T-054", "Source-Commit": builder_sha, "Source-Tree": builder_tree, "Independent-Review": "PASS", "Reviewed-Commit": builder_sha, "Reviewed-Tree": builder_tree}
    if any(builder_trailers.get(key) != value for key, value in expected_builder.items()) or any(reviewer_trailers.get(key) != value for key, value in expected_reviewer.items()):
        raise ReconciliationError("audit role trailers mismatch")

    comparison = record(client.get(f"compare/{builder_sha}...{review_sha}"), "audit review delta")
    files = comparison.get("files")
    commits = comparison.get("commits")
    if comparison.get("total_commits") != 1 or not isinstance(commits, list) or len(commits) != 1 or not isinstance(commits[0], dict) or commits[0].get("sha") != review_sha or not isinstance(files, list):
        raise ReconciliationError("audit review delta is not a direct review commit")
    changed: set[str] = set()
    for item in files:
        if not isinstance(item, dict) or not allowed_review_path(item.get("filename")) or item.get("status") not in {"added", "modified"} or "previous_filename" in item:
            raise ReconciliationError("audit review delta exceeds allowlist")
        changed.add(str(item["filename"]))
    if not {AUDIT_PATH, RECONCILIATION_PATH}.issubset(changed):
        raise ReconciliationError("audit review delta lacks required evidence")

    for commit in (parent_sha, builder_sha, review_sha, result_sha):
        if content_oid(client, commit, WORKFLOW["path"]) != WORKFLOW["trustedBlobOid"]:
            raise ReconciliationError("audit trusted workflow blob mismatch")
    for path, oid in blob_oids.items():
        if content_oid(client, builder_sha, path) != oid:
            raise ReconciliationError("audit builder-tree blob mismatch")
    for commit in (review_sha, result_sha, main_sha):
        if content_oid(client, commit, AUDIT_PATH) != audit["evidenceBlobOid"] or content_oid(client, commit, RECONCILIATION_PATH) != reconciliation_oid:
            raise ReconciliationError("audit evidence was not retained")
    ancestry = record(client.get(f"compare/{result_sha}...{main_sha}"), "audit main ancestry")
    merge_base = ancestry.get("merge_base_commit")
    if ancestry.get("status") not in {"ahead", "identical"} or not isinstance(merge_base, dict) or merge_base.get("sha") != result_sha:
        raise ReconciliationError("audit squash result is not on current main")
    validate_review_gate(client, review_sha, parent_sha, int(audit["pullRequestNumber"]))
    return str(audit["evidenceBlobOid"])


def atomic_json(path: Path, value: object) -> None:
    payload = json.dumps(value, ensure_ascii=True, indent=2, sort_keys=True) + "\n"
    temporary = path.with_name(f".{path.name}.{os.getpid()}.tmp")
    temporary.write_text(payload, encoding="utf-8", newline="\n")
    os.replace(temporary, path)


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--manifest", type=Path, required=True)
    parser.add_argument("--root", type=Path)
    parser.add_argument("--print-task-ids", action="store_true")
    parser.add_argument("--live-github", action="store_true")
    parser.add_argument("--main-sha")
    parser.add_argument("--main-tree")
    parser.add_argument("--verdict-out", type=Path)
    args = parser.parse_args()
    try:
        manifest = load_json(args.manifest.resolve(strict=True))
        root = args.root.resolve(strict=True) if args.root else None
        validation = validate(manifest, root)
        if args.live_github:
            if root is None or not isinstance(args.main_sha, str) or not SHA.fullmatch(args.main_sha) or not isinstance(args.main_tree, str) or not SHA.fullmatch(args.main_tree) or args.verdict_out is None:
                raise ReconciliationError("live verdict requires root, main SHA/tree and verdict output")
            client = GitHub(os.environ.get("GITHUB_TOKEN", ""))
            validate_live(manifest, client)
            branch = record(client.get("branches/main"), "main branch")
            branch_commit = branch.get("commit")
            if not isinstance(branch_commit, dict) or branch_commit.get("sha") != args.main_sha:
                raise ReconciliationError("live main ref mismatch")
            main_commit = api_commit(client, args.main_sha)
            if main_commit["tree"].get("sha") != args.main_tree or git(root, "rev-parse", "HEAD") != args.main_sha or git(root, "rev-parse", "HEAD^{tree}") != args.main_tree:
                raise ReconciliationError("live/local main identity mismatch")
            reconciliation_oid = blob_at(root, args.main_sha, RECONCILIATION_PATH)
            if content_oid(client, args.main_sha, RECONCILIATION_PATH) != reconciliation_oid:
                raise ReconciliationError("live/local reconciliation blob mismatch")
            evidence_oid = validate_audit(manifest, client, args.main_sha, args.main_tree, reconciliation_oid)
            audit = {"state": validation["auditState"]} if evidence_oid == "none" else {"state": "passed", "evidenceBlobOid": evidence_oid}
            verdict = {
                "schemaVersion": 1,
                "contract": "riftward-reconciliation-verdict-v1",
                "mainCommit": args.main_sha,
                "mainTree": args.main_tree,
                "reconciliationBlobOid": reconciliation_oid,
                "historicalProjectionSha256": validation["historicalProjectionSha256"],
                "audit": audit,
                "recordedTaskIds": validation["recordedTaskIds"],
                "doneEligibleTaskIds": validation["doneEligibleTaskIds"],
            }
            atomic_json(args.verdict_out.resolve(), verdict)
        elif args.verdict_out is not None or args.main_sha is not None or args.main_tree is not None:
            raise ReconciliationError("verdict output is available only in live GitHub mode")
        if args.print_task_ids:
            print("\n".join(validation["recordedTaskIds"]))
    except (OSError, ValueError, json.JSONDecodeError, ReconciliationError) as exc:
        print(f"Reconciliation abgelehnt: {exc}", file=sys.stderr)
        return 2
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
