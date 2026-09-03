namespace PortfolioOS.Infrastructure.Demo;

/// <summary>
/// Which sandbox the current request belongs to, if any. Scoped: one instance per request.
/// </summary>
/// <remarks>
/// Read by the <see cref="ApplicationDbContext"/> registration when it builds the connection
/// string, which is why it has to be bound before anything resolves a DbContext - the API's
/// demo middleware does that, immediately after authentication.
/// <para>
/// Left unbound everywhere else: the owner's own requests, and background services that have
/// no HTTP context at all, both fall through to <c>public</c>.
/// </para>
/// </remarks>
public sealed class DemoSessionContext
{
    public Guid? SessionId { get; private set; }

    public string? Schema { get; private set; }

    public bool IsDemo => Schema is not null;

    public void Bind(Guid sessionId, string schema)
    {
        if (!DemoSchema.IsValid(schema))
            throw new ArgumentException($"Not a demo schema name: '{schema}'", nameof(schema));

        SessionId = sessionId;
        Schema = schema;
    }
}
