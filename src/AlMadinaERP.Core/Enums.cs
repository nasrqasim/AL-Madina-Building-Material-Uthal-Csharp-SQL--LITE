namespace AlMadinaERP.Core.Enums
{
    public enum UserRole
    {
        Admin,
        Manager,
        Cashier
    }

    public enum AccountType
    {
        Asset,
        Liability,
        Equity,
        Revenue,
        Expense
    }

    public enum InvoiceType
    {
        SaleInvoice,
        SaleReturn,
        POSCounterSale
    }

    public enum PurchaseType
    {
        PurchaseInvoice,
        PurchaseReturn
    }

    public enum PaymentMethod
    {
        Cash,
        Bank,
        Online
    }

    public enum DeliveryStatus
    {
        Received,
        NotReceived
    }

    public enum ReceiptType
    {
        CashReceipt,
        BankReceipt,
        OtherIncome
    }

    public enum PaymentType
    {
        CashPayment,
        BankPayment,
        CustomerRefund
    }
}
