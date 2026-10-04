using System.Collections.Generic;
using System.Linq;

namespace PvPAdventure.Common.Independent;

internal static class KitBudget
{
    internal readonly record struct Offer(int Type, int Stack, int MaxStack, int Price);

    internal static bool TryPrice(IEnumerable<Offer> catalog, IReadOnlyDictionary<int, int> selection,
        int budget, int availableSlots, out int price)
    {
        price = 0;
        if (selection == null || budget < 0 || availableSlots < 0)
            return false;
        Dictionary<int, Offer> offers = [];
        foreach (Offer offer in catalog)
            offers.TryAdd(offer.Type, offer);
        long total = 0;
        long slots = 0;
        foreach (var purchase in selection)
        {
            if (!offers.TryGetValue(purchase.Key, out Offer offer) || offer.Type <= 0 ||
                offer.Price < 0 || offer.Stack < 1 || offer.Stack > offer.MaxStack || purchase.Value < 1)
                return false;
            slots += purchase.Value;
            total += (long)offer.Price * purchase.Value;
            if (slots > availableSlots || total > budget)
                return false;
        }
        price = (int)total;
        return true;
    }
}
