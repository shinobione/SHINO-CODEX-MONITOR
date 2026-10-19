using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnosticss;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ShinoCodexMonitor
{
    // Only official, stable GitHub assets with an independent API digest can be installed.
    internal sealed class ReleaseAsset
    {
        internal PublicRelease Release;
        internal string Name;
        internal string Sha256;
        internal long Size;
        internal string Url;
    }

    internal sealed class StagedUpdate
    {
        internal ReleaseAsset Asset;
        internal string Directory;
        internal string Archive;
        internal string Installer;
    }

    internal static class ReleasePackage
    {
        internal const long MaxZipBytes = 32L * 1024 * 1024;
        internal const long MaxExpandedBytes = 96L * 1024 * 1024;
        private const int MaxEntries = 20;
        internal static string AssetFile(Version version)
        {
            if (version == null || version.Major < 0 || version.Minor < 0 || version.Major > 99 || version.Minor > 99)
                throw new InvalidDataException("Unsupported version.");
            return string.Format("SHINO_CODEX_MONITOR_V{0}_{1}_WINDOWS.zip", version.Major, version.Minor);
        }
        internal static string InstallerFile(Version version)
        {
            return string.Format("INSTALLER-V{0}.{1}.ps1", version.Major, version.Minor);
        }
        internal static ReleaseAsset SelectAsset(PublicRelease release, string json)
        {
            if (release == null || release.Number == null) throw new InvalidDataException("Invalid release.");
            if (json == null || json.Length > 131072) throw new InvalidDataException("GitHub response too large.");
            Dictionary<string, object> root = Json.Object(Json.Serializer.DeserializeObject(json));
            if (root == null) throw new InvalidDataException("Invalid GitHub response.");
            object raw = Json.Get(root, "assets");
            object[] assets = raw as object[];
            if (assets == null || assets.Length > 30) throw new InvalidDataException("Missing or invalid asset list.");
            string name = AssetFile(release.Number);
            ReleaseAsset chosen = null;
            foreach (object x in assets)
            {
                Dictionary<string, object> item = Json.Object(x);
                if (item == null || Json.String(Json.Get(item, "name")) != name) continue;
                if (chosen != null) throw new InvalidDataException("Duplicate ZIP asset.");
                string digest = Json.String(Json.Get(item, "digest"));
                Match match = Regex.Match(digest ?? "", "^sha256:([0-9a-fA-F]{64})$", RegexOptions.CultureInvariant);
                if (!match.Success) throw new InvalidDataException("GitHub SHA-256 missing: installation refused.");
                long? size = Json.Long(Json.Get(item, "size"));
                if (!size.HasValue || size.Value < 1 || size.Value > MaxZipBytes)
                    throw new InvalidDataException("Invalid asset size.");
                // Never trust browser_download_url supplied in remote JSON.
                chosen = new ReleaseAsset {
                    Release = release, Name = name, Sha256 = match.Groups[1].Value.ToLowerInvariant(),
                    Size = size.Value, Url = ReleaseUpdater.RepoUrl + "/releases/download/" + release.Tag + "/" + name
                };
            }
            if (chosen == null) throw new InvalidDataException("Official Windows package not found.");
            return chosen;
        }
        internal static string HashFile(string path)
        {
            using (SHA256 sha = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }
        internal static void VerifyAssetFile(ReleaseAsset asset, string zip)
        {
            if (asset == null || !File.Exists(zip)) throw new InvalidDataException("Archive missing.");
            FileInfo fi = new FileInfo(zip);
            if (fi.Length != asset.Size || fi.Length > MaxZipBytes || fi.Length < 1)
                throw new InvalidDataException("Incomplete download or unexpected size.");
            if (!string.Equals(HashFile(zip), asset.Sha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Incorrect GitHub SHA-256.");
        }
        internal static void VerifyZip(ReleaseAsset asset, string zip, string destination)
        {
            VerifyAssetFile(asset, zip);
            HashSet<string> allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
                "SHINO-CODEX-MONITOR.exe", "SHINO-CODEX.ico", "SHA256SUMS.txt", "README-FR.txt",
                "DESINSTALLER-V" + asset.Release.Number.Major + "." + asset.Release.Number.Minor + ".ps1",
                "INSTALLER-V" + asset.Release.Number.Major + "." + asset.Release.Number.Minor + ".ps1",
                "INSTALLER-V" + asset.Release.Number.Major + "." + asset.Release.Number.Minor + ".bat"
            };
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            long total = 0;
            using (ZipArchive package = ZipFile.OpenRead(zip))
            {
                if (package.Entries.Count < 5 || package.Entries.Count > MaxEntries)
                    throw new InvalidDataException("Unexpected ZIP structure.");
                // Verify every entry before creating any output files.
                foreach (ZipArchiveEntry e in package.Entries)
                {
                    if (e.FullName.EndsWith("/") || e.FullName.EndsWith("\\"))
                        continue;
                    string name = ValidateEntry(e, asset.Release.Number);
                    if (!allowed.Contains(name) || !seen.Add(name)) throw new InvalidDataException("Unapproved or duplicate ZIP file.");
                    if (e.Length < 0 || e.Length > MaxExpandedBytes || total > MaxExpandedBytes - e.Length)
                        throw new InvalidDataException("Expanded size exceeds limit.");
                    total += e.Length;
                }
                if (!seen.SetEquals(allowed))
                    throw new InvalidDataException("Required files missing.");
                Directory.CreateDirectory(destination);
                foreach (ZipArchiveEntry e in package.Entries)
                {
                    if (e.FullName.EndsWith("/") || e.FullName.EndsWith("\\")) continue;
                    string name = ValidateEntry(e, asset.Release.Number);
                    string path = Path.Combine(destination, name);
                    using (Stream source = e.Open())
                    using (FileStream output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    {
                        byte[] buffer = new byte[8192]; int n; long copied = 0;
                        while ((n = source.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            copied += n;
                            if (copied > e.Length || copied > MaxExpandedBytes) throw new InvalidDataException("Invalid ZIP.");
                            output.Write(buffer, 0, n);
                        }
                        if (copied != e.Length) throw new InvalidDataException("Truncated ZIP entry.");
                    }
                }
            }
            string manifest = File.ReadAllText(Path.Combine(destination, "SHA256SUMS.txt"));
            Match m = Regex.Match(manifest, @"(?im)^([0-9a-f]{64})\s+SHINO-CODEX-MONITOR\.exe\s*$");
            if (!m.Success || Regex.Matches(manifest, @"(?im)^[0-9a-f]{64}\s+SHINO-CODEX-MONITOR\.exe\s*$").Count != 1 ||
                !string.Equals(m.Groups[1].Value, HashFile(Path.Combine(destination, "SHINO-CODEX-MONITOR.exe")), StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Invalid executable checksum.");
        }
        private static string ValidateEntry(ZipArchiveEntry entry, Version version)
        {
            string value = entry.FullName.Replace('\\', '/');
            string[] parts = value.Split('/');
            // Compress-Archive may include exactly one staging root directory or files at ZIP root.
            if (parts.Length != 1 && (parts.Length != 2 ||
                !string.Equals(parts[0], Path.GetFileNameWithoutExtension(AssetFile(version)), StringComparison.Ordinal)))
                throw new InvalidDataException("Unexpected ZIP path.");
            string name = parts[parts.Length - 1];
            if (name.Length == 0 || name == "." || name == ".." || name.IndexOf(':') >= 0 || name.IndexOf('\\') >= 0 ||
                name.IndexOf('/') >= 0 || name.IndexOf('\0') >= 0 || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                throw new InvalidDataException("Unsafe ZIP path.");
            // POSIX symlinks must never be extracted, even when ZIP came from a GitHub asset.
            if (((entry.ExternalAttributes >> 16) & 0xF000) == 0xA000)
                throw new InvalidDataException("Lien symbolique in le ZIP.");
            return name;
        }
        internal static string ValidateRedirect(string url)
        {
            Uri uri;
            if (!Uri.TryCreate(url, UriKind.Absolute, out uri) || uri.Scheme != Uri.UriSchemeHttps ||
                uri.UserInfo.Length > 0 || uri.Port != 443 || uri.Fragment.Length > 0 ||
                !(uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase) ||
                  uri.Host.Equals("release-assets.githubusercontent.com", StringComparison.OrdinalIgnoreCase) ||
                  uri.Host.Equals("objects.githubusercontent.com", StringComparison.OrdinalIgnoreCase)))
                throw new InvalidDataException("Download redirect not allowed.");
            return uri.AbsoluteUri;
        }
        internal static Task<StagedUpdate> StageAsync(ReleaseAsset asset)
        {
            return Task.Run(delegate {
                string home = Path.Combine(Path.GetTempPath(), "SHINO-CODEX-UPDATE-" + Guid.NewGuid().ToString("N"));
                string zip = Path.Combine(home, asset.Name);
                string dest = Path.Combine(home, "package");
                Directory.CreateDirectory(home);
                try {
                    ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
                    string url = ValidateRedirect(asset.Url);
                    using (FileStream output = new FileStream(zip, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    {
                        int hops = 0;
                        while (true)
                        {
                            if (++hops > 5) throw new WebException("Too many GitHub redirects.");
                            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
                            request.Method = "GET";
                            request.UserAgent = "SHINO-CODEX-MONITOR/" + typeof(ReleasePackage).Assembly.GetName().Version.ToString(3) + " (verified-release-download)";
                            request.Timeout = 12000; request.ReadWriteTimeout = 12000;
                            request.AllowAutoRedirect = false;
                            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                            {
                                if ((int)response.StatusCode >= 300 && (int)response.StatusCode < 400)
                                {
                                    string loc = response.Headers["Location"];
                                    url = ValidateRedirect(new Uri(new Uri(url), loc).AbsoluteUri);
                                    continue;
                                }
                                if (response.StatusCode != HttpStatusCode.OK) throw new WebException("Invalid download response.");
                                if (response.ContentLength > MaxZipBytes) throw new InvalidDataException("File too large.");
                                using (Stream input = response.GetResponseStream())
                                {
                                    if (input == null) throw new IOException("Empty download.");
                                    byte[] buffer = new byte[8192]; int n; long count = 0;
                                    Stopwatch time = Stopwatch.StartNew();
                                    while ((n = input.Read(buffer, 0, buffer.Length)) > 0)
                                    {
                                        count += n;
                                        if (count > MaxZipBytes || time.Elapsed.TotalSeconds > 90)
                                            throw new IOException("Download size or time limit exceeded.");
                                        output.Write(buffer, 0, n);
                                    }
                                }
                            }
                            break;
                        }
                    }
                    VerifyZip(asset, zip, dest);
                    return new StagedUpdate { Asset = asset, Directory = home, Archive = zip,
                        Installer = Path.Combine(dest, InstallerFile(asset.Release.Number)) };
                } catch {
                    try { Directory.Delete(home, true); } catch { }
                    throw;
                }
            });
        }
    }
}
