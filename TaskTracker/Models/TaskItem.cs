public class TaskItem
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Priority Priority { get; set; }
    public Status Status { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public List<Tag> Tags { get; set; } = new();
}
