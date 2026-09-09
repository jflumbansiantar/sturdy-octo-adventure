using System.Text.RegularExpressions;

namespace PortfolioOS.Application.Savings;

/// <summary>
/// Sorts a recorded expense into the two buckets the 50/30/20 rule needs: kebutuhan (needs) and
/// keinginan (wants).
/// </summary>
/// <remarks>
/// Transactions carry a free-text <c>Type</c> and <c>Name</c> rather than a needs/wants flag, so
/// the split is keyword-based over both fields. Two decisions keep it honest:
/// <list type="bullet">
/// <item>Essentials are checked first, so "Belanja Bulanan" stays a need even though "belanja"
/// also appears in discretionary shopping.</item>
/// <item>Anything unrecognised counts as a need. That understates the amount available to save,
/// which is the direction an unrecognised expense should push a suggestion.</item>
/// </list>
/// Keywords are matched on word boundaries: plain substring matching turns "kosmetik" into "kos".
/// Boundaries alone are not enough, though - a bare "air" claims "Liburan Air Asia" for the
/// utilities bucket - so the water bill is keyed on the phrases it actually shows up in. Being
/// specific costs nothing: whatever stays unmatched falls through to needs anyway.
/// </remarks>
public static class ExpenseClassifier
{
    private static readonly Regex Essential = Build([
        "groceries", "belanja bulanan", "sembako", "pasar", "dapur",
        "transport", "transportasi", "bensin", "bbm", "fuel", "parkir", "tol", "ojek", "commute",
        "utilities", "utilitas", "listrik", "pln", "pdam", "tagihan air", "air bersih", "gas",
        "internet", "wifi", "pulsa", "kuota", "telepon",
        "rent", "sewa", "kontrakan", "kos", "kost", "iuran", "keamanan", "kebersihan",
        "insurance", "asuransi", "bpjs", "health", "kesehatan", "obat", "dokter", "rumah sakit",
        "education", "sekolah", "spp", "kuliah", "les", "kursus", "daycare",
        "pajak", "tax", "zakat", "infak", "sedekah",
        "cicilan", "angsuran", "kredit", "debt payment",
    ]);

    private static readonly Regex Discretionary = Build([
        "food", "dining", "restaurant", "resto", "cafe", "kafe", "kopi", "coffee",
        "makan", "jajan", "nongkrong", "snack", "delivery", "gofood", "grabfood",
        "entertainment", "hiburan", "bioskop", "movie", "konser", "game", "gaming",
        "subscription", "langganan", "streaming", "netflix", "spotify", "disney", "youtube",
        "travel", "liburan", "vacation", "hotel", "staycation", "wisata",
        "shopping", "belanja online", "fashion", "baju", "sepatu", "tas", "gadget",
        "hobi", "hobby", "salon", "spa", "gym", "skincare", "kosmetik", "rokok",
    ]);

    /// <summary>True when the expense is a want — the only spending the advisor treats as cuttable.</summary>
    public static bool IsDiscretionary(string? type, string? name)
    {
        var text = $"{type} {name}";
        if (Essential.IsMatch(text)) return false;
        return Discretionary.IsMatch(text);
    }

    private static Regex Build(string[] keywords) => new(
        @"\b(" + string.Join("|", keywords.Select(Regex.Escape)) + @")\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
}
