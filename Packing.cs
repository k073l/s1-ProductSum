namespace ProductSum;

#if MONO
using ScheduleOne.Economy;
#else
using Il2CppScheduleOne.Economy;
#endif

public class Packing
{
    public string Name { get; set; }
    public Dictionary<string, int> Items { get; private set; }
    
    public static readonly SortedDictionary<string, int> PackagingCapacities = new SortedDictionary<string, int>()
    {
        { "Baggie", 1 },
        { "Jar", 5 },
        { "Brick", 20 }
    };

    public Packing(string name, int quantity)
    {
        Name = name;
        Items = DistributeQuantityToPackaging(quantity);
    }
    
    public Packing(string name, Dictionary<string, int> items)
    {
        Name = name;
        Items = new Dictionary<string, int>(items);
    }

    private static Dictionary<string, int> DistributeQuantityToPackaging(int quantity)
    {
        var result = new Dictionary<string, int>();
        int remainingQuantity = quantity;

        // process packaging types in descending order of capacity (largest first)
        foreach (var packaging in PackagingCapacities.OrderByDescending(p => p.Value))
        {
            string packagingType = packaging.Key;
            int packagingCapacity = packaging.Value;

            if (remainingQuantity >= packagingCapacity)
            {
                int count = remainingQuantity / packagingCapacity;
                result[packagingType] = count;
                remainingQuantity %= packagingCapacity;
            }
        }

        // if there's still some quantity left, use the smallest packaging for the remainder
        if (remainingQuantity > 0 && PackagingCapacities.Count > 0)
        {
            string smallestPackaging = PackagingCapacities.OrderBy(p => p.Value).First().Key;
            if (result.ContainsKey(smallestPackaging))
                result[smallestPackaging] += 1;
            else
                result[smallestPackaging] = 1;
        }

        return result;
    }

    // merge this packing with another packing of the same product
    public void MergeWith(Packing other)
    {
        if (Name != other.Name)
            throw new ArgumentException("Cannot merge packings of different products");

        foreach (var item in other.Items)
        {
            if (Items.ContainsKey(item.Key))
                Items[item.Key] += item.Value;
            else
                Items[item.Key] = item.Value;
        }
    }

    // Flattens packaging into total item count
    public Dictionary<string, int> Flatten()
    {
        Dictionary<string, int> flat = new Dictionary<string, int>();

        foreach (var item in Items)
        {
            string packagingType = item.Key;
            int count = item.Value;

            if (PackagingCapacities.ContainsKey(packagingType))
            {
                flat[Name] = (flat.ContainsKey(Name) ? flat[Name] : 0) +
                             count * PackagingCapacities[packagingType];
            }
        }

        return flat;
    }

    // get the total quantity of this item across all packaging types
    public int GetTotalQuantity()
    {
        int total = 0;
        foreach (var item in Items)
        {
            if (PackagingCapacities.ContainsKey(item.Key))
            {
                total += item.Value * PackagingCapacities[item.Key];
            }
        }

        return total;
    }
}

public class AbbreviatedDealInfo
{
    public EDealWindow TimeSlot { get; set; }
    public Dictionary<string, Packing> Products { get; set; }

    public AbbreviatedDealInfo(EDealWindow timeSlot)
    {
        TimeSlot = timeSlot;
        Products = new Dictionary<string, Packing>();
    }

    public void AddProduct(string productName, int quantity)
    {
        var newPacking = new Packing(productName, quantity);

        if (Products.ContainsKey(productName))
        {
            Products[productName].MergeWith(newPacking);
        }
        else
        {
            Products[productName] = newPacking;
        }
    }

    // for merging all products from another time slot
    public void MergeFrom(AbbreviatedDealInfo other)
    {
        foreach (var product in other.Products)
        {
            if (Products.ContainsKey(product.Key))
            {
                Products[product.Key].MergeWith(product.Value);
            }
            else
            {
                // deep copy of the Packing object to avoid reference issues
                Products[product.Key] = new Packing(product.Key, new Dictionary<string, int>(product.Value.Items));
            }
        }
    }

    // converts to simple dictionary mapping product names to total quantities
    public Dictionary<string, int> FlattenProducts()
    {
        Dictionary<string, int> flattened = new Dictionary<string, int>();

        foreach (var product in Products)
        {
            string productName = product.Key;
            int totalQuantity = product.Value.GetTotalQuantity();

            flattened[productName] = totalQuantity;
        }

        return flattened;
    }

    public string GetFormattedSummary(bool showPackaging)
    {
        var sb = new System.Text.StringBuilder();
        string timeSlotLabel = Enum.GetName(typeof(EDealWindow), TimeSlot);
        sb.AppendLine($"<u>{timeSlotLabel}</u>");

        foreach (var product in Products)
        {
            string productName = product.Key;

            if (showPackaging)
            {
                sb.Append($"<i>{productName}</i>: ");
                var packagingDetails = new List<string>();

                foreach (var packaging in product.Value.Items)
                {
                    packagingDetails.Add($"<b>{packaging.Value}</b>x {packaging.Key}");
                }

                sb.AppendLine(string.Join(", ", packagingDetails));
            }
            else
            {
                int totalQuantity = product.Value.GetTotalQuantity();
                sb.AppendLine($"<b>{totalQuantity}</b>x <i>{productName}</i>");
            }
        }

        return sb.ToString();
    }
}