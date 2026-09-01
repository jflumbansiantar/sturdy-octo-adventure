-- Dijalankan sekali oleh image postgres saat volume data pertama kali dibuat
-- (docker-entrypoint-initdb.d). Skema tabelnya sendiri dibuat oleh EF Core
-- migrations milik PortfolioOS.Admin saat service pertama kali start.
SELECT 'CREATE DATABASE portfolioos_admin'
WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'portfolioos_admin')
\gexec
