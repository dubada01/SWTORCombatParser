import copy
import importlib.util
from pathlib import Path
import unittest

spec = importlib.util.spec_from_file_location("download_manifest", Path(__file__).parents[1] / "Update-DownloadManifest.py")
publisher = importlib.util.module_from_spec(spec)
spec.loader.exec_module(publisher)
DIGEST = "a" * 64


def manifest():
    return {"schemaVersion": 1, "downloads": {
        "windows-store": {"version": "2.3.0.0", "url": "https://apps.microsoft.com/detail/9P63T6ZQMHBZ"},
        "macos-arm64": {"version": "2.5.1", "url": "previous-mac"},
        "linux-x64": {"version": "2.5.2", "url": "previous-linux"}}}


def release(version, name=None):
    suffix = version.split(".")[-1]
    template = publisher.ASSETS[suffix][1][0]
    name = name or template.format(version=version)
    tag = "v" + version
    def asset(filename):
        return {"name": filename, "state": "uploaded", "size": 100,
                "digest": "sha256:" + DIGEST,
                "browser_download_url": publisher.download_url(tag, filename)}
    return {"tag_name": tag, "draft": False, "prerelease": False,
            "assets": [asset(name), asset(name + ".sha256")]}


def checksum(url):
    from urllib.parse import unquote
    return DIGEST + "  " + unquote(url.rsplit("/", 1)[1][:-7]) + "\n"


class ReconcileTests(unittest.TestCase):
    def test_updates_each_platform_without_changing_store(self):
        previous = manifest()
        updated = publisher.reconcile(previous, [release("2.6.1"), release("2.6.2")], checksum)
        self.assertEqual(updated["downloads"]["macos-arm64"]["version"], "2.6.1")
        self.assertEqual(updated["downloads"]["linux-x64"]["version"], "2.6.2")
        self.assertEqual(updated["downloads"]["windows-store"], previous["downloads"]["windows-store"])
        self.assertEqual(previous, manifest())

    def test_windows_msi_uses_platform_zero_and_encoded_asset_name(self):
        updated = publisher.reconcile(manifest(), [release("2.6.0", "Orbs Setup.msi")], checksum)
        self.assertEqual(updated["downloads"]["windows-msi"]["version"], "2.6.0")
        self.assertIn("Orbs%20Setup.msi", updated["downloads"]["windows-msi"]["url"])

    def test_selects_largest_version_not_api_order(self):
        updated = publisher.reconcile(manifest(), [release("2.10.2"), release("2.9.2"), release("2.6.1")], checksum)
        self.assertEqual(updated["downloads"]["linux-x64"]["version"], "2.10.2")

    def test_draft_and_prerelease_never_replace_stable_download(self):
        for flag in ("draft", "prerelease"):
            item = release("9.0.2")
            item[flag] = True
            self.assertEqual(publisher.reconcile(manifest(), [item], checksum), manifest())

    def test_incomplete_assets_leave_current_download(self):
        for mutate in (lambda r: r["assets"].pop(), lambda r: r["assets"][0].update(state="new"),
                       lambda r: r["assets"][0].update(size=0)):
            item = release("2.6.2")
            mutate(item)
            self.assertEqual(publisher.reconcile(manifest(), [item], checksum), manifest())

    def test_does_not_downgrade_a_platform(self):
        self.assertEqual(publisher.reconcile(manifest(), [release("2.4.2")], checksum), manifest())

    def test_ignores_wrong_architecture_and_wrong_platform_assets(self):
        for item in (release("2.6.1", "Orbs-2.6.1-macOS-x64.dmg"),
                     release("2.6.2", "Orbs-2.6.2-macOS-arm64.dmg")):
            self.assertEqual(publisher.reconcile(manifest(), [item], checksum), manifest())

    def test_legacy_four_component_tag_is_not_a_new_release(self):
        item = release("2.6.2")
        item["tag_name"] = "v2.6.1.2"
        self.assertEqual(publisher.reconcile(manifest(), [item], checksum), manifest())

    def test_checksum_mismatch_fails_without_mutating_manifest(self):
        previous = manifest()
        with self.assertRaisesRegex(ValueError, "disagrees"):
            publisher.reconcile(previous, [release("2.6.2")], lambda _: "b" * 64 + "  Orbs-2.6.2-linux-x64.tar.gz\n")
        self.assertEqual(previous, manifest())

    def test_missing_digest_is_rejected(self):
        item = release("2.6.2")
        item["assets"][0]["digest"] = None
        with self.assertRaisesRegex(ValueError, "Missing SHA-256"):
            publisher.reconcile(manifest(), [item], checksum)

    def test_other_repository_url_is_rejected_before_fetching(self):
        item = release("2.6.2")
        item["assets"][0]["browser_download_url"] = "https://example.com/installer"
        with self.assertRaisesRegex(ValueError, "expected repository"):
            publisher.reconcile(manifest(), [item], lambda _: self.fail("Unexpected network call"))

    def test_repeat_run_is_idempotent_and_does_not_refetch_sidecar(self):
        releases = [release("2.6.2")]
        first = publisher.reconcile(manifest(), releases, checksum)
        self.assertEqual(publisher.reconcile(first, releases, lambda _: self.fail("Unchanged download refetched")), first)

    def test_unknown_schema_is_rejected(self):
        item = copy.deepcopy(manifest())
        item["schemaVersion"] = 2
        with self.assertRaises(ValueError):
            publisher.reconcile(item, [], checksum)


if __name__ == "__main__":
    unittest.main()
