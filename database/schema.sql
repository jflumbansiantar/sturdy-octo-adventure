-- =============================================================
-- PortfolioOS — PostgreSQL Schema
-- Target: .NET (EF Core + CQRS)
--
-- Tiga database terpisah:
--   1. portfolioos           — data bisnis, dibuat EF Core migrations milik
--                              PortfolioOS.Infrastructure (Persistence/Migrations).
--   2. portfolioos_identity  — user/role + operational store Duende IdentityServer,
--                              dibuat EF Core migrations milik PortfolioOS.Identity.
--                              Lihat bagian "DATABASE IDENTITY" di bagian bawah file.
--   3. portfolioos_admin     — setting web milik microservice admin, dibuat EF Core
--                              migrations milik PortfolioOS.Admin.
--                              Lihat bagian "DATABASE ADMIN" di akhir file.
--
-- File ini adalah DDL referensi yang mencerminkan hasil migrations — dipakai untuk
-- membaca struktur tanpa membuka file migration, bukan sebagai jalur deployment.
-- Kalau entity atau konfigurasi berubah, tambahkan migration lalu selaraskan file ini.
--
-- Dua perbedaan yang disengaja terhadap hasil migrations:
--   * nama constraint primary key di sini memakai default Postgres (<tabel>_pkey),
--     sedangkan EF Core menamainya PK_<tabel>;
--   * blok VIEWS di bawah tidak dibuat migrations — lihat catatan di sana.
-- =============================================================


-- #############################################################
-- DATABASE BISNIS: portfolioos
-- #############################################################

-- =============================================================
-- ENUM TYPES
-- =============================================================

CREATE TYPE holding_type AS ENUM (
    'Stock',
    'ETF',
    'Crypto',
    'Mutual Fund'
);

CREATE TYPE market_type AS ENUM (
    'US',
    'ID'
);

CREATE TYPE transaction_category AS ENUM (
    'STOCK',
    'DEBT',
    'INCOME',
    'EXPENSE'
);

CREATE TYPE account_type AS ENUM (
    'Asset',
    'Liability',
    'Equity',
    'Income',
    'Expense'
);

CREATE TYPE normal_balance_type AS ENUM (
    'Debit',
    'Credit'
);

CREATE TYPE debt_type AS ENUM (
    'Credit Card',
    'Personal Loan',
    'Mortgage',
    'Auto Loan',
    'Student Loan',
    'Other'
);

CREATE TYPE debt_status AS ENUM (
    'Active',
    'Lunas'
);

CREATE TYPE currency_type AS ENUM (
    'USD',
    'IDR'
);


-- -------------------------------------------------------------
-- CAST text -> enum
-- Npgsql mengirim nilai enum yang dikonversi ke string sebagai parameter `text`,
-- dan Postgres tidak punya cast bawaan text -> enum. Cast assignment di bawah
-- memakai fungsi input tiap enum supaya parameter string bisa langsung
-- di-INSERT/UPDATE ke kolom enum.
-- -------------------------------------------------------------
CREATE CAST (text AS holding_type)         WITH INOUT AS ASSIGNMENT;
CREATE CAST (text AS market_type)          WITH INOUT AS ASSIGNMENT;
CREATE CAST (text AS transaction_category) WITH INOUT AS ASSIGNMENT;
CREATE CAST (text AS account_type)         WITH INOUT AS ASSIGNMENT;
CREATE CAST (text AS normal_balance_type)  WITH INOUT AS ASSIGNMENT;
CREATE CAST (text AS debt_type)            WITH INOUT AS ASSIGNMENT;
CREATE CAST (text AS debt_status)          WITH INOUT AS ASSIGNMENT;
CREATE CAST (text AS currency_type)        WITH INOUT AS ASSIGNMENT;


-- =============================================================
-- TABLES
-- =============================================================

-- -------------------------------------------------------------
-- holdings
-- Posisi investasi: saham, ETF, crypto, reksa dana
-- -------------------------------------------------------------
CREATE TABLE holdings (
    id          UUID            NOT NULL DEFAULT gen_random_uuid() PRIMARY KEY,
    ticker      VARCHAR(20)     NOT NULL,
    name        VARCHAR(255)    NOT NULL,
    type        holding_type    NOT NULL,
    sub_type    VARCHAR(100)    NOT NULL DEFAULT '',
    market      market_type     NOT NULL,
    shares      NUMERIC(18, 8)  NOT NULL CHECK (shares >= 0),
    avg_cost    NUMERIC(18, 6)  NOT NULL CHECK (avg_cost >= 0),
    created_at  TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    updated_at  TIMESTAMPTZ     NOT NULL DEFAULT NOW()
);

-- -------------------------------------------------------------
-- transactions
-- Riwayat transaksi: beli/jual saham, pembayaran utang, income, expense
-- -------------------------------------------------------------
CREATE TABLE transactions (
    id          UUID                    NOT NULL DEFAULT gen_random_uuid() PRIMARY KEY,
    date        DATE                    NOT NULL,
    category    transaction_category    NOT NULL DEFAULT 'STOCK',
    name        VARCHAR(255)            NOT NULL,       -- ticker untuk STOCK, deskripsi untuk lainnya
    type        VARCHAR(100)            NOT NULL,       -- BUY, SELL, SALARY, PAYMENT, BONUS, FOOD, dll
    total       NUMERIC(18, 6)          NOT NULL CHECK (total >= 0),
    market      market_type             NULL,           -- hanya untuk STOCK
    shares      NUMERIC(18, 8)          NULL CHECK (shares >= 0),   -- hanya untuk STOCK
    price       NUMERIC(18, 6)          NULL CHECK (price >= 0),    -- hanya untuk STOCK
    created_at  TIMESTAMPTZ             NOT NULL DEFAULT NOW(),
    updated_at  TIMESTAMPTZ             NOT NULL DEFAULT NOW()
);

-- -------------------------------------------------------------
-- price_caches
-- Cache harga live dari Yahoo Finance
-- -------------------------------------------------------------
CREATE TABLE price_caches (
    ticker          VARCHAR(20)     NOT NULL PRIMARY KEY,   -- uppercase, e.g. 'BBCA.JK', 'AAPL'
    currency        currency_type   NOT NULL,
    current_price   NUMERIC(18, 6)  NOT NULL CHECK (current_price >= 0),
    previous_close  NUMERIC(18, 6)  NOT NULL CHECK (previous_close >= 0),
    updated_at      TIMESTAMPTZ     NOT NULL DEFAULT NOW()
);

-- -------------------------------------------------------------
-- ledger_accounts
-- Chart of Accounts untuk double-entry bookkeeping
-- Primary key adalah business ID seperti 'A1000', 'L2000'
-- -------------------------------------------------------------
CREATE TABLE ledger_accounts (
    id              VARCHAR(20)         NOT NULL PRIMARY KEY,   -- e.g. 'A1000', 'L2000', 'E3001'
    code            VARCHAR(20)         NOT NULL,
    name            VARCHAR(255)        NOT NULL,
    type            account_type        NOT NULL,
    normal_balance  normal_balance_type NOT NULL,
    opening_balance NUMERIC(18, 6)      NOT NULL DEFAULT 0,
    created_at      TIMESTAMPTZ         NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ         NOT NULL DEFAULT NOW()
);

-- -------------------------------------------------------------
-- journal_entries
-- Header jurnal double-entry
-- Primary key adalah business ID seperti 'JE001'
-- -------------------------------------------------------------
CREATE TABLE journal_entries (
    id          VARCHAR(20)     NOT NULL PRIMARY KEY,   -- e.g. 'JE001', 'JE002'
    date        DATE            NOT NULL,
    description VARCHAR(500)    NOT NULL,
    created_at  TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    updated_at  TIMESTAMPTZ     NOT NULL DEFAULT NOW()
);

-- -------------------------------------------------------------
-- journal_lines
-- Baris detail debit/credit dari setiap journal entry
-- -------------------------------------------------------------
CREATE TABLE journal_lines (
    id          UUID            NOT NULL DEFAULT gen_random_uuid() PRIMARY KEY,
    entry_id    VARCHAR(20)     NOT NULL REFERENCES journal_entries(id) ON DELETE CASCADE,
    account_id  VARCHAR(20)     NOT NULL REFERENCES ledger_accounts(id) ON DELETE CASCADE,
    debit       NUMERIC(18, 6)  NOT NULL DEFAULT 0 CHECK (debit >= 0),
    credit      NUMERIC(18, 6)  NOT NULL DEFAULT 0 CHECK (credit >= 0),

    -- Setiap baris harus murni debit ATAU murni credit, tidak bisa keduanya
    CONSTRAINT chk_journal_lines_debit_xor_credit CHECK (
        (debit > 0 AND credit = 0) OR (debit = 0 AND credit > 0)
    )
);

-- -------------------------------------------------------------
-- debts
-- Tracking utang: kartu kredit, KPR, KTA, dll
-- -------------------------------------------------------------
CREATE TABLE debts (
    id                      UUID            NOT NULL DEFAULT gen_random_uuid() PRIMARY KEY,
    name                    VARCHAR(255)    NOT NULL,
    type                    debt_type       NOT NULL,
    balance                 NUMERIC(18, 6)  NOT NULL CHECK (balance >= 0),
    interest_rate           NUMERIC(8, 4)   NOT NULL CHECK (interest_rate >= 0),        -- Annual EAR (%)
    monthly_interest_rate   NUMERIC(8, 4)   NULL CHECK (monthly_interest_rate >= 0),   -- Monthly (%)
    tenor                   INTEGER         NULL CHECK (tenor >= 1),                    -- Dalam bulan
    minimum_payment         NUMERIC(18, 6)  NOT NULL CHECK (minimum_payment >= 0),
    due_day                 INTEGER         NOT NULL DEFAULT 1 CHECK (due_day BETWEEN 1 AND 31),
    currency                currency_type   NOT NULL DEFAULT 'USD',
    debt_app                VARCHAR(255)    NOT NULL DEFAULT '',   -- Nama aplikasi/bank
    notes                   TEXT            NOT NULL DEFAULT '',
    status                  debt_status     NOT NULL DEFAULT 'Active',
    created_at              TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    updated_at              TIMESTAMPTZ     NOT NULL DEFAULT NOW()
);

-- -------------------------------------------------------------
-- app_settings
-- Key-value store untuk konfigurasi aplikasi
-- Value disimpan sebagai JSONB agar bisa menampung tipe apapun
-- -------------------------------------------------------------
CREATE TABLE app_settings (
    key         VARCHAR(100)    NOT NULL PRIMARY KEY,
    value       JSONB           NOT NULL,
    created_at  TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    updated_at  TIMESTAMPTZ     NOT NULL DEFAULT NOW()
);


-- =============================================================
-- INDEXES
-- =============================================================

-- holdings
CREATE UNIQUE INDEX uq_holdings_ticker ON holdings(ticker);
CREATE INDEX idx_holdings_market   ON holdings(market);
CREATE INDEX idx_holdings_type     ON holdings(type);

-- transactions
CREATE INDEX idx_transactions_date      ON transactions(date);
CREATE INDEX idx_transactions_category  ON transactions(category);
CREATE INDEX idx_transactions_name      ON transactions(name);
CREATE INDEX idx_transactions_market    ON transactions(market);

-- journal_lines
CREATE INDEX idx_journal_lines_entry_id   ON journal_lines(entry_id);
CREATE INDEX idx_journal_lines_account_id ON journal_lines(account_id);

-- journal_entries
CREATE INDEX idx_journal_entries_date ON journal_entries(date);

-- debts
CREATE INDEX idx_debts_status ON debts(status);
CREATE INDEX idx_debts_type   ON debts(type);

-- price_caches
CREATE INDEX idx_price_caches_updated_at ON price_caches(updated_at);


-- =============================================================
-- TRIGGER: auto-update updated_at
-- =============================================================

CREATE OR REPLACE FUNCTION fn_update_updated_at()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = NOW();
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_holdings_updated_at
    BEFORE UPDATE ON holdings
    FOR EACH ROW EXECUTE FUNCTION fn_update_updated_at();

CREATE TRIGGER trg_transactions_updated_at
    BEFORE UPDATE ON transactions
    FOR EACH ROW EXECUTE FUNCTION fn_update_updated_at();

CREATE TRIGGER trg_ledger_accounts_updated_at
    BEFORE UPDATE ON ledger_accounts
    FOR EACH ROW EXECUTE FUNCTION fn_update_updated_at();

CREATE TRIGGER trg_journal_entries_updated_at
    BEFORE UPDATE ON journal_entries
    FOR EACH ROW EXECUTE FUNCTION fn_update_updated_at();

CREATE TRIGGER trg_debts_updated_at
    BEFORE UPDATE ON debts
    FOR EACH ROW EXECUTE FUNCTION fn_update_updated_at();

CREATE TRIGGER trg_app_settings_updated_at
    BEFORE UPDATE ON app_settings
    FOR EACH ROW EXECUTE FUNCTION fn_update_updated_at();

CREATE TRIGGER trg_price_caches_updated_at
    BEFORE UPDATE ON price_caches
    FOR EACH ROW EXECUTE FUNCTION fn_update_updated_at();


-- =============================================================
-- VIEWS (computed fields)
--
-- CATATAN: view di bawah TIDAK dibuat EF Core migrations, jadi tidak ada di
-- database hasil `dotnet ef database update` maupun di container docker. Field
-- turunan yang sama dihitung di Application layer (handler CQRS). View ini
-- disimpan sebagai dokumentasi rumusnya + alat bantu query manual; kalau mau
-- dipakai aplikasi, buat migration tersendiri untuk membuatnya.
-- =============================================================

-- v_holdings_enriched: holdings dengan data price dari cache
-- Equivalent to enrichHoldings() di holdingsService.js
CREATE OR REPLACE VIEW v_holdings_enriched AS
SELECT
    h.id,
    h.ticker,
    h.name,
    h.type,
    h.sub_type,
    h.market,
    h.shares,
    h.avg_cost,
    COALESCE(pc.current_price, 0)   AS current_price,
    COALESCE(pc.previous_close, 0)  AS previous_close,
    pc.currency,
    pc.updated_at                   AS price_updated_at,
    -- Calculated fields
    h.shares * h.avg_cost                               AS cost_basis,
    h.shares * COALESCE(pc.current_price, 0)            AS market_value,
    (h.shares * COALESCE(pc.current_price, 0))
        - (h.shares * h.avg_cost)                       AS gain_loss,
    CASE
        WHEN h.avg_cost = 0 THEN 0
        ELSE ROUND(
            ((COALESCE(pc.current_price, 0) - h.avg_cost) / h.avg_cost) * 100,
            2
        )
    END                                                 AS gain_loss_pct,
    COALESCE(pc.current_price, 0) - COALESCE(pc.previous_close, 0)  AS day_change,
    CASE
        WHEN COALESCE(pc.previous_close, 0) = 0 THEN 0
        ELSE ROUND(
            ((COALESCE(pc.current_price, 0) - COALESCE(pc.previous_close, 0))
                / COALESCE(pc.previous_close, 0)) * 100,
            2
        )
    END                                                 AS day_change_pct,
    h.shares * (COALESCE(pc.current_price, 0) - COALESCE(pc.previous_close, 0)) AS day_gain_loss,
    h.created_at,
    h.updated_at
FROM holdings h
LEFT JOIN price_caches pc ON pc.ticker = h.ticker;

-- v_ledger_account_balances: saldo akun dengan total debit/credit dari journal lines
-- Equivalent to computeBalances() di ledgerService.js
CREATE OR REPLACE VIEW v_ledger_account_balances AS
SELECT
    la.id,
    la.code,
    la.name,
    la.type,
    la.normal_balance,
    la.opening_balance,
    COALESCE(SUM(jl.debit), 0)      AS total_debits,
    COALESCE(SUM(jl.credit), 0)     AS total_credits,
    la.opening_balance + CASE
        WHEN la.normal_balance = 'Debit'
            THEN COALESCE(SUM(jl.debit), 0) - COALESCE(SUM(jl.credit), 0)
        ELSE
            COALESCE(SUM(jl.credit), 0) - COALESCE(SUM(jl.debit), 0)
    END                             AS balance,
    la.created_at,
    la.updated_at
FROM ledger_accounts la
LEFT JOIN journal_lines jl ON jl.account_id = la.id
GROUP BY la.id, la.code, la.name, la.type, la.normal_balance, la.opening_balance,
         la.created_at, la.updated_at;

-- v_debt_payments: utang dengan total pembayaran dari tabel transactions
-- Equivalent to computed totalPaid/monthsPaid di debtService.js
CREATE OR REPLACE VIEW v_debt_payments AS
SELECT
    d.*,
    COALESCE(t.total_paid, 0)   AS total_paid,
    COALESCE(t.months_paid, 0)  AS months_paid
FROM debts d
LEFT JOIN (
    SELECT
        name,
        SUM(total)  AS total_paid,
        COUNT(*)    AS months_paid
    FROM transactions
    WHERE category = 'DEBT'
    GROUP BY name
) t ON t.name = d.name;


-- #############################################################
-- DATABASE IDENTITY: portfolioos_identity
--
-- Database terpisah milik microservice PortfolioOS.Identity. Dibuat oleh dua
-- DbContext berbeda dan otomatis di-migrate saat service start:
--
--   PortfolioIdentityDbContext  -> store user & role (ASP.NET Core Identity)
--   PersistedGrantDbContext     -> operational store Duende IdentityServer 7
--
-- Identifier PascalCase sengaja di-quote: EF Core membuat kolom (dan tabel milik
-- Duende) persis dengan casing tersebut, bukan lower case seperti tabel bisnis.
--
-- Jalankan bagian ini setelah connect ke database yang benar:
--   CREATE DATABASE portfolioos_identity;
--   \connect portfolioos_identity
-- #############################################################

-- -------------------------------------------------------------
-- roles / users
-- IdentityRole<Guid> dan IdentityUser<Guid> plus kolom tambahan PortfolioOS:
-- Description pada role; DisplayName, PreferredCurrency, CreatedAt, LastLoginAt,
-- IsActive pada user. DisplayName/PreferredCurrency ikut jadi claim di access token.
-- -------------------------------------------------------------
CREATE TABLE roles (
    "Id"                UUID            NOT NULL PRIMARY KEY,
    "Description"       VARCHAR(256)    NOT NULL,
    "Name"              VARCHAR(256)    NULL,
    "NormalizedName"    VARCHAR(256)    NULL,
    "ConcurrencyStamp"  TEXT            NULL
);

CREATE TABLE users (
    "Id"                    UUID            NOT NULL PRIMARY KEY,
    "DisplayName"           VARCHAR(128)    NOT NULL,
    "PreferredCurrency"     VARCHAR(3)      NOT NULL,
    "CreatedAt"             TIMESTAMPTZ     NOT NULL,
    "LastLoginAt"           TIMESTAMPTZ     NULL,
    "IsActive"              BOOLEAN         NOT NULL,
    "UserName"              VARCHAR(256)    NULL,
    "NormalizedUserName"    VARCHAR(256)    NULL,
    "Email"                 VARCHAR(256)    NULL,
    "NormalizedEmail"       VARCHAR(256)    NULL,
    "EmailConfirmed"        BOOLEAN         NOT NULL,
    "PasswordHash"          TEXT            NULL,
    "SecurityStamp"         TEXT            NULL,
    "ConcurrencyStamp"      TEXT            NULL,
    "PhoneNumber"           TEXT            NULL,
    "PhoneNumberConfirmed"  BOOLEAN         NOT NULL,
    "TwoFactorEnabled"      BOOLEAN         NOT NULL,
    "LockoutEnd"            TIMESTAMPTZ     NULL,
    "LockoutEnabled"        BOOLEAN         NOT NULL,
    "AccessFailedCount"     INTEGER         NOT NULL
);

CREATE TABLE role_claims (
    "Id"         INTEGER GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
    "RoleId"     UUID    NOT NULL REFERENCES roles("Id") ON DELETE CASCADE,
    "ClaimType"  TEXT    NULL,
    "ClaimValue" TEXT    NULL
);

CREATE TABLE user_claims (
    "Id"         INTEGER GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
    "UserId"     UUID    NOT NULL REFERENCES users("Id") ON DELETE CASCADE,
    "ClaimType"  TEXT    NULL,
    "ClaimValue" TEXT    NULL
);

CREATE TABLE user_logins (
    "LoginProvider"       TEXT NOT NULL,
    "ProviderKey"         TEXT NOT NULL,
    "ProviderDisplayName" TEXT NULL,
    "UserId"              UUID NOT NULL REFERENCES users("Id") ON DELETE CASCADE,

    PRIMARY KEY ("LoginProvider", "ProviderKey")
);

CREATE TABLE user_roles (
    "UserId" UUID NOT NULL REFERENCES users("Id") ON DELETE CASCADE,
    "RoleId" UUID NOT NULL REFERENCES roles("Id") ON DELETE CASCADE,

    PRIMARY KEY ("UserId", "RoleId")
);

CREATE TABLE user_tokens (
    "UserId"        UUID NOT NULL REFERENCES users("Id") ON DELETE CASCADE,
    "LoginProvider" TEXT NOT NULL,
    "Name"          TEXT NOT NULL,
    "Value"         TEXT NULL,

    PRIMARY KEY ("UserId", "LoginProvider", "Name")
);

CREATE UNIQUE INDEX "RoleNameIndex"         ON roles("NormalizedName");
CREATE UNIQUE INDEX "UserNameIndex"         ON users("NormalizedUserName");
CREATE INDEX        "EmailIndex"            ON users("NormalizedEmail");
CREATE INDEX        "IX_role_claims_RoleId" ON role_claims("RoleId");
CREATE INDEX        "IX_user_claims_UserId" ON user_claims("UserId");
CREATE INDEX        "IX_user_logins_UserId" ON user_logins("UserId");
CREATE INDEX        "IX_user_roles_RoleId"  ON user_roles("RoleId");


-- -------------------------------------------------------------
-- Operational store Duende IdentityServer (PersistedGrantDbContext)
-- Refresh token, authorization code, consent, device flow, server-side session,
-- pushed authorization request, dan signing key yang dikelola IdentityServer.
-- Client/scope/resource TIDAK disimpan di sini — semuanya in-memory di
-- PortfolioOS.Identity/Config/IdentityServerConfig.cs.
-- -------------------------------------------------------------
CREATE TABLE "PersistedGrants" (
    "Id"           BIGINT GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
    "Key"          VARCHAR(200)    NULL,
    "Type"         VARCHAR(50)     NOT NULL,
    "SubjectId"    VARCHAR(200)    NULL,
    "SessionId"    VARCHAR(100)    NULL,
    "ClientId"     VARCHAR(200)    NOT NULL,
    "Description"  VARCHAR(200)    NULL,
    "CreationTime" TIMESTAMPTZ     NOT NULL,
    "Expiration"   TIMESTAMPTZ     NULL,
    "ConsumedTime" TIMESTAMPTZ     NULL,
    "Data"         VARCHAR(50000)  NOT NULL
);

CREATE TABLE "DeviceCodes" (
    "UserCode"     VARCHAR(200)    NOT NULL PRIMARY KEY,
    "DeviceCode"   VARCHAR(200)    NOT NULL,
    "SubjectId"    VARCHAR(200)    NULL,
    "SessionId"    VARCHAR(100)    NULL,
    "ClientId"     VARCHAR(200)    NOT NULL,
    "Description"  VARCHAR(200)    NULL,
    "CreationTime" TIMESTAMPTZ     NOT NULL,
    "Expiration"   TIMESTAMPTZ     NOT NULL,
    "Data"         VARCHAR(50000)  NOT NULL
);

CREATE TABLE "Keys" (
    "Id"                TEXT         NOT NULL PRIMARY KEY,
    "Version"           INTEGER      NOT NULL,
    "Created"           TIMESTAMPTZ  NOT NULL,
    "Use"               TEXT         NULL,
    "Algorithm"         VARCHAR(100) NOT NULL,
    "IsX509Certificate" BOOLEAN      NOT NULL,
    "DataProtected"     BOOLEAN      NOT NULL,
    "Data"              TEXT         NOT NULL
);

CREATE TABLE "ServerSideSessions" (
    "Id"          BIGINT GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
    "Key"         VARCHAR(100) NOT NULL,
    "Scheme"      VARCHAR(100) NOT NULL,
    "SubjectId"   VARCHAR(100) NOT NULL,
    "SessionId"   VARCHAR(100) NULL,
    "DisplayName" VARCHAR(100) NULL,
    "Created"     TIMESTAMPTZ  NOT NULL,
    "Renewed"     TIMESTAMPTZ  NOT NULL,
    "Expires"     TIMESTAMPTZ  NULL,
    "Data"        TEXT         NOT NULL
);

CREATE TABLE "PushedAuthorizationRequests" (
    "Id"                 BIGINT GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
    "ReferenceValueHash" VARCHAR(64)  NOT NULL,
    "ExpiresAtUtc"       TIMESTAMPTZ  NOT NULL,
    "Parameters"         TEXT         NOT NULL
);

CREATE UNIQUE INDEX "IX_PersistedGrants_Key"                      ON "PersistedGrants"("Key");
CREATE INDEX        "IX_PersistedGrants_Expiration"               ON "PersistedGrants"("Expiration");
CREATE INDEX        "IX_PersistedGrants_ConsumedTime"             ON "PersistedGrants"("ConsumedTime");
CREATE INDEX        "IX_PersistedGrants_SubjectId_ClientId_Type"  ON "PersistedGrants"("SubjectId", "ClientId", "Type");
CREATE INDEX        "IX_PersistedGrants_SubjectId_SessionId_Type" ON "PersistedGrants"("SubjectId", "SessionId", "Type");

CREATE UNIQUE INDEX "IX_DeviceCodes_DeviceCode" ON "DeviceCodes"("DeviceCode");
CREATE INDEX        "IX_DeviceCodes_Expiration" ON "DeviceCodes"("Expiration");

CREATE INDEX        "IX_Keys_Use" ON "Keys"("Use");

CREATE UNIQUE INDEX "IX_ServerSideSessions_Key"         ON "ServerSideSessions"("Key");
CREATE INDEX        "IX_ServerSideSessions_Expires"     ON "ServerSideSessions"("Expires");
CREATE INDEX        "IX_ServerSideSessions_SubjectId"   ON "ServerSideSessions"("SubjectId");
CREATE INDEX        "IX_ServerSideSessions_SessionId"   ON "ServerSideSessions"("SessionId");
CREATE INDEX        "IX_ServerSideSessions_DisplayName" ON "ServerSideSessions"("DisplayName");

CREATE UNIQUE INDEX "IX_PushedAuthorizationRequests_ReferenceValueHash" ON "PushedAuthorizationRequests"("ReferenceValueHash");
CREATE INDEX        "IX_PushedAuthorizationRequests_ExpiresAtUtc"       ON "PushedAuthorizationRequests"("ExpiresAtUtc");


-- -------------------------------------------------------------
-- Seed identity
-- Role (admin, user, viewer) dan user awal dibuat IdentitySeeder saat service
-- start, dibaca dari konfigurasi SeedUsers — bukan lewat SQL. Password di-hash
-- ASP.NET Core Identity, jadi jangan meng-INSERT user manual di sini.
-- -------------------------------------------------------------


-- #############################################################
-- DATABASE ADMIN: portfolioos_admin
--
-- Database terpisah milik microservice PortfolioOS.Admin. Isinya hanya setting yang
-- benar-benar dimiliki service itu: hal-hal yang mengatur tampilan dan perilaku
-- aplikasi web/admin.
--
-- Yang TIDAK ada di sini, dan memang bukan miliknya:
--   * user & role       -> portfolioos_identity (PortfolioOS.Identity)
--   * setting aplikasi  -> portfolioos.app_settings (PortfolioOS.API)
-- Keduanya diakses admin service lewat HTTP dengan meneruskan token pemanggil,
-- tidak pernah disalin ke database ini.
--
-- Jalankan bagian ini setelah connect ke database yang benar:
--   CREATE DATABASE portfolioos_admin;
--   \connect portfolioos_admin
-- #############################################################

-- -------------------------------------------------------------
-- web_settings
-- Nilai (value) dimiliki admin; sisanya metadata milik kode. WebSettingSeeder
-- menyegarkan metadata tiap service start dan menambah key baru, tapi tidak pernah
-- menimpa value yang sudah diubah. Key yang hilang dari kode ikut dihapus.
--
-- value_type menentukan editor di UI sekaligus validasi di server:
--   string | text | bool | int | select   (select memakai kolom options)
-- -------------------------------------------------------------
CREATE TABLE web_settings (
    key           VARCHAR(100)  PRIMARY KEY,
    value         VARCHAR(2000) NOT NULL,
    value_type    VARCHAR(20)   NOT NULL,
    category      VARCHAR(50)   NOT NULL,
    description   VARCHAR(500)  NOT NULL,
    options       VARCHAR(500),                  -- pilihan sah untuk value_type = 'select', dipisah koma
    default_value VARCHAR(2000) NOT NULL,        -- nilai bawaan dari kode, dipakai tombol reset
    sort_order    INTEGER       NOT NULL,
    created_at    TIMESTAMPTZ   NOT NULL,
    updated_at    TIMESTAMPTZ   NOT NULL,
    updated_by    VARCHAR(256)                   -- email admin terakhir; NULL selama masih nilai seed
);

CREATE INDEX ix_web_settings_category ON web_settings(category, sort_order);
