#!/usr/bin/env python3
"""Publish verified stable release links to the independent downloads branch."""
import base64
import copy
from datetime import datetime, timezone
import json
import os
import re
import urllib.error
import urllib.parse
import urllib.request

REPOSITORY = "dubada01/SWTORCombatParser"
BRANCH = "downloads"
ASSETS = {
    "0": ("windows-msi", ("Orbs-{version}-windows-x64.msi", "Orbs-{version}-win-x64.msi", "Orbs Setup.msi")),
    "1": ("macos-arm64", ("Orbs-{version}-macOS-arm64.dmg",)),
    "2": ("linux-x64", ("Orbs-{version}-linux-x64.tar.gz",)),
}


def version_tuple(value):
    return tuple(map(int, value.split(".")))


def download_url(tag, name):
    return f"https://github.com/{REPOSITORY}/releases/download/{tag}/{urllib.parse.quote(name, safe='')}"


def fetch_checksum(url):
    # Public release assets never receive the repository write token.
    with urllib.request.urlopen(url, timeout=30) as response:
        return response.read(65537).decode("utf-8")


def reconcile(manifest, releases, read_checksum=fetch_checksum):
    if manifest.get("schemaVersion") != 1 or not isinstance(manifest.get("downloads"), dict):
        raise ValueError("Unsupported download manifest")
    result = copy.deepcopy(manifest)
    candidates = {}
    for release in releases:
        match = re.fullmatch(r"v(\d+\.\d+\.([012]))", release.get("tag_name", ""))
        if not match or release.get("draft") or release.get("prerelease"):
            continue
        version, suffix = match.groups()
        source_id, templates = ASSETS[suffix]
        assets = {asset["name"]: asset for asset in release.get("assets", [])}
        for template in templates:
            name = template.format(version=version)
            asset = assets.get(name)
            sidecar = assets.get(name + ".sha256")
            if not asset or not sidecar:
                continue
            if any(item.get("state") != "uploaded" or item.get("size", 0) <= 0 for item in (asset, sidecar)):
                continue
            if source_id not in candidates or version_tuple(version) > version_tuple(candidates[source_id][0]):
                candidates[source_id] = (version, release["tag_name"], asset, sidecar)
            break

    for source_id, (version, tag, asset, sidecar) in candidates.items():
        current = result["downloads"].get(source_id, {})
        if current and version_tuple(version) < version_tuple(current["version"]):
            continue
        url = download_url(tag, asset["name"])
        sidecar_url = download_url(tag, sidecar["name"])
        if asset.get("browser_download_url") != url or sidecar.get("browser_download_url") != sidecar_url:
            raise ValueError("Release asset URL does not belong to the expected repository")
        digest = asset.get("digest", "") or ""
        if not re.fullmatch(r"sha256:[a-f0-9]{64}", digest):
            raise ValueError(f"Missing SHA-256 digest: {asset['name']}")
        entry = {"version": version, "url": url, "sha256": digest[7:], "releaseTag": tag}
        if current == entry:
            continue
        text = read_checksum(sidecar_url)
        if len(text) > 65536:
            raise ValueError("Checksum file is too large")
        checksums = {}
        for line in text.splitlines():
            match = re.fullmatch(r"([a-fA-F0-9]{64})[ \t]+\*?(.+)", line.strip())
            if match:
                checksums[match[2]] = match[1].lower()
        if checksums.get(asset["name"]) != digest[7:]:
            raise ValueError(f"Checksum sidecar disagrees with GitHub's digest: {asset['name']}")
        result["downloads"][source_id] = entry
    if result["downloads"] != manifest["downloads"]:
        result["updatedAt"] = datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")
    return result


class GitHub:
    def __init__(self, token):
        self.token = token

    def request(self, path, data=None):
        headers = {"Accept": "application/vnd.github+json", "Content-Type": "application/json", "X-GitHub-Api-Version": "2022-11-28",
                   "Authorization": f"Bearer {self.token}", "User-Agent": "Orbs-download-manifest"}
        payload = None if data is None else json.dumps(data).encode()
        request = urllib.request.Request(f"https://api.github.com/repos/{REPOSITORY}/{path}",
                                         data=payload, headers=headers, method="GET" if data is None else "PUT")
        with urllib.request.urlopen(request, timeout=30) as response:
            return json.load(response)

    def releases(self):
        result = []
        for page in range(1, 101):
            batch = self.request(f"releases?per_page=100&page={page}")
            result.extend(batch)
            if len(batch) < 100:
                return result
        raise RuntimeError("Release pagination limit exceeded")


def main():
    if os.environ.get("GITHUB_REPOSITORY", REPOSITORY) != REPOSITORY:
        raise SystemExit("This publisher only updates the Orbs repository")
    github = GitHub(os.environ["GH_TOKEN"])
    releases = github.releases()
    for attempt in range(3):
        source = github.request(f"contents/downloads.json?ref={BRANCH}")
        current = json.loads(base64.b64decode(source["content"]))
        updated = reconcile(current, releases)
        if updated == current:
            print("Download manifest already matches the verified stable releases")
            return
        payload = (json.dumps(updated, indent=2) + "\n").encode()
        try:
            response = github.request("contents/downloads.json", {
                "branch": BRANCH, "sha": source["sha"],
                "message": "Update verified Orbs release downloads",
                "content": base64.b64encode(payload).decode()})
            print("Published download manifest commit " + response["commit"]["sha"])
            return
        except urllib.error.HTTPError as error:
            if error.code not in (409, 422) or attempt == 2:
                raise
    raise RuntimeError("Could not update manifest after concurrent changes")


if __name__ == "__main__":
    main()
