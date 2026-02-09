# Shop Management System (Athens)

## Project Overview
ระบบนี้เป็นตัวอย่าง **ระบบจัดการร้านค้าแบบครบวงจร** สำหรับหมวดสินค้า **เครื่องดื่ม (Beverage)**
ครอบคลุมการจัดการสินค้า คลังสินค้า การขาย/ออกใบเสร็จ รายงาน และการจัดเก็บข้อมูลด้วย SQLite ตามแนว OOP + Design Pattern

## ฟีเจอร์หลัก
- Product Management: เพิ่ม/แก้ไข/ลบแบบ Soft Delete และค้นหาสินค้า
- Inventory Management: รับเข้า/ขายออก + ประวัติการเคลื่อนไหว + Low Stock Alert
- POS & Receipt: ตะกร้าสินค้า, คำนวณยอดรวม/ภาษี/ยอดสุทธิ, สร้างไฟล์ใบเสร็จ (.pdf นามสกุล)
- Database: SQLite schema แบบ normalized + Backup/Restore
- Reporting: รายงานสินค้าคงคลัง

## Installation Guide
### Prerequisites
- Visual Studio 2022+ (หรือ 2026)
- .NET Framework 4.7.2 Developer Pack
- NuGet Restore enabled

### Steps
1. เปิดไฟล์ `athens.slnx`
2. Restore NuGet packages
3. Build และ Run โปรเจค `athens`

## User Manual
1. หน้า **สินค้า**
   - กด `เพิ่มตัวอย่างสินค้า` เพื่อสร้างข้อมูลเริ่มต้น
   - กด `ค้นหาทั้งหมด` เพื่อรีเฟรชรายการ
   - กด `แจ้งเตือนสต็อกต่ำ` เพื่อตรวจสินค้าคงเหลือต่ำ
2. หน้า **POS**
   - กด `ขายสินค้าและออกใบเสร็จ` เพื่อจำลองการขาย
   - ระบบจะบันทึกสต็อกและสร้างใบเสร็จไว้ในโฟลเดอร์ `receipts/`
3. หน้า **รายงาน**
   - กด `สร้างรายงานสินค้าคงคลัง`

## Source Code Structure
- `athens/Models.cs`: Domain entities + enums
- `athens/DataAccess.cs`: SQLite schema + Repository + Singleton DB context
- `athens/Services.cs`: Business logic (Product/Inventory/POS/Reporting)
- `athens/MainWindow.xaml(.cs)`: WPF UI
- `docs/diagrams/*.puml`: Use Case / Sequence diagrams

## OOP & Design Pattern
- Encapsulation: แยก Model/Repository/Service/UI
- Singleton: `DatabaseContext.Instance`
- Repository Pattern: `IProductRepository`, `ProductRepository`
- Service Layer: แยก business rules ออกจาก UI

## Database Design (Normalization)
ตารางหลัก:
- `Products`
- `InventoryTransactions`
- `Sales`
- `SaleItems`

ความสัมพันธ์:
- 1 Product : Many InventoryTransactions
- 1 Sale : Many SaleItems
- SaleItems อ้างอิง Product

## สมาชิกและการแบ่งงาน (ตัวอย่าง)
- สมาชิก A: Database + Repository + Backup/Restore
- สมาชิก B: UI + POS + Receipt
- สมาชิก C: Reporting + Diagram + README + Testing

## เทคโนโลยีที่ใช้
- C# / WPF (.NET Framework 4.7.2)
- SQLite
- PlantUML
- Git/GitHub

## เอกสาร Diagram
- Use Case: `docs/diagrams/use-case.puml`
- Sequence - Add Product: `docs/diagrams/sequence-add-product.puml`
- Sequence - Checkout: `docs/diagrams/sequence-checkout.puml`
