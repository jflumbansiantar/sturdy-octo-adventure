using FluentAssertions;
using PortfolioOS.Application.Savings;

namespace PortfolioOS.Application.Tests;

public class ExpenseClassifierTests
{
    [Theory]
    [InlineData("Groceries", "Belanja Bulanan")]
    [InlineData("Transport", "Bensin & Parkir")]
    [InlineData("Utilities", "Tagihan Listrik")]
    [InlineData("Utilities", "Tagihan Air")]
    [InlineData("Insurance", "Premi BPJS")]
    [InlineData("Rent", "Sewa Kontrakan")]
    // Nothing matched: counted as a need, which understates what is available to save.
    [InlineData("Misc", "Keperluan lain-lain")]
    public void EssentialSpending_IsNotDiscretionary(string type, string name) =>
        ExpenseClassifier.IsDiscretionary(type, name).Should().BeFalse();

    [Theory]
    [InlineData("Food", "Makan & Nongkrong")]
    [InlineData("Subscription", "Netflix + Spotify")]
    [InlineData("Travel", "Liburan ke Bali")]
    [InlineData("Shopping", "Sepatu baru")]
    // "kos" is a word here, not the first three letters of kosmetik.
    [InlineData("Shopping", "Kosmetik")]
    // The airline is not the water bill: "air" alone is not a utilities keyword.
    [InlineData("Travel", "Liburan Air Asia")]
    public void DiscretionarySpending_IsRecognised(string type, string name) =>
        ExpenseClassifier.IsDiscretionary(type, name).Should().BeTrue();

    [Fact]
    public void EssentialKeywordWins_WhenBothAppear()
    {
        // "Belanja bulanan" is groceries even though "belanja" also reads as shopping.
        ExpenseClassifier.IsDiscretionary("Groceries", "Belanja bulanan + kopi").Should().BeFalse();
    }
}
