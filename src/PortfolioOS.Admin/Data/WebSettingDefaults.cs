namespace PortfolioOS.Admin.Data;

/// <summary>
/// Daftar setting web yang dikenal sistem beserta metadatanya. Metadata (tipe, kategori,
/// deskripsi, pilihan) dimiliki kode, sedangkan nilainya dimiliki admin — seeder
/// menyegarkan metadata tiap start tapi tidak pernah menimpa nilai yang sudah diubah.
/// </summary>
public static class WebSettingDefaults
{
    public static class Categories
    {
        public const string General = "Umum";
        public const string Appearance = "Tampilan";
        public const string Features = "Fitur";
        public const string Security = "Keamanan";
    }

    public static readonly IReadOnlyList<WebSetting> All =
    [
        // --- Umum ---
        Def("web.app_name", "PortfolioOS", WebSettingTypes.String, Categories.General, 10,
            "Nama aplikasi yang tampil di judul halaman dan app bar"),

        Def("web.support_email", "support@portfolioos.local", WebSettingTypes.String, Categories.General, 20,
            "Alamat email yang ditampilkan saat pengguna butuh bantuan"),

        Def("web.maintenance_mode", "false", WebSettingTypes.Bool, Categories.General, 30,
            "Menutup aplikasi web untuk pengguna non-admin"),

        Def("web.maintenance_message", "Aplikasi sedang dalam pemeliharaan. Silakan coba beberapa saat lagi.",
            WebSettingTypes.Text, Categories.General, 40,
            "Pesan yang ditampilkan ketika mode pemeliharaan aktif"),

        // --- Tampilan ---
        Def("web.default_theme", "dark", WebSettingTypes.Select, Categories.Appearance, 10,
            "Tema bawaan aplikasi web", options: "dark,light"),

        Def("web.default_currency", "IDR", WebSettingTypes.Select, Categories.Appearance, 20,
            "Mata uang yang ditampilkan pertama kali sebelum pengguna menggantinya", options: "IDR,USD"),

        Def("web.items_per_page", "25", WebSettingTypes.Int, Categories.Appearance, 30,
            "Jumlah baris per halaman pada tabel"),

        Def("web.privacy_mode_default", "false", WebSettingTypes.Bool, Categories.Appearance, 40,
            "Menyembunyikan nominal secara default saat aplikasi dibuka"),

        // --- Fitur ---
        Def("web.feature_market_page", "true", WebSettingTypes.Bool, Categories.Features, 10,
            "Menampilkan halaman Market (harga pasar real-time)"),

        Def("web.feature_ledger_page", "true", WebSettingTypes.Bool, Categories.Features, 20,
            "Menampilkan halaman Ledger (pembukuan double-entry)"),

        Def("web.feature_ocr_upload", "true", WebSettingTypes.Bool, Categories.Features, 30,
            "Mengizinkan input transaksi lewat pemindaian dokumen di aplikasi mobile"),

        // --- Keamanan ---
        Def("web.session_timeout_minutes", "60", WebSettingTypes.Int, Categories.Security, 10,
            "Lama sesi web sebelum pengguna diminta login ulang"),

        Def("web.allow_self_registration", "false", WebSettingTypes.Bool, Categories.Security, 20,
            "Mengizinkan pengguna mendaftar sendiri tanpa dibuatkan admin"),
    ];

    private static WebSetting Def(
        string key,
        string value,
        string valueType,
        string category,
        int sortOrder,
        string description,
        string? options = null) => new()
        {
            Key = key,
            Value = value,
            DefaultValue = value,
            ValueType = valueType,
            Category = category,
            SortOrder = sortOrder,
            Description = description,
            Options = options,
        };
}
