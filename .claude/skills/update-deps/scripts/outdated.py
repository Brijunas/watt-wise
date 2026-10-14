#!/usr/bin/env python3
"""Read-only survey of every dependency source the update-deps skill covers.

Usage: python3 .claude/skills/update-deps/scripts/outdated.py   (from the repo root)

Prints one table per source: current -> newest allowed version, with MAJOR
flagged and releases younger than MIN_AGE marked "too new". Changes nothing.

Rules it applies (the skill explains why):
- NuGet: a package on a prerelease stays on the newest prerelease or stable
  of any version; a package on a stable version moves to the newest stable.
- .NET SDK: newest SDK of the major in global.json, from the official feed.
- Docker: postgres stays on its major and suffix (e.g. 18.x-trixie);
  the other images move to their newest plain x.y[.z] tag.
- pnpm and mise report through their own commands.
"""

import gzip
import json
import re
import subprocess
import sys
import urllib.request
from concurrent.futures import ThreadPoolExecutor
from datetime import datetime, timedelta, timezone
from pathlib import Path

MIN_AGE = timedelta(days=1)
NOW = datetime.now(timezone.utc)
ROOT = Path.cwd()


def fetch_json(url):
    with urllib.request.urlopen(url, timeout=30) as resp:
        body = resp.read()
    if resp.headers.get("Content-Encoding") == "gzip":
        body = gzip.decompress(body)
    return json.loads(body)


def version_key(v):
    """Sort key for SemVer-like versions ("11.0.0-rc.1.26425.128", "18.6")."""
    core, _, pre = v.partition("-")
    nums = tuple(int(p) if p.isdigit() else 0 for p in core.split("."))
    nums += (0,) * (4 - len(nums))
    if not pre:
        return nums + (1,)
    parts = tuple((0, int(p), "") if p.isdigit() else (1, 0, p) for p in re.split(r"[.]", pre))
    return nums + (0,) + parts


def major(v):
    return v.split(".")[0]


def is_pre(v):
    return "-" in v


def age_note(published):
    if not published:
        return ""
    when = datetime.fromisoformat(published.replace("Z", "+00:00"))
    return "too new" if NOW - when < MIN_AGE else ""


def print_table(title, rows):
    print(f"\n## {title}")
    if not rows:
        print("(everything is current)")
        return
    widths = [max(len(str(r[i])) for r in rows) for i in range(len(rows[0]))]
    for r in rows:
        print("  ".join(str(c).ljust(w) for c, w in zip(r, widths)).rstrip())


def nuget_row(pkg, cur):
    # The gz-semver2 hive is the only one that lists SemVer 2 versions such as
    # 11.0.0-rc.2.26504.105; the plain hives silently leave them out.
    reg = fetch_json(f"https://api.nuget.org/v3/registration5-gz-semver2/{pkg.lower()}/index.json")
    published = {}
    for page in reg["items"]:
        items = page.get("items") or fetch_json(page["@id"])["items"]
        for it in items:
            ce = it["catalogEntry"]
            if ce.get("listed", True):
                published[ce["version"]] = ce.get("published")
    candidates = [v for v in published if is_pre(cur) or not is_pre(v)]
    newest = max(candidates, key=version_key)
    if version_key(newest) <= version_key(cur):
        return None
    flag = "MAJOR" if major(newest) != major(cur) else ""
    return (pkg, cur, "->", newest, flag, age_note(published[newest]))


def nuget():
    props = (ROOT / "backend/Directory.Packages.props").read_text()
    pkgs = re.findall(r'<PackageVersion Include="([^"]+)" Version="([^"]+)"', props)
    with ThreadPoolExecutor(max_workers=8) as pool:
        rows = [r for r in pool.map(lambda p: nuget_row(*p), pkgs) if r]
    print_table("NuGet (backend/Directory.Packages.props)", rows)


def dotnet_sdk():
    cur = json.loads((ROOT / "global.json").read_text())["sdk"]["version"]
    feed = fetch_json(
        f"https://dotnetcli.blob.core.windows.net/dotnet/release-metadata/{major(cur)}.0/releases.json"
    )
    newest = feed["latest-sdk"]
    release = next(r for r in feed["releases"] if r["sdk"]["version"] == newest)
    rows = []
    if newest != cur:
        note = age_note(release["release-date"] + "T00:00:00Z")
        rows.append((".NET SDK", cur, "->", newest, f"runtime {release['release-version']}", note))
    tools = json.loads((ROOT / ".config/dotnet-tools.json").read_text())["tools"]
    ef = tools["dotnet-ef"]["version"]
    if ef != release["release-version"] and version_key(release["release-version"]) > version_key(ef):
        rows.append(("dotnet-ef", ef, "->", "matching EF build", "check NuGet", ""))
    print_table(f".NET SDK (global.json, mise.toml; support phase: {feed['support-phase']})", rows)


def docker_tags(repo, pattern):
    url = f"https://hub.docker.com/v2/repositories/{repo}/tags?page_size=100&ordering=last_updated"
    tags = {}
    for _ in range(5):
        data = fetch_json(url)
        for t in data["results"]:
            if re.fullmatch(pattern, t["name"]):
                tags[t["name"]] = t["last_updated"]
        url = data.get("next")
        if not url:
            break
    return tags


def docker():
    compose = (ROOT / "deploy/docker-compose.development.yml").read_text()
    rows = []
    for image, cur in re.findall(r"image:\s*([\w./-]+):(\S+)", compose):
        repo = image if "/" in image else f"library/{image}"
        if image == "postgres":
            m = re.fullmatch(r"(\d+)\.\d+(-.+)?", cur)
            pattern = rf"{m.group(1)}\.\d+{re.escape(m.group(2) or '')}"
        else:
            pattern = r"\d+\.\d+(\.\d+)?"
        tags = docker_tags(repo, pattern)
        if not tags:
            rows.append((image, cur, "->", "?", "no matching tags found", ""))
            continue
        newest = max(tags, key=lambda t: version_key(t.split("-")[0]))
        if version_key(newest.split("-")[0]) > version_key(cur.split("-")[0]):
            flag = "MAJOR" if major(newest) != major(cur) else ""
            rows.append((image, cur, "->", newest, flag, age_note(tags[newest])))
    print_table("Docker images (deploy/*.yml, PostgresContainerFixture)", rows)


def run(title, cmd):
    print(f"\n## {title}\n$ {' '.join(cmd)}")
    out = subprocess.run(cmd, capture_output=True, text=True)
    print((out.stdout + out.stderr).strip() or "(everything is current)")


def main():
    if not (ROOT / "backend/WattWise.slnx").exists():
        sys.exit("Run from the repo root.")
    run("mise tools (mise.toml)", ["mise", "outdated"])
    dotnet_sdk()
    run("pnpm, latest including majors", ["pnpm", "outdated", "-r", "--long"])
    nuget()
    docker()


if __name__ == "__main__":
    main()
