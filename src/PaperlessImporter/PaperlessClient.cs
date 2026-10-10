using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace PaperlessImporter;

public interface IPaperlessClient
{
    Task VerifyStartupPrerequisitesAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<PaperlessTaskResult>> GetTasksAsync(CancellationToken cancellationToken);
    Task<string> UploadAsync(string path, string fileName, CancellationToken cancellationToken);
}

public sealed class PaperlessClient(HttpClient httpClient, IOptions<PaperlessOptions> options) : IPaperlessClient
{
    internal const string AddDocumentPermission = "add_document";
    internal const string ViewTaskPermission = "view_paperlesstask";
    internal const string ViewUiSettingsPermission = "view_uisettings";
    private readonly PaperlessOptions _options = options.Value;

    public async Task VerifyStartupPrerequisitesAsync(CancellationToken cancellationToken)
    {
        using (var request = CreateRequest(HttpMethod.Get, "api/ui_settings/"))
        using (var response = await httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken))
        {
            if (!response.IsSuccessStatusCode)
                throw new PaperlessStartupException(
                    $"The effective permission check failed with HTTP {(int)response.StatusCode}. " +
                    "Grant UISettings: View so Paperless can report this account's permissions.");

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var missing = FindMissingPermissions(json.RootElement);
            if (missing.Count > 0)
                throw new PaperlessStartupException(
                    $"The Paperless account is missing required permissions: {string.Join(", ", missing)}.");
        }

        using var taskRequest = CreateRequest(HttpMethod.Get, "api/tasks/?page_size=1");
        using var taskResponse = await httpClient.SendAsync(
            taskRequest,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        if (!taskResponse.IsSuccessStatusCode)
            throw new PaperlessStartupException(
                $"The task-list access check failed with HTTP {(int)taskResponse.StatusCode}.");
    }

    public async Task<IReadOnlyList<PaperlessTaskResult>> GetTasksAsync(CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Get, "api/tasks/");
        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        return ParseTasks(json.RootElement);
    }

    public async Task<string> UploadAsync(string path, string fileName, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Post, "api/documents/post_document/");
        await using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, true);
        using var fileContent = new StreamContent(file);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        using var form = new MultipartFormDataContent();
        form.Add(fileContent, "document", fileName);
        request.Content = form;
        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        using var json = JsonDocument.Parse(payload);
        if (json.RootElement.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(json.RootElement.GetString()))
            throw new InvalidDataException("Paperless returned an invalid task identifier.");
        return json.RootElement.GetString()!;
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string relativePath)
    {
        if (_options.BaseUrl is null || !_options.BaseUrl.IsAbsoluteUri)
            throw new InvalidOperationException("Paperless:BaseUrl must be an absolute URL.");
        var token = ResolveToken();
        var request = new HttpRequestMessage(method, new Uri(_options.BaseUrl, relativePath));
        request.Headers.Authorization = new AuthenticationHeaderValue("Token", token);
        request.Headers.Accept.ParseAdd("application/json; version=9");
        return request;
    }

    private string ResolveToken()
    {
        var token = !string.IsNullOrWhiteSpace(_options.TokenFile)
            ? File.ReadAllText(_options.TokenFile).Trim()
            : _options.Token.Trim();
        if (string.IsNullOrWhiteSpace(token)) throw new InvalidOperationException("A Paperless token or token file is required.");
        return token;
    }

    public static IReadOnlyList<PaperlessTaskResult> ParseTasks(JsonElement root)
    {
        var array = root.ValueKind == JsonValueKind.Array
            ? root
            : root.TryGetProperty("results", out var results) && results.ValueKind == JsonValueKind.Array
                ? results
                : throw new InvalidDataException("Paperless returned an invalid tasks response.");
        var tasks = new List<PaperlessTaskResult>();
        foreach (var item in array.EnumerateArray())
        {
            var id = GetString(item, "task_id");
            if (string.IsNullOrWhiteSpace(id)) continue;
            var status = GetString(item, "status")?.ToUpperInvariant();
            tasks.Add(new PaperlessTaskResult(
                id,
                GetString(item, "task_file_name"),
                GetDate(item, "date_created"),
                status switch { "SUCCESS" => PaperlessTaskState.Success, "FAILURE" => PaperlessTaskState.Failure, _ => PaperlessTaskState.Pending },
                status == "FAILURE" ? "paperless-task-failed" : null));
        }
        return tasks;
    }

    internal static IReadOnlyList<string> FindMissingPermissions(JsonElement settings)
    {
        if (settings.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("Paperless returned an invalid UI settings response.");

        if (settings.TryGetProperty("user", out var user) &&
            user.ValueKind == JsonValueKind.Object &&
            user.TryGetProperty("is_superuser", out var superuser) &&
            superuser.ValueKind == JsonValueKind.True)
            return [];

        if (!settings.TryGetProperty("permissions", out var permissions) ||
            permissions.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("Paperless returned an invalid effective permission response.");

        var granted = permissions
            .EnumerateArray()
            .Where(value => value.ValueKind == JsonValueKind.String)
            .Select(value => value.GetString()!)
            .ToHashSet(StringComparer.Ordinal);
        return new[] { AddDocumentPermission, ViewTaskPermission, ViewUiSettingsPermission }
            .Where(permission => !granted.Contains(permission))
            .ToArray();
    }

    private static string? GetString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static DateTimeOffset? GetDate(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String &&
        DateTimeOffset.TryParse(value.GetString(), out var parsed) ? parsed : null;
}

public sealed class PaperlessStartupException(string message) : Exception(message);
