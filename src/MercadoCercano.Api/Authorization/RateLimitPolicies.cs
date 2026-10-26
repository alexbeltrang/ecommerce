namespace MercadoCercano.Api.Authorization;

public static class RateLimitPolicies
{
    public const string Login = "Login";
    public const string CheckoutQuote = "CheckoutQuote";
    public const string OrderCreation = "OrderCreation";
    public const string OrderTracking = "OrderTracking";
    public const string PaymentEvidence = "PaymentEvidence";
}
