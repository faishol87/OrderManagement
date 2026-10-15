# Order Management API

Prototype REST API untuk Order Management, dibuat dengan ASP.NET Core 8 sebagai fondasi
rewrite sistem distribusi. Fokus utama: untuk menangani idempotency dan concurrency handling.

## Tech Stack

- .NET 8 / ASP.NET Core Web API (controller-based)
- Entity Framework Core 8 + SQL Server
- Serilog (structured logging)
- Swashbuckle (Swagger UI)
- xUnit + Testcontainers (MSSQL) + `WebApplicationFactory` (integration test)

## Pilihan Database: SQL Server

Alasan:
- Punya tipe `rowversion` bawaan, karena mau menggunakan optimistic concurrency.
- Dukungan transaksi & locking yang matang, dan operasi `UPDATE ... WHERE` atomik
  untuk pengurangan stok.
- Sesuai untuk lingkungan yang sudah memakai ekosistem Microsoft.
- Mudah dijalankan via Docker untuk development & test.

> Database dijalankan di Docker: image `mcr.microsoft.com/mssql/server`.

## Struktur Project
```

Order Management/ Common/ ApiException, ErrorResponse, ExceptionHandlingMiddleware, Validators Data/ AppDbContext, DbSeeder, Migrations/ Domain/ Order, OrderItem, OrderStatus, Product Features/ Idempotency/ IdempotencyRecord Orders/ Controller + handlers + contracts Products/ Controller + handlers + contracts Logging/ CorrelationIdMiddleware Program.cs Order Management.Tests/ ApiFactory.cs Testcontainers + WebApplicationFactory ConcurrencyTests.cs

````

Pola aplikasi: Controller → Handler (application layer) → DbContext. Logika bisnis
dan validasi transisi ada di domain (`Order`, `Product`).

## Endpoints

### Orders
| Method | Route | Keterangan |
| POST | `/api/orders` | Buat order (butuh header `Idempotency-Key`) |
| GET | `/api/orders/{id}` | Detail order |
| GET | `/api/orders` | List + filter (`status`, `customerId`, `from`, `to`) + pagination (`page`, `pageSize`) |
| PATCH | `/api/orders/{id}/status` | Update status |
| POST | `/api/orders/{id}/cancel` | Cancel order (stock dikembalikan) |

### Products
| Method | Route | Keterangan |
| POST | `/api/products` | Buat produk |
| GET | `/api/products/{id}` | Detail produk |
| GET | `/api/products` | List + filter nama + pagination |
| DELETE | `/api/products/{id}` | Soft delete |

Contoh:
```bash
curl -X POST http://localhost:5086/api/orders \
  -H "Content-Type: application/json" \
  -H "Idempotency-Key: 8f3c1e2a-1111-2222-3333-444455556666" \
  -d '{
    "customerId": "11111111-1111-1111-1111-111111111111",
    "shippingAddress": "Jl. Contoh No. 1",
    "items": [{ "productId": "<productId>", "quantity": 2 }]
  }'
````

Validasi Transisi Status
------------------------

```
Pending   -> Confirmed | Cancelled
Confirmed -> Shipped   | Cancelled
Shipped   -> Delivered
Delivered / Cancelled = terminal (tidak bisa diubah)
```

Update status hanya lewat `PATCH .../status` (bukan `Cancelled`); untuk membatalkan order gunakan endpoint `POST .../cancel` agar pengembalian stok terpusat.

Idempotency
-----------

Strategi: header `Idempotency-Key` + tabel `IdempotencyKeys`.

- Client mengirim `Idempotency-Key` (GUID) untuk satu niat request.
- Server menyimpan kunci tersebut bersama hash payload dan hasil response.
- Retry dengan kunci sama tidak membuat order baru, melainkan mengembalikan response tersimpan.
- Kunci sama dengan payload berbeda → `422 IDEMPOTENCY_KEY_REUSED`.
- Ada masa berlaku (TTL 24 jam) dan pembersihan berkala (tabel ini transien, pakai hard delete).

Alasan memilih header, bukan hash payload saja: hash tidak bisa membedakan "retry" dari "user memang sengaja memesan 2x dengan isi identik". Header menyatakan niat secara eksplisit dan merupakan standar de facto (mis. Stripe). Hash tetap dipakai sebagai pelengkap untuk mendeteksi penyalahgunaan kunci.

Concurrency Handling
--------------------

### Skenario A — Concurrent stock deduction

Strategi: operasi `UPDATE` atomik bersyarat, bukan baca-lalu-tulis.

```sql
UPDATE "Products"
SET    "StockQuantity" = "StockQuantity" - @qty
WHERE  "Id" = @id AND "StockQuantity" >= @qty;
```

Bila `rowsAffected == 0` → stok tidak cukup → order ditolak (`422 INSUFFICIENT_STOCK`). Semua item diproses dalam satu transaksi (all-or-nothing), dan item diurutkan berdasarkan `ProductId` agar urutan penguncian konsisten dan terhindar dari deadlock. Stok tidak mungkin minus karena database yang mengevaluasi syarat dalam satu statement.

### Skenario B — Concurrent status update

Strategi: optimistic concurrency dengan kolom `RowVersion` (`rowversion`).

EF Core menambahkan `RowVersion` ke klausa `WHERE` pada setiap `UPDATE`. Bila ada admin lain yang commit lebih dulu, `rowsAffected == 0` → EF melempar `DbUpdateConcurrencyException` → di-response menjadi `409 ORDER_MODIFIED`. Jadi hanya satu update yang menang.

### Skenario C — Idempotent create under race

Strategi: unique index pada tabel `IdempotencyKeys (CustomerId, Key)`.

Dua request dengan kunci sama yang tiba bersamaan: hanya satu `INSERT` yang sukses, yang lain kena unique violation (`SQL error 2601/2627`) → server tahu request lain sedang/sudah memproses → mengembalikan hasil tersimpan (`201`) atau `409 REQUEST_IN_PROGRESS` bila belum selesai. Hasil akhir: **hanya satu order dibuat.**

### Race Condition Lain yang Diidentifikasi

1. **Cancel ganda / cancel bersamaan** — berpotensi mengembalikan stok dua kali. Dicegah dengan: update status memakai `RowVersion` (optimistic) sehingga hanya satu yang berhasil, plus validasi `IsCancellable` (hanya `Pending`/`Confirmed`). Pengembalian stok hanya terjadi pada transaksi yang berhasil commit.
2. **Soft delete produk saat order dibuat** — produk yang sudah `IsDeleted`/dihapus tidak boleh dipakai order. Dicegah dengan: `HasQueryFilter(!IsDeleted)` pada `Product` (semua query otomatis mengabaikan yang terhapus), sehingga `UPDATE ... WHERE` pada Skenario A tidak menemukan produk terhapus (`rowsAffected == 0`) dan order ditolak.
3. **Hilangnya kunci idempotency saat order gagal** — bila order gagal setelah kunci tersimpan, retry bisa terkunci permanen (`REQUEST_IN_PROGRESS`). Dicegah dengan menghapus kunci pada jalur gagal (rollback + cleanup).

Error Handling
--------------

Semua error memakai format konsisten:

```json
{
  "traceId": "3f1a...",
  "code": "INSUFFICIENT_STOCK",
  "message": "Stock produk ... tidak cukup.",
  "errors": { "items[0].quantity": ["..."] }
}
```

Status code yang dipakai: `400` (validasi), `404` (tidak ditemukan), `409` (konflik concurrency/idempotency), `422` (aturan bisnis).

Kode error antara lain: `VALIDATION_ERROR`, `INSUFFICIENT_STOCK`, `PRODUCT_NOT_FOUND`, `ORDER_NOT_FOUND`, `INVALID_STATUS_TRANSITION`, `ORDER_NOT_CANCELLABLE`, `ORDER_MODIFIED`, `REQUEST_IN_PROGRESS`, `IDEMPOTENCY_KEY_REUSED`, `PRODUCT_IN_USE`.

Logging
-------

- **Serilog** (structured logging) ke Console + file harian (`logs/log-.txt`).
- **Correlation ID**: `CorrelationIdMiddleware` membaca/membuat header `X-Correlation-Id`, mengembalikannya di response, dan mendorongnya ke log scope. Nilai yang sama dipakai sebagai `traceId` di `ErrorResponse` → satu request bisa ditelusuri dari log hingga response error.

Persistensi
-----------

- Migration disertakan di `Data/Migrations`.
- Skema mencakup `Products`, `Orders`, `OrderItems`, `IdempotencyKeys`.
- Seeder (`DbSeeder`) mengisi beberapa produk contoh saat Development (idempoten).

Testing
-------

- `xUnit` + `Testcontainers.MsSql` (SQL Server asli, bukan InMemory — penting agar transaksi/locking/`rowversion` benar-benar teruji).
- `WebApplicationFactory` meng-override `DbContext` ke container test.
- Test yang disertakan:
    - **Skenario A**: dua order butuh 10 unit dari stok 15 → hanya satu sukses, stok akhir 5 (tidak minus).
    - **Skenario B**: dua update status bersamaan → hanya satu sukses, lainnya `409`/`422`.
    - **Skenario C**: dua request `POST` dengan `Idempotency-Key` sama → tetap satu order.

```bash
dotnet test
```

Cara Menjalankan
----------------

1. Jalankan SQL Server via Docker:

    ```bash
    docker run -d --name sqlserver \
  -e "ACCEPT_EULA=Y" -e "MSSQL_SA_PASSWORD=Your_pass123" \
  -p 1433:1433 mcr.microsoft.com/mssql/server:2022-latest
    ```

2. Set connection string di `appsettings.Development.json`:

    ```json
    "ConnectionStrings": {
  "Default": "Server=localhost,1433;Database=OrderManagement;User Id=sa;Password=Your_pass123;TrustServerCertificate=True;"
    }
    ```

3. Terapkan migration:

    ```bash
    cd "Order Management"
    dotnet ef database update
    ```

4. Jalankan:

    ```bash
    dotnet run
    ```

    Swagger: `http://localhost:5086/swagger`

5. Test:

    ```bash
    dotnet test
    ```
