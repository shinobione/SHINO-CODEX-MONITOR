using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ShinoCodexMonitor
{
    // This module performs one read-only, unauthenticated GET to the project's
    // public GitHub Releases endpoint. It never downloads or executes a binary.
    internal sealed class PublicRelease
    {
        internal string Tag;
        internal Version Number;
        internal string PageUrl;
        internal string ApiResponse;
    }

    internal static class ReleaseUpdater
    {
        internal const string RepoUrl = "https://github.com/shinobione/SHINO-CODEX-MONITOR";
        internal const string ApiUrl = "https://api.github.com/repos/shinobione/SHINO-CODEX-MONITOR/releases/latest";

        internal static PublicRelease ParseLatest(string response)
        {
            if (string.IsNullOrEmpty(response) || response.Length > 131072)
                throw new InvalidDataException("Réponse GitHub absente ou trop volumineuse.");
            Dictionary<string, object> item;
            try { item = Json.Object(new System.Web.Script.Serialization.JavaScriptSerializer { MaxJsonLength = 131072 }.DeserializeObject(response)); }
            catch (Exception ex) { throw new InvalidDataException("Réponse GitHub invalide.", ex); }
            if (item == null || Json.Bool(Json.Get(item, "draft")) == true ||
                Json.Bool(Json.Get(item, "prerelease")) == true)
                throw new InvalidDataException("Aucune Release stable vérifiable.");
            string tag = Json.String(Json.Get(item, "tag_name"));
            Version number = ParseTag(tag);
            if (number == null) throw new InvalidDataException("Version de Release invalide.");
            return new PublicRelease {
                Tag = tag,
                Number = number,
                // Construct from an allowlisted repository and a strictly validated tag;
                // never open a URL supplied directly in an external JSON response.
                PageUrl = RepoUrl + "/releases/tag/" + tag
            };
        }

        internal static Version ParseTag(string tag)
        {
            if (tag == null) return null;
            Match m = Regex.Match(tag, @"^v([0-9]{1,3})\.([0-9]{1,3})\.([0-9]{1,3})$", RegexOptions.CultureInvariant);
            if (!m.Success) return null;
            try { return new Version(int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value), int.Parse(m.Groups[3].Value)); }
            catch (Exception) { return null; }
        }

        internal static bool IsNewer(PublicRelease latest, Version local)
        {
            return latest != null && latest.Number != null && local != null && latest.Number.CompareTo(
                new Version(local.Major, local.Minor, Math.Max(0, local.Build))) > 0;
        }

        internal static Task<PublicRelease> CheckAsync()
        {
            return Task.Run(delegate {
                // On .NET Framework, TLS 1.2 is required by GitHub.
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(ApiUrl);
                request.Method = "GET";
                request.Accept = "application/vnd.github+json";
                request.UserAgent = "SHINO-CODEX-MONITOR/" + typeof(ReleaseUpdater).Assembly.GetName().Version.ToString(3) + " (public-release-check)";
                request.Timeout = 8000;
                request.ReadWriteTimeout = 8000;
                request.AllowAutoRedirect = false;
                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                {
                    if (response.StatusCode != HttpStatusCode.OK)
                        throw new WebException("Le service de Release GitHub ne répond pas correctement.");
                    using (Stream input = response.GetResponseStream())
                    {
                        if (input == null) throw new InvalidDataException("GitHub n'a retourné aucune donnée.");
                        using (MemoryStream buffer = new MemoryStream())
                        {
                            byte[] chunk = new byte[4096];
                            int read;
                            while ((read = input.Read(chunk, 0, chunk.Length)) > 0)
                            {
                                if (buffer.Length + read > 131072)
                                    throw new InvalidDataException("Réponse GitHub trop volumineuse.");
                                buffer.Write(chunk, 0, read);
                            }
                            string json = Encoding.UTF8.GetString(buffer.ToArray());
                            PublicRelease parsed = ParseLatest(json);
                            parsed.ApiResponse = json;
                            return parsed;
                        }
                    }
                }
            });
        }
    }
}
