using System.Net;
using System.Net.Http.Json;
using Xunit;

public class ProjectStatsEndpointsTests : TestBase
{
    [Fact]
    public async Task GetStats_ProjectNotFound_Returns404()
    {
        var response = await Client.GetAsync("/api/projects/99999/stats");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetStats_NoTasks_ReturnsZeroCountsAndNullAverage()
    {
        var projectId = await SeedProjectAsync();

        var response = await Client.GetAsync($"/api/projects/{projectId}/stats");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var stats = await response.Content.ReadFromJsonAsync<ProjectStats>();
        Assert.Equal(0, stats!.TotalTasks);
        Assert.Equal(0, stats.ByStatus.Todo);
        Assert.Equal(0, stats.ByStatus.InProgress);
        Assert.Equal(0, stats.ByStatus.Done);
        Assert.Null(stats.AverageCreatedAt);
    }

    [Fact]
    public async Task GetStats_WithMixedStatuses_ReturnsCorrectCounts()
    {
        var projectId = await SeedProjectAsync();
        var task1 = await SeedTaskAsync(projectId, "Task 1");
        var task2 = await SeedTaskAsync(projectId, "Task 2");
        var task3 = await SeedTaskAsync(projectId, "Task 3");

        await Client.PutAsJsonAsync($"/api/tasks/{task2}/status", new { status = Status.InProgress });
        await Client.PutAsJsonAsync($"/api/tasks/{task3}/status", new { status = Status.Done });

        var response = await Client.GetAsync($"/api/projects/{projectId}/stats");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var stats = await response.Content.ReadFromJsonAsync<ProjectStats>();
        Assert.Equal(3, stats!.TotalTasks);
        Assert.Equal(1, stats.ByStatus.Todo);
        Assert.Equal(1, stats.ByStatus.InProgress);
        Assert.Equal(1, stats.ByStatus.Done);
        Assert.NotNull(stats.AverageCreatedAt);
    }

    [Fact]
    public async Task GetStats_AllSameStatus_ReturnsZeroForOtherStatuses()
    {
        var projectId = await SeedProjectAsync();
        await SeedTaskAsync(projectId, "Task A");
        await SeedTaskAsync(projectId, "Task B");

        var response = await Client.GetAsync($"/api/projects/{projectId}/stats");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var stats = await response.Content.ReadFromJsonAsync<ProjectStats>();
        Assert.Equal(2, stats!.TotalTasks);
        Assert.Equal(2, stats.ByStatus.Todo);
        Assert.Equal(0, stats.ByStatus.InProgress);
        Assert.Equal(0, stats.ByStatus.Done);
    }

    private record ProjectStats(int ProjectId, int TotalTasks, StatusCounts ByStatus, DateTime? AverageCreatedAt);
    private record StatusCounts(int Todo, int InProgress, int Done);
}
