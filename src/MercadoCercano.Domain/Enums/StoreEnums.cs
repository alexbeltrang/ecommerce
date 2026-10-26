namespace MercadoCercano.Domain.Enums;

public enum UserRole
{
    Administrator = 1,
    OrderOperator = 2
}

public enum DeliveryMethod
{
    CoordinatedPickup = 1,
    LocalDelivery = 2,
    CarrierShipping = 3
}

public enum PaymentMethod
{
    BankTransfer = 1,
    DigitalWallet = 2,
    CashOnDelivery = 3
}

public enum OrderStatus
{
    PendingConfirmation = 1,
    Confirmed = 2,
    Preparing = 3,
    ReadyForDelivery = 4,
    Shipped = 5,
    Delivered = 6,
    Cancelled = 7
}

public enum PaymentStatus
{
    Pending = 1,
    UnderReview = 2,
    Paid = 3,
    Rejected = 4,
    Refunded = 5
}

public enum InventoryMovementType
{
    PurchaseEntry = 1,
    Sale = 2,
    ManualAddition = 3,
    ManualRemoval = 4,
    OrderCancellation = 5,
    CustomerReturn = 6
}

public enum OrderReturnKind { CustomerReturn = 1, Cancellation = 2, FailedDelivery = 3 }
public enum OrderReturnStatus { Requested = 1, Received = 2, Refunded = 3, Rejected = 4 }

public enum DiscountType
{
    Percentage = 1,
    FixedAmount = 2
}
