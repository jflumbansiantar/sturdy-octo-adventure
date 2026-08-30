using System.Globalization;

namespace PortfolioOS.Mobile.Converters;

public class StringNotEmptyConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is string s && !string.IsNullOrEmpty(s);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class InverseBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b && !b;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b && !b;
}

public class GainColorConverter : IValueConverter
{
    private static readonly Color Positive = Color.FromArgb("#4CAF50");
    private static readonly Color Negative = Color.FromArgb("#F44336");

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is decimal d) return d >= 0 ? Positive : Negative;
        if (value is double dbl) return dbl >= 0 ? Positive : Negative;
        return Colors.White;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class DebtStatusColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is string s && s == "Active"
            ? Color.FromArgb("#FFC107")
            : Color.FromArgb("#4CAF50");

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class DebtProgressConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // Shows a fractional progress; since we don't have the original balance,
        // just show a symbolic fill based on paid amount normalised to $10k
        if (value is decimal paid)
            return (double)Math.Min(paid / 10_000m, 1m);
        return 0d;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class ZeroToTrueConverter : IValueConverter
{
    // true kalau koleksi kosong - dipakai untuk menampilkan pesan "belum ada data"
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is int i && i == 0;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// Formats an amount with the right symbol for its currency.
/// Accepts either a currency code ("USD"/"IDR") or a market code ("US"/"ID");
/// anything else - including null, as on non-market transactions - is treated
/// as rupiah, which is this app's default.
/// </summary>
public class MoneyConverter : IMultiValueConverter
{
    private static readonly CultureInfo Usd = CultureInfo.GetCultureInfo("en-US");
    private static readonly CultureInfo Idr = CultureInfo.GetCultureInfo("id-ID");

    public object Convert(object[]? values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values is null || values.Length == 0) return string.Empty;

        decimal amount = values[0] switch
        {
            decimal d => d,
            double dbl => (decimal)dbl,
            int i => i,
            _ => 0m
        };

        var code = values.Length > 1 ? values[1] as string : null;
        return Format(amount, code);
    }

    public static string Format(decimal amount, string? code)
    {
        var isUsd = string.Equals(code, "USD", StringComparison.OrdinalIgnoreCase)
                 || string.Equals(code, "US", StringComparison.OrdinalIgnoreCase);

        // rupiah has no meaningful minor unit, so no decimals
        return isUsd
            ? "$" + amount.ToString("N2", Usd)
            : "Rp " + amount.ToString("N0", Idr);
    }

    public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

