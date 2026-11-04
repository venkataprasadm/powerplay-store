namespace IplStore.Application.Contracts;

public sealed record OrderLineDto(string ProductId, string ProductName, int Quantity, decimal UnitPrice, decimal LineTotal);
