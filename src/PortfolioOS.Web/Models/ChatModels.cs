namespace PortfolioOS.Web.Models;

public record ChatSourceModel(string Label, string? SourceId);

public record ChatTableModel(List<string> Columns, List<List<string>> Rows);

/// <summary>
/// Mirrors the API's ChatAnswer. Duplicated here rather than shared, matching how every other
/// view model in this client is defined.
/// </summary>
public record ChatAnswerModel(
    string Text,
    string? SkillId,
    ChatTableModel? Table,
    List<ChatSourceModel> Sources,
    double Confidence,
    List<string> Suggestions);

/// <summary>One turn on screen. Questions and answers share a list so ordering is trivial.</summary>
public record ChatMessage(bool IsUser, string Text, ChatAnswerModel? Answer = null);
