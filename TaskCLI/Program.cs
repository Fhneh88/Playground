using TaskCLI;

const string StoragePath = "tasks.txt";

if (args.Length == 0)
{
    PrintUsage();
    return 1;
}

return args[0].ToLowerInvariant() switch
{
    "add"    => Add(args.Skip(1).ToArray()),
    "list"   => List(args.Skip(1).ToArray()),
    "done"   => Done(args.Skip(1).ToArray()),
    "delete" => Delete(args.Skip(1).ToArray()),
    "edit"   => Edit(args.Skip(1).ToArray()),
    _        => UnknownCommand(args[0])
};

// ── Команды ──────────────────────────────────────────────────────────────────

static int Add(string[] parts)
{
    if (parts.Length == 0)
    {
        Console.Error.WriteLine("Укажите текст задачи.");
        return 1;
    }

    var text  = string.Join(' ', parts);
    var tasks = TaskStorage.Load(StoragePath);
    tasks.Add(new TaskItem { Text = text });
    TaskStorage.Save(StoragePath, tasks);
    Console.WriteLine($"Добавлена задача #{tasks.Count}");
    return 0;
}

static int List(string[] parts)
{
    bool onlyOpen = parts.Contains("--only-open");
    var tasks     = TaskStorage.Load(StoragePath);

    if (tasks.Count == 0)
    {
        Console.WriteLine("Задач пока нет.");
        return 0;
    }

    bool anyPrinted = false;
    for (int i = 0; i < tasks.Count; i++)
    {
        if (onlyOpen && tasks[i].IsDone) continue;
        string mark = tasks[i].IsDone ? "x" : " ";
        Console.WriteLine($"{i + 1}. [{mark}] {tasks[i].Text}");
        anyPrinted = true;
    }

    if (!anyPrinted)
        Console.WriteLine("Все задачи выполнены. Добавьте новые или уберите --only-open.");

    return 0;
}

static int Done(string[] parts)
{
    var tasks = TaskStorage.Load(StoragePath);
    if (!TryResolveIndex(parts, tasks, out int idx)) return 1;

    if (tasks[idx].IsDone)
    {
        Console.WriteLine($"Задача #{idx + 1} уже была отмечена как выполненная.");
        return 0;
    }

    tasks[idx].IsDone = true;
    TaskStorage.Save(StoragePath, tasks);
    Console.WriteLine($"Задача #{idx + 1} отмечена как выполненная.");
    return 0;
}

static int Delete(string[] parts)
{
    var tasks = TaskStorage.Load(StoragePath);
    if (!TryResolveIndex(parts, tasks, out int idx)) return 1;

    int number = idx + 1;
    tasks.RemoveAt(idx);
    TaskStorage.Save(StoragePath, tasks);
    Console.WriteLine($"Задача #{number} удалена.");
    return 0;
}

static int Edit(string[] parts)
{
    // edit <номер> <новый текст>
    if (parts.Length < 2)
    {
        Console.Error.WriteLine("Использование: edit <номер> <новый текст>");
        return 1;
    }

    var tasks = TaskStorage.Load(StoragePath);
    if (!TryResolveIndex(parts[..1], tasks, out int idx)) return 1;

    tasks[idx].Text = string.Join(' ', parts.Skip(1));
    TaskStorage.Save(StoragePath, tasks);
    Console.WriteLine($"Задача #{idx + 1} обновлена.");
    return 0;
}

static int UnknownCommand(string command)
{
    Console.Error.WriteLine($"Неизвестная команда: \"{command}\"");
    PrintUsage();
    return 1;
}

static void PrintUsage()
{
    Console.WriteLine("""
        TaskCLI - менеджер задач в терминале

        Команды:
          add <текст>              Добавить новую задачу
          list [--only-open]       Показать задачи (флаг скрывает выполненные)
          done <номер>             Отметить задачу как выполненную
          delete <номер>           Удалить задачу
          edit <номер> <текст>     Изменить текст задачи
        """);
}

// ── Вспомогательный метод ────────────────────────────────────────────────────

static bool TryResolveIndex(string[] parts, List<TaskItem> tasks, out int index)
{
    index = -1;

    if (parts.Length == 0)
    {
        Console.Error.WriteLine("Укажите номер задачи.");
        return false;
    }

    if (!int.TryParse(parts[0], out int number))
    {
        Console.Error.WriteLine($"Некорректный номер: \"{parts[0]}\". Ожидается целое число.");
        return false;
    }

    if (number < 1 || number > tasks.Count)
    {
        Console.Error.WriteLine($"Задача #{number} не существует. Всего задач: {tasks.Count}.");
        return false;
    }

    index = number - 1;
    return true;
}
