namespace TaskCLI;

// Отвечает только за чтение и запись файла.
// Остальной код не знает, как именно задачи хранятся на диске.
public static class TaskStorage
{
    private const char Separator = '\t';

    public static List<TaskItem> Load(string path)
    {
        if (!File.Exists(path))
            return [];

        return File.ReadAllLines(path)
            .Where(line => line.Contains(Separator))
            .Select(line =>
            {
                var parts = line.Split(Separator, 2);
                return new TaskItem
                {
                    IsDone = parts[0] == "1",
                    Text   = parts[1]
                };
            })
            .ToList();
    }

    public static void Save(string path, List<TaskItem> tasks)
    {
        var lines = tasks.Select(t => $"{(t.IsDone ? "1" : "0")}{Separator}{t.Text}");
        File.WriteAllLines(path, lines);
    }
}
