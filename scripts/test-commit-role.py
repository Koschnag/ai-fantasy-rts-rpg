#!/usr/bin/env python3
"""Hermetic positive and negative checks for new Riftward commit roles."""

from __future__ import annotations

import json
from pathlib import Path
import subprocess
import sys
import tempfile


ROOT = Path(__file__).resolve().parent.parent
CHECKER = ROOT / "scripts/check-commit-role.py"
POLICY = ROOT / ".ai/policies/commit-role-policy.json"
ZERO = "0" * 40
ONE = "1" * 40


def run(*args: str, cwd: Path, check: bool = True, env: dict[str, str] | None = None) -> subprocess.CompletedProcess[str]:
    result = subprocess.run(args, cwd=cwd, env=env, text=True, encoding="utf-8", stdout=subprocess.PIPE, stderr=subprocess.PIPE, check=False)
    if check and result.returncode != 0:
        raise RuntimeError(f"command failed: {' '.join(args)}")
    return result


def commit(repo: Path, name: str, email: str, message: str) -> str:
    (repo / "value.txt").write_text(message + "\n", encoding="utf-8")
    run("git", "add", "--all", cwd=repo)
    run("git", "-c", f"user.name={name}", "-c", f"user.email={email}", "commit", "-m", message, cwd=repo)
    return run("git", "rev-parse", "HEAD", cwd=repo).stdout.strip()


def verify(repo: Path, base: str) -> subprocess.CompletedProcess[str]:
    return run(sys.executable, str(CHECKER), "--root", str(repo), "--policy", str(POLICY), "--base", base, cwd=repo, check=False)


def require(condition: bool, message: str) -> None:
    if not condition:
        raise RuntimeError(message)


def builder_message() -> str:
    return f"build: fixture\n\nAgent-Role: builder\nTask-ID: T-054\nSource-Commit: {ZERO}\nSource-Tree: {ONE}"


def write_public_status(repo: Path, value: str = "candidate") -> str:
    path = repo / ".ai/public-status-v3.json"
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps({"value": value}, sort_keys=True) + "\n", encoding="utf-8")
    return run("git", "hash-object", str(path), cwd=repo).stdout.strip()


def reviewer_message() -> str:
    return (
        "review: fixture\n\n"
        f"Agent-Role: reviewer\nTask-ID: T-054\nSource-Commit: {ZERO}\nSource-Tree: {ONE}\n"
        f"Independent-Review: PASS\nReviewed-Commit: {ZERO}\nReviewed-Tree: {ONE}"
    )


def fixture() -> tuple[tempfile.TemporaryDirectory[str], Path, str]:
    temporary = tempfile.TemporaryDirectory(prefix="riftward-commit-role-")
    repo = Path(temporary.name)
    run("git", "init", "-q", cwd=repo)
    base = commit(repo, "Historical Identity", "historical@example.invalid", "baseline")
    return temporary, repo, base


def main() -> int:
    try:
        policy = json.loads(POLICY.read_text(encoding="utf-8"))
        require(policy.get("history") == "validate-new-commits-only-no-rewrite", "policy history boundary changed")
        require(set(policy.get("roles", {})) == {"planner", "builder", "reviewer", "repair", "wip", "project-lead"}, "role set changed")

        temp, repo, base = fixture()
        with temp:
            commit(repo, "Riftward Builder Autopilot", "riftward-builder-autopilot@users.noreply.github.com", builder_message())
            commit(repo, "Riftward Reviewer Autopilot", "riftward-reviewer-autopilot@users.noreply.github.com", reviewer_message())
            require(verify(repo, base).returncode == 0, "valid builder/reviewer roles rejected")

        temp, repo, base = fixture()
        with temp:
            blob = write_public_status(repo)
            commit(repo, "Riftward Builder Autopilot", "riftward-builder-autopilot@users.noreply.github.com", builder_message() + f"\nPublic-Status-Blob: {blob}")
            require(verify(repo, base).returncode == 0, "valid public status binding rejected")

        temp, repo, base = fixture()
        with temp:
            write_public_status(repo)
            commit(repo, "Riftward Builder Autopilot", "riftward-builder-autopilot@users.noreply.github.com", builder_message())
            require(verify(repo, base).returncode == 2, "unbound public status sidecar accepted")

        temp, repo, base = fixture()
        with temp:
            write_public_status(repo)
            commit(repo, "Riftward Builder Autopilot", "riftward-builder-autopilot@users.noreply.github.com", builder_message() + f"\nPublic-Status-Blob: {ZERO}")
            require(verify(repo, base).returncode == 2, "wrong public status blob accepted")

        temp, repo, base = fixture()
        with temp:
            commit(repo, "Riftward Builder Autopilot", "riftward-builder-autopilot@users.noreply.github.com", builder_message() + f"\nPublic-Status-Blob: {ZERO}")
            require(verify(repo, base).returncode == 2, "unearned public status blob trailer accepted")

        temp, repo, base = fixture()
        with temp:
            commit(repo, "Unknown Bot", "unknown@example.invalid", builder_message())
            require(verify(repo, base).returncode == 2, "unknown identity accepted")

        temp, repo, base = fixture()
        with temp:
            commit(repo, "Riftward Builder Autopilot", "riftward-builder-autopilot@users.noreply.github.com", "build: missing trailers")
            require(verify(repo, base).returncode == 2, "missing trailers accepted")

        temp, repo, base = fixture()
        with temp:
            commit(repo, "Riftward Reviewer Autopilot", "riftward-reviewer-autopilot@users.noreply.github.com", builder_message().replace("builder", "reviewer"))
            require(verify(repo, base).returncode == 2, "reviewer without independent receipt accepted")

        temp, repo, base = fixture()
        with temp:
            bad = builder_message().replace(f"Source-Tree: {ONE}", "Source-Tree: unknown")
            commit(repo, "Riftward Builder Autopilot", "riftward-builder-autopilot@users.noreply.github.com", bad)
            require(verify(repo, base).returncode == 2, "invalid source identity accepted")
    except (OSError, ValueError, json.JSONDecodeError, RuntimeError) as exc:
        print(f"Commitrollen-Test fehlgeschlagen: {exc}", file=sys.stderr)
        return 2
    print("COMMIT_ROLE_POLICY_PASS")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
