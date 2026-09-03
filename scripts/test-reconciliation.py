#!/usr/bin/env python3
"""Hermetic negative tests for GitHub reconciliation and main-gate binding."""

from __future__ import annotations

import base64
from copy import deepcopy
from datetime import datetime, timezone
import json
from pathlib import Path
import tempfile

import pages_status
from pages_status import StatusError, accepted_task_ids
from pages_github_observation import SIDECAR, git_blob_oid, main_gate, observe, read_sidecar
from validate_reconciliation import AUDIT_BLOB_PATHS, AUDIT_PATH, AUDIT_TASKS, BUILDER, CHECK, EXPECTED_HISTORICAL_SHA256, RECONCILIATION_PATH, REPOSITORY, REVIEWER, WORKFLOW, ReconciliationError, load_json, validate, validate_audit, validate_live


ROOT = Path(__file__).resolve().parent.parent


class Fixture:
    def __init__(self, manifest: dict[str, object]):
        repo = {"id": int(REPOSITORY["id"]), "full_name": REPOSITORY["name"]}
        self.responses: dict[str, object] = {
            "": {**repo, "default_branch": "main"},
            f"actions/workflows/{WORKFLOW['id']}": {"id": int(WORKFLOW["id"]), "path": WORKFLOW["path"]},
        }
        for item in manifest["receipts"]:
            pr_number = item["pullRequestNumber"]
            self.responses[f"pulls/{pr_number}"] = {
                "number": pr_number, "state": "closed", "merged": True, "merge_commit_sha": item["resultSha"],
                "base": {"ref": "main", "sha": item["baseSha"], "repo": repo},
                "head": {"sha": item["headSha"], "repo": repo},
            }
            self.responses[f"actions/runs/{item['runId']}"] = {
                "id": int(item["runId"]), "workflow_id": int(WORKFLOW["id"]), "run_attempt": 1,
                "check_suite_id": int(item["checkSuiteId"]), "path": WORKFLOW["path"],
                "event": "pull_request", "head_sha": item["headSha"], "conclusion": "success", "repository": repo,
                "pull_requests": [{"number": pr_number, "base": {"sha": item["baseSha"]}, "head": {"sha": item["headSha"]}}],
            }
            job = {"id": int(item["checkRunId"]), "name": CHECK["name"], "head_sha": item["headSha"], "run_attempt": 1, "conclusion": "success"}
            self.responses[f"actions/runs/{item['runId']}/attempts/1/jobs?per_page=100"] = {"total_count": 1, "jobs": [job]}
            app = {"id": int(CHECK["appId"]), "slug": CHECK["appSlug"]}
            self.responses[f"check-suites/{item['checkSuiteId']}"] = {"id": int(item["checkSuiteId"]), "head_sha": item["headSha"], "conclusion": "success", "app": app}
            self.responses[f"check-runs/{item['checkRunId']}"] = {"id": int(item["checkRunId"]), "name": CHECK["name"], "head_sha": item["headSha"], "status": "completed", "conclusion": "success", "check_suite": {"id": int(item["checkSuiteId"])}, "app": app}
            for commit in (item["baseSha"], item["headSha"], item["resultSha"]):
                self.responses[f"contents/{WORKFLOW['path']}?ref={commit}"] = {"type": "file", "path": WORKFLOW["path"], "sha": WORKFLOW["trustedBlobOid"]}
            self.responses[f"contents/{item['taskManifestPath']}?ref={item['resultSha']}"] = {"type": "file", "path": item["taskManifestPath"], "sha": item["taskManifestBlobOid"]}
            self.responses[f"contents/{item['reviewEvidencePath']}?ref={item['resultSha']}"] = {"type": "file", "path": item["reviewEvidencePath"], "sha": item["reviewEvidenceBlobOid"]}

    def get(self, endpoint: str) -> object:
        if endpoint not in self.responses:
            raise ReconciliationError("fixture endpoint missing")
        return deepcopy(self.responses[endpoint])


def rejected(manifest: dict[str, object], mutate) -> None:
    fixture = Fixture(manifest)
    mutate(fixture, manifest["receipts"][0])
    try:
        validate_live(manifest, fixture)
    except ReconciliationError:
        return
    raise AssertionError("adversarial GitHub fixture was accepted")


def passed_manifest(manifest: dict[str, object]) -> dict[str, object]:
    value = deepcopy(manifest)
    value["audit"] = {"state": "passed", "pullRequestNumber": 90, "evidencePath": AUDIT_PATH, "evidenceBlobOid": "9" * 40, "doneEligibleTaskIds": list(AUDIT_TASKS)}
    return value


class AuditFixture(Fixture):
    def __init__(self, manifest: dict[str, object]):
        super().__init__(manifest)
        self.parent, self.builder, self.builder_tree = "1" * 40, "2" * 40, "3" * 40
        self.reviewer, self.reviewer_tree, self.result = "4" * 40, "5" * 40, "6" * 40
        self.main, self.main_tree = "7" * 40, "8" * 40
        self.reconciliation_oid, self.evidence_oid = "a" * 40, "9" * 40
        self.blob_oids = {path: f"{index:x}" * 40 for index, path in enumerate(AUDIT_BLOB_PATHS, 10)}
        self.evidence = {
            "schemaVersion": 1,
            "contract": "riftward-historical-reconciliation-audit-v1",
            "reviewedBuilderCommit": self.builder,
            "reviewedBuilderTree": self.builder_tree,
            "historicalProjectionSha256": EXPECTED_HISTORICAL_SHA256,
            "builderTreeBlobOids": self.blob_oids,
            "coveredTaskIds": list(AUDIT_TASKS),
            "criteria": "PASS",
            "historicalRoleSeparation": "not-publicly-proven",
            "currentAuditSeparation": "builder-separated-agent-audit",
            "identityAssurance": "role-declaration-not-personhood-proof",
        }
        repo = {"id": int(REPOSITORY["id"]), "full_name": REPOSITORY["name"]}
        self.responses["pulls/90"] = {"number": 90, "state": "closed", "merged": True, "merge_commit_sha": self.result, "base": {"ref": "main", "sha": self.parent, "repo": repo}, "head": {"sha": self.reviewer, "repo": repo}}
        self.responses[f"git/commits/{self.parent}"] = self.commit(self.parent, "0" * 40, [], {"name": "GitHub", "email": "noreply@github.com"}, "base")
        self.responses[f"git/commits/{self.builder}"] = self.commit(self.builder, self.builder_tree, [self.parent], BUILDER, self.builder_message())
        self.responses[f"git/commits/{self.reviewer}"] = self.commit(self.reviewer, self.reviewer_tree, [self.builder], REVIEWER, self.reviewer_message())
        self.responses[f"git/commits/{self.result}"] = self.commit(self.result, self.reviewer_tree, [self.parent], {"name": "GitHub", "email": "noreply@github.com"}, "squash")
        self.responses[f"git/commits/{self.main}"] = self.commit(self.main, self.main_tree, [self.result], {"name": "GitHub", "email": "noreply@github.com"}, "main")
        self.responses[f"compare/{self.builder}...{self.reviewer}"] = {"total_commits": 1, "commits": [{"sha": self.reviewer}], "files": [{"filename": AUDIT_PATH, "status": "added"}, {"filename": RECONCILIATION_PATH, "status": "modified"}]}
        self.responses[f"compare/{self.result}...{self.main}"] = {"status": "ahead", "merge_base_commit": {"sha": self.result}}
        for commit in (self.parent, self.builder, self.reviewer, self.result):
            self.responses[f"contents/{WORKFLOW['path']}?ref={commit}"] = {"type": "file", "path": WORKFLOW["path"], "sha": WORKFLOW["trustedBlobOid"]}
        for path, oid in self.blob_oids.items():
            self.responses[f"contents/{path}?ref={self.builder}"] = {"type": "file", "path": path, "sha": oid}
        for commit in (self.reviewer, self.result, self.main):
            self.responses[f"contents/{RECONCILIATION_PATH}?ref={commit}"] = {"type": "file", "path": RECONCILIATION_PATH, "sha": self.reconciliation_oid}
        self.encode_evidence()
        app = {"id": int(CHECK["appId"]), "slug": CHECK["appSlug"]}
        self.responses[f"commits/{self.reviewer}/check-runs?per_page=100"] = {"total_count": 1, "check_runs": [{"id": 901, "name": CHECK["name"], "head_sha": self.reviewer, "status": "completed", "conclusion": "success", "check_suite": {"id": 902}, "app": app}]}
        self.responses[f"actions/runs?head_sha={self.reviewer}&event=pull_request&per_page=100"] = {"total_count": 1, "workflow_runs": [{"id": 903, "workflow_id": int(WORKFLOW["id"]), "check_suite_id": 902, "path": WORKFLOW["path"], "run_attempt": 1, "event": "pull_request", "head_sha": self.reviewer, "repository": repo, "pull_requests": [{"number": 90, "base": {"sha": self.parent}, "head": {"sha": self.reviewer}}]}]}
        self.responses["actions/runs/903/attempts/1/jobs?per_page=100"] = {"total_count": 1, "jobs": [{"id": 901, "name": CHECK["name"], "head_sha": self.reviewer, "run_attempt": 1, "conclusion": "success"}]}
        self.responses["check-suites/902"] = {"id": 902, "head_sha": self.reviewer, "conclusion": "success", "app": app}
        self.responses["check-runs/901"] = {"id": 901, "name": CHECK["name"], "head_sha": self.reviewer, "status": "completed", "conclusion": "success", "check_suite": {"id": 902}, "app": app}

    @staticmethod
    def commit(sha: str, tree: str, parents: list[str], identity: dict[str, str], message: str) -> dict[str, object]:
        return {"sha": sha, "tree": {"sha": tree}, "parents": [{"sha": item} for item in parents], "author": dict(identity), "committer": dict(identity), "message": message}

    def builder_message(self) -> str:
        return f"feat: build audit candidate\n\nAgent-Role: builder\nTask-ID: T-054\nSource-Commit: {self.parent}\nSource-Tree: {'0' * 40}\n"

    def reviewer_message(self) -> str:
        return f"test: audit historical receipts\n\nAgent-Role: reviewer\nTask-ID: T-054\nSource-Commit: {self.builder}\nSource-Tree: {self.builder_tree}\nIndependent-Review: PASS\nReviewed-Commit: {self.builder}\nReviewed-Tree: {self.builder_tree}\n"

    def encode_evidence(self) -> None:
        payload = (json.dumps(self.evidence, sort_keys=True, separators=(",", ":")) + "\n").encode()
        response = {"type": "file", "path": AUDIT_PATH, "sha": self.evidence_oid, "encoding": "base64", "content": base64.b64encode(payload).decode(), "size": len(payload)}
        for commit in (self.reviewer, self.result, self.main):
            self.responses[f"contents/{AUDIT_PATH}?ref={commit}"] = deepcopy(response)


def audit_rejected(manifest: dict[str, object], mutate) -> None:
    fixture = AuditFixture(manifest)
    mutate(fixture)
    try:
        validate_audit(manifest, fixture, fixture.main, fixture.main_tree, fixture.reconciliation_oid)
    except (ReconciliationError, AssertionError):
        return
    raise AssertionError("adversarial retrospective audit was accepted")


class GateFixture:
    def __init__(self):
        self.main = "a" * 40
        self.tree = "b" * 40
        self.head = "c" * 40
        self.base = "f" * 40
        self.workflow_head_blob = WORKFLOW["trustedBlobOid"]
        self.check_total_count = 1
        self.run_total_count = 1
        self.job_total_count = 1
        repo = {"id": 1333151301, "full_name": "Koschnag/ai-fantasy-rts-rpg"}
        self.closed = [{"number": 90, "state": "closed", "merged_at": "2026-09-03T00:00:00Z", "merge_commit_sha": self.main, "base": {"ref": "main", "sha": self.base, "repo": repo}, "head": {"sha": self.head, "repo": repo}}]
        self.checks = [{"id": 7, "name": "Repository gates", "head_sha": self.head, "status": "completed", "conclusion": "success", "check_suite": {"id": 8}, "app": {"id": 15368, "slug": "github-actions"}}]
        self.run_workflow_id = int(WORKFLOW["id"])
        self.run_workflow_path = WORKFLOW["path"]
        self.run_suite_id = 8
        self.job_attempt = 1
        self.check_suite_id = 8

    def get(self, endpoint: str) -> object:
        if endpoint.startswith("pulls?state=closed"):
            return deepcopy(self.closed)
        if endpoint == f"git/commits/{self.head}":
            return {"tree": {"sha": self.tree}}
        if endpoint == f"actions/workflows/{WORKFLOW['id']}":
            return {"id": int(WORKFLOW["id"]), "path": WORKFLOW["path"]}
        if endpoint == f"contents/{WORKFLOW['path']}?ref={self.main}":
            return {"type": "file", "path": WORKFLOW["path"], "sha": WORKFLOW["trustedBlobOid"]}
        if endpoint == f"contents/{WORKFLOW['path']}?ref={self.head}":
            return {"type": "file", "path": WORKFLOW["path"], "sha": self.workflow_head_blob}
        if endpoint == f"commits/{self.head}/check-runs?per_page=100":
            return {"total_count": self.check_total_count if len(self.checks) == 1 else len(self.checks), "check_runs": deepcopy(self.checks)}
        if endpoint == f"actions/runs?head_sha={self.head}&event=pull_request&per_page=100":
            repo = {"id": 1333151301, "full_name": "Koschnag/ai-fantasy-rts-rpg"}
            return {"total_count": self.run_total_count, "workflow_runs": [{"id": 77, "workflow_id": self.run_workflow_id, "path": self.run_workflow_path, "check_suite_id": self.run_suite_id, "run_attempt": 1, "event": "pull_request", "head_sha": self.head, "conclusion": "success", "repository": repo, "pull_requests": [{"number": 90, "base": {"sha": self.base}, "head": {"sha": self.head}}]}]}
        if endpoint == "actions/runs/77/attempts/1/jobs?per_page=100":
            return {"total_count": self.job_total_count, "jobs": [{"id": 7, "name": "Repository gates", "head_sha": self.head, "run_attempt": self.job_attempt, "conclusion": "success"}]}
        if endpoint.startswith("check-suites/"):
            requested = int(endpoint.rsplit("/", 1)[1])
            return {"id": requested, "head_sha": self.head, "conclusion": "success", "app": {"id": 15368, "slug": "github-actions"}}
        if endpoint == "check-runs/7":
            return {"id": 7, "name": "Repository gates", "head_sha": self.head, "status": "completed", "conclusion": "success", "check_suite": {"id": self.check_suite_id}, "app": {"id": 15368, "slug": "github-actions"}}
        raise AssertionError(endpoint)


def test_main_gate() -> None:
    valid = GateFixture()
    assert main_gate(valid, valid.main, valid.tree) == "passed"
    direct = GateFixture(); direct.closed = []; assert main_gate(direct, direct.main, direct.tree) == "unknown"
    foreign = GateFixture(); foreign.closed[0]["head"]["repo"]["id"] = 1; assert main_gate(foreign, foreign.main, foreign.tree) == "unknown"
    mismatch = GateFixture(); mismatch.tree = "d" * 40; assert main_gate(mismatch, mismatch.main, "b" * 40) == "unknown"
    missing = GateFixture(); missing.checks = []; assert main_gate(missing, missing.main, missing.tree) == "unknown"
    duplicate = GateFixture(); duplicate.checks.append(deepcopy(duplicate.checks[0])); assert main_gate(duplicate, duplicate.main, duplicate.tree) == "unknown"
    foreign_workflow = GateFixture(); foreign_workflow.run_workflow_id = 1; assert main_gate(foreign_workflow, foreign_workflow.main, foreign_workflow.tree) == "unknown"
    foreign_workflow_path = GateFixture(); foreign_workflow_path.run_workflow_path = ".github/workflows/other.yml"; assert main_gate(foreign_workflow_path, foreign_workflow_path.main, foreign_workflow_path.tree) == "unknown"
    foreign_run_suite = GateFixture(); foreign_run_suite.run_suite_id = 9; assert main_gate(foreign_run_suite, foreign_run_suite.main, foreign_run_suite.tree) == "unknown"
    foreign_suite = GateFixture(); foreign_suite.checks[0]["check_suite"]["id"] = 9; assert main_gate(foreign_suite, foreign_suite.main, foreign_suite.tree) == "unknown"
    wrong_attempt = GateFixture(); wrong_attempt.job_attempt = 2; assert main_gate(wrong_attempt, wrong_attempt.main, wrong_attempt.tree) == "unknown"
    paginated = GateFixture(); paginated.check_total_count = 101; assert main_gate(paginated, paginated.main, paginated.tree) == "unknown"
    mutated_workflow = GateFixture(); mutated_workflow.workflow_head_blob = "0" * 40; assert main_gate(mutated_workflow, mutated_workflow.main, mutated_workflow.tree) == "unknown"


def test_stale_wip_does_not_claim_offline() -> None:
    gate = GateFixture()
    wip, wip_tree, source, source_tree = "d" * 40, "e" * 40, "1" * 40, "2" * 40
    sidecar = {"schemaVersion": 1, "candidate": {"taskId": "T-054", "lifecycleStatus": "IN_PROGRESS", "blocker": "none"}, "activity": {"state": "active", "taskId": "T-054", "phase": "building", "role": "wip", "lastGate": "passed", "blocker": "none", "autonomy": "bounded-autopilot", "parentClass": "root"}}
    payload = json.dumps(sidecar).encode()
    encoded = base64.b64encode(payload).decode()
    blob_oid = git_blob_oid(payload)
    identity = {"name": "Riftward WIP Autopilot", "email": "riftward-wip-autopilot@users.noreply.github.com"}
    message = f"chore: publish bounded WIP status\n\nAgent-Role: wip\nTask-ID: T-054\nSource-Commit: {source}\nSource-Tree: {source_tree}\nPublic-Status-Blob: {blob_oid}\n"

    class ObservationFixture(GateFixture):
        def get(self, endpoint: str) -> object:
            if endpoint == "": return {"id": 1333151301, "full_name": "Koschnag/ai-fantasy-rts-rpg", "default_branch": "main"}
            if endpoint == "branches/main": return {"commit": {"sha": self.main}}
            if endpoint == f"git/commits/{self.main}": return {"tree": {"sha": self.tree}}
            if endpoint == "pulls?state=open&base=main&per_page=100&sort=created&direction=asc": return []
            if endpoint == "git/ref/heads/autopilot/live-wip": return {"object": {"sha": wip}}
            if endpoint == f"git/commits/{wip}": return {"sha": wip, "tree": {"sha": wip_tree}, "parents": [{"sha": source}], "author": identity, "committer": {**identity, "date": "2026-09-02T00:00:00Z"}, "message": message}
            if endpoint == f"git/commits/{source}": return {"tree": {"sha": source_tree}}
            if endpoint == f"contents/{SIDECAR}?ref={wip}": return {"type": "file", "path": SIDECAR, "encoding": "base64", "content": encoded, "size": len(payload), "sha": blob_oid}
            return super().get(endpoint)

    value = observe(ObservationFixture(), gate.main, gate.tree, "2026-09-03T00:00:00Z")
    assert value["continuity"]["state"] == "stale"
    assert value["activity"] == {"state": "unknown"}


class SidecarFixture:
    def __init__(self):
        self.commit = "a" * 40
        self.source = "b" * 40
        self.source_tree = "c" * 40
        self.value = {"schemaVersion": 1, "candidate": {"taskId": "T-054", "lifecycleStatus": "REVIEW", "blocker": "awaiting-review"}, "activity": {"state": "waiting", "taskId": "T-054", "phase": "reviewing", "role": "reviewer", "lastGate": "waiting", "blocker": "awaiting-review", "autonomy": "human-gated", "parentClass": "child"}}
        self.payload = (json.dumps(self.value, sort_keys=True, separators=(",", ":")) + "\n").encode()
        self.oid = git_blob_oid(self.payload)
        self.api_oid = self.oid
        self.identity = {"name": "Riftward Reviewer Autopilot", "email": "riftward-reviewer-autopilot@users.noreply.github.com"}
        self.message = f"test: publish reviewed status\n\nAgent-Role: reviewer\nTask-ID: T-054\nSource-Commit: {self.source}\nSource-Tree: {self.source_tree}\nPublic-Status-Blob: {self.oid}\n"

    def commit_record(self) -> dict[str, object]:
        return {"sha": self.commit, "tree": {"sha": "d" * 40}, "parents": [{"sha": self.source}], "author": deepcopy(self.identity), "committer": deepcopy(self.identity), "message": self.message}

    def get(self, endpoint: str) -> object:
        if endpoint == f"contents/{SIDECAR}?ref={self.commit}":
            return {"type": "file", "path": SIDECAR, "encoding": "base64", "content": base64.b64encode(self.payload).decode(), "size": len(self.payload), "sha": self.api_oid}
        if endpoint == f"git/commits/{self.source}":
            return {"tree": {"sha": self.source_tree}}
        raise AssertionError(endpoint)


def test_sidecar_blob_binding() -> None:
    valid = SidecarFixture()
    assert read_sidecar(valid, valid.commit, valid.commit_record()) == valid.value

    def rejected_sidecar(mutate) -> None:
        fixture = SidecarFixture()
        commit = fixture.commit_record()
        mutate(fixture, commit)
        assert read_sidecar(fixture, fixture.commit, commit) is None

    rejected_sidecar(lambda fixture, commit: commit.__setitem__("message", fixture.message.replace(f"Public-Status-Blob: {fixture.oid}\n", "")))
    rejected_sidecar(lambda fixture, commit: commit.__setitem__("message", fixture.message + f"Public-Status-Blob: {fixture.oid}\n"))
    rejected_sidecar(lambda fixture, commit: commit.__setitem__("message", fixture.message.replace(fixture.oid, "0" * 40)))
    rejected_sidecar(lambda fixture, commit: setattr(fixture, "api_oid", "0" * 40))
    rejected_sidecar(lambda fixture, commit: commit.__setitem__("message", fixture.message.replace("Agent-Role: reviewer", "Agent-Role: unknown-role")))
    rejected_sidecar(lambda fixture, commit: (commit.__setitem__("message", fixture.message.replace("Agent-Role: reviewer", "Agent-Role: builder")), commit.__setitem__("author", {"name": "Riftward Builder Autopilot", "email": "riftward-builder-autopilot@users.noreply.github.com"}), commit.__setitem__("committer", {"name": "Riftward Builder Autopilot", "email": "riftward-builder-autopilot@users.noreply.github.com"})))
    rejected_sidecar(lambda fixture, commit: commit["author"].__setitem__("email", "other@example.invalid"))
    rejected_sidecar(lambda fixture, commit: commit.__setitem__("parents", [{"sha": "0" * 40}]))

    stale = SidecarFixture()
    stale_commit = stale.commit_record()
    stale_commit["message"] = stale.message.replace(f"Public-Status-Blob: {stale.oid}\n", "")
    assert read_sidecar(stale, stale.commit, stale_commit) is None


def test_pending_builder_eligibility(validation: dict[str, object]) -> None:
    recorded = set(AUDIT_TASKS)
    commit, tree, reconciliation_oid = "a" * 40, "b" * 40, "c" * 40
    verdict = {
        "schemaVersion": 1,
        "contract": "riftward-reconciliation-verdict-v1",
        "mainCommit": commit,
        "mainTree": tree,
        "reconciliationBlobOid": reconciliation_oid,
        "historicalProjectionSha256": EXPECTED_HISTORICAL_SHA256,
        "audit": {"state": "pending"},
        "recordedTaskIds": list(AUDIT_TASKS),
        "doneEligibleTaskIds": [],
    }
    original_git = pages_status.git
    pages_status.git = lambda root, *args: reconciliation_oid
    try:
        assert pages_status.validate_verdict(ROOT, verdict, validation, commit, tree) == (recorded, set())
        bad = deepcopy(verdict)
        bad["doneEligibleTaskIds"] = ["T-034"]
        try:
            pages_status.validate_verdict(ROOT, bad, validation, commit, tree)
        except StatusError:
            pass
        else:
            raise AssertionError("pending ephemeral verdict granted DONE eligibility")
    finally:
        pages_status.git = original_git

    assert accepted_task_ids(ROOT, {"T-001"}, recorded, set()) == ["T-001"]
    try:
        accepted_task_ids(ROOT, {"T-034"}, recorded, set())
    except StatusError:
        pass
    else:
        raise AssertionError("historical DONE was accepted without an audit verdict")
    try:
        accepted_task_ids(ROOT, set(AUDIT_TASKS), recorded, {"T-034"})
    except StatusError:
        pass
    else:
        raise AssertionError("partial audit eligibility was accepted")
    with tempfile.TemporaryDirectory() as temporary:
        root = Path(temporary)
        tasks = root / ".ai/tasks"
        tasks.mkdir(parents=True)
        for task_id in AUDIT_TASKS:
            status = "review" if task_id == "T-037" else "accepted"
            (tasks / f"{task_id}-fixture.json").write_text(json.dumps({"id": task_id, "status": status}), encoding="utf-8")
        try:
            accepted_task_ids(root, set(AUDIT_TASKS), recorded, recorded)
        except StatusError:
            pass
        else:
            raise AssertionError("mismatched task-manifest status was accepted")


def test_passed_audit(manifest: dict[str, object]) -> None:
    passed = passed_manifest(manifest)
    validation = validate(passed, None)
    assert validation["doneEligibleTaskIds"] == AUDIT_TASKS
    valid = AuditFixture(passed)
    validate_live(passed, valid)
    assert validate_audit(passed, valid, valid.main, valid.main_tree, valid.reconciliation_oid) == valid.evidence_oid

    audit_rejected(passed, lambda f: f.responses[f"contents/{AUDIT_PATH}?ref={f.reviewer}"].__setitem__("sha", "0" * 40))
    audit_rejected(passed, lambda f: (f.evidence.__setitem__("reviewedBuilderCommit", "0" * 40), f.encode_evidence()))
    audit_rejected(passed, lambda f: (f.evidence.__setitem__("reviewedBuilderTree", "0" * 40), f.encode_evidence()))
    audit_rejected(passed, lambda f: f.responses[f"git/commits/{f.builder}"]["author"].__setitem__("email", "other@example.invalid"))
    audit_rejected(passed, lambda f: f.responses[f"git/commits/{f.reviewer}"].__setitem__("message", f.reviewer_message().replace("Independent-Review: PASS", "Independent-Review: BLOCK")))
    audit_rejected(passed, lambda f: f.responses[f"git/commits/{f.reviewer}"].__setitem__("parents", [{"sha": f.parent}]))
    audit_rejected(passed, lambda f: f.responses[f"compare/{f.builder}...{f.reviewer}"]["files"].append({"filename": "scripts/runtime.py", "status": "modified"}))
    audit_rejected(passed, lambda f: f.responses["pulls/90"]["base"].__setitem__("ref", "other"))
    audit_rejected(passed, lambda f: f.responses["pulls/90"]["head"].__setitem__("sha", "0" * 40))
    audit_rejected(passed, lambda f: f.responses["pulls/90"]["head"]["repo"].__setitem__("id", 1))
    audit_rejected(passed, lambda f: f.responses["check-runs/901"].__setitem__("conclusion", "failure"))
    audit_rejected(passed, lambda f: f.responses["check-suites/902"]["app"].__setitem__("id", 1))
    audit_rejected(passed, lambda f: f.responses["actions/runs/903/attempts/1/jobs?per_page=100"]["jobs"][0].__setitem__("run_attempt", 2))
    audit_rejected(passed, lambda f: f.responses[f"git/commits/{f.result}"]["tree"].__setitem__("sha", "0" * 40))
    audit_rejected(passed, lambda f: f.responses[f"git/commits/{f.main}"]["tree"].__setitem__("sha", "0" * 40))
    audit_rejected(passed, lambda f: f.responses[f"compare/{f.result}...{f.main}"].__setitem__("status", "diverged"))
    audit_rejected(passed, lambda f: f.responses[f"contents/{next(iter(AUDIT_BLOB_PATHS))}?ref={f.builder}"].__setitem__("sha", "0" * 40))

    class Outage:
        def get(self, endpoint: str) -> object:
            raise ReconciliationError("fixture outage")

    try:
        validate_audit(passed, Outage(), valid.main, valid.main_tree, valid.reconciliation_oid)
    except ReconciliationError:
        pass
    else:
        raise AssertionError("API outage did not fail closed")


def main() -> int:
    manifest = load_json(ROOT / "docs/showcase/reconciliation.json")
    assert isinstance(manifest, dict)
    validation = validate(manifest, ROOT)
    assert validation["recordedTaskIds"] == AUDIT_TASKS
    assert validation["doneEligibleTaskIds"] == []
    validate_live(manifest, Fixture(manifest))
    rejected(manifest, lambda f, i: f.responses[f"pulls/{i['pullRequestNumber']}"]["head"]["repo"].__setitem__("id", 1))
    rejected(manifest, lambda f, i: f.responses[f"check-runs/{i['checkRunId']}"]["check_suite"].__setitem__("id", 1))
    rejected(manifest, lambda f, i: f.responses[f"actions/runs/{i['runId']}/attempts/1/jobs?per_page=100"]["jobs"][0].__setitem__("run_attempt", 2))
    rejected(manifest, lambda f, i: f.responses[f"check-suites/{i['checkSuiteId']}"]["app"].__setitem__("id", 1))
    rejected(manifest, lambda f, i: f.responses[f"actions/runs/{i['runId']}/attempts/1/jobs?per_page=100"].__setitem__("jobs", [{"id": 999, "name": CHECK["name"], "head_sha": i["headSha"], "run_attempt": 1, "conclusion": "success"}]))
    rejected(manifest, lambda f, i: f.responses[f"contents/{WORKFLOW['path']}?ref={i['headSha']}"].__setitem__("sha", "0" * 40))
    historical_claim = deepcopy(manifest)
    historical_claim["receipts"][0]["roleSeparation"] = "independent"
    try:
        validate(historical_claim, None)
    except ReconciliationError:
        pass
    else:
        raise AssertionError("altered historical role claim was accepted")
    pending_overclaim = deepcopy(manifest); pending_overclaim["audit"]["doneEligibleTaskIds"] = ["T-034"]
    try:
        validate(pending_overclaim, None)
    except ReconciliationError:
        pass
    else:
        raise AssertionError("pending audit granted eligibility")
    test_passed_audit(manifest)
    test_pending_builder_eligibility(validation)
    test_main_gate()
    test_stale_wip_does_not_claim_offline()
    test_sidecar_blob_binding()
    print("RECONCILIATION_HERMETIC_PASS")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
