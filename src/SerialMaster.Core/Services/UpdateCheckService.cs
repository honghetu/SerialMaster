using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace SerialMaster.Core.Services;

public class UpdateInfo
{
    public bool HasUpdate { get; init; }
    public Version? LatestVersion { get; init; }
    public Version? CurrentVersion { get; init; }
    public string? ReleaseUrl { get; init; }
    public string? Notes { get; init; }
    public string? Error { get; init; }
}

public sealed class UpdateCheckService
{
    private const string Owner = "honghetu";
    private const string Repo = "SerialMaster";
    public static string ReleasesPageUrl => $"https://github.com/{Owner}/{Repo}/releases";

    private static readonly HttpClient _http = CreateClient();

    private static HttpClient CreateClient()
    {
        var c = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
        c.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("SerialMaster", "1.0"));
        c.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return c;
    }

    public async Task<UpdateInfo> CheckAsync(Version currentVersion, CancellationToken token = default)
    {
        try
        {
            var url = $"https://api.github.com/repos/{Owner}/{Repo}/releases/latest";
            var resp = await _http.GetAsync(url, token).ConfigureAwait(false);

            if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return new UpdateInfo
                {
                    CurrentVersion = currentVersion,
                    Error = "仓库尚无 Release 发布"
                };
            }
            resp.EnsureSuccessStatusCode();

            var json = await resp.Content.ReadAsStringAsync(token).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var tagName = root.GetProperty("tag_name").GetString() ?? "";
            var htmlUrl = root.GetProperty("html_url").GetString();
            var body = root.TryGetProperty("body", out var b) ? b.GetString() : null;

            // tag_name often like "v1.6.0" or "1.6.0"
            var verStr = tagName.TrimStart('v', 'V').Trim();
            if (!Version.TryParse(verStr, out var latest))
            {
                return new UpdateInfo
                {
                    CurrentVersion = currentVersion,
                    Error = $"无法解析远程版本号: {tagName}"
                };
            }

            var normalizedCurrent = NormalizeVersion(currentVersion);
            var normalizedLatest = NormalizeVersion(latest);

            return new UpdateInfo
            {
                CurrentVersion = currentVersion,
                LatestVersion = latest,
                HasUpdate = normalizedLatest > normalizedCurrent,
                ReleaseUrl = htmlUrl,
                Notes = body
            };
        }
        catch (TaskCanceledException)
        {
            return new UpdateInfo { CurrentVersion = currentVersion, Error = "请求超时（网络不通？）" };
        }
        catch (HttpRequestException ex)
        {
            return new UpdateInfo { CurrentVersion = currentVersion, Error = $"网络错误: {ex.Message}" };
        }
        catch (Exception ex)
        {
            FileLogger.Error("UpdateCheck failed", ex);
            return new UpdateInfo { CurrentVersion = currentVersion, Error = ex.Message };
        }
    }

    private static Version NormalizeVersion(Version v)
        => new(v.Major, v.Minor, Math.Max(0, v.Build), Math.Max(0, v.Revision));
}
