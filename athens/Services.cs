using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Data.SQLite;

namespace athens
{
    public class ProductService
    {
        private readonly IProductRepository _repository;

        public ProductService(IProductRepository repository)
        {
            _repository = repository;
        }

        public int CreateProduct(string code, string name, decimal price, int quantity, ProductCategory category, string description)
        {
            ValidateProductInput(code, name, price, quantity);

            if (_repository.GetByCode(code) != null)
            {
                throw new InvalidOperationException("รหัสสินค้านี้มีอยู่แล้ว");
            }

            var now = DateTime.UtcNow;
            return _repository.Add(new Product
            {
                ProductCode = code,
                Name = name,
                Price = price,
                Quantity = quantity,
                Category = category,
                Description = description,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        public void UpdateProduct(Product product)
        {
            ValidateProductInput(product.ProductCode, product.Name, product.Price, product.Quantity);
            _repository.Update(product);
        }

        public void DeleteProduct(int id)
        {
            _repository.SoftDelete(id);
        }

        public IList<Product> SearchProducts(string keyword, ProductCategory? category)
        {
            return _repository.Search(keyword, category);
        }

        public IList<Product> GetLowStockProducts(int threshold)
        {
            return _repository.Search(string.Empty, null).Where(x => x.Quantity <= threshold).ToList();
        }

        private static void ValidateProductInput(string code, string name, decimal price, int quantity)
        {
            if (string.IsNullOrWhiteSpace(code)) throw new ArgumentException("กรุณาระบุรหัสสินค้า");
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("กรุณาระบุชื่อสินค้า");
            if (price < 0) throw new ArgumentException("ราคาไม่สามารถติดลบได้");
            if (quantity < 0) throw new ArgumentException("จำนวนสินค้าไม่สามารถติดลบได้");
        }
    }

    public class InventoryService
    {
        private readonly IProductRepository _productRepository;
        private readonly DatabaseContext _databaseContext;

        public InventoryService(IProductRepository productRepository, DatabaseContext databaseContext)
        {
            _productRepository = productRepository;
            _databaseContext = databaseContext;
        }

        public void AddStock(int productId, int quantity, string note)
        {
            ApplyStockChange(productId, quantity, InventoryTransactionType.StockIn, note);
        }

        public void ConsumeStock(int productId, int quantity, string note)
        {
            ApplyStockChange(productId, -Math.Abs(quantity), InventoryTransactionType.Sale, note);
        }

        public IList<InventoryTransaction> GetHistory(int? productId = null)
        {
            var result = new List<InventoryTransaction>();
            using (var connection = _databaseContext.CreateConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"SELECT Id, ProductId, TransactionType, QuantityChange, Note, CreatedAt
FROM InventoryTransactions
WHERE (@productId = 0 OR ProductId = @productId)
ORDER BY CreatedAt DESC";
                command.Parameters.AddWithValue("@productId", productId ?? 0);
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        result.Add(new InventoryTransaction
                        {
                            Id = Convert.ToInt32(reader["Id"]),
                            ProductId = Convert.ToInt32(reader["ProductId"]),
                            TransactionType = (InventoryTransactionType)Enum.Parse(typeof(InventoryTransactionType), reader["TransactionType"].ToString()),
                            QuantityChange = Convert.ToInt32(reader["QuantityChange"]),
                            Note = reader["Note"].ToString(),
                            CreatedAt = DateTime.Parse(reader["CreatedAt"].ToString(), null, DateTimeStyles.RoundtripKind)
                        });
                    }
                }
            }

            return result;
        }

        private void ApplyStockChange(int productId, int delta, InventoryTransactionType transactionType, string note)
        {
            var product = _productRepository.GetById(productId);
            if (product == null) throw new InvalidOperationException("ไม่พบสินค้า");
            if (product.Quantity + delta < 0) throw new InvalidOperationException("สต็อกไม่เพียงพอ");

            product.Quantity += delta;
            _productRepository.Update(product);

            using (var connection = _databaseContext.CreateConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"INSERT INTO InventoryTransactions (ProductId, TransactionType, QuantityChange, Note, CreatedAt)
VALUES (@productId, @type, @quantity, @note, @createdAt)";
                command.Parameters.AddWithValue("@productId", productId);
                command.Parameters.AddWithValue("@type", transactionType.ToString());
                command.Parameters.AddWithValue("@quantity", delta);
                command.Parameters.AddWithValue("@note", note ?? string.Empty);
                command.Parameters.AddWithValue("@createdAt", DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
                command.ExecuteNonQuery();
            }
        }
    }

    public class PosService
    {
        private readonly IProductRepository _productRepository;
        private readonly InventoryService _inventoryService;
        private readonly DatabaseContext _databaseContext;

        public PosService(IProductRepository productRepository, InventoryService inventoryService, DatabaseContext databaseContext)
        {
            _productRepository = productRepository;
            _inventoryService = inventoryService;
            _databaseContext = databaseContext;
        }

        public SaleReceipt Checkout(string storeName, IList<CartItem> cartItems, decimal taxRate)
        {
            if (cartItems == null || cartItems.Count == 0)
            {
                throw new InvalidOperationException("ไม่พบสินค้าในตะกร้า");
            }

            var subTotal = cartItems.Sum(x => x.LineTotal);
            var taxAmount = Math.Round(subTotal * taxRate, 2, MidpointRounding.AwayFromZero);
            var receipt = new SaleReceipt
            {
                ReceiptNumber = $"RCPT-{DateTime.Now:yyyyMMddHHmmss}",
                IssuedAt = DateTime.Now,
                StoreName = storeName,
                SubTotal = subTotal,
                TaxAmount = taxAmount
            };

            foreach (var item in cartItems)
            {
                _inventoryService.ConsumeStock(item.Product.Id, item.Quantity, $"ขายสินค้าใบเสร็จ {receipt.ReceiptNumber}");
            }

            SaveSale(receipt, cartItems);
            ReceiptExporter.ExportAsPdfLikeText(receipt, cartItems);

            return receipt;
        }

        private void SaveSale(SaleReceipt saleReceipt, IList<CartItem> cartItems)
        {
            using (var connection = _databaseContext.CreateConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"INSERT INTO Sales (ReceiptNumber, IssuedAt, StoreName, SubTotal, TaxAmount)
VALUES (@receiptNumber, @issuedAt, @storeName, @subTotal, @taxAmount);
SELECT last_insert_rowid();";
                command.Parameters.AddWithValue("@receiptNumber", saleReceipt.ReceiptNumber);
                command.Parameters.AddWithValue("@issuedAt", saleReceipt.IssuedAt.ToString("O", CultureInfo.InvariantCulture));
                command.Parameters.AddWithValue("@storeName", saleReceipt.StoreName);
                command.Parameters.AddWithValue("@subTotal", saleReceipt.SubTotal);
                command.Parameters.AddWithValue("@taxAmount", saleReceipt.TaxAmount);
                var saleId = Convert.ToInt32(command.ExecuteScalar());

                foreach (var item in cartItems)
                {
                    using (var itemCommand = connection.CreateCommand())
                    {
                        itemCommand.CommandText = @"INSERT INTO SaleItems (SaleId, ProductId, ProductName, UnitPrice, Quantity)
VALUES (@saleId, @productId, @productName, @unitPrice, @quantity)";
                        itemCommand.Parameters.AddWithValue("@saleId", saleId);
                        itemCommand.Parameters.AddWithValue("@productId", item.Product.Id);
                        itemCommand.Parameters.AddWithValue("@productName", item.Product.Name);
                        itemCommand.Parameters.AddWithValue("@unitPrice", item.Product.Price);
                        itemCommand.Parameters.AddWithValue("@quantity", item.Quantity);
                        itemCommand.ExecuteNonQuery();
                    }
                }
            }
        }
    }

    public static class ReceiptExporter
    {
        public static string ExportAsPdfLikeText(SaleReceipt receipt, IEnumerable<CartItem> items)
        {
            var receiptsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "receipts");
            Directory.CreateDirectory(receiptsDir);
            var filePath = Path.Combine(receiptsDir, receipt.ReceiptNumber + ".pdf");

            var builder = new StringBuilder();
            builder.AppendLine(receipt.StoreName);
            builder.AppendLine("ใบเสร็จรับเงิน");
            builder.AppendLine($"เลขที่: {receipt.ReceiptNumber}");
            builder.AppendLine($"วันที่: {receipt.IssuedAt:dd/MM/yyyy HH:mm:ss}");
            builder.AppendLine(new string('-', 50));
            foreach (var item in items)
            {
                builder.AppendLine($"{item.Product.Name} x{item.Quantity} @ {item.Product.Price:N2} = {item.LineTotal:N2}");
            }
            builder.AppendLine(new string('-', 50));
            builder.AppendLine($"ยอดรวม: {receipt.SubTotal:N2}");
            builder.AppendLine($"ภาษี: {receipt.TaxAmount:N2}");
            builder.AppendLine($"ยอดสุทธิ: {receipt.GrandTotal:N2}");

            File.WriteAllText(filePath, builder.ToString(), Encoding.UTF8);
            return filePath;
        }
    }

    public class ReportingService
    {
        private readonly DatabaseContext _databaseContext;

        public ReportingService(DatabaseContext databaseContext)
        {
            _databaseContext = databaseContext;
        }

        public IList<string> InventoryReport()
        {
            var lines = new List<string>();
            using (var connection = _databaseContext.CreateConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT ProductCode, Name, Quantity, Price FROM Products WHERE IsDeleted = 0 ORDER BY Name";
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        lines.Add($"{reader["ProductCode"]} | {reader["Name"]} | คงเหลือ {reader["Quantity"]} | ราคา {Convert.ToDecimal(reader["Price"]):N2}");
                    }
                }
            }

            return lines;
        }
    }
}
