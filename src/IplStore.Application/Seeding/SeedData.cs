using IplStore.Domain.Catalogue;

namespace IplStore.Application.Seeding;

/// <summary>
/// The walkthrough products from the brief, plus two more so paging is visible.
/// Products are created one second apart, so insert order is the catalogue order.
/// </summary>
public static class SeedData
{
    public static IReadOnlyList<Product> Products(DateTimeOffset start)
    {
        var products = new List<Product>();

        void Add(string id, string name, ProductType type, string franchiseId, string description, decimal price, int stock)
        {
            var franchise = Franchises.Find(franchiseId)!;
            products.Add(new Product
            {
                Id = id,
                Name = name,
                Type = type,
                FranchiseId = franchise.Id,
                FranchiseName = franchise.Name,
                Description = description,
                Price = price,
                Stock = stock,
                CreatedUtc = start.AddSeconds(products.Count),
            });
        }

        Add("mi-jersey", "MI Jersey", ProductType.Jersey, "mi", "Official Mumbai Indians replica jersey.", 1999.00m, 50);
        Add("csk-cap", "CSK Cap", ProductType.Cap, "csk", "Official Chennai Super Kings cap.", 499.00m, 100);
        Add("rcb-flag", "RCB Flag", ProductType.Flag, "rcb", "Official Royal Challengers Bengaluru flag.", 299.00m, 200);
        Add("kkr-signed-photo", "KKR Signed Photo", ProductType.SignedPhotograph, "kkr", "Limited edition photograph signed by the squad.", 4999.00m, 2);
        Add("csk-jersey", "CSK Jersey", ProductType.Jersey, "csk", "Official Chennai Super Kings replica jersey.", 1899.00m, 75);
        Add("mi-cap", "MI Cap", ProductType.Cap, "mi", "Official Mumbai Indians cap.", 549.00m, 120);

        return products;
    }
}
