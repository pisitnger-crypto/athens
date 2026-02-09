using System;

namespace athens
{
    public enum ProductCategory
    {
        Beverage,
        Snack,
        Household,
        PersonalCare
    }

    public class Product
    {
        public int Id { get; set; }
        public string ProductCode { get; set; }
        public string Name { get; set; }
        public decimal Price { get; set; }
        public int Quantity { get; set; }
        public ProductCategory Category { get; set; }
        public string Description { get; set; }
        public bool IsDeleted { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public enum InventoryTransactionType
    {
        StockIn,
        Sale,
        Adjustment
    }

    public class InventoryTransaction
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public InventoryTransactionType TransactionType { get; set; }
        public int QuantityChange { get; set; }
        public string Note { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class CartItem
    {
        public Product Product { get; set; }
        public int Quantity { get; set; }
        public decimal LineTotal => Product.Price * Quantity;
    }

    public class SaleReceipt
    {
        public string ReceiptNumber { get; set; }
        public DateTime IssuedAt { get; set; }
        public string StoreName { get; set; }
        public decimal SubTotal { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal GrandTotal => SubTotal + TaxAmount;
    }
}
