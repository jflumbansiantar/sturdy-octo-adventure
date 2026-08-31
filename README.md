# PortfolioOS — Mini Wealth Management

Aplikasi manajemen kekayaan pribadi berbasis web dan mobile. Fitur utama mencakup portofolio saham multi-market (US & IDR), pelacakan transaksi, manajemen utang, pembukuan double-entry, integrasi harga pasar real-time via Yahoo Finance, dan input transaksi dengan memotret dokumen (OCR on-device di aplikasi mobile).

---

## Tech Stack

| Layer | Teknologi |
|---|---|
| **Backend API** | ASP.NET Core 8.0 Web API |
| **Frontend Web** | Blazor WebAssembly + MudBlazor |
| **Mobile** | .NET MAUI 8 (Android & iOS) |
| **Database** | PostgreSQL 15+ via EF Core 8 (Npgsql) |
| **ORM** | Entity Framework Core 8.0 |
| **CQRS** | MediatR 14 |
| **Validation** | FluentValidation 12 |
| **Mapping** | AutoMapper 16 |
| **Auth** | JWT Bearer (HS256) |
| **Market Data** | Yahoo Finance HTTP API |
| **Mobile MVVM** | CommunityToolkit.Mvvm 8 |
| **OCR (mobile)** | ML Kit Text Recognition (Android) + Vision framework (iOS) — on-device, tanpa jaringan |
| **Unit Testing** | xUnit + FluentAssertions + EF InMemory |

---

## Arsitektur

Menggunakan **Clean Architecture** dengan pemisahan layer:

```
Domain ← Application ← Infrastructure ← API / Web / Mobile
```

- **Domain** — entitas, enum, tidak ada dependency eksternal
- **Application** — CQRS commands/queries via MediatR, interfaces
- **Infrastructure** — implementasi DbContext (PostgreSQL), YahooFinance service
- **API** — ASP.NET Core controllers + JWT auth + Swagger
- **Web** — Blazor WASM, consume API
- **Mobile** — .NET MAUI, consume API via `HttpClient`

---

## Struktur Folder

```
PortfolioOS.sln
├── src/
│   ├── PortfolioOS.Domain/
│   │   ├── Entities/           # Holding, Transaction, Debt, LedgerAccount, JournalEntry, ...
│   │   ├── Enums/              # HoldingType, Market, TransactionCategory, DebtType, ...
│   │   └── Interfaces/
│   │
│   ├── PortfolioOS.Application/
│   │   ├── Common/
│   │   │   ├── Behaviors/      # ValidationBehavior (MediatR pipeline)
│   │   │   └── Interfaces/     # IApplicationDbContext
│   │   ├── Holdings/           # CreateHolding, UpdateHolding, DeleteHolding, GetHoldings
│   │   ├── Transactions/       # CreateTransaction, DeleteTransaction, GetTransactions
│   │   ├── Debts/              # CreateDebt, UpdateDebt, DeleteDebt, GetDebts
│   │   ├── Portfolio/          # GetPortfolioSummary
│   │   ├── Performance/        # GetPerformance
│   │   ├── Market/             # GetQuote, RefreshPrices
│   │   ├── Ledger/             # CreateAccount, CreateJournalEntry, GetLedger
│   │   └── Settings/           # GetSettings, UpsertSetting
│   │
│   ├── PortfolioOS.Infrastructure/
│   │   ├── Persistence/
│   │   │   ├── ApplicationDbContext.cs
│   │   │   ├── DataSeeder.cs          # ← data seed awal
│   │   │   ├── Configurations/        # IEntityTypeConfiguration per tabel
│   │   │   └── Migrations/            # EF Core migrations
│   │   └── Services/
│   │       └── YahooFinanceMarketDataService.cs
│   │
│   ├── PortfolioOS.Shared/     # DTOs / constants bersama
│   │   └── Scanning/           # mesin baca dokumen: MoneyParser, IndoDateParser,
│   │                           # AmountPicker, DocumentClassifier, Parsers/*
│   │                           # pure C#, tanpa I/O — bisa dites tanpa emulator
│   │
│   ├── PortfolioOS.API/
│   │   ├── Controllers/        # AuthController, HoldingsController, ...
│   │   ├── Middleware/         # ExceptionHandlingMiddleware
│   │   ├── Program.cs
│   │   └── appsettings.json
│   │
│   ├── PortfolioOS.Web/        # Blazor WASM
│   │   └── Pages/              # Dashboard, Holdings, Transactions, Debts, Ledger
│   │
│   └── PortfolioOS.Mobile/     # .NET MAUI
│       ├── Pages/              # LoginPage, DashboardPage, HoldingsPage, ScanReviewPage, ...
│       ├── ViewModels/         # MVVM, CommunityToolkit.Mvvm
│       ├── Services/           # AuthService, ApiClient
│       │   └── Ocr/            # IOcrService — implementasi per-platform di Platforms/
│       ├── Models/             # ApiModels.cs
│       ├── Converters/         # GainColorConverter, DebtProgressConverter, ...
│       ├── AppShell.xaml       # Tab bar navigation
│       └── MauiProgram.cs      # DI wiring
│
└── tests/
    ├── PortfolioOS.Shared.Tests/        # Unit tests — parser struk/transfer/slip/tagihan/saham
    ├── PortfolioOS.Application.Tests/   # Unit tests — Holdings, Transactions
    └── PortfolioOS.API.Tests/           # Integration tests
```

---

## Prasyarat

| Tool | Versi Minimum |
|---|---|
| .NET SDK | 8.0 |
| PostgreSQL | 15 |
| Node.js *(opsional, untuk tooling Blazor)* | 18+ |
| JDK | **21** (wajib untuk build Android MAUI) |
| Android SDK | API 34 (compile) |
| Android minimum | **API 23** (Android 6.0) — dituntut oleh ML Kit |
| dotnet-ef | 8.0 (global tool) |

> **Catatan JDK:** .NET MAUI 8 membutuhkan JDK 21. JDK 22+ **tidak** kompatibel.  
> Download: [Eclipse Temurin 21](https://adoptium.net/temurin/releases/?version=21)

---

## Instalasi & Setup

### 1. Clone repository

```bash
git clone <repo-url>
cd MiniWealthManagement
```

### 2. Konfigurasi database

Buat database PostgreSQL:

```sql
CREATE DATABASE portfolioos;
```

Sesuaikan connection string di `src/PortfolioOS.API/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=portfolioos;Username=postgres;Password=postgres"
  },
  "Jwt": {
    "Secret": "ganti-dengan-random-string-minimal-32-karakter",
    "Issuer": "PortfolioOS",
    "Audience": "PortfolioOS",
    "ExpiryHours": 24
  },
  "Auth": {
    "Username": "*username*",
    "Password": "*password*"
  }
}
```

### 3. Install EF Core CLI (jika belum ada)

```bash
dotnet tool install --global dotnet-ef
```

### 4. Jalankan migrations

Migrations dijalankan **otomatis** saat API pertama kali start. Namun jika ingin dijalankan manual:

```bash
dotnet ef database update \
  --project src/PortfolioOS.Infrastructure \
  --startup-project src/PortfolioOS.API
```

### 5. Jalankan API

```bash
dotnet run --project src/PortfolioOS.API
```

API berjalan di `https://localhost:7195` (HTTPS) atau `http://localhost:5195` (HTTP).  
Swagger UI tersedia di: `https://localhost:7195/swagger`

Data seed awal (holdings, transaksi, utang, ledger accounts, journal entries) akan dimasukkan otomatis jika database kosong.

### 6. Jalankan Web (Blazor)

```bash
dotnet run --project src/PortfolioOS.Web
```

Buka browser: `https://localhost:7001`

> Pastikan URL API di `src/PortfolioOS.Web/wwwroot/appsettings.json` sudah sesuai.

### 7. Jalankan Mobile (MAUI Android)

Pastikan JDK 21 terinstall dan terset sebagai `JAVA_HOME`:

```bash
# Cek versi Java
java -version   # harus 21.x

# Build untuk Android emulator
dotnet build src/PortfolioOS.Mobile -f net8.0-android

# Run di emulator
dotnet run --project src/PortfolioOS.Mobile -f net8.0-android
```

> URL API di `src/PortfolioOS.Mobile/MauiProgram.cs` default ke `https://10.0.2.2:7195`  
> (Android emulator → host machine localhost). Sesuaikan untuk perangkat fisik atau iOS.

### 8. Jalankan Unit Tests

```bash
dotnet test tests/PortfolioOS.Shared.Tests        # parser dokumen — cepat, tanpa emulator
dotnet test tests/PortfolioOS.Application.Tests
dotnet test tests/PortfolioOS.API.Tests
```

---

## Default Login

| Field | Value |
|---|---|
| Username | `admin` |
| Password | `password` |

Ubah di `appsettings.json` → section `Auth`.

---

## Membuat Migrations Baru

Setelah mengubah entity atau konfigurasi:

```bash
dotnet ef migrations add NamaMigration \
  --project src/PortfolioOS.Infrastructure \
  --startup-project src/PortfolioOS.API \
  --output-dir Persistence/Migrations
```

---

## Seed Data

Data awal diisi otomatis oleh `DataSeeder.cs` saat API startup (hanya jika tabel kosong):

| Tabel | Isi |
|---|---|
| `app_settings` | default_currency, portfolio_benchmark, display_name |
| `ledger_accounts` | 18 akun (Aset, Liabilitas, Ekuitas, Pendapatan, Beban) |
| `holdings` | 13 aset (AAPL, MSFT, NVDA, BBCA, TLKM, BBRI, ASII, ETH, BTC, reksa dana, ETF) |
| `price_caches` | Harga terakhir untuk semua holding |
| `debts` | KK BCA, KTA Mandiri, KPR BNI, cicilan HP (Lunas) |
| `transactions` | 20 transaksi (gaji, dividen, belanja, cicilan, beli saham/crypto) |
| `journal_entries` | 5 jurnal double-entry (gaji, KPR, dividen, belanja, beli saham) |

---

## Menjalankan via Docker

Cara tercepat untuk menjalankan API + Web + PostgreSQL sekaligus, tanpa install Postgres/dotnet SDK secara lokal:

```bash
docker compose up -d --build
```

- Web: `http://localhost:8081`
- API / Swagger: `http://localhost:5243/swagger`
- PostgreSQL: `localhost:5432` (`postgres` / `postgres`), data persisten di named volume `pgdata`

Migrations dan seed data otomatis dijalankan oleh container `api` saat pertama kali start (menunggu `postgres` sehat lebih dulu via healthcheck). Default login tetap `admin` / `password`.

Untuk mematikan (dan hapus data DB):

```bash
docker compose down -v
```

Untuk hanya mematikan tanpa hapus data:

```bash
docker compose down
```

> Web (Blazor WASM) mengarah ke API via `src/PortfolioOS.Web/wwwroot/appsettings.Production.json` (`http://localhost:5243`) — file terpisah dari `appsettings.json` yang dipakai `dotnet run` lokal, karena secara default Blazor WASM standalone yang dilayani nginx berjalan di environment `Production`. Jika port API di-`docker-compose.yml` diubah, sesuaikan juga file ini.

Mobile (MAUI) tidak di-Dockerize karena butuh build native Android/iOS.

---

## Variabel Lingkungan (Produksi)

Untuk deployment produksi, override konfigurasi via environment variables:

```bash
ConnectionStrings__DefaultConnection="Host=db;Database=portfolioos;Username=app;Password=secret"
Jwt__Secret="production-secret-min-32-chars-random"
Auth__Username="admin"
Auth__Password="strong-password-here"
Cors__AllowedOrigins__0="https://yourdomain.com"
```
