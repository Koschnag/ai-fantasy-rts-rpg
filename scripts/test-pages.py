#!/usr/bin/env python3
"""Dependency-free contract and negative tests for the Riftward Pages artifact."""

from __future__ import annotations

import argparse
from copy import deepcopy
from datetime import datetime
from hashlib import sha256
from html.parser import HTMLParser
import json
from pathlib import Path
import re
import sys
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
WIP_BOUNDARY = "Kontinuitätssnapshot, kein akzeptierter Fortschritt"


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
    require(videos and all("autoplay" not in video for video in videos), "concept video autoplay is forbidden")
    require("CONCEPT · NOT GAMEPLAY" in html, "visible concept boundary is missing")
    require(WIP_BOUNDARY in html, "WIP non-acceptance boundary is missing")
    require("aria-live=\"polite\"" in html and "id=\"project-status-message\"" in html, "accessible status announcement is missing")
    require("T-010 · walking skeleton" not in html and "9637ec8" not in html, "stale status fallback remains")

    fallback_expectations = {
        "accepted": "—",
        "review": "—",
        "ready": "—",
        "shortCommit": "—",
        "nextReady": "Nicht geladen",
        "committedAt": "Nicht geladen",
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
    for name in ("description", "referrer", "twitter:card", "twitter:title", "twitter:description", "twitter:image", *[f"riftward-source-{field}" for field in ("commit", "tree", "branch", "classification")]):
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
    require(".artifact-panel{animation:none}" in compact, "reduced-motion animation override missing")
    require(".mastheadnav{display:none}" not in compact, "mobile navigation is hidden")
    require("@media(max-width:980px)" in compact and ".mastheadnav{display:flex" in compact, "mobile navigation contract missing")
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
    required = {
        "index.html", "showcase.css", "showcase.js", "status.schema.json",
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
    require(".catch(setUnavailable)" in script and "Status nicht verfügbar" in script, "status fetch does not fail visibly")
    require(WIP_BOUNDARY in script, "dynamic WIP boundary is missing")
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


def aware_timestamp(value: object, field: str) -> None:
    require(isinstance(value, str), f"{field} is not a string")
    try:
        parsed = datetime.fromisoformat(value.replace("Z", "+00:00"))
    except ValueError as exc:
        raise ContractError(f"{field} is not ISO-8601") from exc
    require(parsed.tzinfo is not None, f"{field} has no timezone")


def validate_status(status: object, expected_meta: dict[str, str] | None = None) -> dict[str, object]:
    require(isinstance(status, dict), "status root is not an object")
    require(set(status) == {"schemaVersion", "statusContract", "source", "workItems", "candidate", "wip", "claims"}, "status root fields mismatch")
    require(status["schemaVersion"] == 2 and status["statusContract"] == "riftward-public-status-v2", "status schema mismatch")
    require(not any(value is None for value in walk(status)), "status contains null instead of an explicit state")

    source = status["source"]
    require(isinstance(source, dict) and set(source) == {"branch", "classification", "commit", "tree", "committedAt", "dirty"}, "source fields mismatch")
    require(isinstance(source["commit"], str) and FULL_HASH.fullmatch(source["commit"]) is not None, "invalid source commit")
    require(isinstance(source["tree"], str) and FULL_HASH.fullmatch(source["tree"]) is not None, "invalid source tree")
    require(isinstance(source["branch"], str) and re.fullmatch(r"[A-Za-z0-9._/-]{1,200}", source["branch"]) is not None, "invalid source branch")
    require(source["classification"] in {"accepted-main", "candidate-branch"} and source["dirty"] is False, "invalid source classification")
    require((source["branch"] == "main") == (source["classification"] == "accepted-main"), "main classification mismatch")
    aware_timestamp(source["committedAt"], "source.committedAt")

    work = status["workItems"]
    require(isinstance(work, dict) and set(work) == {"accepted", "ready", "review", "nextReady"}, "work item fields mismatch")
    require(all(isinstance(work[name], int) and not isinstance(work[name], bool) and work[name] >= 0 for name in ("accepted", "ready", "review")), "invalid work item counter")
    next_ready = work["nextReady"]
    require(isinstance(next_ready, dict) and set(next_ready) == {"state", "taskIds"}, "next-ready fields mismatch")
    task_ids = next_ready["taskIds"]
    require(isinstance(task_ids, list) and len(task_ids) == len(set(task_ids)) and all(isinstance(item, str) and TASK_ID.fullmatch(item) for item in task_ids), "invalid ready task IDs")
    expected_state = "none" if not task_ids else "single" if len(task_ids) == 1 else "multiple"
    require(next_ready["state"] == expected_state and work["ready"] == len(task_ids), "ready state/count mismatch")

    candidate = status["candidate"]
    require(isinstance(candidate, dict) and set(candidate) == {"state", "reason"} and isinstance(candidate["reason"], str) and candidate["reason"], "candidate fields mismatch")
    expected_candidate = "not-observed" if source["classification"] == "accepted-main" else "checked-out-candidate"
    require(candidate["state"] == expected_candidate, "candidate state mismatch")

    wip = status["wip"]
    require(isinstance(wip, dict) and wip.get("state") in {"published", "not-observed"}, "invalid WIP state")
    require(wip.get("classification") == "continuity-snapshot-not-accepted-progress", "WIP acceptance boundary mismatch")
    if wip["state"] == "published":
        require(set(wip) == {"state", "classification", "branch", "commit", "committedAt"}, "published WIP fields mismatch")
        require(wip["branch"] == "autopilot/live-wip" and isinstance(wip["commit"], str) and FULL_HASH.fullmatch(wip["commit"]) is not None, "invalid WIP identity")
        aware_timestamp(wip["committedAt"], "wip.committedAt")
    else:
        require(set(wip) == {"state", "classification"}, "unobserved WIP must not contain invented provenance")

    claims = status["claims"]
    expected_claims = {"gameplay": False, "targetHardwareValidated": False, "physicalEdition": False, "twentyFourSevenAutonomy": False}
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


def check_built(source: Path, built: Path) -> set[str]:
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
    status = validate_status(json.loads(status_path.read_text(encoding="utf-8")), expected_meta)
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
        "schemaVersion": 2,
        "statusContract": "riftward-public-status-v2",
        "source": {"branch": "main", "classification": "accepted-main", "commit": "a" * 40, "tree": "b" * 40, "committedAt": "2026-09-02T12:00:00+00:00", "dirty": False},
        "workItems": {"accepted": 1, "ready": 0, "review": 1, "nextReady": {"state": "none", "taskIds": []}},
        "candidate": {"state": "not-observed", "reason": "not observed"},
        "wip": {"state": "not-observed", "classification": "continuity-snapshot-not-accepted-progress"},
        "claims": {"gameplay": False, "targetHardwareValidated": False, "physicalEdition": False, "twentyFourSevenAutonomy": False},
    }


def negative_matrix(source: Path) -> None:
    html = (source / "index.html").read_text(encoding="utf-8")
    expect_failure("autoplay", lambda: check_html(source, html.replace("<video controls", "<video autoplay controls", 1)))
    expect_failure("stale fallback", lambda: check_html(source, html.replace('data-bind="accepted">—', 'data-bind="accepted">7', 1)))
    expect_failure("broken local link", lambda: check_html(source, html.replace("</footer>", '<a href="missing.html">x</a></footer>', 1)))
    expect_failure("quarantine reference", lambda: check_html(source, html.replace("showcase.css", "assets/quarantine/private.css", 1)))
    expect_failure("missing status announcement", lambda: check_html(source, html.replace(' aria-live="polite"', "", 1)))
    expect_failure("missing WIP boundary", lambda: check_html(source, html.replace(WIP_BOUNDARY, "WIP snapshot", 1)))
    expect_failure("weak CSP", lambda: check_html(source, html.replace("object-src 'none'; ", "", 1)))

    budget = json.loads((source / "assets/media-budget.json").read_text(encoding="utf-8"))
    first = next(iter(budget["files"]))
    budget["files"][first] = (source / first).stat().st_size - 1
    expect_failure("oversized asset", lambda: check_media(source, budget))

    cases: list[tuple[str, dict[str, object]]] = []
    malformed = valid_status_fixture(); malformed["schemaVersion"] = 1; cases.append(("malformed status schema", malformed))
    missing_provenance = valid_status_fixture(); del missing_provenance["source"]["tree"]; cases.append(("missing provenance", missing_provenance))
    invented_active = valid_status_fixture(); invented_active["activeTask"] = {"id": "T-042", "status": "accepted"}; cases.append(("invented active T-042", invented_active))
    accepted_wip = valid_status_fixture(); accepted_wip["wip"]["classification"] = "accepted-progress"; cases.append(("WIP counted as acceptance", accepted_wip))
    unsupported_claim = valid_status_fixture(); unsupported_claim["claims"]["twentyFourSevenAutonomy"] = True; cases.append(("unsupported 24/7 claim", unsupported_claim))
    null_cost = valid_status_fixture(); null_cost["candidate"]["reason"] = None; cases.append(("null unknown", null_cost))
    for name, status in cases:
        expect_failure(name, lambda status=status: validate_status(status))


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
    args = parser.parse_args()
    try:
        source = args.source.resolve(strict=True)
        external = check_source(source)
        negative_matrix(source)
        if args.built is not None:
            external |= check_built(source, args.built.resolve(strict=True))
        if args.external_links:
            check_external(external)
    except (ContractError, OSError, ValueError, json.JSONDecodeError) as exc:
        print(f"Pages-Vertrag verletzt: {exc}", file=sys.stderr)
        return 2
    print("PAGES_CONTRACT_PASS")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
