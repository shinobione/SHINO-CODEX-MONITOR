using System;
using System.IO;

namespace ShinoCodexMonitor
{
    // Explicit opt-in smoke test. Uses the same reader, downloader and ZIP
    // validator as the real updater, but NEVER launches the installer.
    // This test does not use Codex credentials or application settings.
    internal static class UpdateDownloadSmoke
    {
        internal static int Run()
        {
            string reportPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                "V4_UPDATE_DOWNLOAD_REPORT.txt");
            StagedUpdate staged = null;
            try
            {
                PublicRelease release = ReleaseUpdater.CheckAsync().GetAwaiter().GetResult();
                ReleaseAsset asset = ReleasePackage.SelectAsset(release, release.ApiResponse);
                staged = ReleasePackage.StageAsync(asset).GetAwaiter().GetResult();
                if (staged == null || !File.Exists(staged.Installer))
                    throw new InvalidDataException("The verified package is missing its installer.");
                ReleasePackage.VerifyAssetFile(asset, staged.Archive);
                string report = "PASS: real GitHub Release download and SHA-256/ZIP validation\r\n"
                    + "Release: " + release.Tag + "\r\n"
                    + "Asset: " + asset.Name + "\r\n"
                    + "Bytes: " + asset.Size + "\r\n"
                    + "Installer: NOT EXECUTED\r\n";
                File.WriteAllText(reportPath, report);
                return 0;
            }
            catch (Exception ex)
            {
                try { File.WriteAllText(reportPath, "FAIL: real GitHub Release download smoke\r\n"
                    + ex.GetType().Name + ": " + ex.Message + "\r\n"); }
                catch { }
                return 1;
            }
            finally
            {
                if (staged != null && !string.IsNullOrEmpty(staged.Directory))
                {
                    try { Directory.Delete(staged.Directory, true); } catch { }
                }
            }
        }
    }
}
