namespace EnterpriseSystem.Domain.Enums;

public enum PurchaseOrderStatus
{
    PendingApproval = 0,
    Approved = 1,
    Rejected = 2
}

public enum PaymentMethod
{
    CashOnDelivery = 0,
    BankTransfer = 1,
    CorporateCredit = 2,
    CardOnDelivery = 3
}
