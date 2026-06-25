using System;
using System.Collections.Generic;
using System.Linq;

public class MainClass {
    public static void Main() {
        int n = int.Parse(Console.ReadLine());
        var header = Console.ReadLine().Split(',');

        int catIdx    = Array.IndexOf(header, "category");
        int amountIdx = Array.IndexOf(header, "amount");

        var totals = new Dictionary<string, int>();

        for (int i = 0; i < n; i++)
        {
            var cols = Console.ReadLine().Split(',');
            string cat = cols[catIdx];
            int amount  = int.Parse(cols[amountIdx]);
            totals[cat] = totals.GetValueOrDefault(cat) + amount;
        }

        foreach (var kv in totals.OrderBy(kv => kv.Key))
            Console.WriteLine($"{kv.Key} {kv.Value}");
    }
}
