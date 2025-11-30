using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace BinlogMcpServer.Tests;

/// <summary>
/// Integration tests that test the MCP server with a real binlog file.
/// These tests require generating a binlog file first.
/// </summary>
public class BinlogIntegrationTests : IDisposable
{
    private readonly string _testBinlogPath;
    private readonly string _testDirectory;
    private readonly BinlogAnalysisService _service;

    public BinlogIntegrationTests()
    {
        _service = new BinlogAnalysisService();
        _testDirectory = Path.Combine(Path.GetTempPath(), $"binlog-test-{Guid.NewGuid()}");
        _testBinlogPath = Path.Combine(_testDirectory, "test.binlog");
        
        // Create test directory and generate a binlog
        Directory.CreateDirectory(_testDirectory);
        GenerateTestBinlog();
    }

    public void Dispose()
    {
        // Clean up test directory
        try
        {
            if (Directory.Exists(_testDirectory))
            {
                Directory.Delete(_testDirectory, recursive: true);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    private void GenerateTestBinlog()
    {
        var projectContent = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <OutputType>Exe</OutputType>
    <ImplicitUsings>enable</ImplicitUsings>
    <TestProperty>TestValue</TestProperty>
  </PropertyGroup>
  <ItemGroup>
    <TestItem Include=""Item1"" />
    <TestItem Include=""Item2"" />
  </ItemGroup>
</Project>";

        var programContent = @"Console.WriteLine(""Hello from test"");";

        File.WriteAllText(Path.Combine(_testDirectory, "Test.csproj"), projectContent);
        File.WriteAllText(Path.Combine(_testDirectory, "Program.cs"), programContent);

        // Run dotnet build with binlog
        var process = new System.Diagnostics.Process
        {
            StartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"build /bl:{_testBinlogPath}",
                WorkingDirectory = _testDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };
        process.Start();
        process.WaitForExit(60000);
    }

    [Fact]
    public void LoadBinlog_WithValidFile_ReturnsSuccess()
    {
        if (!File.Exists(_testBinlogPath))
        {
            return; // Skip if binlog generation failed
        }

        var result = _service.LoadBinlog(_testBinlogPath);

        result.Success.Should().BeTrue();
        result.ProjectCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public void GetBuildSummary_AfterLoad_ReturnsValidSummary()
    {
        if (!File.Exists(_testBinlogPath))
        {
            return; // Skip if binlog generation failed
        }

        _service.LoadBinlog(_testBinlogPath);

        var summary = _service.GetBuildSummary();

        summary.Should().NotBeNull();
        summary!.ProjectCount.Should().BeGreaterThan(0);
        summary.Duration.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public void GetSlowestTargets_AfterLoad_ReturnsTargets()
    {
        if (!File.Exists(_testBinlogPath))
        {
            return; // Skip if binlog generation failed
        }

        _service.LoadBinlog(_testBinlogPath);

        var targets = _service.GetSlowestTargets(count: 5);

        targets.Should().NotBeEmpty();
        targets.First().Duration.Should().BeGreaterOrEqualTo(TimeSpan.Zero);
    }

    [Fact]
    public void GetSlowestTasks_AfterLoad_ReturnsTasks()
    {
        if (!File.Exists(_testBinlogPath))
        {
            return; // Skip if binlog generation failed
        }

        _service.LoadBinlog(_testBinlogPath);

        var tasks = _service.GetSlowestTasks(count: 5);

        tasks.Should().NotBeEmpty();
        tasks.First().InvocationCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Search_WithValidQuery_ReturnsResults()
    {
        if (!File.Exists(_testBinlogPath))
        {
            return; // Skip if binlog generation failed
        }

        _service.LoadBinlog(_testBinlogPath);

        // Search for Build target which should exist in any build
        var results = _service.Search("$target Build");

        results.Should().NotBeEmpty();
    }

    [Fact]
    public void GetProjectTree_AfterLoad_ReturnsTree()
    {
        if (!File.Exists(_testBinlogPath))
        {
            return; // Skip if binlog generation failed
        }

        _service.LoadBinlog(_testBinlogPath);

        var tree = _service.GetProjectTree();

        tree.Should().NotBeNull();
        tree!.Name.Should().Be("Build");
    }

    [Fact]
    public void GetEvaluationProfile_AfterLoad_ReturnsProfiles()
    {
        if (!File.Exists(_testBinlogPath))
        {
            return; // Skip if binlog generation failed
        }

        _service.LoadBinlog(_testBinlogPath);

        var profiles = _service.GetEvaluationProfile();

        profiles.Should().NotBeEmpty();
    }

    [Fact]
    public void CheckSuspiciousPatterns_AfterLoad_ReturnsResult()
    {
        if (!File.Exists(_testBinlogPath))
        {
            return; // Skip if binlog generation failed
        }

        _service.LoadBinlog(_testBinlogPath);

        var result = _service.CheckSuspiciousPatterns();

        result.Should().NotBeNull();
        // A simple build may or may not have issues, just verify the API works
    }

    [Fact]
    public void GetCompilerInvocations_AfterLoad_ReturnsInvocations()
    {
        if (!File.Exists(_testBinlogPath))
        {
            return; // Skip if binlog generation failed
        }

        _service.LoadBinlog(_testBinlogPath);

        var invocations = _service.GetCompilerInvocations("csharp");

        invocations.Should().NotBeEmpty();
        invocations.First().Language.Should().Be("C#");
    }

    [Fact]
    public void BinlogTools_LoadAndSearch_ReturnsValidJson()
    {
        if (!File.Exists(_testBinlogPath))
        {
            return; // Skip if binlog generation failed
        }

        var loadResult = BinlogTools.LoadBinlog(_testBinlogPath);
        var loadJson = JsonDocument.Parse(loadResult);
        loadJson.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();

        var searchResult = BinlogTools.Search("$target");
        var searchJson = JsonDocument.Parse(searchResult);
        searchJson.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
    }
}
