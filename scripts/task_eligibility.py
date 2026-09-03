#!/usr/bin/env python3
"""Derive task start eligibility without mutating frozen task manifests.

This is a shared decision function, not proof that an external selector enforces it.
"""

from __future__ import annotations

import argparse
import json
from pathlib import Path
import re
import sys


TASK = re.compile(r"^T-[0-9]{3}$")
STATUS = {"draft": "DRAFT", "ready": "READY", "running": "IN_PROGRESS", "review": "REVIEW", "accepted": "DONE", "blocked": "BLOCKED", "cancelled": "UNKNOWN"}


class EligibilityError(RuntimeError):
    pass


def load_task(path: Path) -> dict[str, object]:
    value = json.loads(path.read_text(encoding="utf-8"))
    if not isinstance(value, dict) or not isinstance(value.get("id"), str) or not TASK.fullmatch(value["id"]):
        raise EligibilityError("invalid task manifest")
    if value.get("status") not in STATUS:
        raise EligibilityError("invalid task lifecycle status")
    return value


def dependency_ready(root: Path, task: dict[str, object]) -> bool:
    dependencies = task.get("dependencies", [])
    if not isinstance(dependencies, list) or any(not isinstance(item, str) or not TASK.fullmatch(item) for item in dependencies):
        raise EligibilityError("invalid task dependencies")
    manifests = list((root / ".ai/tasks").glob("T-*.json"))
    by_id = {item["id"]: item for item in (load_task(path) for path in manifests)}
    return all(dependency in by_id and by_id[dependency]["status"] == "accepted" for dependency in dependencies)


def evaluate(root: Path, task_id: str) -> dict[str, str]:
    matches = list((root / ".ai/tasks").glob(f"{task_id}-*.json"))
    if len(matches) != 1:
        raise EligibilityError(f"expected exactly one manifest for {task_id}")
    task = load_task(matches[0])
    lifecycle = STATUS[str(task["status"])]
    if task_id == "T-053":
        if task["status"] != "ready":
            raise EligibilityError("frozen T-053 lifecycle must remain ready")
        targets = list((root / ".ai/tasks").glob("T-042-*.json"))
        if len(targets) != 1:
            return {"taskId": task_id, "lifecycleStatus": lifecycle, "effectiveStartEligibility": "waiting", "waitingReason": "awaiting-preregistered-t042-start-eligibility", "selectorEnforcement": "pending"}
        target = load_task(targets[0])
        if target["status"] != "ready" or not dependency_ready(root, target):
            return {"taskId": task_id, "lifecycleStatus": lifecycle, "effectiveStartEligibility": "waiting", "waitingReason": "awaiting-preregistered-t042-start-eligibility", "selectorEnforcement": "pending"}
        return {"taskId": task_id, "lifecycleStatus": lifecycle, "effectiveStartEligibility": "eligible", "waitingReason": "none", "selectorEnforcement": "pending"}
    if task["status"] == "ready" and dependency_ready(root, task):
        return {"taskId": task_id, "lifecycleStatus": lifecycle, "effectiveStartEligibility": "eligible", "waitingReason": "none", "selectorEnforcement": "pending"}
    if task["status"] == "blocked":
        return {"taskId": task_id, "lifecycleStatus": lifecycle, "effectiveStartEligibility": "blocked", "waitingReason": "blocked", "selectorEnforcement": "pending"}
    return {"taskId": task_id, "lifecycleStatus": lifecycle, "effectiveStartEligibility": "waiting", "waitingReason": "awaiting-review", "selectorEnforcement": "pending"}


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--root", type=Path, required=True)
    parser.add_argument("--task", required=True)
    args = parser.parse_args()
    try:
        root = args.root.resolve(strict=True)
        if not TASK.fullmatch(args.task):
            raise EligibilityError("invalid task id")
        print(json.dumps(evaluate(root, args.task), sort_keys=True, separators=(",", ":")))
    except (OSError, ValueError, json.JSONDecodeError, EligibilityError) as exc:
        print(f"Startberechtigung unbekannt: {exc}", file=sys.stderr)
        return 2
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
