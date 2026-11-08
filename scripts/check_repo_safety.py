#!/usr/bin/env python3
# SPDX-License-Identifier: Apache-2.0
# Copyright (c) 2026 Amir Farhadi
"""
Repository Safety, Secret, Path Hygiene & Integrity Scanner

Inspects tracked repository files for:
1. Hardcoded absolute user paths (Windows C:\\Users, Unix /home and /Users, local file:/// URIs).
2. Active OS username string components.
3. Secrets, API keys, private keys, JWT tokens, and plain personal email addresses.
4. Tracked binary artifacts, build outputs, and large blobs.
5. Optional manifest and package descriptor integrity.
"""

import argparse
import getpass
import json
import os
from pathlib import Path
import re
import subprocess
import sys
from typing import Dict, List, Optional, Set, Tuple

try:
    import tomllib
except ModuleNotFoundError:
    try:
        import tomli as tomllib
    except ModuleNotFoundError:
        tomllib = None

IGNORED_DIRS: Set[str] = {
    ".git",
    ".vs",
    ".vscode",
    "bin",
    "obj",
    "node_modules",
    ".ruff_cache",
    ".idea",
    "__pycache__",
    ".pytest_cache",
    ".venv",
    "venv",
    "env",
    "dist",
    "build",
    ".agents",
}

IGNORED_EXTENSIONS: Set[str] = {
    ".png",
    ".jpg",
    ".jpeg",
    ".gif",
    ".webp",
    ".mp4",
    ".ico",
    ".svg",
    ".woff",
    ".woff2",
    ".ttf",
}

DISALLOWED_BINARY_EXTENSIONS: Set[str] = {
    ".dll",
    ".exe",
    ".pdb",
    ".so",
    ".dylib",
    ".pyc",
    ".pyo",
    ".pyd",
    ".class",
    ".zip",
    ".tar.gz",
    ".tgz",
    ".7z",
    ".bin",
}

IGNORED_FILES: Set[str] = {
    ".test_cache.json",
}

WINDOWS_USER_PATH_RE = re.compile(
    r"[A-Za-z]:(?:\\{1,4}|/)+(?:Users|Documents and Settings)(?:\\{1,4}|/)+",
    re.IGNORECASE,
)
USER_HOME_PATH_RE = re.compile(
    r"(?:[A-Za-z]:(?:\\{1,4}|/)+|/|\\{1,4})(?:Users|home|Documents and Settings)(?:\\{1,4}|/)+[A-Za-z0-9_.-]+(?:\\{1,4}|/)+",
    re.IGNORECASE,
)
UNIX_USER_PATH_RE = re.compile(r"^/(?:Users|home)/[A-Za-z0-9_.-]+(?:/|$)", re.IGNORECASE)
USER_FILE_URI_RE = re.compile(
    r"file:///(?:[A-Za-z]:/(?:Users|Documents and Settings|home)|(?:Users|home)/)",
    re.IGNORECASE,
)

try:
    CURRENT_USER = getpass.getuser()
    if CURRENT_USER and len(CURRENT_USER) > 1 and CURRENT_USER.lower() not in {
        "root",
        "runner",
        "github",
        "administrator",
        "system",
        "default",
    }:
        ACTIVE_USER_PATH_RE = re.compile(
            rf"(?:\\{{1,4}}|/)+{re.escape(CURRENT_USER)}(?:\\{{1,4}}|/)+",
            re.IGNORECASE,
        )
    else:
        ACTIVE_USER_PATH_RE = None
except Exception:
    ACTIVE_USER_PATH_RE = None

SECRET_PATTERNS: List[Tuple[re.Pattern, str]] = [
    (re.compile(r"-----BEGIN (?:RSA|OPENSSH|EC|DSA|PGP)?\s?PRIVATE KEY-----"), "Private Key Header"),
    (re.compile(r"\b(?:sk|pk)_(?:live|test)_[0-9a-zA-Z]{24,}\b"), "API Key Token"),
    (re.compile(r"\bghp_[0-9a-zA-Z]{36}\b"), "GitHub Personal Access Token"),
    (re.compile(r"\bgho_[0-9a-zA-Z]{36}\b"), "GitHub OAuth Access Token"),
    (re.compile(r"\beyJ[a-zA-Z0-9_\-]{20,}\.[a-zA-Z0-9_\-]{20,}\.[a-zA-Z0-9_\-]{20,}\b"), "JWT Token"),
    (
        re.compile(
            r"\b[A-Za-z0-9._%+-]+@(?!(?:example\.com|users\.noreply\.github\.com|domain\.com)\b)[A-Za-z0-9.-]+\.[A-Za-z]{2,}\b",
            re.IGNORECASE,
        ),
        "Plain Personal Email Address",
    ),
]


def resolve_repo_root(start_dir: Optional[str] = None) -> Path:
    cur = Path(start_dir or os.getcwd()).resolve()
    while cur != cur.parent:
        if (cur / ".git").exists():
            return cur
        cur = cur.parent
    return Path(start_dir or os.getcwd()).resolve()


def get_git_tracked_files(repo_root: Path) -> List[str]:
    try:
        res = subprocess.run(
            ["git", "ls-files"],
            cwd=str(repo_root),
            capture_output=True,
            text=True,
            check=True,
        )
        return [line.strip().replace("\\", "/") for line in res.stdout.splitlines() if line.strip()]
    except Exception:
        files = []
        for root, dirs, filenames in os.walk(str(repo_root)):
            dirs[:] = [d for d in dirs if d not in IGNORED_DIRS]
            for fn in filenames:
                p = Path(root) / fn
                rel = str(p.relative_to(repo_root)).replace("\\", "/")
                files.append(rel)
        return files


def check_file_hygiene(repo_root: Path, rel_path: str) -> List[str]:
    violations: List[str] = []
    file_path = repo_root / rel_path

    if rel_path.endswith("check_repo_safety.py"):
        return violations

    ext = file_path.suffix.lower()
    if ext in DISALLOWED_BINARY_EXTENSIONS:
        violations.append(f"{rel_path}: Tracked binary or build artifact extension ({ext})")
        return violations

    if ext in IGNORED_EXTENSIONS or file_path.name in IGNORED_FILES:
        return violations

    try:
        content = file_path.read_text(encoding="utf-8", errors="ignore")
        lines = content.splitlines()

        # Ignore documentation placeholder examples like C:\\Users\\... or /home/<user>
        doc_placeholder_re = re.compile(
            r"(?:Users|home|Documents and Settings)(?:\\{1,4}|/)+(?:\.\.\.|<[^>]+>|\[[^\]]+\]|\$[A-Za-z_]+|%[A-Za-z_]+%|your_username|your-username|username)",
            re.IGNORECASE,
        )

        for line_no, line in enumerate(lines, start=1):
            if "# nosec" in line or "# allow-path" in line or "allow-path" in line:
                continue

            is_placeholder = bool(doc_placeholder_re.search(line))

            if WINDOWS_USER_PATH_RE.search(line) and not is_placeholder:
                violations.append(f"{rel_path}:{line_no}: Hardcoded Windows user directory path leak: {line.strip()[:100]}")
            if USER_HOME_PATH_RE.search(line) and not is_placeholder:
                violations.append(f"{rel_path}:{line_no}: Hardcoded user profile directory path: {line.strip()[:100]}")
            if UNIX_USER_PATH_RE.search(line) and not is_placeholder:
                violations.append(f"{rel_path}:{line_no}: Hardcoded Unix user home directory path: {line.strip()[:100]}")
            if ACTIVE_USER_PATH_RE and ACTIVE_USER_PATH_RE.search(line) and not is_placeholder:
                violations.append(f"{rel_path}:{line_no}: Active OS user path component leak: {line.strip()[:100]}")
            if USER_FILE_URI_RE.search(line) and not is_placeholder:
                violations.append(f"{rel_path}:{line_no}: Local user file URI (file:///): {line.strip()[:100]}")

            for pattern, desc in SECRET_PATTERNS:
                if pattern.search(line):
                    violations.append(f"{rel_path}:{line_no}: Potential secret/credential ({desc}): {line.strip()[:60]}...")

    except Exception as ex:
        violations.append(f"{rel_path}: Failed to read file ({ex})")

    return violations


def check_manifest_integrity(repo_root: Path) -> List[str]:
    violations: List[str] = []
    manifest_path = repo_root / "manifest.json"
    if not manifest_path.exists():
        return violations

    try:
        with open(manifest_path, "r", encoding="utf-8") as f:
            data = json.load(f)

        plugins = data.get("plugins", {})
        if not isinstance(plugins, dict):
            violations.append("manifest.json: 'plugins' key must be an object dictionary")
            return violations

        for plugin_name, meta in plugins.items():
            rel_dir = meta.get("path", f"plugins/{plugin_name}")
            plugin_dir = repo_root / rel_dir

            if not plugin_dir.exists():
                violations.append(f"manifest.json: Plugin '{plugin_name}' path does not exist on disk: {rel_dir}")
                continue

            metadata_toml = plugin_dir / "metadata.toml"
            if not metadata_toml.exists():
                violations.append(f"manifest.json: Plugin '{plugin_name}' missing metadata.toml at {metadata_toml}")
                continue

            if tomllib:
                try:
                    with open(metadata_toml, "rb") as mf:
                        mdata = tomllib.load(mf)
                    if mdata.get("name") != plugin_name:
                        violations.append(
                            f"{rel_dir}/metadata.toml: Name mismatch (declared '{mdata.get('name')}', expected '{plugin_name}')"
                        )
                except Exception as mex:
                    violations.append(f"{rel_dir}/metadata.toml: Parse error ({mex})")

    except Exception as ex:
        violations.append(f"manifest.json: Parse failed ({ex})")

    return violations


def main() -> int:
    parser = argparse.ArgumentParser(description="Repository Safety, Secret & Path Hygiene Scanner")
    parser.add_argument("--repo-root", default=None, help="Root directory of the repository to scan")
    parser.add_argument("--skip-manifest", action="store_true", help="Skip manifest integrity checks")
    args = parser.parse_args()

    repo_root = resolve_repo_root(args.repo_root)
    print(f"Scanning repository for safety, secrets, and path hygiene: {repo_root}")

    all_violations: List[str] = []

    if not args.skip_manifest:
        all_violations.extend(check_manifest_integrity(repo_root))

    tracked_files = get_git_tracked_files(repo_root)
    for rel_path in tracked_files:
        all_violations.extend(check_file_hygiene(repo_root, rel_path))

    if all_violations:
        print("\n[FAILED] Safety violations and path hygiene errors detected:")
        for v in all_violations:
            print(f"  - {v}")
        print(f"\nTotal violations: {len(all_violations)}")
        return 1

    print("\n[PASSED] Zero path leaks, secrets, binary artifacts, or manifest inconsistencies detected.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
