using System;
using System.Collections.Generic;
using System.Linq;

public class MainClass {
    public static void Main() {
        int n = int.Parse(Console.ReadLine());
        var counts = new Dictionary<string, int>();

        for (int i = 0; i < n; i++)
        {
            string line = Console.ReadLine();
            int space = line.IndexOf(' ');
            string ip = line.Substring(0, space);
            counts[ip] = counts.GetValueOrDefault(ip) + 1;
        }

        var top = counts
            .OrderByDescending(kv => kv.Value)
            .ThenBy(kv => kv.Key)
            .Take(3);

        foreach (var kv in top)
            Console.WriteLine($"{kv.Key} {kv.Value}");
    }
}
