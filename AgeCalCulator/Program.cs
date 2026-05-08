Console.Write("Введите ваше имя: ");
string name = Console.ReadLine();

int birthYear;

while (true)
{
    Console.Write("Введите год рождения: ");
    string input = Console.ReadLine();

    if (int.TryParse(input, out birthYear))
    {
int currentYear = DateTime.Now.Year;

if (birthYear > currentYear || birthYear < 1900)
{
    Console.WriteLine("Ошибка: введите корректный год рождения.");
    continue;
}

int age = currentYear - birthYear;
Console.WriteLine($"{name}, вам сейчас {age} или {age - 1} год(лет) (зависит от того, был ли уже день рождения в этом году).");
break;
    }
    else
    {
Console.WriteLine("Ошибка: год рождения должен быть числом. Попробуйте ещё раз.");
    }
}