using System.Net;
using System.Net.Http.Json;
using Xunit;

public class TagEndpointsTests : TestBase
{
    [Fact]
    public async Task PostTag_ValidName_Returns201WithLocation()
    {
        var response = await Client.PostAsJsonAsync("/api/tags", new { name = "backend" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
    }

    [Fact]
    public async Task AttachTag_ValidTaskAndTag_Returns200()
    {
        var projectId = await SeedProjectAsync();
        var taskId = await SeedTaskAsync(projectId);
        var tagId = await SeedTagAsync("feature");

        var response = await Client.PostAsync($"/api/tasks/{taskId}/tags/{tagId}", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task AttachTag_TaskNotFound_Returns404()
    {
        var tagId = await SeedTagAsync("bug");

        var response = await Client.PostAsync($"/api/tasks/99999/tags/{tagId}", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AttachTag_TagNotFound_Returns404()
    {
        var projectId = await SeedProjectAsync();
        var taskId = await SeedTaskAsync(projectId);

        var response = await Client.PostAsync($"/api/tasks/{taskId}/tags/99999", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AttachTag_AlreadyAttached_Returns409()
    {
        var projectId = await SeedProjectAsync();
        var taskId = await SeedTaskAsync(projectId);
        var tagId = await SeedTagAsync("duplicate");

        await Client.PostAsync($"/api/tasks/{taskId}/tags/{tagId}", null);
        var response = await Client.PostAsync($"/api/tasks/{taskId}/tags/{tagId}", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }
}
