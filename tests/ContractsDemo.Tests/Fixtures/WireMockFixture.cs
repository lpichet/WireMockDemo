using WireMock.Server;

namespace ContractsDemo.Tests.Fixtures;

/// <summary>
/// A reusable WireMock fixture that can be shared across test classes.
/// This demonstrates how WireMock can be configured once and reused.
/// </summary>
public class WireMockFixture : IDisposable
{
    public WireMockServer Server { get; }

    public string BaseUrl => Server.Url!;

    public WireMockFixture()
    {
        Server = WireMockServer.Start();
    }

    public void Reset()
    {
        Server.Reset();
    }

    public void Dispose()
    {
        Server.Stop();
        Server.Dispose();
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Collection fixture for sharing WireMock across tests
/// </summary>
[CollectionDefinition("WireMock")]
public class WireMockCollection : ICollectionFixture<WireMockFixture>
{
}
