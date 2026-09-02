#!/usr/bin/env python3
"""Generate the public Riftward status from a clean, committed Git tree."""

from __future__ import annotations

import argparse
import json
import os
from pathlib import Path
import re
import subprocess
import tempfile
from datetime import datetime


FULL_HASH = re.compile(r"^[0-9a-f]{40}$")
BRANCH = re.compile(r"^[A-Za-z0-9._/-]{1,200}$")
TASK_ID = re.compile(r"^T-[0-9]{3,}$")
BACKLOG_ROW = re.compile(r"^\|\s*(T-[0-9]{3,})\s*\|")
PUBLIC_STATUSES = {"DONE", "READY", "REVIEW", "DRAFT", "BLOCKED", "CANCELLED", "RUNNING"}


class StatusError(RuntimeError):
    pass


def git(root: Path, *args: str) -> str:
    result = subprocess.run(
        ["git", "-C", str(root), *args],
        check=False,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        text=True,
        encoding="utf-8",
    )
    if result.returncode != 0:
        raise StatusError(f"git {' '.join(args)} failed")
    return result.stdout.strip()


def timestamp(value: str, field: str) -> str:
    try:
        parsed = datetime.fromisoformat(value.replace("Z", "+00:00"))
    except ValueError as exc:
        raise StatusError(f"{field} is not an ISO-8601 timestamp") from exc
    if parsed.tzinfo is None:
        raise StatusError(f"{field} must include a timezone")
    return value


def parse_backlog(path: Path) -> dict[str, list[str]]:
    statuses: dict[str, list[str]] = {status: [] for status in PUBLIC_STATUSES}
    seen: set[str] = set()
    for line in path.read_text(encoding="utf-8").splitlines():
        match = BACKLOG_ROW.match(line)
        if not match:
            continue
        columns = [column.strip() for column in line.strip().strip("|").split("|")]
        if len(columns) != 7:
            raise StatusError(f"malformed BACKLOG row for {match.group(1)}")
        task_id, status = columns[0], columns[6]
        if not TASK_ID.fullmatch(task_id) or status not in PUBLIC_STATUSES:
            raise StatusError(f"unsupported BACKLOG row for {task_id}")
        if task_id in seen:
            raise StatusError(f"duplicate BACKLOG task {task_id}")
        seen.add(task_id)
        statuses[status].append(task_id)
    if not seen:
        raise StatusError("BACKLOG contains no task rows")
    return statuses


def clean_source(root: Path) -> None:
    observed = git(root, "status", "--porcelain=v1", "--untracked-files=all")
    if observed:
        raise StatusError("public status inputs are not a clean committed tree")


def generate(root: Path, branch: str, wip_commit: str | None, wip_committed_at: str | None,
             public_main_commit: str | None) -> dict[str, object]:
    if not BRANCH.fullmatch(branch) or branch.startswith("/") or ".." in branch:
        raise StatusError("invalid source branch")
    if (wip_commit is None) != (wip_committed_at is None):
        raise StatusError("WIP commit and timestamp must be supplied together")

    clean_source(root)
    commit = git(root, "rev-parse", "HEAD")
    tree = git(root, "rev-parse", "HEAD^{tree}")
    committed_at = timestamp(git(root, "show", "-s", "--format=%cI", "HEAD"), "source committedAt")
    if not FULL_HASH.fullmatch(commit) or not FULL_HASH.fullmatch(tree):
        raise StatusError("invalid source Git identity")
    if branch == "main":
        if public_main_commit is None or not FULL_HASH.fullmatch(public_main_commit):
            raise StatusError("public origin/main commit is required for accepted-main")
        if commit != public_main_commit:
            raise StatusError("HEAD does not match public origin/main")

    statuses = parse_backlog(root / "BACKLOG.md")
    ready_ids = sorted(statuses["READY"])
    ready_state = "none" if not ready_ids else "single" if len(ready_ids) == 1 else "multiple"
    classification = "accepted-main" if branch == "main" else "candidate-branch"

    wip: dict[str, object] = {
        "state": "not-observed",
        "classification": "continuity-snapshot-not-accepted-progress",
        "provenance": {"observed": False, "source": "public-remote-ref", "reason": "No public WIP ref was supplied."},
    }
    if wip_commit is not None and wip_committed_at is not None:
        if not FULL_HASH.fullmatch(wip_commit):
            raise StatusError("invalid WIP commit")
        git(root, "cat-file", "-e", f"{wip_commit}^{{commit}}")
        public_wip = git(root, "rev-parse", "refs/remotes/origin/autopilot/live-wip")
        if public_wip != wip_commit:
            raise StatusError("WIP commit does not match the fetched public branch")
        actual_wip_time = git(root, "show", "-s", "--format=%cI", wip_commit)
        if timestamp(wip_committed_at, "WIP committedAt") != actual_wip_time:
            raise StatusError("WIP timestamp does not match the public commit")
        wip = {
            "state": "published",
            "classification": "continuity-snapshot-not-accepted-progress",
            "branch": "autopilot/live-wip",
            "commit": wip_commit,
            "committedAt": actual_wip_time,
            "provenance": {"observed": True, "source": "public-remote-ref", "reason": "Matched fetched autopilot/live-wip ref."},
        }

    candidate_state = "not-observed" if classification == "accepted-main" else "checked-out-candidate"
    candidate_reason = (
        "The Pages build has no authoritative public candidate receipt."
        if candidate_state == "not-observed"
        else "The checked-out branch is a candidate and is not counted as accepted main."
    )
    return {
        "schemaVersion": 2,
        "statusContract": "riftward-public-status-v2",
        "generatedAt": committed_at,
        "freshness": {"basis": "source-commit-time", "sourceCommit": commit},
        "source": {
            "branch": branch,
            "classification": classification,
            "commit": commit,
            "tree": tree,
            "committedAt": committed_at,
            "dirty": False,
        },
        "workItems": {
            "accepted": len(statuses["DONE"]),
            "ready": len(ready_ids),
            "review": len(statuses["REVIEW"]),
            "acceptedTaskIds": sorted(statuses["DONE"]),
            "reviewTaskIds": sorted(statuses["REVIEW"]),
            "nextReady": {"state": ready_state, "taskIds": ready_ids},
        },
        "candidate": {"state": candidate_state, "reason": candidate_reason},
        "wip": wip,
        "claims": {
            "gameplay": False,
            "targetHardwareValidated": False,
            "physicalEdition": False,
            "twentyFourSevenAutonomy": False,
        },
    }


def atomic_json(path: Path, value: dict[str, object]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    payload = json.dumps(value, ensure_ascii=False, indent=2, sort_keys=True) + "\n"
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


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--root", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--branch", required=True)
    parser.add_argument("--wip-commit")
    parser.add_argument("--wip-committed-at")
    parser.add_argument("--public-main-commit")
    args = parser.parse_args()
    try:
        root = args.root.resolve(strict=True)
        status = generate(root, args.branch, args.wip_commit, args.wip_committed_at, args.public_main_commit)
        atomic_json(args.output.resolve(), status)
    except (OSError, StatusError) as exc:
        print(f"Pages-Status abgelehnt: {exc}", file=os.sys.stderr)
        return 2
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
