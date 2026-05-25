using System.Collections.Generic;

namespace MediateX.Tests.TestFixtures;

/// <summary>
/// Shared logger for capturing messages in tests.
/// Can be used across all test files without namespace conflicts.
/// </summary>
public class TestLogger
{
    public IList<string> Messages { get; } = [];

    public void Log(string message) => Messages.Add(message);

    public void Clear() => Messages.Clear();
}

/// <summary>
/// Base response type that can be extended or used directly.
/// </summary>
public class TestResponse
{
    public string? Message { get; set; }
}

/// <summary>
/// Extended response type for tests that need a derived type.
/// </summary>
public class ExtendedTestResponse : TestResponse
{
}
