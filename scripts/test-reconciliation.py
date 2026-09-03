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
import validate_reconciliation as reconciliation
from pages_status import StatusError, accepted_task_ids
from pages_github_observation import SIDECAR, git_blob_oid, main_gate, observe, read_sidecar
from validate_reconciliation import AUDIT_BLOB_PATHS, AUDIT_EVIDENCE_CONTRACT, AUDIT_PATH, AUDIT_TASKS, BUILDER, CHECK, EXPECTED_HISTORICAL_SHA256, MAX_AUDIT_REPAIRS, RECONCILIATION_PATH, REPAIR, REPOSITORY, REVIEWER, WORKFLOW, ReconciliationError, load_json, validate, validate_audit, validate_live


ROOT = Path(__file__).resolve().parent.parent
AUDIT_CHAIN = {
    "candidateBaseCommit": "1" * 40,
    "candidateBaseTree": "0" * 40,
    "builderCommit": "2" * 40,
    "builderTree": "3" * 40,
    "repairChain": [
        {"commit": "4" * 40, "tree": "5" * 40},
        {"commit": "6" * 40, "tree": "7" * 40},
    ],
    "reviewedCandidateCommit": "6" * 40,
    "reviewedCandidateTree": "7" * 40,
}


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
    value["audit"] = {
        "state": "passed", "pullRequestNumber": 90, "evidencePath": AUDIT_PATH, "evidenceBlobOid": "d" * 40,
        "evidenceContract": AUDIT_EVIDENCE_CONTRACT, **deepcopy(AUDIT_CHAIN), "doneEligibleTaskIds": list(AUDIT_TASKS),
    }
    return value


class AuditFixture(Fixture):
    def __init__(self, manifest: dict[str, object]):
        super().__init__(manifest)
        self.manifest = deepcopy(manifest)
        self.parent, self.parent_tree = AUDIT_CHAIN["candidateBaseCommit"], AUDIT_CHAIN["candidateBaseTree"]
        self.builder, self.builder_tree = AUDIT_CHAIN["builderCommit"], AUDIT_CHAIN["builderTree"]
        self.repairs = deepcopy(AUDIT_CHAIN["repairChain"])
        self.candidate, self.candidate_tree = AUDIT_CHAIN["reviewedCandidateCommit"], AUDIT_CHAIN["reviewedCandidateTree"]
        self.reviewer, self.reviewer_tree, self.result = "8" * 40, "9" * 40, "a" * 40
        self.main, self.main_tree = "b" * 40, "c" * 40
        self.reconciliation_oid, self.evidence_oid = "e" * 40, "d" * 40
        self.blob_oids = {path: f"{index:x}"[-1] * 40 for index, path in enumerate(AUDIT_BLOB_PATHS, 1)}
        self.evidence = {
            "schemaVersion": 2,
            "contract": AUDIT_EVIDENCE_CONTRACT,
            **deepcopy(AUDIT_CHAIN),
            "historicalProjectionSha256": EXPECTED_HISTORICAL_SHA256,
            "reviewedCandidateTreeBlobOids": self.blob_oids,
            "coveredTaskIds": list(AUDIT_TASKS),
            "criteria": "PASS",
            "historicalRoleSeparation": "not-publicly-proven",
            "currentAuditSeparation": "candidate-chain-separated-reviewer-audit",
            "identityAssurance": "role-declaration-not-personhood-proof",
        }
        repo = {"id": int(REPOSITORY["id"]), "full_name": REPOSITORY["name"]}
        self.responses["pulls/90"] = {"number": 90, "state": "closed", "merged": True, "merge_commit_sha": self.result, "base": {"ref": "main", "sha": self.parent, "repo": repo}, "head": {"sha": self.reviewer, "repo": repo}}
        self.responses[f"git/commits/{self.parent}"] = self.commit(self.parent, self.parent_tree, [], {"name": "GitHub", "email": "noreply@github.com"}, "base")
        self.responses[f"git/commits/{self.builder}"] = self.commit(self.builder, self.builder_tree, [self.parent], BUILDER, self.builder_message())
        previous_sha, previous_tree = self.builder, self.builder_tree
        for index, repair in enumerate(self.repairs):
            self.responses[f"git/commits/{repair['commit']}"] = self.commit(repair["commit"], repair["tree"], [previous_sha], REPAIR, self.repair_message(index, previous_sha, previous_tree))
            previous_sha, previous_tree = repair["commit"], repair["tree"]
        self.responses[f"git/commits/{self.reviewer}"] = self.commit(self.reviewer, self.reviewer_tree, [self.candidate], REVIEWER, self.reviewer_message())
        self.responses[f"git/commits/{self.result}"] = self.commit(self.result, self.reviewer_tree, [self.parent], {"name": "GitHub", "email": "noreply@github.com"}, "squash")
        self.responses[f"git/commits/{self.main}"] = self.commit(self.main, self.main_tree, [self.result], {"name": "GitHub", "email": "noreply@github.com"}, "main")
        self.add_direct_comparison(self.parent, self.builder, ["README.md", ".ai/public-status-v3.json"])
        self.add_direct_comparison(self.builder, self.repairs[0]["commit"], ["scripts/validate_reconciliation.py"])
        self.add_direct_comparison(self.repairs[0]["commit"], self.repairs[1]["commit"], ["scripts/test-reconciliation.py", ".ai/public-status-v3.json"])
        self.add_direct_comparison(self.candidate, self.reviewer, [AUDIT_PATH, RECONCILIATION_PATH])
        self.responses[f"compare/{self.result}...{self.main}"] = {"status": "ahead", "merge_base_commit": {"sha": self.result}}
        for commit in (self.parent, self.builder, *(item["commit"] for item in self.repairs), self.reviewer, self.result, self.main):
            self.responses[f"contents/{WORKFLOW['path']}?ref={commit}"] = {"type": "file", "path": WORKFLOW["path"], "sha": WORKFLOW["trustedBlobOid"]}
        self.responses[f"contents/.ai/public-status-v3.json?ref={self.builder}"] = {"type": "file", "path": ".ai/public-status-v3.json", "sha": "f" * 40}
        self.responses[f"contents/.ai/public-status-v3.json?ref={self.candidate}"] = {"type": "file", "path": ".ai/public-status-v3.json", "sha": "e" * 40}
        for path, oid in self.blob_oids.items():
            self.responses[f"contents/{path}?ref={self.candidate}"] = {"type": "file", "path": path, "sha": oid}
        for commit in (self.reviewer, self.result, self.main):
            self.responses[f"contents/{RECONCILIATION_PATH}?ref={commit}"] = {"type": "file", "path": RECONCILIATION_PATH, "sha": self.reconciliation_oid}
        self.encode_evidence()
        app = {"id": int(CHECK["appId"]), "slug": CHECK["appSlug"]}
        self.responses[f"commits/{self.reviewer}/check-runs?per_page=100"] = {"total_count": 1, "check_runs": [{"id": 901, "name": CHECK["name"], "head_sha": self.reviewer, "status": "completed", "conclusion": "success", "check_suite": {"id": 902}, "app": app}]}
        self.responses[f"actions/runs?head_sha={self.reviewer}&event=pull_request&per_page=100"] = {"total_count": 1, "workflow_runs": [{"id": 903, "workflow_id": int(WORKFLOW["id"]), "check_suite_id": 902, "path": WORKFLOW["path"], "run_attempt": 1, "event": "pull_request", "head_sha": self.reviewer, "status": "completed", "conclusion": "success", "repository": repo, "pull_requests": [{"number": 90, "base": {"sha": self.parent}, "head": {"sha": self.reviewer}}]}]}
        self.responses["actions/runs/903/attempts/1/jobs?per_page=100"] = {"total_count": 1, "jobs": [{"id": 901, "name": CHECK["name"], "head_sha": self.reviewer, "run_attempt": 1, "status": "completed", "conclusion": "success"}]}
        self.responses["check-suites/902"] = {"id": 902, "head_sha": self.reviewer, "conclusion": "success", "app": app}
        self.responses["check-runs/901"] = {"id": 901, "name": CHECK["name"], "head_sha": self.reviewer, "status": "completed", "conclusion": "success", "check_suite": {"id": 902}, "app": app}

    @staticmethod
    def commit(sha: str, tree: str, parents: list[str], identity: dict[str, str], message: str) -> dict[str, object]:
        return {"sha": sha, "tree": {"sha": tree}, "parents": [{"sha": item} for item in parents], "author": dict(identity), "committer": dict(identity), "message": message}

    def add_direct_comparison(self, source: str, commit: str, paths: list[str]) -> None:
        files = [{"filename": path, "status": "added" if path == AUDIT_PATH else "modified"} for path in paths]
        self.responses[f"compare/{source}...{commit}"] = {"total_commits": 1, "total_files": len(files), "commits": [{"sha": commit}], "files": files}

    def builder_message(self) -> str:
        return f"feat: build audit candidate\n\nAgent-Role: builder\nTask-ID: T-054\nSource-Commit: {self.parent}\nSource-Tree: {self.parent_tree}\nPublic-Status-Blob: {'f' * 40}\n"

    def repair_message(self, index: int, source: str, source_tree: str) -> str:
        status = f"Public-Status-Blob: {'e' * 40}\n" if index == len(self.repairs) - 1 else ""
        return f"fix: repair audit candidate {index + 1}\n\nAgent-Role: repair\nTask-ID: T-054\nSource-Commit: {source}\nSource-Tree: {source_tree}\n{status}"

    def reviewer_message(self) -> str:
        return f"test: audit historical receipts\n\nAgent-Role: reviewer\nTask-ID: T-054\nSource-Commit: {self.candidate}\nSource-Tree: {self.candidate_tree}\nIndependent-Review: PASS\nReviewed-Commit: {self.candidate}\nReviewed-Tree: {self.candidate_tree}\n"

    def encode_evidence(self) -> None:
        payload = (json.dumps(self.evidence, sort_keys=True, separators=(",", ":")) + "\n").encode()
        response = {"type": "file", "path": AUDIT_PATH, "sha": self.evidence_oid, "encoding": "base64", "content": base64.b64encode(payload).decode(), "size": len(payload)}
        for commit in (self.reviewer, self.result, self.main):
            self.responses[f"contents/{AUDIT_PATH}?ref={commit}"] = deepcopy(response)

    def bind_chain(self, name: str, value: object) -> None:
        self.evidence[name] = deepcopy(value)
        self.manifest["audit"][name] = deepcopy(value)
        self.encode_evidence()


def audit_rejected(manifest: dict[str, object], mutate) -> None:
    fixture = AuditFixture(manifest)
    mutate(fixture)
    try:
        validate_audit(fixture.manifest, fixture, fixture.main, fixture.main_tree, fixture.reconciliation_oid)
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
    schema = load_json(ROOT / "docs/showcase/reconciliation.schema.json")
    assert isinstance(schema, dict)
    passed_schema = schema["properties"]["audit"]["oneOf"][1]
    assert set(passed_schema["required"]) == set(passed["audit"])
    assert passed_schema["properties"]["evidenceContract"]["const"] == AUDIT_EVIDENCE_CONTRACT
    assert passed_schema["properties"]["repairChain"]["maxItems"] == MAX_AUDIT_REPAIRS

    valid = AuditFixture(passed)
    validate_live(passed, valid)
    assert validate_audit(passed, valid, valid.main, valid.main_tree, valid.reconciliation_oid) == valid.evidence_oid

    # GitHub may clear the optional workflow-run/PR convenience relation after
    # the exact PR closes. All independent PR, workflow, suite, job and check
    # bindings remain mandatory in this accepted lifecycle state.
    closed = AuditFixture(passed)
    closed.responses[f"actions/runs?head_sha={closed.reviewer}&event=pull_request&per_page=100"]["workflow_runs"][0]["pull_requests"] = []
    assert validate_audit(closed.manifest, closed, closed.main, closed.main_tree, closed.reconciliation_oid) == closed.evidence_oid

    # Zero repairs is a valid candidate chain when the reviewer directly follows the builder.
    zero = AuditFixture(passed)
    zero.bind_chain("repairChain", [])
    zero.bind_chain("reviewedCandidateCommit", zero.builder)
    zero.bind_chain("reviewedCandidateTree", zero.builder_tree)
    zero.responses[f"git/commits/{zero.reviewer}"]["parents"] = [{"sha": zero.builder}]
    zero.responses[f"git/commits/{zero.reviewer}"]["message"] = zero.reviewer_message().replace(zero.candidate, zero.builder).replace(zero.candidate_tree, zero.builder_tree)
    zero.add_direct_comparison(zero.builder, zero.reviewer, [AUDIT_PATH, RECONCILIATION_PATH])
    for path, oid in zero.blob_oids.items():
        zero.responses[f"contents/{path}?ref={zero.builder}"] = {"type": "file", "path": path, "sha": oid}
    assert validate_audit(zero.manifest, zero, zero.main, zero.main_tree, zero.reconciliation_oid) == zero.evidence_oid

    audit_rejected(passed, lambda f: f.responses[f"contents/{AUDIT_PATH}?ref={f.reviewer}"].__setitem__("sha", "0" * 40))
    audit_rejected(passed, lambda f: f.bind_chain("reviewedCandidateCommit", f.builder))
    audit_rejected(passed, lambda f: f.bind_chain("reviewedCandidateTree", f.builder_tree))
    audit_rejected(passed, lambda f: f.responses[f"git/commits/{f.builder}"]["author"].__setitem__("email", "other@example.invalid"))
    # A missing repair cannot be hidden by shortening the evidence chain.
    audit_rejected(passed, lambda f: f.bind_chain("repairChain", f.evidence["repairChain"][:-1]))
    # Repair order is semantic: each commit must directly source its predecessor.
    audit_rejected(passed, lambda f: f.bind_chain("repairChain", list(reversed(f.evidence["repairChain"]))))
    # An extra in-range repair is rejected even before the reviewer binding can skip it.
    audit_rejected(passed, lambda f: f.bind_chain("repairChain", [*f.evidence["repairChain"], {"commit": "f" * 40, "tree": "e" * 40}]))
    # The repair bound is explicit and fail-closed.
    audit_rejected(passed, lambda f: f.bind_chain("repairChain", [{"commit": str(index) * 40, "tree": format(index + 1, "x") * 40} for index in range(1, MAX_AUDIT_REPAIRS + 2)]))
    audit_rejected(passed, lambda f: f.responses[f"git/commits/{f.repairs[1]['commit']}"].__setitem__("message", f.responses[f"git/commits/{f.repairs[1]['commit']}"]["message"].replace("Agent-Role: repair", "Agent-Role: unknown")))
    audit_rejected(passed, lambda f: f.responses[f"git/commits/{f.repairs[1]['commit']}"].__setitem__("parents", [{"sha": f.parent}]))
    audit_rejected(passed, lambda f: f.responses[f"git/commits/{f.repairs[1]['commit']}"].__setitem__("message", f.responses[f"git/commits/{f.repairs[1]['commit']}"]["message"].replace(f"Source-Tree: {f.repairs[0]['tree']}", f"Source-Tree: {'f' * 40}")))
    audit_rejected(passed, lambda f: f.responses[f"git/commits/{f.builder}"].__setitem__("message", f.builder_message().replace(f"Public-Status-Blob: {'f' * 40}\n", "")))
    audit_rejected(passed, lambda f: f.responses[f"git/commits/{f.candidate}"].__setitem__("message", f.responses[f"git/commits/{f.candidate}"]["message"].replace(f"Public-Status-Blob: {'e' * 40}", f"Public-Status-Blob: {'f' * 40}")))
    audit_rejected(passed, lambda f: f.responses[f"compare/{f.parent}...{f.builder}"].__setitem__("files", [{"filename": f"docs/generated/{index}.md", "status": "modified"} for index in range(300)]))
    audit_rejected(passed, lambda f: f.responses[f"git/commits/{f.reviewer}"].__setitem__("message", f.reviewer_message().replace("Independent-Review: PASS", "Independent-Review: BLOCK")))
    audit_rejected(passed, lambda f: f.responses[f"git/commits/{f.reviewer}"].__setitem__("parents", [{"sha": f.parent}]))
    audit_rejected(passed, lambda f: (f.responses[f"compare/{f.candidate}...{f.reviewer}"]["files"].append({"filename": "scripts/runtime.py", "status": "modified"}), f.responses[f"compare/{f.candidate}...{f.reviewer}"].__setitem__("total_files", 3)))
    audit_rejected(passed, lambda f: f.responses["pulls/90"]["base"].__setitem__("ref", "other"))
    audit_rejected(passed, lambda f: f.responses["pulls/90"]["head"].__setitem__("sha", "0" * 40))
    audit_rejected(passed, lambda f: f.responses["pulls/90"]["head"]["repo"].__setitem__("id", 1))
    audit_rejected(passed, lambda f: f.responses["check-runs/901"].__setitem__("conclusion", "failure"))
    audit_rejected(passed, lambda f: f.responses["check-suites/902"]["app"].__setitem__("id", 1))
    audit_rejected(passed, lambda f: f.responses[f"actions/runs?head_sha={f.reviewer}&event=pull_request&per_page=100"]["workflow_runs"][0].__setitem__("conclusion", "failure"))
    audit_rejected(passed, lambda f: f.responses[f"actions/runs?head_sha={f.reviewer}&event=pull_request&per_page=100"]["workflow_runs"][0].__setitem__("pull_requests", [{"number": 90, "base": {"sha": f.parent}, "head": {"sha": f.reviewer}}, {"number": 90, "base": {"sha": f.parent}, "head": {"sha": f.reviewer}}]))
    audit_rejected(passed, lambda f: f.responses["actions/runs/903/attempts/1/jobs?per_page=100"]["jobs"][0].__setitem__("status", "in_progress"))
    audit_rejected(passed, lambda f: f.responses["actions/runs/903/attempts/1/jobs?per_page=100"]["jobs"][0].__setitem__("run_attempt", 2))
    audit_rejected(passed, lambda f: f.responses[f"git/commits/{f.result}"]["tree"].__setitem__("sha", "0" * 40))
    audit_rejected(passed, lambda f: f.responses[f"git/commits/{f.main}"]["tree"].__setitem__("sha", "0" * 40))
    audit_rejected(passed, lambda f: f.responses[f"compare/{f.result}...{f.main}"].__setitem__("status", "diverged"))
    audit_rejected(passed, lambda f: f.responses[f"contents/{next(iter(AUDIT_BLOB_PATHS))}?ref={f.candidate}"].__setitem__("sha", "0" * 40))

    class Outage:
        def get(self, endpoint: str) -> object:
            raise ReconciliationError("fixture outage")

    try:
        validate_audit(passed, Outage(), valid.main, valid.main_tree, valid.reconciliation_oid)
    except ReconciliationError:
        pass
    else:
        raise AssertionError("API outage did not fail closed")


def test_hermetic_manifest_does_not_require_local_history(manifest: dict[str, object]) -> None:
    original_git = reconciliation.git

    def forbidden_git(root: Path, *args: str) -> str:
        raise AssertionError("hermetic validation touched local Git history")

    reconciliation.git = forbidden_git
    try:
        validation = validate(manifest, None)
        assert validation["historicalProjectionSha256"] == EXPECTED_HISTORICAL_SHA256
        assert validation["auditState"] == "pending"
        assert validation["doneEligibleTaskIds"] == []
    finally:
        reconciliation.git = original_git


def test_live_invocation_does_not_require_unreachable_pr_git_objects(manifest: dict[str, object]) -> None:
    original_git = reconciliation.git
    receipts = {item["resultSha"]: item for item in manifest["receipts"]}
    head_shas = {item["headSha"] for item in manifest["receipts"]}

    def main_history_git(root: Path, *args: str, fault: str | None = None) -> str:
        del root
        if args[:2] == ("cat-file", "-p") and args[2] in receipts:
            item = receipts[args[2]]
            tree = "0" * 40 if fault == "result-tree" and item["taskId"] == "T-034" else item["resultTree"]
            parent = "0" * 40 if fault == "result-parent" and item["taskId"] == "T-034" else item["baseSha"]
            return f"tree {tree}\nparent {parent}\n"
        if args[0] == "ls-tree":
            commit, path = args[1], args[3]
            if commit in head_shas:
                raise ReconciliationError("historical PR head is absent from main-only checkout")
            for item in manifest["receipts"]:
                expected = {
                    (item["resultSha"], item["taskManifestPath"]): item["taskManifestBlobOid"],
                    (item["resultSha"], item["reviewEvidencePath"]): item["reviewEvidenceBlobOid"],
                    (item["baseSha"], WORKFLOW["path"]): WORKFLOW["trustedBlobOid"],
                    (item["resultSha"], WORKFLOW["path"]): WORKFLOW["trustedBlobOid"],
                }.get((commit, path))
                if expected is not None:
                    return f"100644 blob {expected}\t{path}"
            raise AssertionError(f"unexpected synthetic ls-tree request: {args}")
        if args[:2] == ("merge-base", "--is-ancestor"):
            if fault == "diverged" and args[2] == manifest["receipts"][0]["resultSha"]:
                raise ReconciliationError("synthetic result is not accepted on HEAD")
            return ""
        raise AssertionError(f"unexpected synthetic Git request: {args}")

    try:
        synthetic_root = Path("/synthetic-main-only-checkout")
        reconciliation.git = main_history_git
        validation = reconciliation.validate_for_invocation(manifest, synthetic_root, True)
        assert validation["historicalProjectionSha256"] == EXPECTED_HISTORICAL_SHA256
        assert validation["recordedTaskIds"] == AUDIT_TASKS

        # Offline --root must continue to require the historical PR-head blobs.
        try:
            reconciliation.validate_for_invocation(manifest, synthetic_root, False)
        except ReconciliationError:
            pass
        else:
            raise AssertionError("offline root validation stopped requiring PR-head history")

        for fault in ("result-tree", "result-parent", "diverged"):
            reconciliation.git = lambda root, *args, selected=fault: main_history_git(root, *args, fault=selected)
            try:
                reconciliation.validate_for_invocation(manifest, synthetic_root, True)
            except ReconciliationError:
                pass
            else:
                raise AssertionError(f"live main-history validation accepted {fault}")
    finally:
        reconciliation.git = original_git


def main() -> int:
    manifest = load_json(ROOT / "docs/showcase/reconciliation.json")
    assert isinstance(manifest, dict)
    # The hermetic PR test must work with actions/checkout fetch-depth=1.
    # Full-history Git-object verification remains opt-in through --root.
    validation = validate(manifest, None)
    assert validation["recordedTaskIds"] == AUDIT_TASKS
    expected_eligible = [] if validation["auditState"] == "pending" else AUDIT_TASKS
    assert validation["doneEligibleTaskIds"] == expected_eligible
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
    pending = deepcopy(manifest)
    pending["audit"] = {"state": "pending", "doneEligibleTaskIds": []}
    pending_validation = validate(pending, None)
    pending_overclaim = deepcopy(pending); pending_overclaim["audit"]["doneEligibleTaskIds"] = ["T-034"]
    try:
        validate(pending_overclaim, None)
    except ReconciliationError:
        pass
    else:
        raise AssertionError("pending audit granted eligibility")
    # Exercise both legal checked-in lifecycle states before the reviewer is
    # allowed to transition the manifest. The same frozen test must pass for
    # today's pending candidate and the later, evidence-bound passed receipt.
    test_passed_audit(pending)
    test_hermetic_manifest_does_not_require_local_history(pending)
    test_live_invocation_does_not_require_unreachable_pr_git_objects(pending)
    test_pending_builder_eligibility(pending_validation)
    test_main_gate()
    test_stale_wip_does_not_claim_offline()
    test_sidecar_blob_binding()
    print("RECONCILIATION_HERMETIC_PASS")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
