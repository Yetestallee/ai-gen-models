using System;
using System.Collections.Generic;

namespace MeshyWorkspace
{
    /// <summary>
    /// Credit cost tables. Prices follow the requirements document and can be
    /// updated when Meshy publishes new pricing.
    /// </summary>
    public static class MeshyPricing
    {
        public static readonly IReadOnlyDictionary<string, int> ImagePrices =
            new Dictionary<string, int>
            {
                { "nano-banana", 3 },
                { "nano-banana-2", 6 },
                { "nano-banana-pro", 9 },
                { "gpt-image-2", 15 }
            };

        public static int ImageCost(string model, int count)
        {
            var price = 0;
            if (!string.IsNullOrEmpty(model) && ImagePrices.TryGetValue(model, out price))
            {
                // price resolved
            }
            else
            {
                price = 3;
            }

            return price * Math.Max(1, count);
        }

        public static int ModelPreviewCost(string aiModel)
        {
            return 20;
        }

        public static int ModelRefineCost(string aiModel)
        {
            return 10;
        }
    }
}
