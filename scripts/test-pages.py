#!/usr/bin/env python3
"""Dependency-free contract and negative tests for the Riftward Pages artifact."""

from __future__ import annotations

import argparse
from copy import deepcopy
from datetime import datetime, timezone
from hashlib import sha256
from html.parser import HTMLParser
import json
from pathlib import Path
import re
import stat
import sys
import subprocess
import tempfile
from urllib.error import HTTPError, URLError
from urllib.parse import urlparse
from urllib.request import Request, urlopen


FULL_HASH = re.compile(r"^[0-9a-f]{40}$")
TASK_ID = re.compile(r"^T-[0-9]{3,}$")
PLACEHOLDERS = {
    "__RIFTWARD_SOURCE_COMMIT__",
    "__RIFTWARD_SOURCE_TREE__",
    "__RIFTWARD_SOURCE_BRANCH__",
    "__RIFTWARD_SOURCE_CLASSIFICATION__",
}
WIP_BOUNDARY = "Kontinuität, nie akzeptierter Fortschritt"
FRESH_FOR_SECONDS = 1800
OFFLINE_AFTER_SECONDS = 21600
EARLIEST_TRUSTED_TIMESTAMP = datetime(2021, 1, 1, tzinfo=timezone.utc)


class ContractError(RuntimeError):
    pass


class Document(HTMLParser):
    def __init__(self) -> None:
        super().__init__(convert_charrefs=True)
        self.tags: list[tuple[str, dict[str, str]]] = []
        self.ids: set[str] = set()

    def handle_starttag(self, tag: str, attrs: list[tuple[str, str | None]]) -> None:
        values = {key: value or "" for key, value in attrs}
        self.tags.append((tag, values))
        if values.get("id"):
            if values["id"] in self.ids:
                raise ContractError(f"duplicate HTML id {values['id']}")
            self.ids.add(values["id"])


def require(condition: bool, message: str) -> None:
    if not condition:
        raise ContractError(message)


def check_tree_files(root: Path, label: str) -> None:
    for path in root.rglob("*"):
        if path.is_symlink():
            raise ContractError(f"{label} contains symlink: {path.relative_to(root)}")
        if not path.is_dir():
            if not stat.S_ISREG(path.stat().st_mode):
                raise ContractError(f"{label} contains non-regular file: {path.relative_to(root)}")


def parse_html(text: str) -> Document:
    document = Document()
    document.feed(text)
    document.close()
    return document


def meta_values(document: Document, key: str) -> list[str]:
    return [attrs.get("content", "") for tag, attrs in document.tags if tag == "meta" and attrs.get("name") == key]


def local_references(source: Path, document: Document) -> set[str]:
    external: set[str] = set()
    for tag, attrs in document.tags:
        for attribute in ("href", "src", "poster"):
            value = attrs.get(attribute)
            if not value or value.startswith(("mailto:", "tel:")):
                continue
            if value.startswith("#"):
                require(value[1:] in document.ids, f"missing fragment target {value}")
                continue
            parsed = urlparse(value)
            if parsed.scheme in {"http", "https"}:
                external.add(value)
                continue
            require(not parsed.scheme and not parsed.netloc, f"unsupported URL {value}")
            relative = parsed.path
            require(relative and not relative.startswith("/"), f"non-relative local URL {value}")
            require(".." not in Path(relative).parts, f"parent traversal URL {value}")
            require("quarantine" not in Path(relative).parts, f"quarantine reference {value}")
            require((source / relative).is_file(), f"missing local reference {value}")
    return external


def check_html(source: Path, html: str | None = None) -> set[str]:
    html = html if html is not None else (source / "index.html").read_text(encoding="utf-8")
    document = parse_html(html)
    tags = document.tags

    videos = [attrs for tag, attrs in tags if tag == "video"]
    require(all("autoplay" not in video for video in videos), "concept video autoplay is forbidden")
    require("CONCEPT · NOT GAMEPLAY" in html, "visible concept boundary is missing")
    require(WIP_BOUNDARY in html, "WIP non-acceptance boundary is missing")
    require("nie als main-Akzeptanz gezählt" in html and "Nur der exakte, verifizierte" in html, "candidate acceptance caveat is missing")
    require("aria-live=\"polite\"" in html and "id=\"project-status-message\"" in html, "accessible status announcement is missing")
    require("T-010 · walking skeleton" not in html and "9637ec8" not in html, "stale status fallback remains")

    fallback_expectations = {
        "main-short-commit": "UNVERFÜGBAR",
        "main-commit": "UNVERFÜGBAR",
        "main-tree": "UNVERFÜGBAR",
        "observation": "UNVERFÜGBAR",
        "current-task": "UNBEKANNT",
        "current-gate": "UNBEKANNT",
        "autonomy": "UNBEKANNT",
        "accepted-summary": "UNVERFÜGBAR",
        "candidate-summary": "UNVERFÜGBAR",
        "wip-summary": "UNVERFÜGBAR",
        "activity-summary": "UNVERFÜGBAR",
    }
    for binding, expected in fallback_expectations.items():
        values = re.findall(rf'<[^>]+data-bind="{re.escape(binding)}"[^>]*>([^<]*)</[^>]+>', html)
        require(values and all(value.strip() == expected for value in values), f"unsafe fallback for {binding}")

    tablists = [attrs for tag, attrs in tags if attrs.get("role") == "tablist"]
    require(len(tablists) == 1 and tablists[0].get("aria-orientation") == "horizontal", "tablist orientation is missing")
    tabs = [attrs for tag, attrs in tags if attrs.get("role") == "tab"]
    require(len(tabs) >= 2, "tab set is incomplete")
    for tab in tabs:
        require(tab.get("aria-controls") in document.ids and tab.get("id"), "tab relation is incomplete")

    csp = [attrs.get("content", "") for tag, attrs in tags if tag == "meta" and attrs.get("http-equiv", "").lower() == "content-security-policy"]
    require(len(csp) == 1, "exactly one meta CSP is required")
    for directive in ("default-src 'none'", "base-uri 'none'", "form-action 'none'", "object-src 'none'", "script-src 'self'", "connect-src 'self'"):
        require(directive in csp[0], f"CSP directive missing: {directive}")

    names = {attrs.get("name") for tag, attrs in tags if tag == "meta"}
    properties = {attrs.get("property") for tag, attrs in tags if tag == "meta"}
    for name in ("description", "referrer", "twitter:card", *[f"riftward-source-{field}" for field in ("commit", "tree", "branch", "classification")]):
        require(name in names, f"metadata missing: {name}")
    for prop in ("og:type", "og:title", "og:description", "og:url", "og:image", "og:image:alt"):
        require(prop in properties, f"Open Graph metadata missing: {prop}")
    canonicals = [attrs.get("href") for tag, attrs in tags if tag == "link" and attrs.get("rel") == "canonical"]
    require(canonicals == ["https://koschnag.github.io/ai-fantasy-rts-rpg/"], "canonical URL mismatch")

    scripts = [attrs for tag, attrs in tags if tag == "script"]
    require(scripts == [{"src": "showcase.js", "defer": ""}], "only the local deferred script is allowed")
    return local_references(source, document)


def check_css(source: Path, css: str | None = None) -> None:
    css = css if css is not None else (source / "showcase.css").read_text(encoding="utf-8")
    compact = re.sub(r"\s+", "", css)
    require("@media(prefers-reduced-motion:reduce)" in compact, "reduced-motion contract missing")
    require("animation-duration:.001ms!important" in compact and "transition-duration:.001ms!important" in compact, "reduced-motion override missing")
    require(".mastheadnav{display:none}" not in compact, "mobile navigation is hidden")
    require("@media(max-width:620px)" in compact and ".mastheadnav{justify-content:flex-start" in compact, "mobile navigation contract missing")
    require(":focus-visible" in css, "visible keyboard focus contract missing")


def checksum_file(path: Path) -> dict[str, str]:
    values: dict[str, str] = {}
    for line in path.read_text(encoding="utf-8").splitlines():
        match = re.fullmatch(r"([0-9a-f]{64})  (.+)", line)
        require(match is not None, f"malformed checksum line in {path.name}")
        digest, relative = match.groups()
        require(relative not in values, f"duplicate checksum path {relative}")
        values[relative] = digest
    return values


def check_media(source: Path, budget_override: dict[str, object] | None = None) -> None:
    budget = budget_override or json.loads((source / "assets/media-budget.json").read_text(encoding="utf-8"))
    require(set(budget) == {"schemaVersion", "policy", "totalBytesMaximum", "files"} and budget["schemaVersion"] == 1, "invalid media budget")
    files = budget["files"]
    require(isinstance(files, dict) and files, "media budget has no files")
    actual_files = {
        path.relative_to(source).as_posix()
        for directory in (source / "assets/concepts", source / "assets/reel")
        for path in directory.rglob("*")
        if path.is_file()
    }
    require(set(files) == actual_files, "media budget inventory mismatch")
    total = 0
    for relative, maximum in files.items():
        require(isinstance(maximum, int) and maximum > 0, f"invalid media budget for {relative}")
        size = (source / relative).stat().st_size
        require(size <= maximum, f"media budget exceeded: {relative}")
        total += size
    require(total <= budget["totalBytesMaximum"], "total media budget exceeded")

    checksums = checksum_file(source / "assets/media-checksums.sha256")
    require(set(checksums) == actual_files, "media checksum inventory mismatch")
    for relative, digest in checksums.items():
        require(sha256((source / relative).read_bytes()).hexdigest() == digest, f"media checksum mismatch: {relative}")

    media_manifest = json.loads((source / "assets/media-manifest.json").read_text(encoding="utf-8"))
    exports = {item["path"]: item["sha256"] for item in media_manifest.get("exports", [])}
    require(exports == checksums, "media manifest and checksum file disagree")
    require(media_manifest.get("publicationPolicy", {}).get("requiredVisibleLabel") == "CONCEPT · NOT GAMEPLAY", "media claim boundary missing")


def check_source(source: Path) -> set[str]:
    check_tree_files(source, "Pages source")
    required = {
        "index.html", "showcase.css", "showcase.js", "status.schema.json",
        "reconciliation.schema.json", "reconciliation.json",
        "robots.txt", "sitemap.xml", "assets/media-budget.json",
        "assets/media-manifest.json", "assets/media-checksums.sha256",
    }
    for relative in required:
        require((source / relative).is_file(), f"required Pages source missing: {relative}")
    html = (source / "index.html").read_text(encoding="utf-8")
    require({token for token in PLACEHOLDERS if token in html} == PLACEHOLDERS, "source provenance placeholders are incomplete")
    require(all(html.count(token) == 1 for token in PLACEHOLDERS), "source provenance placeholder is ambiguous")
    external = check_html(source, html)
    check_css(source)
    check_media(source)
    script = (source / "showcase.js").read_text(encoding="utf-8")
    require(".catch(" in script and "Status nicht verfügbar" in script, "status fetch does not fail visibly")
    require("continuity-not-accepted-progress" in script, "dynamic WIP boundary is missing")
    require("activeTask" not in script and "walking skeleton" not in script, "hardcoded active task remains")
    require("Sitemap: https://koschnag.github.io/ai-fantasy-rts-rpg/sitemap.xml" in (source / "robots.txt").read_text(encoding="utf-8"), "robots sitemap mismatch")
    require("<loc>https://koschnag.github.io/ai-fantasy-rts-rpg/</loc>" in (source / "sitemap.xml").read_text(encoding="utf-8"), "sitemap canonical mismatch")
    for path in source.rglob("*"):
        require("quarantine" not in path.relative_to(source).parts, f"quarantine file in Pages source: {path}")
        if path.is_file() and path.suffix.lower() in {".html", ".js", ".css", ".json", ".xml", ".txt"}:
            text = path.read_text(encoding="utf-8")
            for pattern in (r"-----BEGIN [A-Z ]*PRIVATE KEY-----", r"\bgh[pousr]_[A-Za-z0-9]{20,}\b", r"\bsk-[A-Za-z0-9_-]{20,}\b"):
                require(re.search(pattern, text) is None, f"secret-like value in {path.relative_to(source)}")
    return external


def parsed_timestamp(value: object, field: str) -> datetime:
    require(isinstance(value, str), f"{field} is not a string")
    try:
        parsed = datetime.fromisoformat(value.replace("Z", "+00:00"))
    except ValueError as exc:
        raise ContractError(f"{field} is not ISO-8601") from exc
    require(parsed.tzinfo is not None, f"{field} has no timezone")
    parsed = parsed.astimezone(timezone.utc)
    require(parsed >= EARLIEST_TRUSTED_TIMESTAMP, f"{field} predates trusted public-status epoch")
    return parsed


def current_timestamp(value: str | None) -> datetime:
    return parsed_timestamp(value, "trusted current time") if value is not None else datetime.now(timezone.utc)


def aware_timestamp(value: object, field: str) -> None:
    parsed_timestamp(value, field)


def schema_matches(value: object, schema: object, field: str = "$", root_schema: dict[str, object] | None = None) -> bool:
    """Small dependency-free Draft-2020 subset used by the checked-in contract."""
    if not isinstance(schema, dict):
        return True
    root_schema = root_schema or schema
    if "$ref" in schema:
        reference = schema["$ref"]
        if not isinstance(reference, str) or not reference.startswith("#/"):
            return False
        target: object = root_schema
        for part in reference[2:].split("/"):
            if not isinstance(target, dict) or part not in target:
                return False
            target = target[part]
        return schema_matches(value, target, field, root_schema)
    if "not" in schema and schema_matches(value, schema["not"], field, root_schema):
        return False
    if "anyOf" in schema and not any(schema_matches(value, child, field, root_schema) for child in schema["anyOf"]):
        return False
    if "const" in schema and value != schema["const"]:
        return False
    if "enum" in schema and value not in schema["enum"]:
        return False
    kind = schema.get("type")
    if kind == "object" and not isinstance(value, dict):
        return False
    if isinstance(value, dict):
        if any(key not in value for key in schema.get("required", [])): return False
        properties = schema.get("properties", {})
        if schema.get("additionalProperties") is False and any(key not in properties for key in value): return False
        if any(key in value and not schema_matches(value[key], child, f"{field}.{key}", root_schema) for key, child in properties.items()): return False
    if kind == "array":
        if not isinstance(value, list): return False
        if schema.get("uniqueItems") and len({json.dumps(item, sort_keys=True) for item in value}) != len(value): return False
        if len(value) < schema.get("minItems", 0): return False
        if len(value) > schema.get("maxItems", sys.maxsize): return False
        if "items" in schema and any(not schema_matches(item, schema["items"], field, root_schema) for item in value): return False
    elif kind == "string":
        if not isinstance(value, str): return False
        if "minLength" in schema and len(value) < schema["minLength"]: return False
        if "pattern" in schema and re.fullmatch(schema["pattern"], value) is None: return False
        if schema.get("format") == "date-time":
            try: parsed_timestamp(value, field)
            except ContractError: return False
    elif kind == "integer" and (not isinstance(value, int) or isinstance(value, bool) or value < schema.get("minimum", -sys.maxsize)):
        return False
    for condition in schema.get("allOf", []):
        if not isinstance(condition, dict): return False
        if "if" not in condition:
            if not schema_matches(value, condition, field, root_schema): return False
            continue
        branch = condition.get("then") if schema_matches(value, condition["if"], field, root_schema) else condition.get("else")
        if branch is not None and not schema_matches(value, branch, field, root_schema): return False
    return True


def validate_checked_in_schema(status: object, schema_path: Path) -> None:
    schema = json.loads(schema_path.read_text(encoding="utf-8"))
    require(schema_matches(status, schema), "status does not validate against checked-in status.schema.json")


def validate_status(status: object, expected_meta: dict[str, str] | None = None, schema_path: Path | None = None,
                    trusted_current_time: str | None = None) -> dict[str, object]:
    if schema_path is not None:
        validate_checked_in_schema(status, schema_path)
    require(isinstance(status, dict), "status root is not an object")
    require(set(status) == {"schemaVersion", "statusContract", "observation", "accepted", "candidates", "continuity", "activity", "tasks", "claims"}, "status root fields mismatch")
    require(status["schemaVersion"] == 3 and status["statusContract"] == "riftward-public-status-v3", "status schema mismatch")
    require(not any(value is None for value in walk(status)), "status contains null instead of an explicit state")

    observation = status["observation"]
    require(isinstance(observation, dict) and set(observation) == {"state", "basis", "observedAtUtc", "freshForSeconds", "offlineAfterSeconds", "sourceCommit", "sourceTree"}, "observation fields mismatch")
    require(observation["state"] in {"current", "stale", "offline", "unknown"} and observation["basis"] == "trusted-main-and-allowlisted-inputs-v1", "observation state/basis mismatch")
    require(observation["freshForSeconds"] == FRESH_FOR_SECONDS and observation["offlineAfterSeconds"] == OFFLINE_AFTER_SECONDS, "observation freshness thresholds mismatch")
    observed_at = parsed_timestamp(observation["observedAtUtc"], "observation.observedAtUtc")
    current = current_timestamp(trusted_current_time)
    age = (current - observed_at).total_seconds()
    require(age >= 0, "observation time is in the future")
    expected_observation = "current" if age <= FRESH_FOR_SECONDS else "stale" if age <= OFFLINE_AFTER_SECONDS else "offline"
    require(observation["state"] == expected_observation, "observation state does not match trusted age")

    accepted = status["accepted"]
    require(isinstance(accepted, dict) and set(accepted) == {"main", "tasks"}, "accepted fields mismatch")
    source = accepted["main"]
    require(isinstance(source, dict) and set(source) == {"branch", "classification", "commit", "tree", "committedAt", "gates"}, "accepted main fields mismatch")
    require(isinstance(source["commit"], str) and FULL_HASH.fullmatch(source["commit"]) is not None, "invalid source commit")
    require(isinstance(source["tree"], str) and FULL_HASH.fullmatch(source["tree"]) is not None, "invalid source tree")
    require(source["branch"] == "main" and source["classification"] == "accepted-main" and source["gates"] in {"passed", "blocked", "unknown"}, "invalid accepted main classification")
    aware_timestamp(source["committedAt"], "source.committedAt")
    require(observation["sourceCommit"] == source["commit"] and observation["sourceTree"] == source["tree"], "observation/source identity mismatch")
    accepted_tasks = accepted["tasks"]
    require(isinstance(accepted_tasks, dict) and set(accepted_tasks) == {"count", "ids"}, "accepted tasks fields mismatch")
    accepted_ids = accepted_tasks["ids"]
    require(isinstance(accepted_ids, list) and len(accepted_ids) == len(set(accepted_ids)) and all(isinstance(item, str) and TASK_ID.fullmatch(item) for item in accepted_ids), "invalid accepted task IDs")
    require(accepted_tasks["count"] == len(accepted_ids), "accepted task count mismatch")

    candidates = status["candidates"]
    require(isinstance(candidates, dict) and set(candidates) == {"state", "items"} and candidates["state"] in {"observed", "not-observed", "unavailable"}, "candidate fields mismatch")
    items = candidates["items"]
    require(isinstance(items, list) and len(items) <= 32, "candidate items mismatch")
    require((candidates["state"] == "observed") == bool(items), "candidate observation/items relation mismatch")
    for item in items:
        require(isinstance(item, dict) and set(item) == {"taskId", "lifecycleStatus", "gate", "blocker"}, "candidate item fields mismatch")
        require(isinstance(item["taskId"], str) and TASK_ID.fullmatch(item["taskId"]) is not None, "candidate task ID mismatch")

    continuity = status["continuity"]
    require(isinstance(continuity, dict) and continuity.get("state") in {"published", "not-observed", "stale", "unavailable"} and continuity.get("classification") == "continuity-not-accepted-progress", "continuity fields mismatch")
    if continuity["state"] == "published":
        require(set(continuity) == {"state", "classification", "commit", "committedAt"} and isinstance(continuity["commit"], str) and FULL_HASH.fullmatch(continuity["commit"]) is not None, "published continuity identity mismatch")
        aware_timestamp(continuity["committedAt"], "continuity.committedAt")
    else:
        require(set(continuity) == {"state", "classification"}, "unpublished continuity leaks details")

    activity = status["activity"]
    detailed_states = {"active", "waiting", "blocked", "idle"}
    details = {"taskId", "phase", "role", "lastGate", "blocker", "autonomy", "parentClass"}
    require(isinstance(activity, dict) and activity.get("state") in detailed_states | {"offline", "unknown"}, "activity state mismatch")
    if activity["state"] in detailed_states:
        require(observation["state"] == "current" and set(activity) == details | {"state"}, "activity details outside current observation")
    else:
        require(set(activity) == {"state"}, "offline/unknown activity leaks details")

    tasks = status["tasks"]
    require(isinstance(tasks, dict) and set(tasks) == {"current", "ready"}, "task fields mismatch")
    ready = tasks["ready"]
    require(isinstance(ready, list) and len(ready) == len(set(ready)) and all(isinstance(item, str) and TASK_ID.fullmatch(item) for item in ready), "invalid ready task IDs")
    current_task = tasks["current"]
    require(isinstance(current_task, dict) and set(current_task) == {"taskId", "lifecycleStatus", "effectiveStartEligibility", "waitingReason", "selectorEnforcement"}, "current task fields mismatch")
    require(current_task["taskId"] == "T-053" and current_task["lifecycleStatus"] == "READY", "frozen T-053 lifecycle changed")
    require(current_task["effectiveStartEligibility"] == "waiting" and current_task["waitingReason"] == "awaiting-preregistered-t042-start-eligibility", "T-053 fail-closed eligibility mismatch")
    require(current_task["selectorEnforcement"] == "pending", "selector enforcement is overstated")

    claims = status["claims"]
    expected_claims = {"gameplay": "graybox-only", "targetHardware": "not-validated", "physicalEdition": "not-produced", "twentyFourSevenAutonomy": "not-demonstrated", "concepts": "not-gameplay"}
    require(claims == expected_claims, "unsupported public claim")
    if expected_meta is not None:
        for field in ("commit", "tree", "branch", "classification"):
            require(source[field] == expected_meta[field], f"HTML/status mismatch: {field}")
    return status


def walk(value: object):
    if isinstance(value, dict):
        for item in value.values():
            yield item
            yield from walk(item)
    elif isinstance(value, list):
        for item in value:
            yield item
            yield from walk(item)


def publication_hashes(root: Path) -> dict[str, str]:
    manifest = root / "publication-hashes.sha256"
    require(manifest.is_file(), "publication hash manifest missing")
    values = checksum_file(manifest)
    actual = {
        path.relative_to(root).as_posix()
        for path in root.rglob("*")
        if path.is_file() and path != manifest
    }
    require(set(values) == actual, "publication hash inventory mismatch")
    for relative, digest in values.items():
        require(sha256((root / relative).read_bytes()).hexdigest() == digest, f"publication hash mismatch: {relative}")
    return values


def check_built(source: Path, built: Path, trusted_current_time: str | None) -> set[str]:
    check_tree_files(built, "Pages artifact")
    require((built / ".nojekyll").is_file() and not (built / "README.md").exists(), "Pages packaging boundary mismatch")
    for path in built.rglob("*"):
        require("quarantine" not in path.relative_to(built).parts, f"quarantine file in Pages artifact: {path}")
    html = (built / "index.html").read_text(encoding="utf-8")
    require(not any(token in html for token in PLACEHOLDERS), "unresolved provenance placeholder")
    document = parse_html(html)
    expected_meta: dict[str, str] = {}
    for field in ("commit", "tree", "branch", "classification"):
        values = meta_values(document, f"riftward-source-{field}")
        require(len(values) == 1, f"built source meta missing: {field}")
        expected_meta[field] = values[0]
    status_path = built / "status.json"
    require((built / "status.svg").is_file() and (built / "task.svg").is_file(), "dynamic status badges are missing")
    status = validate_status(json.loads(status_path.read_text(encoding="utf-8")), expected_meta, source / "status.schema.json", trusted_current_time)
    canonical = json.dumps(status, ensure_ascii=False, indent=2, sort_keys=True) + "\n"
    require(status_path.read_text(encoding="utf-8") == canonical, "status JSON is not deterministic canonical output")
    external = check_html(built, html)
    check_css(built)
    check_media(built)
    publication_hashes(built)
    return external


def expect_failure(name: str, callback) -> None:
    try:
        callback()
    except ContractError:
        return
    raise ContractError(f"negative test did not fail: {name}")


def valid_status_fixture() -> dict[str, object]:
    return {
        "schemaVersion": 3,
        "statusContract": "riftward-public-status-v3",
        "observation": {"state": "current", "basis": "trusted-main-and-allowlisted-inputs-v1", "observedAtUtc": "2026-09-02T12:00:00Z", "freshForSeconds": FRESH_FOR_SECONDS, "offlineAfterSeconds": OFFLINE_AFTER_SECONDS, "sourceCommit": "a" * 40, "sourceTree": "b" * 40},
        "accepted": {"main": {"branch": "main", "classification": "accepted-main", "commit": "a" * 40, "tree": "b" * 40, "committedAt": "2026-01-02T12:00:00Z", "gates": "passed"}, "tasks": {"count": 1, "ids": ["T-001"]}},
        "candidates": {"state": "not-observed", "items": []},
        "continuity": {"state": "not-observed", "classification": "continuity-not-accepted-progress"},
        "activity": {"state": "waiting", "taskId": "T-053", "phase": "waiting", "role": "unknown", "lastGate": "waiting", "blocker": "awaiting-preregistered-t042-start-eligibility", "autonomy": "unknown", "parentClass": "unknown"},
        "tasks": {"current": {"taskId": "T-053", "lifecycleStatus": "READY", "effectiveStartEligibility": "waiting", "waitingReason": "awaiting-preregistered-t042-start-eligibility", "selectorEnforcement": "pending"}, "ready": ["T-053"]},
        "claims": {"gameplay": "graybox-only", "targetHardware": "not-validated", "physicalEdition": "not-produced", "twentyFourSevenAutonomy": "not-demonstrated", "concepts": "not-gameplay"},
    }


def negative_matrix(source: Path) -> None:
    html = (source / "index.html").read_text(encoding="utf-8")
    if "<video " in html:
        expect_failure("autoplay", lambda: check_html(source, html.replace("<video ", "<video autoplay ", 1)))
    expect_failure("stale fallback", lambda: check_html(source, html.replace('data-bind="main-commit">UNVERFÜGBAR', 'data-bind="main-commit">' + "a" * 40, 1)))
    expect_failure("broken local link", lambda: check_html(source, html.replace("</footer>", '<a href="missing.html">x</a></footer>', 1)))
    expect_failure("quarantine reference", lambda: check_html(source, html.replace("showcase.css", "assets/quarantine/private.css", 1)))
    expect_failure("missing status announcement", lambda: check_html(source, html.replace(' aria-live="polite"', "", 1)))
    expect_failure("missing WIP boundary", lambda: check_html(source, html.replace(WIP_BOUNDARY, "WIP snapshot", 1)))
    expect_failure("weak CSP", lambda: check_html(source, html.replace("object-src 'none'; ", "", 1)))

    with tempfile.TemporaryDirectory() as temp:
        temp_root = Path(temp)
        (temp_root / "link").symlink_to(source / "index.html")
        expect_failure("source symlink", lambda: check_tree_files(temp_root, "Pages source"))

    budget = json.loads((source / "assets/media-budget.json").read_text(encoding="utf-8"))
    first = next(iter(budget["files"]))
    budget["files"][first] = (source / first).stat().st_size - 1
    expect_failure("oversized asset", lambda: check_media(source, budget))

    cases: list[tuple[str, dict[str, object]]] = []
    schema_path = source / "status.schema.json"
    validate_checked_in_schema(valid_status_fixture(), schema_path)
    malformed = valid_status_fixture(); malformed["schemaVersion"] = 1; cases.append(("malformed status schema", malformed))
    missing_provenance = valid_status_fixture(); del missing_provenance["accepted"]["main"]["tree"]; cases.append(("missing provenance", missing_provenance))
    invented_active = valid_status_fixture(); invented_active["activeTask"] = {"id": "T-042", "status": "accepted"}; cases.append(("invented active T-042", invented_active))
    accepted_wip = valid_status_fixture(); accepted_wip["continuity"]["classification"] = "accepted-progress"; cases.append(("WIP counted as acceptance", accepted_wip))
    unsupported_claim = valid_status_fixture(); unsupported_claim["claims"]["twentyFourSevenAutonomy"] = "demonstrated"; cases.append(("unsupported 24/7 claim", unsupported_claim))
    null_cost = valid_status_fixture(); null_cost["activity"]["blocker"] = None; cases.append(("null unknown", null_cost))
    duplicate_ids = valid_status_fixture(); duplicate_ids["accepted"]["tasks"] = {"count": 2, "ids": ["T-001", "T-001"]}; cases.append(("duplicate task IDs", duplicate_ids))
    freshness_spoof = valid_status_fixture(); freshness_spoof["observation"]["sourceCommit"] = "c" * 40; cases.append(("freshness source spoof", freshness_spoof))
    stale = valid_status_fixture(); stale["observation"]["observedAtUtc"] = "2026-09-02T11:29:59Z"; cases.append(("stale observation labeled current", stale))
    offline = valid_status_fixture(); offline["observation"]["observedAtUtc"] = "2026-09-02T05:59:59Z"; cases.append(("offline observation labeled current", offline))
    future_current = valid_status_fixture(); future_current["observation"]["observedAtUtc"] = "2026-09-02T12:00:01Z"; cases.append(("future observation time", future_current))
    year_2000 = valid_status_fixture(); year_2000["accepted"]["main"]["committedAt"] = "2000-01-01T00:00:00Z"; cases.append(("year-2000 timestamp", year_2000))
    wip_iff = valid_status_fixture(); wip_iff["continuity"] = {"state": "published", "classification": "continuity-not-accepted-progress"}; cases.append(("published WIP without identity", wip_iff))
    stale_activity = valid_status_fixture(); stale_activity["observation"]["state"] = "stale"; stale_activity["observation"]["observedAtUtc"] = "2026-09-02T11:00:00Z"; cases.append(("stale observation with activity details", stale_activity))
    t053_fail_open = valid_status_fixture(); t053_fail_open["tasks"]["current"]["effectiveStartEligibility"] = "eligible"; t053_fail_open["tasks"]["current"]["waitingReason"] = "none"; cases.append(("T-053 fail-open eligibility", t053_fail_open))
    for name, status in cases:
        expect_failure(name, lambda status=status: validate_status(status, schema_path=schema_path, trusted_current_time="2026-09-02T12:00:00Z"))
    published_schema_only = valid_status_fixture(); published_schema_only["continuity"] = {"state": "published", "classification": "continuity-not-accepted-progress"}
    expect_failure("schema published continuity identity relation", lambda: validate_checked_in_schema(published_schema_only, schema_path))


def browser_validator(source: Path, status: dict[str, object], expected_state: str, trusted_now: str | None = "2026-09-02T12:00:00Z",
                      expected_activity: str = "hidden", age_header: str | None = None) -> None:
    """Run the shipped browser validator with a minimal DOM, not a reimplementation."""
    script = source / "showcase.js"
    payload = json.dumps(status)
    js = r'''
const fs = require("fs"), vm = require("vm");
const status = JSON.parse(process.argv[1]);
const message = {dataset:{}, textContent:""};
const root = {dataset:{}};
const bindings = {};
const nodeFor = (name) => bindings[name] ||= {textContent:"", replaceChildren:()=>{}};
const dateHeader = process.argv[4] === "MISSING" ? null : new Date(process.argv[4]).toUTCString();
const ageHeader = process.argv[6] === "MISSING" ? null : process.argv[6];
global.location = {origin:"https://example.test", href:"https://example.test/index.html"};
global.document = {
  documentElement: root,
  getElementById: (id) => id === "project-status-message" ? message : null,
  createElement: () => ({textContent:""}),
  querySelectorAll: (selector) => {
    const match = selector.match(/^\[data-bind="(.+)"\]$/);
    return match ? [nodeFor(match[1])] : [];
  },
  querySelector: (selector) => {
    if (selector === "#project-status-message") return message;
    const match = selector.match(/^meta\[name="riftward-source-(.+)"\]$/);
    if (match) return {getAttribute: () => status.accepted.main[match[1]]};
    return null;
  }
};
global.fetch = () => Promise.resolve({ok:true, url:"https://example.test/status.json", headers:{get:(name) => name === "Date" ? dateHeader : name === "Age" ? ageHeader : null}, json:() => Promise.resolve(status)});
vm.runInThisContext(fs.readFileSync(process.argv[2], "utf8"));
setImmediate(() => {
  if (message.dataset.state !== process.argv[3]) { console.error(`state=${message.dataset.state || "unset"} date=${dateHeader} observed=${status.observation.observedAtUtc} age=${ageHeader}`); process.exit(2); }
  const detail = nodeFor("activity-detail").textContent;
  if (process.argv[5] === "visible" ? !detail.includes("T-053") : detail !== "Aktivität: nicht verfügbar.") process.exit(3);
});
'''
    result = subprocess.run(["node", "-e", js, payload, str(script), expected_state, trusted_now or "MISSING", expected_activity, age_header or "MISSING"], check=False)
    require(result.returncode == 0, f"browser validator state/detail mismatch (expected {expected_state}, exit {result.returncode})")


def check_browser_validator(source: Path) -> None:
    status = valid_status_fixture()
    browser_validator(source, status, "current", expected_activity="visible")
    duplicate_ready = deepcopy(status); duplicate_ready["tasks"]["ready"] = ["T-053", "T-053"]
    browser_validator(source, duplicate_ready, "unavailable")
    stale = deepcopy(status); stale["observation"]["observedAtUtc"] = "2026-09-02T11:29:59Z"
    browser_validator(source, stale, "stale")
    offline = deepcopy(status); offline["observation"]["observedAtUtc"] = "2026-09-02T05:59:59Z"
    browser_validator(source, offline, "offline")
    future_clock = deepcopy(status); future_clock["observation"]["observedAtUtc"] = "2026-09-02T12:00:01Z"
    browser_validator(source, future_clock, "unavailable")
    published_unobserved = deepcopy(status); published_unobserved["continuity"] = {"state": "published", "classification": "continuity-not-accepted-progress"}
    browser_validator(source, published_unobserved, "unavailable")
    selector_overclaim = deepcopy(status); selector_overclaim["tasks"]["current"]["selectorEnforcement"] = "enforced"
    browser_validator(source, selector_overclaim, "unavailable")
    browser_validator(source, status, "unavailable", trusted_now=None)
    unknown = deepcopy(status); unknown["observation"]["state"] = "unknown"; unknown["activity"] = {"state": "unknown"}
    browser_validator(source, unknown, "unknown")
    malformed_date = deepcopy(status); browser_validator(source, malformed_date, "unavailable", trusted_now="invalid-date")
    cached = deepcopy(status); cached["observation"]["observedAtUtc"] = "2026-09-02T11:00:00Z"
    browser_validator(source, cached, "stale", trusted_now="2026-09-02T11:29:01Z", age_header="120")


def check_external(urls: set[str]) -> None:
    allowed_hosts = {"github.com", "koschnag.github.io"}
    for url in sorted(urls):
        parsed = urlparse(url)
        require(parsed.scheme == "https" and parsed.hostname in allowed_hosts, f"external host is not allowlisted: {url}")
        request = Request(url, headers={"User-Agent": "Riftward-Pages-Link-Check/1"}, method="HEAD")
        try:
            with urlopen(request, timeout=12) as response:
                require(200 <= response.status < 400, f"external link failed: {url}")
        except HTTPError as exc:
            if exc.code == 405:
                request = Request(url, headers={"User-Agent": "Riftward-Pages-Link-Check/1"})
                with urlopen(request, timeout=12) as response:
                    require(200 <= response.status < 400, f"external link failed: {url}")
            else:
                raise ContractError(f"external link failed: {url} ({exc.code})") from exc
        except URLError as exc:
            raise ContractError(f"external link failed: {url}") from exc


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--source", type=Path, required=True)
    parser.add_argument("--built", type=Path)
    parser.add_argument("--external-links", action="store_true")
    parser.add_argument("--trusted-current-time", help="inject trusted current time for deterministic built-artifact checks")
    args = parser.parse_args()
    try:
        source = args.source.resolve(strict=True)
        external = check_source(source)
        negative_matrix(source)
        check_browser_validator(source)
        if args.built is not None:
            external |= check_built(source, args.built.resolve(strict=True), args.trusted_current_time)
        if args.external_links:
            check_external(external)
    except (ContractError, OSError, ValueError, json.JSONDecodeError) as exc:
        print(f"Pages-Vertrag verletzt: {exc}", file=sys.stderr)
        return 2
    print("PAGES_CONTRACT_PASS")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
