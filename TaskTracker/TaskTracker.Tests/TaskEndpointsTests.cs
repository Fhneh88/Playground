using System.Net;
using System.Net.Http.Json;
using Xunit;

public class TaskEndpointsTests : TestBase
{
    [Fact]
    public async Task GetTasks_ProjectHasTasks_ReturnsList()
    {
        var projectId = await SeedProjectAsync();
        await SeedTaskAsync(projectId, "Task A");
        await SeedTaskAsync(projectId, "Task B");

        var response = await Client.GetAsync($"/api/projects/{projectId}/tasks");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var tasks = await response.Content.ReadFromJsonAsync<List<TaskItem>>();
        Assert.Equal(2, tasks!.Count);
    }

    [Fact]
    public async Task GetTasks_FilterByStatus_ReturnsMatchingOnly()
    {
        var projectId = await SeedProjectAsync();
        var taskId = await SeedTaskAsync(projectId, "Todo Task");
        await Client.PutAsJsonAsync($"/api/tasks/{taskId}/status", new { status = Status.InProgress });
        await SeedTaskAsync(projectId, "Another Todo");

        var response = await Client.GetAsync($"/api/projects/{projectId}/tasks?status=InProgress");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var tasks = await response.Content.ReadFromJsonAsync<List<TaskItem>>();
        Assert.Single(tasks!);
        Assert.Equal("Todo Task", tasks![0].Title);
    }

    [Fact]
    public async Task GetTasks_FilterByPriority_ReturnsMatchingOnly()
    {
        var projectId = await SeedProjectAsync();
        await SeedTaskAsync(projectId, "High Task", priority: Priority.High);
        await SeedTaskAsync(projectId, "Low Task", priority: Priority.Low);

        var response = await Client.GetAsync($"/api/projects/{projectId}/tasks?priority=High");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var tasks = await response.Content.ReadFromJsonAsync<List<TaskItem>>();
        Assert.Single(tasks!);
        Assert.Equal("High Task", tasks![0].Title);
    }

    [Fact]
    public async Task GetTasks_ProjectNotFound_Returns404()
    {
        var response = await Client.GetAsync("/api/projects/99999/tasks");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PostTask_ValidBody_Returns201WithTodoStatus()
    {
        var projectId = await SeedProjectAsync();

        var response = await Client.PostAsJsonAsync(
            $"/api/projects/{projectId}/tasks",
            new { title = "New Task", description = "Desc", priority = Priority.Medium });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var task = await response.Content.ReadFromJsonAsync<TaskItem>();
        Assert.Equal(Status.Todo, task!.Status);
        Assert.Equal("New Task", task.Title);
    }

    [Fact]
    public async Task PostTask_ProjectNotFound_Returns404()
    {
        var response = await Client.PostAsJsonAsync(
            "/api/projects/99999/tasks",
            new { title = "Task", description = "Desc", priority = Priority.Low });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PutTaskStatus_ValidTask_UpdatesAndReturns200()
    {
        var projectId = await SeedProjectAsync();
        var taskId = await SeedTaskAsync(projectId);

        var response = await Client.PutAsJsonAsync(
            $"/api/tasks/{taskId}/status",
            new { status = Status.Done });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<StatusResult>();
        Assert.Equal(Status.Done, result!.Status);
    }

    [Fact]
    public async Task PutTaskStatus_TaskNotFound_Returns404()
    {
        var response = await Client.PutAsJsonAsync(
            "/api/tasks/99999/status",
            new { status = Status.Done });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // DTO helpers
    private record TaskItem(int Id, string Title, Priority Priority, Status Status);
    private record StatusResult(int Id, Status Status);
}
