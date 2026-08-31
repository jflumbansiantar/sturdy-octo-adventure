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
| **Identity Provider** | Duende IdentityServer 7 (OpenID Connect / OAuth 2.0) |
| **User Store** | ASP.NET Core Identity + EF Core (PostgreSQL) |
| **Auth** | JWT Bearer RS256 dari IdentityServer (HS256 lama masih diterima selama migrasi) |
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

`PortfolioOS.Identity` berdiri sendiri di luar rantai ini — ia microservice terpisah dengan
database sendiri, dan berkomunikasi dengan API hanya lewat token (API memvalidasi tanda tangan
token via endpoint discovery/JWKS, tanpa memanggil service identity per request).

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
│   ├── PortfolioOS.Identity/       # ← microservice autentikasi & otorisasi
│   │   ├── Config/                 # IdentityServerConfig (client, scope, resource), policy
│   │   ├── Controllers/            # UsersController — manajemen user & role (scope admin)
│   │   ├── Data/                   # ApplicationUser/Role, DbContext, seeder, migrations
│   │   ├── Pages/                  # UI login, logout, error (Razor Pages)
│   │   ├── Services/               # PortfolioProfileService — claim yang masuk ke token
│   │   └── Program.cs
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
│       ├── Pages/              # LoginPage, DashboardPage, HoldingsPage, AccountPage, ...
│       ├── ViewModels/         # MVVM, CommunityToolkit.Mvvm
│       │                       # AccountViewModel — tab "Akun": info akun + tombol Keluar
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
    "Username": "admin",
    "Password": "password"
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

### 5. Jalankan Identity Server

Buat database identity (terpisah dari database bisnis):

```sql
CREATE DATABASE portfolioos_identity;
```

```bash
dotnet run --project src/PortfolioOS.Identity
```

Berjalan di `https://localhost:7196` (HTTPS) atau `http://localhost:5244` (HTTP).
Migrations dan seed user/role dijalankan otomatis saat start.

Cek discovery document: `https://localhost:7196/.well-known/openid-configuration`

### 6. Jalankan API

```bash
dotnet run --project src/PortfolioOS.API
```

API berjalan di `https://localhost:7195` (HTTPS) atau `http://localhost:5195` (HTTP).  
Swagger UI tersedia di: `https://localhost:7195/swagger`

Data seed awal (holdings, transaksi, utang, ledger accounts, journal entries) akan dimasukkan otomatis jika database kosong.

### 7. Jalankan Web (Blazor)

```bash
dotnet run --project src/PortfolioOS.Web
```

Buka browser: `https://localhost:7001`

> Pastikan URL API di `src/PortfolioOS.Web/wwwroot/appsettings.json` sudah sesuai.

### 8. Jalankan Mobile (MAUI Android)

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

### 9. Jalankan Unit Tests

```bash
dotnet test tests/PortfolioOS.Shared.Tests        # parser dokumen — cepat, tanpa emulator
dotnet test tests/PortfolioOS.Application.Tests
dotnet test tests/PortfolioOS.API.Tests
```

---

## Default Login

Login lewat **PortfolioOS.Identity** (seed user, ubah di `appsettings.json` → `SeedUsers`):

| Email | Password | Role |
|---|---|---|
| `admin@portfolioos.local` | `Admin#12345` | `admin` |
| `user@portfolioos.local` | `User#12345` | `user` |

Login lama lewat `POST /api/auth/login` di API masih berfungsi (`admin` / `password`) selama
`Auth:AllowLegacyTokens` bernilai `true`. Lihat bagian
[Autentikasi & Otorisasi](#autentikasi--otorisasi).

---

## Autentikasi & Otorisasi

`PortfolioOS.Identity` adalah microservice OpenID Connect berbasis **Duende IdentityServer 7**
dengan store user **ASP.NET Core Identity** di database `portfolioos_identity`.

### Client terdaftar

| client_id | Grant | Dipakai oleh | Secret |
|---|---|---|---|
| `portfolioos-web` | authorization_code + PKCE | Blazor WASM | — (public) |
| `portfolioos-mobile` | authorization_code + PKCE | .NET MAUI (`portfolioos://callback`) | — (public) |
| `portfolioos-swagger` | authorization_code + PKCE | Swagger UI di API | — (public) |
| `portfolioos-jobs` | client_credentials | Background job / service-to-service | ya |
| `portfolioos-legacy` | password (ROPC) | Jembatan login lama Web/Mobile | ya |

> `portfolioos-legacy` hanya untuk masa migrasi. Matikan di produksi lewat
> `Clients:EnableLegacyPasswordClient = false`.

### Scope & role

| Scope | Arti |
|---|---|
| `portfolioos.read` | Baca portofolio, transaksi, utang, ledger |
| `portfolioos.write` | Membuat/mengubah data |
| `portfolioos.admin` | Manajemen user & pengaturan sistem |

Role: `admin`, `user`, `viewer` — dikirim sebagai claim `role` di dalam access token, jadi API
tidak perlu memanggil `/connect/userinfo` per request.

Endpoint `/api/users` dan `/api/roles` di service identity butuh scope `portfolioos.admin`
**sekaligus** role `admin`. Scope saja tidak cukup, jadi user biasa yang memintanya tetap ditolak 403.

### Bagaimana API memvalidasi token

`PortfolioOS.API` menjalankan dua skema sekaligus dan memilihnya dari klaim `iss` di token:

- token dari IdentityServer → divalidasi RS256 lewat JWKS di discovery endpoint;
- token HS256 lama dari `POST /api/auth/login` → tetap diterima selama `Auth:AllowLegacyTokens=true`.

Controller memakai policy berbasis scope: GET butuh `portfolioos.read`, endpoint yang mengubah data
butuh `portfolioos.write`. Token lama tidak mengenal scope, jadi dianggap punya semua akses seperti
perilaku sebelumnya — sampai `Auth:AllowLegacyTokens` dimatikan.

### Konfigurasi `src/PortfolioOS.Identity/appsettings.json`

File ini di-`.gitignore` (seperti appsettings API), jadi buat sendiri:

```json
{
  "ConnectionStrings": {
    "IdentityConnection": "Host=localhost;Database=portfolioos_identity;Username=postgres;Password=postgres"
  },
  "Cookie": { "SameSite": "Lax" },
  "IdentityServer": {
    "PublicOrigin": "https://localhost:7196",
    "IssuerUri": "",
    "MetadataAddress": "",
    "LicenseKey": "",
    "SigningCertificate": { "Path": "", "Password": "" }
  },
  "Clients": {
    "WebBaseUrl": "https://localhost:7001",
    "ApiBaseUrl": "https://localhost:7195",
    "MobileRedirectUri": "portfolioos://callback",
    "MobilePostLogoutRedirectUri": "portfolioos://logout",
    "JobsClientSecret": "jobs-secret-change-me",
    "EnableLegacyPasswordClient": true,
    "LegacyClientSecret": "legacy-secret-change-me"
  },
  "SeedUsers": [
    { "Email": "admin@portfolioos.local", "Password": "Admin#12345", "DisplayName": "Administrator", "Role": "admin", "PreferredCurrency": "IDR" },
    { "Email": "user@portfolioos.local", "Password": "User#12345", "DisplayName": "Pengguna PortfolioOS", "Role": "user", "PreferredCurrency": "IDR" }
  ]
}
```

### Migrations service identity

Dua DbContext terpisah, jadi `--context` wajib disebut:

```bash
# store user/role
dotnet ef migrations add NamaMigration \
  --project src/PortfolioOS.Identity \
  --context PortfolioIdentityDbContext \
  --output-dir Data/Migrations

# persisted grant (refresh token, authorization code, consent)
dotnet ef migrations add NamaMigration \
  --project src/PortfolioOS.Identity \
  --context PersistedGrantDbContext \
  --output-dir Data/Migrations/PersistedGrant
```

### Catatan produksi

- **Lisensi Duende.** IdentityServer gratis untuk development/testing dan untuk organisasi dengan
  pendapatan di bawah ambang yang ditetapkan Duende; di luar itu wajib berlisensi. Isi
  `IdentityServer:LicenseKey`. Tanpa itu service tetap jalan tetapi mencatat peringatan saat start.
- **Signing key.** Di Development dipakai developer key (`tempkey.jwk`, sudah di-gitignore).
  Di luar Development, `IdentityServer:SigningCertificate:Path` **wajib** diisi — service menolak
  start kalau kosong.
- **Cookie SameSite.** Default `Lax` supaya jalan di HTTP lokal. Untuk silent-renew SPA lewat iframe
  di atas HTTPS, set `Cookie:SameSite` ke `None`.
- **Versi paket.** `PortfolioOS.API` mereferensikan `Microsoft.IdentityModel.Protocols.OpenIdConnect`
  8.14.0 secara eksplisit. Tanpa itu NuGet menyisakan campuran versi (Protocols 7.1.2 + core 8.14.0)
  yang membuat parser discovery document diam-diam mengabaikan `jwks_uri`, sehingga semua token
  IdentityServer ditolak dengan `IDX10500`. Jangan hapus referensi itu.

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
- Identity: `http://localhost:5244` (discovery di `/.well-known/openid-configuration`)
- PostgreSQL: `localhost:5432` (`postgres` / `postgres`), data persisten di named volume `pgdata`

Migrations dan seed data otomatis dijalankan oleh container `api` dan `identity` saat pertama kali
start (`api` menunggu `postgres` dan `identity` sehat lebih dulu via healthcheck). Database
`portfolioos_identity` dibuat oleh `database/init-identity-db.sql` saat volume `pgdata` pertama kali
dibuat — kalau volume sudah ada dari sebelumnya, buat manual:

```bash
docker compose exec postgres psql -U postgres -c "CREATE DATABASE portfolioos_identity"
```

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

**PortfolioOS.API**

```bash
ConnectionStrings__DefaultConnection="Host=db;Database=portfolioos;Username=app;Password=secret"
IdentityServer__Authority="https://id.yourdomain.com"
IdentityServer__Audience="portfolioos-api"
# Matikan jalur token lama setelah Web/Mobile selesai dimigrasikan ke OIDC
Auth__AllowLegacyTokens="false"
Cors__AllowedOrigins__0="https://yourdomain.com"
```

**PortfolioOS.Identity**

```bash
ConnectionStrings__IdentityConnection="Host=db;Database=portfolioos_identity;Username=app;Password=secret"
IdentityServer__IssuerUri="https://id.yourdomain.com"
IdentityServer__PublicOrigin="https://id.yourdomain.com"
IdentityServer__LicenseKey="<lisensi-duende>"
IdentityServer__SigningCertificate__Path="/certs/identity-signing.pfx"
IdentityServer__SigningCertificate__Password="<password-pfx>"
Cookie__SameSite="None"
Clients__WebBaseUrl="https://yourdomain.com"
Clients__ApiBaseUrl="https://api.yourdomain.com"
Clients__EnableLegacyPasswordClient="false"
Clients__JobsClientSecret="<secret-acak>"
SeedUsers__0__Email="admin@yourdomain.com"
SeedUsers__0__Password="<password-kuat>"
SeedUsers__0__Role="admin"
```
