using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;

namespace TrustTunnelGui.Services;

public record ReleaseInfo(string TagName, string AssetName, string DownloadUrl, long Size);

public static class UpdateService
{
    private static readonly HttpClient _http = new();

    static UpdateService()
    {
        _http.DefaultRequestHeaders.Add("User-Agent", "trusttunnel-gui");
    }

    // ── TrustTunnel client ─────────────────────────────────────────────────

    public static async Task<ReleaseInfo?> GetLatestClientReleaseAsync(CancellationToken ct = default)
    {
        try
        {
            var rel = await _http.GetFromJsonAsync<GitHubRelease>(
                "https://api.github.com/repos/TrustTunnel/TrustTunnelClient/releases/latest", ct);
            if (rel?.Assets == null) return null;

            var arch = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture;
            string[] patterns = arch == System.Runtime.InteropServices.Architecture.Arm64
                ? new[] { "windows.*aarch64", "windows.*arm64", "win.*arm64" }
                : new[] { "windows.*x86_64", "windows.*amd64", "win.*x64", "win64" };

            foreach (var pat in patterns)
            {
                foreach (var asset in rel.Assets)
                {
                    if (System.Text.RegularExpressions.Regex.IsMatch(asset.Name ?? "", pat,
                            System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                        return new ReleaseInfo(rel.TagName ?? "?", asset.Name ?? "", asset.BrowserDownloadUrl ?? "", asset.Size);
                }
            }
            return null;
        }
        catch { return null; }
    }

    /// <summary>
    /// Downloads the latest client archive, extracts trusttunnel_client.exe
    /// and places it next to the GUI (renames old one to .bak first).
    /// </summary>
    public static async Task<string> UpdateClientAsync(ReleaseInfo release, string destDir,
        IProgress<double>? progress = null, CancellationToken ct = default)
    {
        var tmpArchive = Path.Combine(Path.GetTempPath(), "tt_client_update" + Path.GetExtension(release.AssetName));
        try
        {
            // Download
            using var resp = await _http.GetAsync(release.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, ct);
            resp.EnsureSuccessStatusCode();
            await using var src  = await resp.Content.ReadAsStreamAsync(ct);
            await using var dest = File.Create(tmpArchive);
            var buf   = new byte[81920];
            long done = 0;
            int  read;
            while ((read = await src.ReadAsync(buf, ct)) > 0)
            {
                await dest.WriteAsync(buf.AsMemory(0, read), ct);
                done += read;
                if (release.Size > 0) progress?.Report((double)done / release.Size);
            }
        }
        catch (Exception ex) { throw new Exception($"Download failed: {ex.Message}", ex); }

        // Extract
        var extractDir = Path.Combine(Path.GetTempPath(), "tt_client_extract_" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            Directory.CreateDirectory(extractDir);
            var ext = Path.GetExtension(tmpArchive).ToLowerInvariant();
            if (ext == ".zip")
                System.IO.Compression.ZipFile.ExtractToDirectory(tmpArchive, extractDir, overwriteFiles: true);
            else
                throw new Exception($"Unsupported archive format: {ext}");

            // Find exe
            var exeFiles = Directory.GetFiles(extractDir, "trusttunnel_client.exe", SearchOption.AllDirectories);
            if (exeFiles.Length == 0) throw new Exception("trusttunnel_client.exe not found in archive");

            var destExe = Path.Combine(destDir, "trusttunnel_client.exe");
            var bakExe  = destExe + ".bak";
            if (File.Exists(bakExe))  File.Delete(bakExe);
            if (File.Exists(destExe)) File.Move(destExe, bakExe);
            File.Copy(exeFiles[0], destExe);
            return destExe;
        }
        finally
        {
            try { File.Delete(tmpArchive); } catch { }
            try { Directory.Delete(extractDir, recursive: true); } catch { }
        }
    }

    // ── wintun ─────────────────────────────────────────────────────────────

    private const string WintunVersion = "0.14.1";

    public static async Task<string> UpdateWintunAsync(string destDir,
        IProgress<double>? progress = null, CancellationToken ct = default)
    {
        var url = $"https://www.wintun.net/builds/wintun-{WintunVersion}.zip";
        var tmp = Path.Combine(Path.GetTempPath(), $"wintun-{WintunVersion}.zip");

        using var resp = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        resp.EnsureSuccessStatusCode();
        await using var src  = await resp.Content.ReadAsStreamAsync(ct);
        await using var dest = File.Create(tmp);
        var buf   = new byte[81920];
        long total = resp.Content.Headers.ContentLength ?? 0;
        long done  = 0;
        int  read;
        while ((read = await src.ReadAsync(buf, ct)) > 0)
        {
            await dest.WriteAsync(buf.AsMemory(0, read), ct);
            done += read;
            if (total > 0) progress?.Report((double)done / total);
        }
        dest.Close();

        var extractDir = Path.Combine(Path.GetTempPath(), "wintun_extract_" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            System.IO.Compression.ZipFile.ExtractToDirectory(tmp, extractDir, overwriteFiles: true);
            var arch = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture
                == System.Runtime.InteropServices.Architecture.Arm64 ? "arm64" : "amd64";
            var dll = Path.Combine(extractDir, "wintun", "bin", arch, "wintun.dll");
            if (!File.Exists(dll)) throw new Exception($"wintun.dll not found at expected path: {dll}");
            var dst = Path.Combine(destDir, "wintun.dll");
            var bak = dst + ".bak";
            if (File.Exists(bak)) File.Delete(bak);
            if (File.Exists(dst)) File.Move(dst, bak);
            File.Copy(dll, dst);
            return dst;
        }
        finally
        {
            try { File.Delete(tmp); } catch { }
            try { Directory.Delete(extractDir, recursive: true); } catch { }
        }
    }

    // ── JSON models ────────────────────────────────────────────────────────

    private class GitHubRelease
    {
        [System.Text.Json.Serialization.JsonPropertyName("tag_name")]
        public string? TagName { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("assets")]
        public GitHubAsset[]? Assets { get; set; }
    }

    private class GitHubAsset
    {
        [System.Text.Json.Serialization.JsonPropertyName("name")]
        public string? Name { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("browser_download_url")]
        public string? BrowserDownloadUrl { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("size")]
        public long Size { get; set; }
    }
}
