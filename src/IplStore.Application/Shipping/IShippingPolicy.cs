namespace IplStore.Application.Shipping;

public interface IShippingPolicy
{
    decimal Calculate(decimal subtotal);
}
