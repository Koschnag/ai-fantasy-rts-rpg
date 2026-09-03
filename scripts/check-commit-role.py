#!/usr/bin/env python3
"""Validate only newly introduced commit identities and closed provenance trailers."""

from __future__ import annotations

import argparse
import json
from pathlib import Path
import re
import subprocess
import sys


SHA = re.compile(r"^[0-9a-f]{40}$")
TASK = re.compile(r"^T-[0-9]{3}$")
PUBLIC_STATUS = ".ai/public-status-v3.json"


def fail(message: str) -> None:
    raise RuntimeError(message)


def run(root: Path, *args: str) -> str:
    result = subprocess.run(["git", "-C", str(root), *args], text=True, encoding="utf-8", stdout=subprocess.PIPE, stderr=subprocess.PIPE, check=False)
    if result.returncode != 0:
        fail("unable to inspect new commit range")
    return result.stdout


def try_run(root: Path, *args: str) -> subprocess.CompletedProcess[str]:
    return subprocess.run(
        ["git", "-C", str(root), *args],
        text=True,
        encoding="utf-8",
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        check=False,
    )


def trailers(body: str) -> dict[str, str]:
    result: dict[str, str] = {}
    for line in body.splitlines():
        match = re.fullmatch(r"([A-Za-z][A-Za-z-]*): (.+)", line)
        if match:
            if match.group(1) in result:
                fail("duplicate commit trailer")
            result[match.group(1)] = match.group(2)
    return result


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--root", type=Path, required=True)
    parser.add_argument("--policy", type=Path, required=True)
    parser.add_argument("--base", required=True)
    parser.add_argument("--head", default="HEAD")
    args = parser.parse_args()
    try:
        root = args.root.resolve(strict=True)
        policy = json.loads(args.policy.resolve(strict=True).read_text(encoding="utf-8"))
        if set(policy) != {"$schema", "schemaVersion", "policy", "history", "roles"} or policy["schemaVersion"] != 1 or policy["history"] != "validate-new-commits-only-no-rewrite":
            fail("invalid commit role policy")
        roles = policy["roles"]
        base = run(root, "merge-base", args.base, args.head).strip()
        if not SHA.fullmatch(base):
            fail("invalid commit policy baseline")
        records = run(root, "log", "--reverse", "--format=%H%x00%an%x00%ae%x00%B%x00%x1e", f"{base}..{args.head}")
        for record in records.split("\x1e"):
            fields = record.strip("\n\x00").split("\x00", 3)
            if len(fields) != 4:
                continue
            commit, name, email, body = fields
            matches = [(role, value) for role, value in roles.items() if value.get("name") == name and value.get("email") == email]
            if len(matches) != 1:
                fail(f"unrecognized commit identity at {commit[:12]}")
            role, identity = matches[0]
            found = trailers(body)
            if found.get("Agent-Role") != role or not TASK.fullmatch(found.get("Task-ID", "")):
                fail(f"commit role/task trailer mismatch at {commit[:12]}")
            for key in identity["requiredTrailers"]:
                if key not in found:
                    fail(f"missing required commit trailer at {commit[:12]}")
            if not SHA.fullmatch(found.get("Source-Commit", "")) or not SHA.fullmatch(found.get("Source-Tree", "")):
                fail(f"invalid source identity trailer at {commit[:12]}")
            relation = run(root, "rev-list", "--parents", "-n", "1", commit).split()
            if len(relation) != 2 or relation[0] != commit:
                fail(f"new commit is not a single-parent checkpoint at {commit[:12]}")
            parent = relation[1]
            parent_tree = run(root, "rev-parse", f"{parent}^{{tree}}").strip()
            if found.get("Source-Commit") != parent or found.get("Source-Tree") != parent_tree:
                fail(f"source parent/tree binding mismatch at {commit[:12]}")
            if role == "reviewer" and (
                found.get("Independent-Review") not in {"PASS", "ESCALATE", "BLOCK"}
                or found.get("Reviewed-Commit") != parent
                or found.get("Reviewed-Tree") != parent_tree
            ):
                fail(f"reviewer provenance missing or not bound to its parent at {commit[:12]}")
            changed = set(run(root, "diff-tree", "--root", "--no-commit-id", "--name-only", "-r", commit).splitlines())
            status_trailer = found.get("Public-Status-Blob")
            if PUBLIC_STATUS in changed:
                if not SHA.fullmatch(status_trailer or ""):
                    fail(f"public status blob trailer missing at {commit[:12]}")
                blob = try_run(root, "rev-parse", f"{commit}:{PUBLIC_STATUS}")
                if blob.returncode != 0 or not SHA.fullmatch(blob.stdout.strip()):
                    fail(f"public status sidecar deleted or unreadable at {commit[:12]}")
                if status_trailer != blob.stdout.strip():
                    fail(f"public status blob trailer mismatch at {commit[:12]}")
            elif status_trailer is not None:
                fail(f"unexpected public status blob trailer at {commit[:12]}")
    except (OSError, ValueError, json.JSONDecodeError, RuntimeError) as exc:
        print(f"Commitrollen abgelehnt: {exc}", file=sys.stderr)
        return 2
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
