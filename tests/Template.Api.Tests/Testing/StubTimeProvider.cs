namespace Template.Api.Tests.Testing;

/// <summary>Deterministic <see cref="TimeProvider"/> for tests — no wall-clock dependence, no sleeps.</summary>
public sealed class StubTimeProvider(DateTimeOffset start) : TimeProvider
{
    public DateTimeOffset UtcNow { get; private set; } = start;

    public override DateTimeOffset GetUtcNow() => UtcNow;

    public void Advance(TimeSpan delta) => UtcNow += delta;
}
