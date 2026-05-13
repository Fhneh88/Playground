int n = int.Parse(Console.ReadLine()!);
var tasks = new List<(string Text, bool Done)>();

for (int i = 0; i < n; i++)
{
    string line = Console.ReadLine()!;

    if (line.StartsWith("add "))
    {
        string text = line[4..];
        tasks.Add((text, false));
        Console.WriteLine($"added: {text}");
    }
    else if (line.StartsWith("done "))
    {
        int idx = int.Parse(line[5..]) - 1;
        if (idx < 0 || idx >= tasks.Count)
        {
            Console.WriteLine("bad index");
        }
        else
        {
            tasks[idx] = (tasks[idx].Text, true);
            Console.WriteLine($"done: {tasks[idx].Text}");
        }
    }
    else if (line == "list")
    {
        if (tasks.Count == 0)
        {
            Console.WriteLine("empty");
        }
        else
        {
            foreach (var (text, done) in tasks)
                Console.WriteLine($"{(done ? "[x]" : "[ ]")} {text}");
        }
    }
}
