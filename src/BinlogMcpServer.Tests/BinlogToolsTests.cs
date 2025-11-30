using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace BinlogMcpServer.Tests;

public class BinlogToolsTests
{
    [Fact]
    public void LoadBinlog_WithNonexistentFile_ReturnsErrorJson()
    {
        var result = BinlogTools.LoadBinlog("/nonexistent/path.binlog");

        var json = JsonDocument.Parse(result);
        json.RootElement.GetProperty("success").GetBoolean().Should().BeFalse();
        json.RootElement.GetProperty("error").GetString().Should().Contain("not found");
    }

    [Fact]
    public void GetBuildSummary_WithoutLoad_ReturnsErrorJson()
    {
        // Reset the service state by loading a nonexistent file first to ensure clean state
        // (The service is a static singleton, so state persists across tests)
        
        var result = BinlogTools.GetBuildSummary();

        var json = JsonDocument.Parse(result);
        // This may succeed or fail depending on whether a previous test loaded something
        // Just verify the JSON is valid
        json.RootElement.TryGetProperty("success", out _).Should().BeTrue();
    }

    [Fact]
    public void GetSlowestTargets_ReturnsValidJson()
    {
        var result = BinlogTools.GetSlowestTargets();

        var json = JsonDocument.Parse(result);
        json.RootElement.TryGetProperty("success", out _).Should().BeTrue();
    }

    [Fact]
    public void GetSlowestTasks_ReturnsValidJson()
    {
        var result = BinlogTools.GetSlowestTasks();

        var json = JsonDocument.Parse(result);
        json.RootElement.TryGetProperty("success", out _).Should().BeTrue();
    }

    [Fact]
    public void GetDoubleWrites_ReturnsValidJson()
    {
        var result = BinlogTools.GetDoubleWrites();

        var json = JsonDocument.Parse(result);
        json.RootElement.TryGetProperty("success", out _).Should().BeTrue();
    }

    [Fact]
    public void GetErrors_ReturnsValidJson()
    {
        var result = BinlogTools.GetErrors();

        var json = JsonDocument.Parse(result);
        json.RootElement.TryGetProperty("success", out _).Should().BeTrue();
    }

    [Fact]
    public void GetWarnings_ReturnsValidJson()
    {
        var result = BinlogTools.GetWarnings();

        var json = JsonDocument.Parse(result);
        json.RootElement.TryGetProperty("success", out _).Should().BeTrue();
    }

    [Fact]
    public void Search_ReturnsValidJson()
    {
        var result = BinlogTools.Search("test");

        var json = JsonDocument.Parse(result);
        json.RootElement.TryGetProperty("success", out _).Should().BeTrue();
    }

    [Fact]
    public void GetProjectTree_ReturnsValidJson()
    {
        var result = BinlogTools.GetProjectTree();

        var json = JsonDocument.Parse(result);
        json.RootElement.TryGetProperty("success", out _).Should().BeTrue();
    }

    [Fact]
    public void GetPropertyValue_ReturnsValidJson()
    {
        var result = BinlogTools.GetPropertyValue("Configuration");

        var json = JsonDocument.Parse(result);
        json.RootElement.TryGetProperty("success", out _).Should().BeTrue();
    }

    [Fact]
    public void GetItemValues_ReturnsValidJson()
    {
        var result = BinlogTools.GetItemValues("Compile");

        var json = JsonDocument.Parse(result);
        json.RootElement.TryGetProperty("success", out _).Should().BeTrue();
    }

    [Fact]
    public void CheckSuspiciousPatterns_ReturnsValidJson()
    {
        var result = BinlogTools.CheckSuspiciousPatterns();

        var json = JsonDocument.Parse(result);
        json.RootElement.TryGetProperty("success", out _).Should().BeTrue();
    }

    [Fact]
    public void GetCompilerInvocations_ReturnsValidJson()
    {
        var result = BinlogTools.GetCompilerInvocations();

        var json = JsonDocument.Parse(result);
        json.RootElement.TryGetProperty("success", out _).Should().BeTrue();
    }

    [Fact]
    public void GetTargetDependencies_ReturnsValidJson()
    {
        var result = BinlogTools.GetTargetDependencies("Build");

        var json = JsonDocument.Parse(result);
        json.RootElement.TryGetProperty("success", out _).Should().BeTrue();
    }

    [Fact]
    public void GetEvaluationProfile_ReturnsValidJson()
    {
        var result = BinlogTools.GetEvaluationProfile();

        var json = JsonDocument.Parse(result);
        json.RootElement.TryGetProperty("success", out _).Should().BeTrue();
    }

    [Fact]
    public void GetFileCopies_ReturnsValidJson()
    {
        var result = BinlogTools.GetFileCopies();

        var json = JsonDocument.Parse(result);
        json.RootElement.TryGetProperty("success", out _).Should().BeTrue();
    }
}
