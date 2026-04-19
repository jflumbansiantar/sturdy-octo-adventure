-- =============================================================
-- PortfolioOS — PostgreSQL Schema
-- Target: .NET Rebuild (EF Core + CQRS)
-- =============================================================

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
    updated_at  TIMESTAMPTZ     NOT NULL DEFAULT NOW(),

    CONSTRAINT uq_holdings_ticker UNIQUE (ticker)
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
    account_id  VARCHAR(20)     NOT NULL REFERENCES ledger_accounts(id),
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
CREATE INDEX idx_holdings_market   ON holdings(market);
CREATE INDEX idx_holdings_type     ON holdings(type);

-- transactions
CREATE INDEX idx_transactions_date      ON transactions(date DESC);
CREATE INDEX idx_transactions_category  ON transactions(category);
CREATE INDEX idx_transactions_name      ON transactions(name);
CREATE INDEX idx_transactions_market    ON transactions(market);

-- journal_lines
CREATE INDEX idx_journal_lines_entry_id   ON journal_lines(entry_id);
CREATE INDEX idx_journal_lines_account_id ON journal_lines(account_id);

-- journal_entries
CREATE INDEX idx_journal_entries_date ON journal_entries(date DESC);

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
-- VIEWS (computed fields — menggantikan logic di service layer)
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
