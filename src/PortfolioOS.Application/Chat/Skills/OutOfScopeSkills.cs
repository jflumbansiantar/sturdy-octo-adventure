using PortfolioOS.Application.Chat.Intents;

namespace PortfolioOS.Application.Chat.Skills;

/// <summary>
/// Skills that decline, and say why.
/// </summary>
/// <remarks>
/// These exist because a similarity threshold cannot tell "berapa nilai portofolio saya" from
/// "apakah portofolio saya akan naik tahun depan" - both are squarely about the portfolio, and
/// the second scored 0.91 against the portfolio phrasings. The difference is the kind of request,
/// not the topic, so the only way to catch it is to give that kind of request its own phrases.
/// <para>
/// Declining precisely is also a better answer than the generic fallback: the user learns what
/// this assistant does not do, and is pointed at what it can do instead.
/// </para>
/// </remarks>
internal abstract class OutOfScopeSkill(string skillId, string message, string[] instead) : IChatSkill
{
    public string SkillId => skillId;

    public Task<ChatAnswer> ExecuteAsync(ChatSkillContext context, CancellationToken ct = default) =>
        Task.FromResult(new ChatAnswer(message, Suggestions: instead));
}

internal sealed class ForecastSkill() : OutOfScopeSkill(
    SkillIds.MetaForecast,
    "Saya hanya membaca apa yang sudah tercatat, jadi saya tidak bisa memperkirakan harga atau " +
    "nilai di masa depan — dan menebaknya akan terdengar meyakinkan padahal tidak berdasar. " +
    "Yang bisa saya tunjukkan adalah posisi Anda sekarang dan bagaimana perkembangannya sejauh ini.",
    [
        "Berapa total nilai portofolio saya?",
        "Saham apa yang paling banyak bergerak hari ini?",
        "Berapa kekayaan bersih saya?",
    ]);

internal sealed class AdviceSkill() : OutOfScopeSkill(
    SkillIds.MetaAdvice,
    "Saya tidak memberi rekomendasi atau nasihat investasi. Tapi saya bisa menyajikan angka yang " +
    "biasanya dipakai untuk memutuskan sendiri — misalnya utang mana yang bunganya paling mahal, " +
    "atau ke mana uang Anda paling banyak keluar.",
    [
        "Utang mana yang bunganya paling tinggi?",
        "Pengeluaran saya paling besar di kategori apa?",
        "Bagaimana komposisi portofolio saya?",
    ]);

internal sealed class MutationSkill() : OutOfScopeSkill(
    SkillIds.MetaMutation,
    "Saya hanya bisa membaca data, tidak mengubahnya. Untuk menambah, mengubah, atau menghapus, " +
    "gunakan halaman Transactions, Holdings, atau Debts — supaya perubahannya selalu Anda yang " +
    "konfirmasi sendiri.",
    [
        "Apa saja transaksi terakhir saya?",
        "Berapa total utang saya?",
    ]);

internal sealed class SmallTalkSkill() : OutOfScopeSkill(
    SkillIds.MetaSmallTalk,
    "Halo! Saya asisten PortfolioOS. Saya menjawab pertanyaan tentang portofolio, transaksi, " +
    "utang, dan buku besar Anda — semuanya dari data yang sudah tersimpan.",
    [
        "Kamu bisa menjawab apa saja?",
        "Berapa total nilai portofolio saya?",
        "Berapa pengeluaran saya bulan lalu?",
    ]);

internal sealed class GeneralKnowledgeSkill() : OutOfScopeSkill(
    SkillIds.MetaGeneralKnowledge,
    "Saya hanya menjawab tentang data PortfolioOS Anda, bukan pengetahuan umum — untuk itu " +
    "mesin pencari jauh lebih baik daripada saya.",
    [
        "Kamu bisa menjawab apa saja?",
        "Berapa kekayaan bersih saya?",
    ]);
