using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseHttpsRedirection();

app.MapGet("/", () => "TaskTracker API");

// --- Projects ---

app.MapGet("/api/projects", async (AppDbContext db) =>
{
    var projects = await db.Projects
        .AsNoTracking()
        .Select(p => new
        {
            p.Id,
            p.Name,
            p.Description,
            p.CreatedAt,
            TaskCount = p.Tasks.Count
        })
        .ToListAsync();

    return Results.Ok(projects);
});

app.MapPost("/api/projects", async (AppDbContext db, CreateProjectRequest request) =>
{
    var project = new Project
    {
        Name = request.Name,
        Description = request.Description
    };

    db.Projects.Add(project);
    await db.SaveChangesAsync();

    return Results.Created($"/api/projects/{project.Id}", project);
});

app.MapGet("/api/projects/{id:int}", async (AppDbContext db, int id) =>
{
    var project = await db.Projects
        .AsNoTracking()
        .Include(p => p.Tasks)
            .ThenInclude(t => t.Tags)
        .FirstOrDefaultAsync(p => p.Id == id);

    if (project is null)
        return Results.NotFound(new { message = $"Project {id} not found" });

    return Results.Ok(project);
});

app.MapGet("/api/projects/{id:int}/stats", async (AppDbContext db, int id) =>
{
    var projectExists = await db.Projects.AnyAsync(p => p.Id == id);
    if (!projectExists)
        return Results.NotFound(new { message = $"Project {id} not found" });

    var tasks = await db.Tasks
        .AsNoTracking()
        .Where(t => t.ProjectId == id)
        .Select(t => new { t.Status, t.CreatedAt })
        .ToListAsync();

    var totalCount = tasks.Count;

    var byStatus = tasks
        .GroupBy(t => t.Status)
        .ToDictionary(g => g.Key, g => g.Count());

    DateTime? averageCreatedAt = totalCount > 0
        ? new DateTime((long)tasks.Average(t => t.CreatedAt.Ticks), DateTimeKind.Utc)
        : null;

    return Results.Ok(new
    {
        ProjectId = id,
        TotalTasks = totalCount,
        ByStatus = new
        {
            Todo = byStatus.GetValueOrDefault(Status.Todo, 0),
            InProgress = byStatus.GetValueOrDefault(Status.InProgress, 0),
            Done = byStatus.GetValueOrDefault(Status.Done, 0)
        },
        AverageCreatedAt = averageCreatedAt
    });
});

// --- Tasks ---

app.MapGet("/api/projects/{projectId:int}/tasks", async (
    AppDbContext db,
    int projectId,
    Status? status,
    Priority? priority) =>
{
    var projectExists = await db.Projects.AnyAsync(p => p.Id == projectId);
    if (!projectExists)
        return Results.NotFound(new { message = $"Project {projectId} not found" });

    var query = db.Tasks
        .AsNoTracking()
        .Where(t => t.ProjectId == projectId);

    if (status.HasValue)
        query = query.Where(t => t.Status == status.Value);

    if (priority.HasValue)
        query = query.Where(t => t.Priority == priority.Value);

    var tasks = await query
        .Select(t => new
        {
            t.Id,
            t.Title,
            t.Description,
            t.Priority,
            t.Status,
            t.CreatedAt,
            Tags = t.Tags.Select(tag => new { tag.Id, tag.Name })
        })
        .ToListAsync();

    return Results.Ok(tasks);
});

app.MapPost("/api/projects/{projectId:int}/tasks", async (
    AppDbContext db,
    int projectId,
    CreateTaskRequest request) =>
{
    var projectExists = await db.Projects.AnyAsync(p => p.Id == projectId);
    if (!projectExists)
        return Results.NotFound(new { message = $"Project {projectId} not found" });

    var task = new TaskItem
    {
        Title = request.Title,
        Description = request.Description,
        Priority = request.Priority,
        Status = Status.Todo,
        ProjectId = projectId
    };

    db.Tasks.Add(task);
    await db.SaveChangesAsync();

    return Results.Created($"/api/projects/{projectId}/tasks/{task.Id}", task);
});

app.MapPut("/api/tasks/{id:int}/status", async (
    AppDbContext db,
    int id,
    UpdateStatusRequest request) =>
{
    var task = await db.Tasks.FindAsync(id);
    if (task is null)
        return Results.NotFound(new { message = $"Task {id} not found" });

    task.Status = request.Status;
    await db.SaveChangesAsync();

    return Results.Ok(new { task.Id, task.Status });
});

// --- Tags ---

app.MapPost("/api/tags", async (AppDbContext db, CreateTagRequest request) =>
{
    var tag = new Tag { Name = request.Name };

    db.Tags.Add(tag);
    await db.SaveChangesAsync();

    return Results.Created($"/api/tags/{tag.Id}", tag);
});

app.MapPost("/api/tasks/{taskId:int}/tags/{tagId:int}", async (
    AppDbContext db,
    int taskId,
    int tagId) =>
{
    var task = await db.Tasks
        .Include(t => t.Tags)
        .FirstOrDefaultAsync(t => t.Id == taskId);

    if (task is null)
        return Results.NotFound(new { message = $"Task {taskId} not found" });

    var tag = await db.Tags.FindAsync(tagId);
    if (tag is null)
        return Results.NotFound(new { message = $"Tag {tagId} not found" });

    if (task.Tags.Any(t => t.Id == tagId))
        return Results.Conflict(new { message = "Tag already attached to this task" });

    task.Tags.Add(tag);
    await db.SaveChangesAsync();

    return Results.Ok(new { taskId, tagId });
});

app.Run();

public partial class Program {}

public record CreateProjectRequest(string Name, string Description);
public record CreateTaskRequest(string Title, string Description, Priority Priority);
public record UpdateStatusRequest(Status Status);
public record CreateTagRequest(string Name);
