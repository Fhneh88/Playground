var parts = Console.ReadLine()!.Split(' ');
int a = int.Parse(parts[0]);
string op = parts[1];
int b = int.Parse(parts[2]);

if (op == "/" && b == 0)
{
    Console.WriteLine("error");
    return;
}

int result = op switch
{
    "+" => a + b,
    "-" => a - b,
    "*" => a * b,
    "/" => a / b,
    _ => throw new InvalidOperationException()
};

Console.WriteLine(result);
