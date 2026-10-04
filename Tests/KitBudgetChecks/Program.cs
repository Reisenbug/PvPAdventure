using PvPAdventure.Common.Independent;

KitBudget.Offer[] shop = [new(1, 10, 9999, 100), new(2, 1, 1, 35000)];
int checks = 0;
void Check(bool condition, string name)
{
    if (!condition) throw new Exception(name);
    checks++;
}
bool Price(Dictionary<int, int> selection, int budget = 100000, int slots = 40) =>
    KitBudget.TryPrice(shop, selection, budget, slots, out _);

Check(KitBudget.TryPrice(shop, new Dictionary<int, int> { [1] = 2, [2] = 1 }, 100000, 3, out int cost)
    && cost == 35200, "Mixed kit has correct price");
Check(Price(new() { [1] = 1 }, 100, 1), "Exact budget and slot boundary accepted");
Check(!Price(new() { [2] = 3 }), "Over-budget kit rejected");
Check(!Price(new() { [1] = 2 }, slots: 1), "Insufficient inventory space rejected");
Check(!Price(new() { [3] = 1 }), "Unknown item rejected");
Check(!Price(new() { [1] = 0 }), "Zero quantity rejected");
Check(!Price(new() { [1] = -1 }), "Negative quantity rejected");
Check(!Price(new() { [2] = int.MaxValue }), "Overflowing request rejected");
Check(!Price(null), "Missing preset rejected");
Check(Price(new()), "Empty preset accepted");
Check(!KitBudget.TryPrice([new(1, 2, 1, 100)], new Dictionary<int, int> { [1] = 1 }, 100000, 40, out _),
    "Invalid stack size rejected");
Check(!KitBudget.TryPrice([new(1, 1, 9999, -1)], new Dictionary<int, int> { [1] = 1 }, 100000, 40, out _),
    "Negative configured price rejected");
Check(!Price(new() { [1] = 1 }, slots: -1), "Invalid starter inventory capacity rejected");
Console.WriteLine($"Passed {checks} kit budget checks.");
