using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Microsoft.ML.Tokenizers;
using PortfolioOS.Application.Chat;
using PortfolioOS.Application.Common.Interfaces;

namespace PortfolioOS.Infrastructure.Services;

/// <summary>
/// Sentence embeddings from <c>intfloat/multilingual-e5-small</c>, run locally through ONNX
/// Runtime. No external service, no API key, works offline - which is the whole reason the
/// assistant can answer questions about someone's finances without shipping them anywhere.
/// </summary>
/// <remarks>
/// Registered as a singleton: the ONNX session costs seconds to build and roughly 700MB of
/// working set, and both it and the tokenizer are safe to share across threads.
/// The model is loaded lazily so a deployment missing the model files still boots - only the
/// chat endpoint fails, rather than the whole API.
/// </remarks>
public sealed class OnnxEmbeddingService : IEmbeddingService, IDisposable
{
    private const int MaxTokens = 512;      // e5-small's position-embedding limit

    // XLM-R's own reserved ids, which the sentencepiece model does not carry.
    private const long BosId = 0;           // <s>
    private const long EosId = 2;           // </s>
    private const int UnkId = 3;            // <unk>

    private readonly object _gate = new();
    private readonly string _dir;
    private readonly ILogger<OnnxEmbeddingService> _logger;

    // volatile: read on the fast path outside the lock, so the write must not be reordered
    // ahead of the object it publishes.
    private volatile Model? _model;

    public int Dimensions => ChatDefaults.EmbeddingDimensions;

    public OnnxEmbeddingService(IConfiguration configuration, ILogger<OnnxEmbeddingService> logger)
    {
        _logger = logger;
        _dir = configuration["Embedding:ModelPath"] ?? "models/e5-small";
    }

    /// <summary>
    /// Loads the model on first use, and lets a failed load be retried later.
    /// </summary>
    /// <remarks>
    /// Deliberately not a <see cref="Lazy{T}"/>: with ExecutionAndPublication it caches the
    /// *exception* as well as the value, so a first attempt made before the model was downloaded
    /// would keep throwing the same "file not found" for the lifetime of the process - the
    /// operator downloads the model, retries, and is told it is still missing. Retrying a
    /// failure is exactly the behaviour wanted here.
    /// </remarks>
    private Model GetModel()
    {
        if (_model is { } ready) return ready;

        lock (_gate)
        {
            return _model ??= Load(_dir);
        }
    }

    public Task<float[]> EmbedAsync(string text, EmbeddingKind kind, CancellationToken ct = default)
        => Task.FromResult(Embed(text, kind));

    /// <summary>
    /// Embeds a batch one text at a time. Deliberately not padded-batched: at ~10ms per text a
    /// full reindex of this corpus is about 1.5 seconds, which is not worth the padding-and-mask
    /// bugs that batching invites.
    /// </summary>
    public Task<IReadOnlyList<float[]>> EmbedManyAsync(
        IReadOnlyList<string> texts, EmbeddingKind kind, CancellationToken ct = default)
    {
        var results = new float[texts.Count][];
        for (int i = 0; i < texts.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            results[i] = Embed(texts[i], kind);
        }

        return Task.FromResult<IReadOnlyList<float[]>>(results);
    }

    private float[] Embed(string text, EmbeddingKind kind)
    {
        var model = GetModel();

        // e5 was trained with these literal prefixes; omitting them costs accuracy silently.
        var prefixed = (kind == EmbeddingKind.Query ? "query: " : "passage: ") + text;
        var ids = Tokenize(model, prefixed);

        int n = ids.Length;
        var feeds = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("input_ids", new DenseTensor<long>(ids, [1, n])),
            NamedOnnxValue.CreateFromTensor("attention_mask",
                new DenseTensor<long>(Enumerable.Repeat(1L, n).ToArray(), [1, n])),
            NamedOnnxValue.CreateFromTensor("token_type_ids",
                new DenseTensor<long>(new long[n], [1, n])),
        };

        using var outputs = model.Session.Run(feeds);
        var hidden = outputs[0].AsTensor<float>();      // [1, n, 384]

        // Mean pooling across tokens, then L2 normalise so a dot product is the cosine.
        var vec = new float[Dimensions];
        for (int t = 0; t < n; t++)
            for (int d = 0; d < Dimensions; d++)
                vec[d] += hidden[0, t, d];

        var norm = 0f;
        for (int d = 0; d < Dimensions; d++)
        {
            vec[d] /= n;
            norm += vec[d] * vec[d];
        }

        norm = MathF.Sqrt(norm);
        if (norm > 0)
            for (int d = 0; d < Dimensions; d++) vec[d] /= norm;

        return vec;
    }

    /// <summary>
    /// Turns text into XLM-RoBERTa input ids.
    /// </summary>
    /// <remarks>
    /// The ids the sentencepiece model reports are NOT the ids this network expects: XLM-R
    /// reserves 0-3 for its own specials and shifts every real piece up by one. Rather than
    /// hard-code that shift, pieces are looked up in tokenizer.json's vocabulary, whose list
    /// index *is* the final id. (The +1 rule was verified to hold across the corpus, so the
    /// lookup is a safety net rather than a guess.)
    /// </remarks>
    private static long[] Tokenize(Model model, string text)
    {
        var pieces = model.Tokenizer.EncodeToTokens(text, out _);

        var ids = new List<long>(pieces.Count + 2) { BosId };
        foreach (var piece in pieces)
        {
            if (piece.Value is "<s>" or "</s>") continue;
            ids.Add(model.Vocab.TryGetValue(piece.Value, out var id) ? id : UnkId);
            if (ids.Count == MaxTokens - 1) break;      // leave room for </s>
        }

        ids.Add(EosId);
        return [.. ids];
    }

    private Model Load(string dir)
    {
        var onnx = Path.Combine(dir, "model.onnx");
        var spm = Path.Combine(dir, "sentencepiece.bpe.model");
        var vocabJson = Path.Combine(dir, "tokenizer.json");

        foreach (var required in new[] { onnx, spm, vocabJson })
        {
            if (!File.Exists(required))
            {
                throw new FileNotFoundException(
                    $"Embedding model file not found: {required}. " +
                    "Run scripts/fetch-embedding-model.sh (or set Embedding:ModelPath) - the chat " +
                    "feature needs it, the rest of the API does not.", required);
            }
        }

        _logger.LogInformation("Loading embedding model from {Dir}", dir);

        using var spmStream = File.OpenRead(spm);
        var tokenizer = SentencePieceTokenizer.Create(spmStream);

        var vocab = new Dictionary<string, int>(250_002, StringComparer.Ordinal);
        using (var doc = JsonDocument.Parse(File.ReadAllBytes(vocabJson)))
        {
            int i = 0;
            foreach (var entry in doc.RootElement.GetProperty("model").GetProperty("vocab").EnumerateArray())
                vocab[entry[0].GetString()!] = i++;
        }

        var session = new InferenceSession(onnx);
        _logger.LogInformation("Embedding model ready: {Vocab} vocab entries, {Dim} dimensions",
            vocab.Count, Dimensions);

        return new Model(session, tokenizer, vocab);
    }

    public void Dispose()
    {
        _model?.Session.Dispose();
    }

    private sealed record Model(
        InferenceSession Session,
        SentencePieceTokenizer Tokenizer,
        Dictionary<string, int> Vocab);
}
