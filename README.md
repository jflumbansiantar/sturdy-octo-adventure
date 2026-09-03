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

## Mode Test Drive (akun demo)

Supaya orang lain bisa mencoba aplikasi web tanpa dibuatkan akun — dan tanpa menyentuh data
pemilik — ada satu akun test tetap, dijalankan di **stack Docker terpisah**.

```bash
docker compose -f docker-compose.demo.yml up -d --build
```

| | Stack utama (`docker-compose.yml`) | Stack test drive (`docker-compose.demo.yml`) |
|---|---|---|
| Web | `http://localhost:8081` | `http://localhost:8082` |
| API | `http://localhost:5243` | `http://localhost:5343` |
| PostgreSQL | `localhost:5432`, volume `pgdata` | `localhost:5433`, volume `pgdata-demo` |
| Akun demo | mati (`Demo__Enabled=false`) | hidup: `demo` / `demo123` |
| JWT secret | milik sendiri | berbeda — token dari sini tidak berlaku di stack utama |

Dua stack ini tidak berbagi apa pun: database, proses, volume, maupun batas koneksi. Jadi
apa pun yang terjadi di stack test — sesi membludak, disk penuh, deploy gagal — tidak bisa
menyentuh yang asli. Matikan dan bersihkan seluruh database demo dengan:

```bash
docker compose -f docker-compose.demo.yml down -v
```

Kredensialnya tampil langsung di halaman login (kolomnya dikunci, tinggal klik
**Mulai Test Drive**). Hanya ada di aplikasi **web**; aplikasi mobile tidak menampilkannya.

### Cara kerjanya

Setiap login demo membuat **schema PostgreSQL sendiri** (`demo_<12 hex>`) berisi salinan penuh
tabel aplikasi, diisi data contoh dari `DataSeeder`. Selama sesi, koneksi request itu memakai
`search_path` ke schema tersebut — jadi:

- **Data betul-betul tersimpan di database.** Tambah/ubah/hapus holding, transaksi, utang,
  ledger, dan settings berjalan apa adanya; tidak ada mode read-only atau data palsu.
- **Tidak ada satu pun query handler, entity, atau migration yang tahu soal mode demo.**
- **Data pemilik tidak pernah terlihat** dan tidak bisa disentuh dari sesi demo.

### Kapan data dihapus

| Pemicu | Yang terjadi |
|---|---|
| Klik logout | Dialog konfirmasi dulu, lalu `DROP SCHEMA ... CASCADE` — semua data sesi hilang |
| Waktu sesi habis (`Demo:SessionMinutes`, default 60 menit) | Token kedaluwarsa, schema di-drop janitor |
| Tidak ada request selama `Demo:IdleMinutes` (default 20 menit) | Sesi dianggap ditinggalkan, schema di-drop |
| API restart | Schema `demo_*` yang tidak punya baris registry ikut dibersihkan saat startup |

Menutup atau me-*refresh* tab **tidak** langsung menghapus data (hook `pagehide` tidak bisa
membedakan refresh dari pergi), jadi yang menjaga kebersihan database adalah timeout di server.
Browser tetap menampilkan peringatan sebelum tab ditutup, dan banner hitung mundur selalu
terlihat selama sesi demo berjalan.

### Konfigurasi

```json
"Demo": {
  "Enabled": false,
  "Username": "demo",
  "Password": "demo123",
  "SessionMinutes": 60,
  "IdleMinutes": 20,
  "MaxConcurrentSessions": 5
}
```

**`Enabled` default `false` di mana-mana.** Akun publik tidak boleh muncul hanya karena
seseorang meng-upgrade tanpa membaca; satu-satunya tempat yang menyalakannya adalah
`docker-compose.demo.yml`. Untuk mencobanya lewat `dotnet run`:

```bash
Demo__Enabled=true dotnet run --project src/PortfolioOS.API
```

`MaxConcurrentSessions` membatasi jumlah schema yang boleh hidup bersamaan; kalau penuh, API
menjawab `429` dan halaman login menampilkan pesannya.

Registry sesi aktif ada di tabel `public.demo_sessions`, dibuat otomatis saat API start (di luar
EF migrations — lihat komentar di `DemoSessionStore`).

URL API yang dipakai situs demo di-*mount* dari `docker/demo/`, bukan di-build ulang — Blazor
WASM membaca file itu lewat HTTP saat start. Ada dua salinan dan `DEMO_WEB_CONFIG` memilih
salah satunya:

| File | Untuk |
|---|---|
| `web-appsettings.Production.json` | Dua port di localhost — mode lokal, dipakai kalau `DEMO_WEB_CONFIG` tidak diset |
| `web-appsettings.same-origin.json` | `ApiBaseUrl` kosong = "panggil host yang melayani saya" — untuk di balik reverse proxy |

> Catatan: fitur ini khusus untuk pengguna aplikasi web. Tidak ada akun test terpisah untuk
> dashboard admin.

### Deploy ke VPS

`docker-compose.demo.yml` sudah siap dipakai di server: semua nilainya dibaca dari environment
variable dengan default yang mereproduksi persis run lokal di atas, jadi men-deploy berarti
menulis satu file `.env.demo` dan tidak menyentuh compose-nya sama sekali.

```bash
git clone https://github.com/jflumbansiantar/sturdy-octo-adventure.git portfolioos
cd portfolioos
cp .env.demo.example .env.demo
$EDITOR .env.demo            # minimal: DEMO_WEB_CONFIG, DEMO_JWT_SECRET, DEMO_DB_PASSWORD

docker compose -f docker-compose.demo.yml --env-file .env.demo up -d --build
```

Lalu arahkan reverse proxy yang sudah jalan di VPS ke sana — contoh lengkapnya ada di
`docker/demo/nginx-site.conf.example` dan `docker/demo/Caddyfile.example`:

```nginx
location /api/ { proxy_pass http://127.0.0.1:5343; }   # DEMO_API_PORT
location /     { proxy_pass http://127.0.0.1:8082; }   # DEMO_WEB_PORT
```

**Situs dan API harus dilayani dari satu nama host.** Blazor WASM memanggil path relatif
`api/...` terhadap alamat halamannya sendiri, jadi dengan
`DEMO_WEB_CONFIG=./docker/demo/web-appsettings.same-origin.json` tidak ada nama host yang
tertulis di file mana pun, dan karena tidak ada request lintas origin, CORS tidak ikut bermain.

Yang berbeda dari run lokal, dan kenapa:

| | Kenapa |
|---|---|
| Semua port publish ke `127.0.0.1`, bukan `0.0.0.0` | Di laptop tidak ada bedanya; di VPS ini yang memisahkan "hanya reverse proxy yang bisa menjangkau" dari "seluruh internet bisa" — terutama untuk port Postgres |
| `restart: unless-stopped` | Tanpa ini, VPS yang di-reboot meninggalkan demo mati sampai ada yang SSH manual |
| `DEMO_JWT_SECRET`, `DEMO_DB_PASSWORD`, `DEMO_OWNER_PASSWORD` | Nilai default-nya tertulis di repo publik ini. Untuk stack demo dampaknya terbatas, tapi `DEMO_OWNER_PASSWORD` membuka schema `public` yang **tidak** ter-sandbox |
| `DEMO_ASPNETCORE_ENVIRONMENT` | `Development` (default) menyalakan Swagger di `/swagger`. Itu wajar untuk demo portfolio, tapi jadikan keputusan sadar — `Production` mematikannya. Exception handler tidak pernah membocorkan stack trace di kedua mode |

Kredensial akun demo sendiri (`DEMO_PASSWORD`) memang sengaja dipublikasikan di halaman login —
lihat penjelasan di `AuthController.DemoInfo`.

**Ukuran VPS.** `--build` menjalankan `dotnet publish` dan mengunduh model embedding ~490MB,
dan proses API memuat model itu ke memori. VPS 1GB besar kemungkinan OOM saat build; **2GB
minimum, 4GB nyaman**. Tiap sesi demo juga membangun satu schema penuh lalu meng-index ulang
korpus chat-nya, jadi naikkan `DEMO_MAX_SESSIONS` hanya sejauh RAM mengizinkan.

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

Ada satu stack lagi, `docker-compose.demo.yml`, khusus untuk akun test drive — database, port,
dan JWT secret-nya sendiri, tidak berbagi apa pun dengan stack di atas. Keduanya bisa jalan
bersamaan. Lihat [Mode Test Drive](#mode-test-drive-akun-demo).

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

# Akun test drive — hanya untuk deployment demo yang terpisah dari data asli,
# lihat bagian "Mode Test Drive"
Demo__Enabled="true"
Demo__Username="demo"
Demo__Password="demo123"
Demo__SessionMinutes="60"
Demo__IdleMinutes="20"
Demo__MaxConcurrentSessions="5"
```
