using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace BinlogMcpServer.Tests;

public class BinlogAnalysisServiceTests
{
    private readonly BinlogAnalysisService _service;

    public BinlogAnalysisServiceTests()
    {
        _service = new BinlogAnalysisService();
    }

    [Fact]
    public void IsBuildLoaded_WhenNoLoad_ReturnsFalse()
    {
        _service.IsBuildLoaded.Should().BeFalse();
    }

    [Fact]
    public void LoadBinlog_WithNonexistentFile_ReturnsError()
    {
        var result = _service.LoadBinlog("/nonexistent/path.binlog");

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public void GetBuildSummary_WhenNoLoad_ReturnsNull()
    {
        var summary = _service.GetBuildSummary();

        summary.Should().BeNull();
    }

    [Fact]
    public void GetSlowestTargets_WhenNoLoad_ReturnsEmptyList()
    {
        var targets = _service.GetSlowestTargets();

        targets.Should().BeEmpty();
    }

    [Fact]
    public void GetSlowestTasks_WhenNoLoad_ReturnsEmptyList()
    {
        var tasks = _service.GetSlowestTasks();

        tasks.Should().BeEmpty();
    }

    [Fact]
    public void GetDoubleWrites_WhenNoLoad_ReturnsEmptyList()
    {
        var doubleWrites = _service.GetDoubleWrites();

        doubleWrites.Should().BeEmpty();
    }

    [Fact]
    public void GetErrors_WhenNoLoad_ReturnsEmptyList()
    {
        var errors = _service.GetErrors();

        errors.Should().BeEmpty();
    }

    [Fact]
    public void GetWarnings_WhenNoLoad_ReturnsEmptyList()
    {
        var warnings = _service.GetWarnings();

        warnings.Should().BeEmpty();
    }

    [Fact]
    public void Search_WhenNoLoad_ReturnsEmptyList()
    {
        var results = _service.Search("test");

        results.Should().BeEmpty();
    }

    [Fact]
    public void GetProjectTree_WhenNoLoad_ReturnsNull()
    {
        var tree = _service.GetProjectTree();

        tree.Should().BeNull();
    }

    [Fact]
    public void GetPropertyValues_WhenNoLoad_ReturnsEmptyList()
    {
        var values = _service.GetPropertyValues("TestProperty");

        values.Should().BeEmpty();
    }

    [Fact]
    public void GetItemValues_WhenNoLoad_ReturnsEmptyList()
    {
        var items = _service.GetItemValues("Compile");

        items.Should().BeEmpty();
    }

    [Fact]
    public void GetCompilerInvocations_WhenNoLoad_ReturnsEmptyList()
    {
        var invocations = _service.GetCompilerInvocations();

        invocations.Should().BeEmpty();
    }

    [Fact]
    public void GetTargetDependencies_WhenNoLoad_ReturnsNull()
    {
        var deps = _service.GetTargetDependencies("Build");

        deps.Should().BeNull();
    }

    [Fact]
    public void GetEvaluationProfile_WhenNoLoad_ReturnsEmptyList()
    {
        var profiles = _service.GetEvaluationProfile();

        profiles.Should().BeEmpty();
    }

    [Fact]
    public void GetFileCopies_WhenNoLoad_ReturnsEmptyList()
    {
        var copies = _service.GetFileCopies();

        copies.Should().BeEmpty();
    }

    [Fact]
    public void CheckSuspiciousPatterns_WhenNoLoad_ReturnsEmptyIssues()
    {
        var result = _service.CheckSuspiciousPatterns();

        result.Issues.Should().BeEmpty();
    }
}
