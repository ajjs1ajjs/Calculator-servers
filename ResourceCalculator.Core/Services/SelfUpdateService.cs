using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Text.Json;
using ResourceCalculator.Interfaces;

namespace ResourceCalculator.Services;

public class SelfUpdateService : ISelfUpdateService
{
    private static readonly string BaseUrl = "https://api.github.com/repos/ajjs1ajjs/Calculator-servers/releases/latest";

    private static readonly HttpClient Http = CreateHttpClient();

    public event DownloadProgressHandler? Progress;
    public string? LatestVersion { get; private set; }
    public string? DownloadUrl { get; set; }

    public async Task<SelfUpdateResult> UpdateAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(DownloadUrl))
            return new SelfUpdateResult(SelfUpdateStatus.Failed, "No download URL");

        try
        {
            var tempPath = Path.Combine(Path.GetTempPath(), "ITE.ResourceCalculator_new.exe");
            if (File.Exists(tempPath))
                File.Delete(tempPath);

            using var response = await Http.GetAsync(DownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return new SelfUpdateResult(SelfUpdateStatus.Failed, $"HTTP {(int)response.StatusCode}");

            var totalBytes = response.Content.Headers.ContentLength ?? -1;
            await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

            var buffer = new byte[8192];
            long bytesReceived = 0;
            int bytesRead;

            while ((bytesRead = await contentStream.ReadAsync(buffer, cancellationToken)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                bytesReceived += bytesRead;
                Progress?.Invoke(bytesReceived, totalBytes);
            }

            if (!await VerifyHashAsync(tempPath, cancellationToken))
                return new SelfUpdateResult(SelfUpdateStatus.Failed, "Hash verification failed");

            ApplyUpdate(tempPath);
            return new SelfUpdateResult(SelfUpdateStatus.Completed);
        }
        catch (OperationCanceledException)
        {
            var tempPath = Path.Combine(Path.GetTempPath(), "ITE.ResourceCalculator_new.exe");
            if (File.Exists(tempPath)) File.Delete(tempPath);
            return new SelfUpdateResult(SelfUpdateStatus.Failed, "Download cancelled");
        }
        catch (Exception ex)
        {
            return new SelfUpdateResult(SelfUpdateStatus.Failed, ex.Message);
        }
    }

    private async Task<bool> VerifyHashAsync(string filePath, CancellationToken cancellationToken)
    {
        try
        {
            var assetsResponse = await Http.GetAsync(BaseUrl, cancellationToken);
            if (!assetsResponse.IsSuccessStatusCode) return true;

            using var stream = await assetsResponse.Content.ReadAsStreamAsync(cancellationToken);
            using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var assets = json.RootElement.GetProperty("assets");

            foreach (var asset in assets.EnumerateArray())
            {
                var name = asset.GetProperty("name").GetString();
                if (name == "ITE.ResourceCalculator.exe")
                {
                    var expectedHash = asset.GetProperty("digest").GetString() ?? "";
                    if (expectedHash.StartsWith("sha256:"))
                        expectedHash = expectedHash[7..];

                    if (!string.IsNullOrEmpty(expectedHash))
                    {
                        using var sha = System.Security.Cryptography.SHA256.Create();
                        await using var fileStream = File.OpenRead(filePath);
                        var hash = Convert.ToHexString(sha.ComputeHash(fileStream)).ToLowerInvariant();
                        if (hash != expectedHash.ToLowerInvariant())
                            return false;
                    }
                    break;
                }
            }
        }
        catch { }
        return true;
    }

    private void ApplyUpdate(string newExePath)
    {
        var currentExe = Environment.ProcessPath ?? Assembly.GetExecutingAssembly().Location;
        if (string.IsNullOrEmpty(currentExe)) return;

        var batchPath = Path.Combine(Path.GetTempPath(), "ITE_Update.bat");
        var batch = new StringBuilder();
        batch.AppendLine("@echo off");
        batch.AppendLine("timeout /t 2 /nobreak > nul");
        batch.AppendLine($"copy /Y \"{newExePath}\" \"{currentExe}\"");
        batch.AppendLine($"del \"{newExePath}\"");
        batch.AppendLine($"del \"%~f0\"");

        File.WriteAllText(batchPath, batch.ToString(), Encoding.UTF8);

        Process.Start(new ProcessStartInfo
        {
            FileName = batchPath,
            UseShellExecute = true,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        });
    }

    private static HttpClient CreateHttpClient()
    {
        var handler = new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(5)
        };
        var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(300) };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("ITE.ResourceCalculator", "1.0"));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/octet-stream"));
        return client;
    }
}
