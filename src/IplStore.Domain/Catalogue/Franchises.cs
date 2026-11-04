namespace IplStore.Domain.Catalogue;

/// <summary>The fixed list of IPL franchises a product can belong to.</summary>
public static class Franchises
{
    public static readonly IReadOnlyList<Franchise> All =
    [
        new("mi", "Mumbai Indians"),
        new("csk", "Chennai Super Kings"),
        new("rcb", "Royal Challengers Bengaluru"),
        new("kkr", "Kolkata Knight Riders"),
        new("srh", "Sunrisers Hyderabad"),
        new("dc", "Delhi Capitals"),
        new("rr", "Rajasthan Royals"),
        new("pbks", "Punjab Kings"),
        new("gt", "Gujarat Titans"),
        new("lsg", "Lucknow Super Giants"),
    ];

    public static Franchise? Find(string id)
    {
        return All.FirstOrDefault(franchise => string.Equals(franchise.Id, id, StringComparison.OrdinalIgnoreCase));
    }
}
