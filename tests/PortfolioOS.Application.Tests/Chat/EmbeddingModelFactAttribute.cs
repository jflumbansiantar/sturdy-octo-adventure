namespace PortfolioOS.Application.Tests.Chat;

/// <summary>
/// A fact that runs only when the local embedding model has been downloaded.
/// </summary>
/// <remarks>
/// The model is a ~490MB optional artefact, so a plain [Fact] would turn a fresh clone's test
/// run red for a missing file rather than a broken behaviour. Skipping keeps the suite honest:
/// it is either measured or explicitly reported as not measured, never silently passed.
/// </remarks>
public sealed class EmbeddingModelFactAttribute : FactAttribute
{
    public EmbeddingModelFactAttribute()
    {
        if (ModelPath.Resolve() is null)
        {
            Skip = "Embedding model not present - run scripts/fetch-embedding-model.sh to include " +
                   "the retrieval evaluation in this run.";
        }
    }
}

public static class ModelPath
{
    /// <summary>Walks up from the test binaries to the repo's model directory.</summary>
    public static string? Resolve()
    {
        var fromEnv = Environment.GetEnvironmentVariable("PORTFOLIOOS_EMBEDDING_MODEL");
        if (!string.IsNullOrWhiteSpace(fromEnv) && IsComplete(fromEnv)) return fromEnv;

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "src", "PortfolioOS.API", "models", "e5-small");
            if (IsComplete(candidate)) return candidate;
            dir = dir.Parent;
        }

        return null;
    }

    private static bool IsComplete(string dir) =>
        File.Exists(Path.Combine(dir, "model.onnx")) &&
        File.Exists(Path.Combine(dir, "sentencepiece.bpe.model")) &&
        File.Exists(Path.Combine(dir, "tokenizer.json"));
}
