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
