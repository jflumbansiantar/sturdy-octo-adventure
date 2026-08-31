using System.Text.RegularExpressions;
using PortfolioOS.Domain.Enums;

namespace PortfolioOS.Shared.Scanning.Parsers;

/// <summary>
/// Konfirmasi order dari Stockbit / Ajaib / IPOT. The most demanding document of the set:
/// CreateTransactionValidator rejects a Stock transaction unless Market, Shares AND Price are
/// all present and positive, and CreateTransactionHandler additionally rewrites the matching
/// holding's Shares and AvgCost. A wrong number here corrupts the portfolio, not just the
/// ledger - so everything below stays a guess for the user to confirm.
/// </summary>
public partial class BrokerTradeParser : IDocumentParser
{
    public DocumentKind Kind => DocumentKind.BrokerTrade;

    [GeneratedRegex(@"(?<n>[\d.,]+)\s*(?<unit>lot|lembar|lbr|shares?|unit)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Quantity();

    [GeneratedRegex(@"\b(?<t>[A-Z]{2,5})\b", RegexOptions.CultureInvariant)]
    private static partial Regex UppercaseToken();

    [GeneratedRegex(@"\b(jual|sell)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SellMarker();

    [GeneratedRegex(@"\b(beli|buy)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BuyMarker();

    /// <summary>
    /// Uppercase words that appear on every order slip and are not the ticker. Without this
    /// list the first all-caps word on the page - usually "BELI" - becomes the stock code.
    /// </summary>
    private static readonly HashSet<string> NotTickers = new(StringComparer.Ordinal)
    {
        "BELI", "JUAL", "BUY", "SELL", "LOT", "IDR", "USD", "RP", "TOTAL", "HARGA", "SAHAM",
        "NAMA", "KODE", "FEE", "PPN", "NET", "TGL", "JAM", "WIB", "PT", "TBK", "NO", "REF",
        "QTY", "AVG", "MAX", "MIN", "ALL", "DONE", "OK", "ORDER", "STATUS", "MATCH", "AMOUNT",
        "PRICE", "VALUE", "LEVY", "BROKER", "BEI", "IDX", "KSEI", "RDN", "ARA", "ARB"
    };

    public TransactionDraft Parse(OcrText ocr)
    {
        var warnings = new List<string>();
        var text = ocr.AllText;

        var type = ResolveType(text, warnings);
        var ticker = FindTicker(ocr, warnings);

        // IDX codes are exactly four letters; anything shorter or longer is a US listing.
        var market = ticker.HasValue
            ? new FieldGuess<Market>(ticker.Value!.Length == 4 ? Market.ID : Market.US, Confidence.Low,
                "ditebak dari panjang kode saham")
            : FieldGuess<Market>.Missing;

        var shares = FindShares(ocr, market.Value, warnings);
        var price = AmountPicker.ForLabel(ocr, @"\b(harga|price|avg\s*price|harga\s*rata|rata-?rata)\b");
        var total = AmountPicker.ForLabel(ocr, @"\b(total|nilai|net\s*amount|jumlah|gross\s*amount)\b", fromBottom: true);

        if (!price.HasValue) warnings.Add("Harga per lembar tidak terbaca. Wajib diisi manual.");
        if (!shares.HasValue) warnings.Add("Jumlah lembar tidak terbaca. Wajib diisi manual.");

        if (!total.HasValue && shares.HasValue && price.HasValue)
        {
            total = new FieldGuess<decimal>(shares.Value * price.Value, Confidence.Low, "dihitung dari lembar x harga");
            warnings.Add("Total dihitung dari lembar x harga, belum termasuk fee broker.");
        }
        else if (total.HasValue && shares.HasValue && price.HasValue)
        {
            // Broker fees make an exact match impossible, but an order of magnitude apart
            // means one of the three numbers was misread.
            var expected = shares.Value * price.Value;
            if (expected > 0 && Math.Abs(total.Value - expected) / expected > 0.2m)
                warnings.Add($"Total ({total.Value:N0}) tidak cocok dengan lembar x harga ({expected:N0}). Mohon diperiksa.");
        }

        if (!total.HasValue) total = AmountPicker.FindTotal(ocr);

        var date = IndoDateParser.Find(ocr);
        if (!date.HasValue) warnings.Add("Tanggal tidak terbaca - dipakai tanggal hari ini.");

        return new TransactionDraft
        {
            Kind = Kind,
            Category = new FieldGuess<TransactionCategory>(TransactionCategory.Stock, Confidence.High),
            Type = type,
            // CreateTransactionHandler matches the holding by Transaction.Name, so the ticker
            // - not the broker or the company name - has to be what goes in this field.
            Name = ticker,
            Date = date,
            Total = total,
            Market = market,
            Shares = shares,
            Price = price,
            RawText = ocr.AllText,
            Warnings = warnings
        };
    }

    private static FieldGuess<string> ResolveType(string text, List<string> warnings)
    {
        var sells = SellMarker().Matches(text).Count;
        var buys = BuyMarker().Matches(text).Count;

        if (sells > buys) return new FieldGuess<string>("SELL", Confidence.High);
        if (buys > sells) return new FieldGuess<string>("BUY", Confidence.High);

        warnings.Add("Tidak jelas ini transaksi beli atau jual - default BUY. Mohon diperiksa.");
        return new FieldGuess<string>("BUY", Confidence.Low);
    }

    private static FieldGuess<string> FindTicker(OcrText ocr, List<string> warnings)
    {
        // A labelled code is unambiguous, so it is worth looking for first.
        var labelled = MerchantName.AfterLabel(ocr, @"\b(kode\s*saham|kode|ticker|stock\s*code|symbol)\b");
        if (labelled.HasValue)
        {
            var token = UppercaseToken().Match(labelled.Value!.ToUpperInvariant());
            if (token.Success && !NotTickers.Contains(token.Groups["t"].Value))
                return new FieldGuess<string>(token.Groups["t"].Value, Confidence.High, labelled.Evidence);
        }

        foreach (var line in ocr.InReadingOrder)
        {
            foreach (Match m in UppercaseToken().Matches(line.Text))
            {
                var candidate = m.Groups["t"].Value;
                if (NotTickers.Contains(candidate)) continue;

                // Four letters is the IDX format, so it is the strongest unlabelled signal.
                var confidence = candidate.Length == 4 ? Confidence.Medium : Confidence.Low;
                return new FieldGuess<string>(candidate, confidence, line.Text.Trim());
            }
        }

        warnings.Add("Kode saham tidak terbaca. Wajib diisi manual.");
        return FieldGuess<string>.Missing;
    }

    /// <summary>
    /// Converts the quantity to individual shares, which is what Holding.Shares counts.
    /// One IDX lot is 100 shares - reading "10 Lot" as 10 shares understates the position
    /// by two orders of magnitude.
    /// </summary>
    private static FieldGuess<decimal> FindShares(OcrText ocr, Market market, List<string> warnings)
    {
        foreach (var line in ocr.InReadingOrder)
        {
            var m = Quantity().Match(line.Text);
            if (!m.Success) continue;
            if (!MoneyParser.TryParse(m.Groups["n"].Value, out var quantity) || quantity <= 0) continue;

            var isLot = m.Groups["unit"].Value.Equals("lot", StringComparison.OrdinalIgnoreCase);
            if (!isLot) return new FieldGuess<decimal>(quantity, Confidence.High, line.Text.Trim());

            if (market == Market.US)
                warnings.Add("Satuan lot pada saham US tidak lazim - jumlah lembar mohon diperiksa.");

            return new FieldGuess<decimal>(quantity * 100m, Confidence.High, $"{line.Text.Trim()} (1 lot = 100 lembar)");
        }

        var labelled = AmountPicker.ForLabel(ocr, @"\b(volume|jumlah\s*lot|lot|qty|kuantitas)\b");
        return labelled.HasValue
            ? new FieldGuess<decimal>(labelled.Value * 100m, Confidence.Low, labelled.Evidence)
            : FieldGuess<decimal>.Missing;
    }
}
