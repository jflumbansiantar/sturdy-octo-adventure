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

`PortfolioOS.Identity` dan `PortfolioOS.Admin` berdiri sendiri di luar rantai ini — keduanya
microservice terpisah dengan database sendiri, dan berkomunikasi lewat token (API memvalidasi
tanda tangan token via endpoint discovery/JWKS, tanpa memanggil service identity per request).

- **Domain** — entitas, enum, tidak ada dependency eksternal
- **Application** — CQRS commands/queries via MediatR, interfaces
- **Infrastructure** — implementasi DbContext (PostgreSQL), YahooFinance service
- **API** — ASP.NET Core controllers + JWT auth + Swagger
- **Web** — Blazor WASM, consume API
- **Mobile** — .NET MAUI, consume API via `HttpClient`
- **Identity** — microservice OIDC: user, role, penerbit token
- **Admin** — microservice khusus admin: setting web + fasad manajemen user
- **AdminWeb** — konsol admin (Blazor WASM), satu-satunya UI yang bicara ke `Admin`

### Kenapa admin dipisah jadi service sendiri

Konsol admin butuh dua hal yang pemiliknya berbeda: user/role (milik `Identity`) dan setting
aplikasi (milik database bisnis, lewat `API`). Menaruh keduanya di salah satu service akan
membuat service itu ikut memikirkan urusan service lain.

`PortfolioOS.Admin` menyelesaikannya tanpa menyalin data apa pun:

```
AdminWeb ──▶ Admin ──┬──▶ Identity   (user & role)
                     ├──▶ API        (setting aplikasi / app_settings)
                     └──▶ DB admin   (setting web — satu-satunya data miliknya sendiri)
```

Token milik admin yang sedang login diteruskan apa adanya ke service tujuan, jadi `Admin`
tidak punya kredensial machine-to-machine dan tiap service tetap memutuskan otorisasinya
sendiri. Kalau `Admin` ditembus, ia tidak bisa berbuat lebih banyak daripada token yang
kebetulan sedang lewat.

---

## Struktur Folder

```
PortfolioOS.sln
├── docker-compose.yml              # postgres + identity + api + web
├── database/
│   ├── schema.sql                  # DDL referensi (bisnis + identity), lihat "Skema Database"
│   └── init-identity-db.sql        # CREATE DATABASE portfolioos_identity untuk container postgres
│
├── src/
│   ├── PortfolioOS.Domain/
│   │   ├── Entities/           # Holding, Transaction, Debt, LedgerAccount, JournalEntry,
│   │   │                       # JournalLine, PriceCache, AppSetting
│   │   ├── Enums/              # HoldingType, Market, TransactionCategory, DebtType,
│   │   │                       # DebtStatus, AccountType, NormalBalanceType, CurrencyType
│   │   └── Interfaces/
│   │
│   ├── PortfolioOS.Application/
│   │   ├── Common/
│   │   │   ├── Behaviors/      # ValidationBehavior (MediatR pipeline)
│   │   │   ├── Interfaces/     # IApplicationDbContext, IMarketDataService, IExchangeRateService
│   │   │   └── Services/       # ExchangeRateService — kurs USD↔IDR
│   │   ├── Holdings/           # CreateHolding, UpdateHolding, DeleteHolding, GetHoldings
│   │   ├── Transactions/       # CreateTransaction, DeleteTransaction, GetTransactions
│   │   ├── Debts/              # CreateDebt, UpdateDebt, DeleteDebt, GetDebts
│   │   ├── Portfolio/          # GetPortfolioSummary
│   │   ├── Performance/        # GetPerformance
│   │   ├── Market/             # GetLiveQuotes, GetExchangeRate
│   │   ├── Ledger/             # CreateAccount, UpdateAccount, CreateJournalEntry,
│   │   │                       # DeleteJournalEntry, GetAccounts, GetEntries, GetLedgerSummary
│   │   └── Settings/           # GetSettings, UpdateSetting
│   │
│   ├── PortfolioOS.Infrastructure/
│   │   ├── Persistence/
│   │   │   ├── ApplicationDbContext.cs
│   │   │   ├── DataSeeder.cs          # ← data seed awal
│   │   │   ├── Configurations/        # IEntityTypeConfiguration per tabel
│   │   │   └── Migrations/            # EF Core migrations (database bisnis)
│   │   └── Services/
│   │       └── YahooFinanceMarketDataService.cs
│   │
│   ├── PortfolioOS.Shared/     # DTOs / constants bersama
│   │   └── Scanning/           # mesin baca dokumen: MoneyParser, IndoDateParser,
│   │                           # AmountPicker, DocumentClassifier, Parsers/*
│   │                           # pure C#, tanpa I/O — bisa dites tanpa emulator
│   │
│   ├── PortfolioOS.Identity/       # ← microservice autentikasi & otorisasi
│   │   ├── Config/                 # IdentityServerConfig (client, scope, resource),
│   │   │                           # AuthorizationPolicies, ClientUrlOptions
│   │   ├── Controllers/            # UsersController — manajemen user & role (scope admin)
│   │   ├── Data/                   # ApplicationUser/Role, PortfolioIdentityDbContext,
│   │   │                           # IdentitySeeder, Migrations/ (+ Migrations/PersistedGrant/)
│   │   ├── Pages/                  # UI login, logout, error (Razor Pages)
│   │   ├── Services/               # PortfolioProfileService — claim yang masuk ke token
│   │   ├── Dockerfile
│   │   └── Program.cs
│   │
│   ├── PortfolioOS.Admin/          # ← microservice khusus admin (backend)
│   │   ├── Authorization/          # AdminPolicies, ScopeRequirement — scope admin + role admin
│   │   ├── Configuration/          # DownstreamOptions — alamat Identity & API
│   │   ├── Controllers/            # UsersController, RolesController,
│   │   │                           # ApplicationSettingsController, WebSettingsController
│   │   ├── Data/                   # WebSetting, AdminDbContext, WebSettingDefaults,
│   │   │                           # WebSettingSeeder, Migrations/
│   │   ├── Middleware/             # ExceptionHandlingMiddleware — meneruskan error downstream
│   │   ├── Models/                 # DTO request/response
│   │   ├── Services/               # IdentityAdminClient, PortfolioApiClient,
│   │   │                           # BearerForwardingHandler
│   │   ├── Dockerfile
│   │   └── Program.cs
│   │
│   ├── PortfolioOS.API/
│   │   ├── Authorization/      # AuthenticationSetup (dua skema JWT + pemilih issuer),
│   │   │                       # PortfolioPolicies, ScopeRequirement
│   │   ├── Controllers/        # AuthController (login lama), Holdings, Transactions, Debts,
│   │   │                       # Ledger, Market, Portfolio, Settings
│   │   ├── Middleware/         # ExceptionHandlingMiddleware
│   │   ├── Dockerfile
│   │   ├── Program.cs
│   │   └── appsettings.json
│   │
│   ├── PortfolioOS.AdminWeb/       # ← konsol admin (Blazor WASM + MudBlazor)
│   │   ├── Pages/                  # Index (beranda), Users, Settings, Authentication
│   │   ├── Layout/                 # MainLayout, NavMenu
│   │   ├── Shared/                 # RedirectToLogin, AccessDenied
│   │   ├── Services/               # AdminApiClient, AdminAuthorizationMessageHandler,
│   │   │                           # ArrayClaimsPrincipalFactory
│   │   ├── Dockerfile              # build WASM lalu disajikan nginx
│   │   └── nginx.conf
│   │
│   ├── PortfolioOS.Web/        # Blazor WASM
│   │   ├── Pages/              # Dashboard, Holdings, Transactions, Debts, Ledger,
│   │   │                       # Market, Settings, Login
│   │   ├── Layout/             # MainLayout, NavMenu
│   │   ├── Services/           # AuthService, PortfolioApiClient, AppState
│   │   ├── Dockerfile          # build WASM lalu disajikan nginx
│   │   └── nginx.conf
│   │
│   └── PortfolioOS.Mobile/     # .NET MAUI
│       ├── Pages/              # LoginPage, DashboardPage, HoldingsPage, TransactionsPage,
│       │                       # DebtsPage, ScanReviewPage, AccountPage
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
    └── PortfolioOS.API.Tests/           # kerangka test API (belum ada test case)
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
    "Password": "ganti-password-ini",
    "AllowLegacyTokens": true
  },
  "IdentityServer": {
    "Authority": "https://localhost:7196",
    "MetadataAddress": "",
    "Audience": "portfolioos-api"
  },
  "Cors": {
    "AllowedOrigins": [ "https://localhost:7001" ]
  }
}
```

> Skema tabelnya sendiri dibuat oleh EF Core migrations saat API start — `database/schema.sql`
> hanya DDL referensi untuk membaca struktur tabel, view, dan enum tanpa membuka migration.

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

### 8. Jalankan Admin Service (backend admin)

Buat database admin (terpisah dari database bisnis dan identity):

```sql
CREATE DATABASE portfolioos_admin;
```

```bash
dotnet run --project src/PortfolioOS.Admin
```

Berjalan di `https://localhost:7197` (HTTPS) atau `http://localhost:5245` (HTTP).
Swagger UI: `https://localhost:7197/swagger`. Migrations dan seed setting web dijalankan
otomatis saat start.

> Service ini memanggil Identity dan API, jadi keduanya harus hidup lebih dulu.

### 9. Jalankan Admin Web (konsol admin)

```bash
dotnet run --project src/PortfolioOS.AdminWeb
```

Buka browser: `https://localhost:7002`

Login memakai akun ber-role `admin` di halaman login IdentityServer (authorization code + PKCE).
Akun tanpa role admin tetap bisa login tapi langsung disambut halaman "Akses ditolak".

> URL service admin dan authority OIDC dibaca dari
> `src/PortfolioOS.AdminWeb/wwwroot/appsettings.json`.

### 10. Jalankan Mobile (MAUI Android)

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

### 11. Jalankan Unit Tests

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

Konsol admin (`PortfolioOS.AdminWeb`) hanya menerima akun ber-role `admin`.

Login lama lewat `POST /api/auth/login` di API masih berfungsi selama `Auth:AllowLegacyTokens`
bernilai `true`. Kredensialnya dibaca dari `Auth:Username` / `Auth:Password` di
`src/PortfolioOS.API/appsettings.json` (bukan dari database identity). Lihat bagian
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
| `portfolioos-admin` | authorization_code + PKCE | Konsol admin (Blazor WASM) | — (public) |
| `portfolioos-swagger` | authorization_code + PKCE | Swagger UI di API | — (public) |
| `portfolioos-jobs` | client_credentials | Background job / service-to-service | ya |
| `portfolioos-legacy` | password (ROPC) | Jembatan login lama Web/Mobile | ya |

> `portfolioos-legacy` hanya untuk masa migrasi. Matikan di produksi lewat
> `Clients:EnableLegacyPasswordClient = false`.

> `portfolioos-admin` adalah satu-satunya client yang boleh meminta scope `portfolioos.admin`,
> dan umur tokennya paling pendek (access token 30 menit, refresh token sliding 8 jam).
> `portfolioos-web` sengaja tidak diberi scope itu, jadi token aplikasi biasa tidak akan
> pernah bisa memanggil endpoint admin sekalipun yang login seorang admin.

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

### Endpoint & policy

`PortfolioOS.API` — semua endpoint di bawah butuh token; policy `read` = scope `portfolioos.read`,
`write` = scope `portfolioos.write`.

| Endpoint | Policy |
|---|---|
| `POST /api/auth/login` | — (anonim, jalur token lama) |
| `GET /api/holdings` · `GET /api/transactions` · `GET /api/debts` | read |
| `POST /api/holdings` · `PATCH /api/holdings/{id}` · `DELETE /api/holdings/{id}` | write |
| `POST /api/transactions` · `DELETE /api/transactions/{id}` | write |
| `POST /api/debts` · `PATCH /api/debts/{id}` · `DELETE /api/debts/{id}` | write |
| `GET /api/ledger/accounts` · `GET /api/ledger/entries` · `GET /api/ledger/summary` | read |
| `POST /api/ledger/accounts` · `PATCH /api/ledger/accounts/{id}` | write |
| `POST /api/ledger/entries` · `DELETE /api/ledger/entries/{id}` | write |
| `GET /api/portfolio/summary` | read |
| `GET /api/market/quotes` · `GET /api/market/fx` | read |
| `GET /api/settings` | read |
| `PATCH /api/settings` | write |

`PortfolioOS.Admin` — **seluruh** endpoint butuh scope `portfolioos.admin` **dan** role `admin`
(dipasang sebagai fallback policy, jadi endpoint baru pun tertutup secara default). Hanya
`/health` yang anonim. Token HS256 lama ditolak di sini — token itu tidak mengenal scope maupun
role, jadi tidak bisa membuktikan pemakainya seorang admin.

| Endpoint | Fungsi | Sumber data |
|---|---|---|
| `GET /api/admin/users` · `GET /api/admin/users/{id}` | daftar & detail user | Identity |
| `POST /api/admin/users` | buat user baru | Identity |
| `PUT /api/admin/users/{id}/roles` | ganti role user | Identity |
| `POST /api/admin/users/{id}/activate` · `/deactivate` | aktif/nonaktifkan user | Identity |
| `POST /api/admin/users/{id}/reset-password` | set ulang password | Identity |
| `GET /api/admin/roles` | daftar role | Identity |
| `GET /api/admin/settings/application` | setting aplikasi (`app_settings`) | API |
| `PATCH /api/admin/settings/application` | ubah satu setting aplikasi | API |
| `GET /api/admin/settings/web` | setting web + metadatanya | DB admin |
| `PUT /api/admin/settings/web` | simpan beberapa setting web sekaligus | DB admin |
| `PUT /api/admin/settings/web/{key}` | simpan satu setting web | DB admin |
| `POST /api/admin/settings/web/{key}/reset` | kembalikan ke nilai bawaan | DB admin |

Dua pengaman khusus konsol: seorang admin tidak bisa menonaktifkan akunnya sendiri, dan tidak
bisa melepas role `admin` dari akunnya sendiri — keduanya dijawab `409 Conflict`. Tanpa itu satu
klik salah bisa mengunci semua orang keluar dari konsol, dan jalan baliknya hanya lewat database.

`PortfolioOS.Identity` — butuh scope `portfolioos.admin` **dan** role `admin`:

| Endpoint | Fungsi |
|---|---|
| `GET /api/users` · `GET /api/users/{id}` | daftar & detail user |
| `POST /api/users` | buat user baru |
| `PUT /api/users/{id}/roles` | ganti role user |
| `POST /api/users/{id}/activate` · `/deactivate` | aktif/nonaktifkan user |
| `POST /api/users/{id}/reset-password` | set ulang password |
| `GET /api/roles` | daftar role yang dikenal sistem |

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

### Konfigurasi `src/PortfolioOS.Admin/appsettings.json`

File ini di-`.gitignore` (seperti appsettings Identity), jadi buat sendiri:

```json
{
  "ConnectionStrings": {
    "AdminConnection": "Host=localhost;Database=portfolioos_admin;Username=postgres;Password=postgres"
  },
  "IdentityServer": {
    "Authority": "https://localhost:7196",
    "MetadataAddress": "",
    "Audience": "portfolioos-api",
    "RequireHttpsMetadata": true
  },
  "Downstream": {
    "IdentityBaseUrl": "https://localhost:7196",
    "ApiBaseUrl": "https://localhost:7195",
    "AllowInvalidCertificates": true,
    "TimeoutSeconds": 30
  },
  "Cors": {
    "AllowedOrigins": [ "https://localhost:7002", "http://localhost:5228" ]
  }
}
```

> `Downstream:AllowInvalidCertificates` hanya berlaku di Development — sertifikat dev ASP.NET
> Core sering belum di-trust runtime, sehingga panggilan ke `https://localhost` gagal sebelum
> sempat mengirim apa pun. Di luar Development flag ini diabaikan.

Konfigurasi konsolnya sendiri ada di `src/PortfolioOS.AdminWeb/wwwroot/appsettings.json`. File
itu ikut ter-version karena isinya memang publik — seluruhnya dikirim ke browser:

```json
{
  "AdminApiBaseUrl": "https://localhost:7197",
  "Oidc": {
    "Authority": "https://localhost:7196",
    "ClientId": "portfolioos-admin",
    "RedirectUri": "https://localhost:7002/authentication/login-callback",
    "PostLogoutRedirectUri": "https://localhost:7002/authentication/logout-callback"
  }
}
```

Daftar scope-nya sengaja ditulis di `Program.cs`, bukan di file itu: scope admin bukan hal yang
sebaiknya bisa berubah diam-diam lewat konfigurasi.

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

Untuk admin service (tabel setting web):

```bash
dotnet ef migrations add NamaMigration \
  --project src/PortfolioOS.Admin \
  --output-dir Data/Migrations
```

---

## Skema Database

Tiga database terpisah, semuanya dibuat dan di-migrate otomatis oleh EF Core saat service start:

| Database | Dibuat oleh | Isi |
|---|---|---|
| `portfolioos` | `PortfolioOS.Infrastructure` (`Persistence/Migrations`) | `holdings`, `transactions`, `price_caches`, `ledger_accounts`, `journal_entries`, `journal_lines`, `debts`, `app_settings` + 8 enum type Postgres |
| `portfolioos_identity` | `PortfolioOS.Identity` — `PortfolioIdentityDbContext` | `users`, `roles`, `user_roles`, `user_claims`, `user_logins`, `user_tokens`, `role_claims` |
| `portfolioos_identity` | `PortfolioOS.Identity` — `PersistedGrantDbContext` (Duende) | `PersistedGrants`, `DeviceCodes`, `Keys`, `ServerSideSessions`, `PushedAuthorizationRequests` |
| `portfolioos_admin` | `PortfolioOS.Admin` — `AdminDbContext` | `web_settings` |

`database/schema.sql` memuat DDL referensi untuk **ketiganya** — dipakai membaca struktur tabel,
index, enum, dan cast tanpa membuka file migration. File itu bukan jalur deployment: skema
sebenarnya tetap dibuat migrations, jadi kalau entity berubah, tambah migration lalu selaraskan
`schema.sql`.

> Blok `VIEWS` di akhir `schema.sql` (`v_holdings_enriched`, `v_ledger_account_balances`,
> `v_debt_payments`) tidak ikut dibuat migrations — field turunannya dihitung di Application layer.
> View itu berfungsi sebagai dokumentasi rumus dan alat bantu query manual.

Client, scope, dan API resource IdentityServer **tidak** disimpan di database (tanpa
`ConfigurationDbContext`) — semuanya in-memory di `PortfolioOS.Identity/Config/IdentityServerConfig.cs`
supaya perubahannya ikut code review.

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

`PortfolioOS.Admin` mengisi `web_settings` saat startup lewat `WebSettingSeeder`. Daftar key-nya
ditentukan kode (`WebSettingDefaults`), jadi seeder menambah key baru dan menyegarkan metadatanya
tiap start, tapi **tidak pernah** menimpa nilai yang sudah diubah admin. Key yang dihapus dari
kode ikut dibersihkan dari tabel.

| Kategori | Key |
|---|---|
| Umum | `web.app_name`, `web.support_email`, `web.maintenance_mode`, `web.maintenance_message` |
| Tampilan | `web.default_theme`, `web.default_currency`, `web.items_per_page`, `web.privacy_mode_default` |
| Fitur | `web.feature_market_page`, `web.feature_ledger_page`, `web.feature_ocr_upload` |
| Keamanan | `web.session_timeout_minutes`, `web.allow_self_registration` |

---

## Menjalankan via Docker

Cara tercepat untuk menjalankan seluruh service + PostgreSQL sekaligus, tanpa install Postgres/dotnet SDK secara lokal:

```bash
docker compose up -d --build
```

- Web: `http://localhost:8081`
- Konsol admin: `http://localhost:8082`
- API / Swagger: `http://localhost:5243/swagger`
- Admin API / Swagger: `http://localhost:5245/swagger`
- Identity: `http://localhost:5244` (discovery di `/.well-known/openid-configuration`)
- PostgreSQL: `localhost:5432` (`postgres` / `postgres`), data persisten di named volume `pgdata`

Migrations dan seed data otomatis dijalankan oleh container `api` dan `identity` saat pertama kali
start (`api` dan `admin` menunggu `postgres` dan `identity` sehat lebih dulu via healthcheck).
Database `portfolioos_identity` dan `portfolioos_admin` dibuat oleh `database/init-identity-db.sql`
dan `database/init-admin-db.sql` saat volume `pgdata` pertama kali dibuat — kalau volume sudah ada
dari sebelumnya, buat manual:

```bash
docker compose exec postgres psql -U postgres -c "CREATE DATABASE portfolioos_identity"
docker compose exec postgres psql -U postgres -c "CREATE DATABASE portfolioos_admin"
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

Konsol admin memakai pola yang sama: `src/PortfolioOS.AdminWeb/wwwroot/appsettings.Production.json`
mengarah ke `http://localhost:5245` (admin API) dan `http://localhost:5244` (Identity), dengan
redirect URI `http://localhost:8082/...`. Ketiganya harus cocok dengan `Clients__AdminWebBaseUrl`
di service `identity`, kalau tidak IdentityServer akan menolak redirect saat login.

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
Clients__AdminWebBaseUrl="https://admin.yourdomain.com"
Clients__EnableLegacyPasswordClient="false"
Clients__JobsClientSecret="<secret-acak>"
SeedUsers__0__Email="admin@yourdomain.com"
SeedUsers__0__Password="<password-kuat>"
SeedUsers__0__Role="admin"
```

**PortfolioOS.Admin**

```bash
ConnectionStrings__AdminConnection="Host=db;Database=portfolioos_admin;Username=app;Password=secret"
IdentityServer__Authority="https://id.yourdomain.com"
IdentityServer__Audience="portfolioos-api"
Downstream__IdentityBaseUrl="https://id.yourdomain.com"
Downstream__ApiBaseUrl="https://api.yourdomain.com"
Cors__AllowedOrigins__0="https://admin.yourdomain.com"
```

**PortfolioOS.AdminWeb** — bukan environment variable: konsolnya statis, jadi sesuaikan
`wwwroot/appsettings.Production.json` sebelum build (`AdminApiBaseUrl`, `Oidc:Authority`, dan
kedua redirect URI harus memakai domain produksi).
