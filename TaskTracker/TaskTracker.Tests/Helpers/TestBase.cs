using Microsoft.Extensions.DependencyInjection;
using System.Net.Http.Json;

public abstract class TestBase : IDisposable
{
    protected readonly CustomWebApplicationFactory Factory;
    protected readonly HttpClient Client;

    protected TestBase()
    {
        Factory = new CustomWebApplicationFactory();
        Client = Factory.CreateClient();
    }

    protected async Task<int> SeedProjectAsync(string name = "Test Project", string description = "Desc")
    {
        var response = await Client.PostAsJsonAsync("/api/projects", new { name, description });
        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<IdResponse>();
        return created!.Id;
    }

    protected async Task<int> SeedTaskAsync(int projectId, string title = "Test Task",
        string description = "Desc", Priority priority = Priority.Medium)
    {
        var response = await Client.PostAsJsonAsync(
            $"/api/projects/{projectId}/tasks",
            new { title, description, priority });
        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<IdResponse>();
        return created!.Id;
    }

    protected async Task<int> SeedTagAsync(string name)
    {
        var response = await Client.PostAsJsonAsync("/api/tags", new { name });
        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<IdResponse>();
        return created!.Id;
    }

    public void Dispose()
    {
        Client.Dispose();
        Factory.Dispose();
    }

    private record IdResponse(int Id);
}
