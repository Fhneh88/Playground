using System.Net;
using System.Net.Http.Json;
using Xunit;

public class ProjectEndpointsTests : TestBase
{
    [Fact]
    public async Task GetProjects_EmptyDb_ReturnsEmptyList()
    {
        var response = await Client.GetAsync("/api/projects");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var projects = await response.Content.ReadFromJsonAsync<List<object>>();
        Assert.Empty(projects!);
    }

    [Fact]
    public async Task GetProjects_WithProjects_ReturnsCorrectTaskCount()
    {
        var projectId = await SeedProjectAsync("Alpha");
        await SeedTaskAsync(projectId, "T1");
        await SeedTaskAsync(projectId, "T2");

        var response = await Client.GetAsync("/api/projects");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var projects = await response.Content.ReadFromJsonAsync<List<ProjectListItem>>();
        var project = Assert.Single(projects!);
        Assert.Equal("Alpha", project.Name);
        Assert.Equal(2, project.TaskCount);
    }

    [Fact]
    public async Task PostProject_ValidBody_Returns201WithLocation()
    {
        var response = await Client.PostAsJsonAsync("/api/projects",
            new { name = "New Project", description = "Some description" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
    }

    [Fact]
    public async Task GetProject_Exists_ReturnsProjectWithTasksAndTags()
    {
        var projectId = await SeedProjectAsync("Detail Project");
        var taskId = await SeedTaskAsync(projectId, "My Task");
        var tagId = await SeedTagAsync("urgent");
        await Client.PostAsync($"/api/tasks/{taskId}/tags/{tagId}", null);

        var response = await Client.GetAsync($"/api/projects/{projectId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var project = await response.Content.ReadFromJsonAsync<ProjectDetail>();
        Assert.Equal("Detail Project", project!.Name);
        var task = Assert.Single(project.Tasks);
        Assert.Equal("My Task", task.Title);
        var tag = Assert.Single(task.Tags);
        Assert.Equal("urgent", tag.Name);
    }

    [Fact]
    public async Task GetProject_NotFound_Returns404()
    {
        var response = await Client.GetAsync("/api/projects/99999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // DTO helpers
    private record ProjectListItem(int Id, string Name, string Description, int TaskCount);
    private record ProjectDetail(int Id, string Name, List<TaskDetail> Tasks);
    private record TaskDetail(int Id, string Title, List<TagItem> Tags);
    private record TagItem(int Id, string Name);
}
