using System.Text.Json;

namespace PaperlessImporter.Tests;

public sealed class PaperlessClientTests
{
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
}
