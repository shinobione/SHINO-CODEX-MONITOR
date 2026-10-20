"""Static release-engineering gates. Real Windows builder execution is CI-only."""
from pathlib import Path
import re
import unittest

ROOT = Path(__file__).resolve().parents[1]
read = lambda name: (ROOT / name).read_text(encoding="utf-8-sig")


class V52StableReleasePreparation(unittest.TestCase):
    def test_assembly_has_one_supported_channel(self):
        assembly = read("src/AssemblyInfo.cs")
        self.assertIn('AssemblyVersion("5.2.0.0")', assembly)
        versions = re.findall(r'AssemblyInformationalVersion\("([^"]+)"\)', assembly)
        self.assertEqual(len(versions), 1)
        self.assertIn(versions[0], ("5.2.0-preview.1", "5.2.0"))

    def test_preview_and_stable_builds_have_disjoint_names(self):
        preview = read("BUILD-PERISCOPE-V5.2.ps1")
        stable = read("BUILD-RELEASE-V5.2.ps1")
        self.assertIn("CODEX_PERISCOPE_V5_2_PREVIEW_WINDOWS.zip", preview)
        self.assertIn("CODEX_PERISCOPE_V5_2_WINDOWS.zip", stable)
        self.assertNotIn("CODEX_PERISCOPE_V5_2_PREVIEW_WINDOWS.zip", stable)
        self.assertIn('AssemblyInformationalVersion("5.2.0-preview.1")', preview)
        self.assertIn('AssemblyInformationalVersion("5.2.0")', stable)
        self.assertNotIn('AssemblyInformationalVersion("5.2.0-preview.1")', stable)
        self.assertIn("TEST-PERISCOPE-RADAR.ps1", stable)
        for flag in ("--premium-self-test", "--p0-self-test", "--instrument-self-test",
                     "--live-task-self-test", "-ValidateOnly", "Get-FileHash"):
            self.assertIn(flag, stable)

    def test_stable_zip_readme_has_no_preview_identity(self):
        stable = read("BUILD-RELEASE-V5.2.ps1")
        readme = read("README-EN-STABLE-V5.2.txt")
        self.assertIn("README-EN-STABLE-V5.2.txt", stable)
        self.assertIn("README-EN.txt", stable)
        self.assertIn("WINDOWS STABLE", readme)
        self.assertNotIn("THIS IS AN UNSIGNED PREVIEW", readme)
        self.assertIn("independent", readme.lower())
        self.assertIn("unsigned", readme.lower())
        self.assertIn("Codex", readme)

    def test_updater_and_stable_archive_have_matching_asset(self):
        package = read("src/ReleasePackage.cs")
        stable = read("BUILD-RELEASE-V5.2.ps1")
        self.assertIn("CODEX_PERISCOPE_V{0}_{1}_WINDOWS.zip", package)
        self.assertIn("CODEX_PERISCOPE_V5_2_WINDOWS.zip", stable)
        self.assertIn("INSTALL-PERISCOPE-V5.2.ps1", stable)
        self.assertIn("SHINO-CODEX-MONITOR.exe", stable)
        self.assertIn("SHA256SUMS.txt", stable)

    def test_publication_requires_manual_action_and_main(self):
        workflow = read(".github/workflows/publish-v5.2.yml")
        self.assertIn("workflow_dispatch:", workflow)
        self.assertNotRegex(workflow, r"(?m)^\s{2}(push|pull_request|schedule):")
        self.assertIn("default: false", workflow)
        self.assertIn("inputs.publish == true", workflow)
        self.assertIn("github.ref == 'refs/heads/main'", workflow)
        self.assertIn("Release source must explicitly be stable 5.2.0", workflow)
        self.assertIn("gh release create v5.2.0", workflow)
        self.assertIn("--draft", workflow)
        self.assertIn("gh release download v5.2.0", workflow)
        self.assertIn("cmp -s", workflow)
        self.assertIn("gh release edit v5.2.0", workflow)
        self.assertLess(workflow.index("cmp -s"), workflow.index("gh release edit v5.2.0"))
        # The filename is expected in the archive allowlist; executing it here is forbidden.
        self.assertNotIn("Start-Process", workflow)
        self.assertNotIn("powershell -File INSTALL-PERISCOPE", workflow)

    def test_regular_ci_handles_both_channels_without_publication(self):
        workflow = read(".github/workflows/build-windows.yml")
        self.assertIn("BUILD-PERISCOPE-V5.2.ps1", workflow)
        self.assertIn("BUILD-RELEASE-V5.2.ps1", workflow)
        self.assertIn("steps.build_mode.outputs.channel == 'preview'", workflow)
        self.assertIn("steps.build_mode.outputs.channel == 'stable'", workflow)
        self.assertNotIn("gh release create", workflow)

    def test_notes_do_not_claim_preview_is_stable(self):
        notes = read("RELEASE-NOTES-V5.2.md")
        self.assertIn("CODEX_PERISCOPE_V5_2_WINDOWS.zip", notes)
        self.assertIn("not** a stable Release asset", notes)
        self.assertIn("UNVERIFIED", notes)
        self.assertIn("explicit maintainer authorization", notes)


if __name__ == "__main__":
    unittest.main()
