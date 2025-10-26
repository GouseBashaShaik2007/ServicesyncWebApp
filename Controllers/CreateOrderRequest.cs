namespace ServicesyncWebApp.Controllers
{
    public record CreateOrderRequest(
        int? UserID,
        int ProfessionalID,
        int CategoryID,
        string ServiceAddress1,
        string? ServiceAddress2,
        string City,
        string State,
        string PostalCode,
        string ScheduledStart,
        string? ScheduledEnd,
        string? Notes,
        decimal Subtotal,
        decimal TaxAmount,
        decimal DiscountAmount,
        byte PaymentStatus,
        byte OrderStatus,
        bool IsActive
    );
}
