using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Globalization;
using System.IO;

namespace athens
{
    public sealed class DatabaseContext
    {
        private static readonly Lazy<DatabaseContext> InstanceHolder = new Lazy<DatabaseContext>(() => new DatabaseContext());
        public static DatabaseContext Instance => InstanceHolder.Value;

        public string DatabasePath { get; }
        public string ConnectionString => $"Data Source={DatabasePath};Version=3;";

        private DatabaseContext()
        {
            DatabasePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "shop.db");
            Initialize();
        }

        public SQLiteConnection CreateConnection()
        {
            var connection = new SQLiteConnection(ConnectionString);
            connection.Open();
            return connection;
        }

        private void Initialize()
        {
            if (!File.Exists(DatabasePath))
            {
                SQLiteConnection.CreateFile(DatabasePath);
            }

            using (var connection = CreateConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
CREATE TABLE IF NOT EXISTS Products (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    ProductCode TEXT NOT NULL UNIQUE,
    Name TEXT NOT NULL,
    Price REAL NOT NULL,
    Quantity INTEGER NOT NULL,
    Category TEXT NOT NULL,
    Description TEXT,
    IsDeleted INTEGER NOT NULL DEFAULT 0,
    CreatedAt TEXT NOT NULL,
    UpdatedAt TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS InventoryTransactions (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    ProductId INTEGER NOT NULL,
    TransactionType TEXT NOT NULL,
    QuantityChange INTEGER NOT NULL,
    Note TEXT,
    CreatedAt TEXT NOT NULL,
    FOREIGN KEY(ProductId) REFERENCES Products(Id)
);

CREATE TABLE IF NOT EXISTS Sales (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    ReceiptNumber TEXT NOT NULL UNIQUE,
    IssuedAt TEXT NOT NULL,
    StoreName TEXT NOT NULL,
    SubTotal REAL NOT NULL,
    TaxAmount REAL NOT NULL
);

CREATE TABLE IF NOT EXISTS SaleItems (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    SaleId INTEGER NOT NULL,
    ProductId INTEGER NOT NULL,
    ProductName TEXT NOT NULL,
    UnitPrice REAL NOT NULL,
    Quantity INTEGER NOT NULL,
    FOREIGN KEY(SaleId) REFERENCES Sales(Id),
    FOREIGN KEY(ProductId) REFERENCES Products(Id)
);

CREATE INDEX IF NOT EXISTS idx_products_name ON Products(Name);
CREATE INDEX IF NOT EXISTS idx_inventory_product ON InventoryTransactions(ProductId);
CREATE INDEX IF NOT EXISTS idx_sales_issued_at ON Sales(IssuedAt);
";
                command.ExecuteNonQuery();
            }
        }

        public void Backup(string destinationPath)
        {
            File.Copy(DatabasePath, destinationPath, true);
        }

        public void Restore(string sourcePath)
        {
            File.Copy(sourcePath, DatabasePath, true);
        }
    }

    public interface IProductRepository
    {
        int Add(Product product);
        void Update(Product product);
        void SoftDelete(int id);
        IList<Product> Search(string keyword, ProductCategory? category, bool includeDeleted = false);
        Product GetById(int id);
        Product GetByCode(string code);
    }

    public class ProductRepository : IProductRepository
    {
        private readonly DatabaseContext _databaseContext;

        public ProductRepository(DatabaseContext databaseContext)
        {
            _databaseContext = databaseContext;
        }

        public int Add(Product product)
        {
            using (var connection = _databaseContext.CreateConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"INSERT INTO Products (ProductCode, Name, Price, Quantity, Category, Description, IsDeleted, CreatedAt, UpdatedAt)
VALUES (@code, @name, @price, @quantity, @category, @description, 0, @createdAt, @updatedAt);
SELECT last_insert_rowid();";

                command.Parameters.AddWithValue("@code", product.ProductCode);
                command.Parameters.AddWithValue("@name", product.Name);
                command.Parameters.AddWithValue("@price", product.Price);
                command.Parameters.AddWithValue("@quantity", product.Quantity);
                command.Parameters.AddWithValue("@category", product.Category.ToString());
                command.Parameters.AddWithValue("@description", product.Description ?? string.Empty);
                command.Parameters.AddWithValue("@createdAt", product.CreatedAt.ToString("O", CultureInfo.InvariantCulture));
                command.Parameters.AddWithValue("@updatedAt", product.UpdatedAt.ToString("O", CultureInfo.InvariantCulture));

                return Convert.ToInt32(command.ExecuteScalar());
            }
        }

        public void Update(Product product)
        {
            using (var connection = _databaseContext.CreateConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"UPDATE Products
SET Name=@name, Price=@price, Quantity=@quantity, Category=@category, Description=@description, UpdatedAt=@updatedAt
WHERE Id=@id";
                command.Parameters.AddWithValue("@id", product.Id);
                command.Parameters.AddWithValue("@name", product.Name);
                command.Parameters.AddWithValue("@price", product.Price);
                command.Parameters.AddWithValue("@quantity", product.Quantity);
                command.Parameters.AddWithValue("@category", product.Category.ToString());
                command.Parameters.AddWithValue("@description", product.Description ?? string.Empty);
                command.Parameters.AddWithValue("@updatedAt", DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
                command.ExecuteNonQuery();
            }
        }

        public void SoftDelete(int id)
        {
            using (var connection = _databaseContext.CreateConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "UPDATE Products SET IsDeleted = 1, UpdatedAt = @updatedAt WHERE Id = @id";
                command.Parameters.AddWithValue("@id", id);
                command.Parameters.AddWithValue("@updatedAt", DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
                command.ExecuteNonQuery();
            }
        }

        public IList<Product> Search(string keyword, ProductCategory? category, bool includeDeleted = false)
        {
            var products = new List<Product>();
            using (var connection = _databaseContext.CreateConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"SELECT Id, ProductCode, Name, Price, Quantity, Category, Description, IsDeleted, CreatedAt, UpdatedAt
FROM Products
WHERE (@includeDeleted = 1 OR IsDeleted = 0)
AND (@keyword = '' OR Name LIKE '%' || @keyword || '%' OR ProductCode LIKE '%' || @keyword || '%')
AND (@category = '' OR Category = @category)
ORDER BY Name";
                command.Parameters.AddWithValue("@includeDeleted", includeDeleted ? 1 : 0);
                command.Parameters.AddWithValue("@keyword", keyword ?? string.Empty);
                command.Parameters.AddWithValue("@category", category.HasValue ? category.ToString() : string.Empty);

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        products.Add(MapProduct(reader));
                    }
                }
            }

            return products;
        }

        public Product GetById(int id)
        {
            using (var connection = _databaseContext.CreateConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"SELECT Id, ProductCode, Name, Price, Quantity, Category, Description, IsDeleted, CreatedAt, UpdatedAt
FROM Products WHERE Id = @id";
                command.Parameters.AddWithValue("@id", id);
                using (var reader = command.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        return MapProduct(reader);
                    }
                }
            }

            return null;
        }

        public Product GetByCode(string code)
        {
            using (var connection = _databaseContext.CreateConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"SELECT Id, ProductCode, Name, Price, Quantity, Category, Description, IsDeleted, CreatedAt, UpdatedAt
FROM Products WHERE ProductCode = @code";
                command.Parameters.AddWithValue("@code", code);
                using (var reader = command.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        return MapProduct(reader);
                    }
                }
            }

            return null;
        }

        private static Product MapProduct(SQLiteDataReader reader)
        {
            return new Product
            {
                Id = Convert.ToInt32(reader["Id"]),
                ProductCode = reader["ProductCode"].ToString(),
                Name = reader["Name"].ToString(),
                Price = Convert.ToDecimal(reader["Price"]),
                Quantity = Convert.ToInt32(reader["Quantity"]),
                Category = (ProductCategory)Enum.Parse(typeof(ProductCategory), reader["Category"].ToString()),
                Description = reader["Description"].ToString(),
                IsDeleted = Convert.ToInt32(reader["IsDeleted"]) == 1,
                CreatedAt = DateTime.Parse(reader["CreatedAt"].ToString(), null, DateTimeStyles.RoundtripKind),
                UpdatedAt = DateTime.Parse(reader["UpdatedAt"].ToString(), null, DateTimeStyles.RoundtripKind)
            };
        }
    }
}
