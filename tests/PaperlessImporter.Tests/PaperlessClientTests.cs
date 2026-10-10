using System.Text.Json;

namespace PaperlessImporter.Tests;

public sealed class PaperlessClientTests
{
    [Fact]
    public void AcceptsRequiredLeastPrivilegePermissions()
    {
        using var json = JsonDocument.Parse("""
            {
              "permissions": [
                "add_document",
                "view_paperlesstask",
                "view_uisettings"
              ],
              "user": {"is_superuser": false}
            }
            """);

        Assert.Empty(PaperlessClient.FindMissingPermissions(json.RootElement));
    }

    [Fact]
    public void ReportsEveryMissingStartupPermission()
    {
        using var json = JsonDocument.Parse("""
            {"permissions": [], "user": {"is_superuser": false}}
            """);

        var missing = PaperlessClient.FindMissingPermissions(json.RootElement);

        Assert.Equal(
            ["add_document", "view_paperlesstask", "view_uisettings"],
            missing);
    }

    [Fact]
    public async Task StartupPreflightAlsoProbesTaskEndpoint()
    {
        var handler = new QueueHandler(
            JsonResponse("""
                {
                  "permissions": [
                    "add_document",
                    "view_paperlesstask",
                    "view_uisettings"
                  ],
                  "user": {"is_superuser": false}
                }
                """),
            JsonResponse("{\"results\": []}"));
        using var http = new HttpClient(handler);
        var client = new PaperlessClient(
            http,
            Microsoft.Extensions.Options.Options.Create(new PaperlessOptions
            {
                BaseUrl = new Uri("https://paperless.example.invalid/"),
                Token = "synthetic-token"
            }));

        await client.VerifyStartupPrerequisitesAsync(TestContext.Current.CancellationToken);

        Assert.Equal(["/api/ui_settings/", "/api/tasks/?page_size=1"], handler.RequestTargets);
    }

    [Fact]
    public async Task StartupPreflightFailsBeforeTaskProbeWhenPermissionIsMissing()
    {
        var handler = new QueueHandler(JsonResponse("""
            {
              "permissions": ["add_document", "view_uisettings"],
              "user": {"is_superuser": false}
            }
            """));
        using var http = new HttpClient(handler);
        var client = new PaperlessClient(
            http,
            Microsoft.Extensions.Options.Options.Create(new PaperlessOptions
            {
                BaseUrl = new Uri("https://paperless.example.invalid/"),
                Token = "synthetic-token"
            }));

        var exception = await Assert.ThrowsAsync<PaperlessStartupException>(() =>
            client.VerifyStartupPrerequisitesAsync(TestContext.Current.CancellationToken));

        Assert.Contains("view_paperlesstask", exception.Message, StringComparison.Ordinal);
        Assert.Single(handler.RequestTargets);
    }

    [Fact]
    public async Task StartupPreflightFailsWhenEffectiveTaskAccessIsRejected()
    {
        var handler = new QueueHandler(
            JsonResponse("""
                {
                  "permissions": [
                    "add_document",
                    "view_paperlesstask",
                    "view_uisettings"
                  ],
                  "user": {"is_superuser": false}
                }
                """),
            new HttpResponseMessage(System.Net.HttpStatusCode.Forbidden));
        using var http = new HttpClient(handler);
        var client = new PaperlessClient(
            http,
            Microsoft.Extensions.Options.Options.Create(new PaperlessOptions
            {
                BaseUrl = new Uri("https://paperless.example.invalid/"),
                Token = "synthetic-token"
            }));

        var exception = await Assert.ThrowsAsync<PaperlessStartupException>(() =>
            client.VerifyStartupPrerequisitesAsync(TestContext.Current.CancellationToken));

        Assert.Contains("HTTP 403", exception.Message, StringComparison.Ordinal);
        Assert.Equal(2, handler.RequestTargets.Count);
    }

    [Fact]
    public void ParsesArrayTaskResponse()
    {
        using var json = JsonDocument.Parse("""
            [{
              "task_id": "7fb074b8-cfad-4b41-8fb0-c47a1d147d4d",
              "task_file_name": "synthetic.pdf",
              "date_created": "2026-10-09T12:00:00Z",
              "status": "SUCCESS"
            }]
            """);

        var task = Assert.Single(PaperlessClient.ParseTasks(json.RootElement));

        Assert.Equal("synthetic.pdf", task.FileName);
        Assert.Equal(PaperlessTaskState.Success, task.State);
        Assert.Equal(new DateTimeOffset(2026, 10, 9, 12, 0, 0, TimeSpan.Zero), task.CreatedAt);
    }

    [Fact]
    public void ParsesPaginatedTaskResponseAndFailure()
    {
        using var json = JsonDocument.Parse("""
            {"results": [{
              "task_id": "7fb074b8-cfad-4b41-8fb0-c47a1d147d4d",
              "task_file_name": "synthetic.pdf",
              "date_created": "2026-10-09T12:00:00+00:00",
              "status": "FAILURE"
            }]}
            """);

        var task = Assert.Single(PaperlessClient.ParseTasks(json.RootElement));

        Assert.Equal(PaperlessTaskState.Failure, task.State);
        Assert.Equal("paperless-task-failed", task.ErrorCode);
    }

    private static HttpResponseMessage JsonResponse(string json) =>
        new(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
        };

    private sealed class QueueHandler(params HttpResponseMessage[] responses) : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new(responses);
        public List<string> RequestTargets { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestTargets.Add(request.RequestUri?.PathAndQuery ?? string.Empty);
            return Task.FromResult(_responses.Dequeue());
        }
    }
}
