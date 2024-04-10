using System;
using System.Collections.Generic;
using System.Linq;

// This class implements the weighted random picker where less frequently picked items
// are more likely to be selected. It also includes mechanisms for decay and fair initialization of new items.
public class WeightedRandomPicker
{
    private Dictionary<int, int> selectionFrequencies; // Tracks the selection frequencies of each item.
    private Random random; // Random number generator for item selection.
    private int totalSelections; // Total number of selections made across all items.

    // Constructor initializes the picker with a set of items, each with an initial selection frequency of 0.
    public WeightedRandomPicker(IEnumerable<int> items)
    {
        this.selectionFrequencies = items.ToDictionary(item => item, item => 0);
        this.random = new Random();
        this.totalSelections = 0; // Initially, no selections have been made.
    }

    // Picks an item with probability inversely proportional to its selection frequency, ensuring a fair chance for all.
    public int Pick()
    {
        if (totalSelections == 0)
        {
            // If it's the first pick, select an item randomly since no selections have been made yet.
            var allItems = selectionFrequencies.Keys.ToList();
            var randomItem = allItems[random.Next(allItems.Count)];
            selectionFrequencies[randomItem]++;
            totalSelections++;
            return randomItem;
        }

        // For subsequent picks, calculate selection probabilities based on the inverse of selection frequencies.
        var probabilities = selectionFrequencies.ToDictionary(
            item => item.Key,
            item => 1.0 / (1 + item.Value));

        var totalProbability = probabilities.Values.Sum(); // Sum of all probabilities for normalization.

        // Generate a random number within the total probability range to select an item.
        var randomNumber = random.NextDouble() * totalProbability;
        double cumulativeProbability = 0;

        foreach (var item in probabilities)
        {
            cumulativeProbability += item.Value;
            if (randomNumber < cumulativeProbability)
            {
                selectionFrequencies[item.Key]++;
                totalSelections++;
                
                // Apply decay to adjust selection frequencies dynamically every N selections.
                if (totalSelections % 50 == 0) ApplyDecay(0.1); // Example: Apply 10% decay every 50 selections.
                
                return item.Key;
            }
        }

        throw new InvalidOperationException("Selection failed, which should be impossible.");
    }

    // Adds a new item with an initial selection frequency based on the average to mitigate new item favoritism.
    public void AddItem(int newItem)
    {
        if (!selectionFrequencies.ContainsKey(newItem))
        {
            var averageFrequency = selectionFrequencies.Count > 0 ? selectionFrequencies.Values.Average() : 0;
            int initialFrequency = (int)Math.Round(averageFrequency, MidpointRounding.AwayFromZero);
            initialFrequency = Math.Max(initialFrequency, 1); // Ensure a minimum frequency of 1.
            selectionFrequencies.Add(newItem, initialFrequency);
            totalSelections += initialFrequency;
        }
    }

    // Applies a decay to selection frequencies to ensure dynamism in item selection over time.
    private void ApplyDecay(double decayFactor)
    {
        decayFactor = Math.Clamp(decayFactor, 0, 1); // Ensures decayFactor is within a valid range.

        var keys = selectionFrequencies.Keys.ToList();
        foreach (var key in keys)
        {
            // Apply decay and ensure at least a frequency of 1 to keep all items selectable.
            selectionFrequencies[key] = (int)Math.Max(1, Math.Floor(selectionFrequencies[key] * (1 - decayFactor)));
        }

        // Recalculate total selections to accurately reflect the decayed frequencies.
        totalSelections = selectionFrequencies.Values.Sum();
    }
}

class Program
{
    static void Main()
    {
        var items = Enumerable.Range(1, 5); // Initialize with items numbered 1 through 5.
        var picker = new WeightedRandomPicker(items);

        // Demonstrate item selection 20 times, showing adjustment in selection probabilities.
        for (int i = 0; i < 20; i++)
        {
            Console.WriteLine($"Picked: {picker.Pick()}");
        }
    }
}
