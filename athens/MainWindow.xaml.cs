using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace athens
{
    public partial class MainWindow : Window
    {
        private readonly ProductService _productService;
        private readonly InventoryService _inventoryService;
        private readonly PosService _posService;
        private readonly ReportingService _reportingService;

        public MainWindow()
        {
            InitializeComponent();

            var db = DatabaseContext.Instance;
            var productRepository = new ProductRepository(db);

            _productService = new ProductService(productRepository);
            _inventoryService = new InventoryService(productRepository, db);
            _posService = new PosService(productRepository, _inventoryService, db);
            _reportingService = new ReportingService(db);

            LoadProducts();
        }

        private void SeedProducts_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SeedSampleProducts();
                LoadProducts();
                StatusText.Text = "เพิ่มสินค้าเริ่มต้นแล้ว";
            }
            catch (Exception ex)
            {
                StatusText.Text = ex.Message;
            }
        }

        private void LoadProducts_Click(object sender, RoutedEventArgs e)
        {
            LoadProducts();
        }

        private void LowStock_Click(object sender, RoutedEventArgs e)
        {
            var lowStock = _productService.GetLowStockProducts(5);
            if (!lowStock.Any())
            {
                StatusText.Text = "ไม่มีสินค้าที่สต็อกต่ำ";
                return;
            }

            StatusText.Text = "สินค้าใกล้หมด: " + string.Join(", ", lowStock.Select(x => x.Name));
        }

        private void Checkout_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var products = _productService.SearchProducts(string.Empty, null);
                if (!products.Any())
                {
                    StatusText.Text = "ไม่พบสินค้าสำหรับขาย";
                    return;
                }

                var cart = new List<CartItem>
                {
                    new CartItem { Product = products.First(), Quantity = 1 }
                };

                var receipt = _posService.Checkout("Athens Beverage Shop", cart, 0.07m);
                LoadProducts();
                StatusText.Text = $"ขายสำเร็จ ใบเสร็จ {receipt.ReceiptNumber}";
            }
            catch (Exception ex)
            {
                StatusText.Text = ex.Message;
            }
        }

        private void InventoryReport_Click(object sender, RoutedEventArgs e)
        {
            ReportList.ItemsSource = _reportingService.InventoryReport();
            StatusText.Text = "สร้างรายงานสินค้าคงคลังเรียบร้อย";
        }

        private void LoadProducts()
        {
            ProductsGrid.ItemsSource = _productService.SearchProducts(string.Empty, null);
            StatusText.Text = "โหลดรายการสินค้าแล้ว";
        }

        private void SeedSampleProducts()
        {
            var initialProducts = new[]
            {
                new { Code = "BV001", Name = "กาแฟดำ", Price = 45m, Qty = 20, Category = ProductCategory.Beverage, Description = "Americano" },
                new { Code = "BV002", Name = "ชาเขียว", Price = 40m, Qty = 15, Category = ProductCategory.Beverage, Description = "Matcha latte" },
                new { Code = "BV003", Name = "โกโก้เย็น", Price = 50m, Qty = 4, Category = ProductCategory.Beverage, Description = "Iced cocoa" }
            };

            foreach (var item in initialProducts)
            {
                try
                {
                    _productService.CreateProduct(item.Code, item.Name, item.Price, item.Qty, item.Category, item.Description);
                }
                catch
                {
                    // Ignore duplicate sample codes.
                }
            }
        }
    }
}
