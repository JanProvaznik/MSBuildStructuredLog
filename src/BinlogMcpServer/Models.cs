namespace BinlogMcpServer;

/// <summary>
/// Result of loading a binlog file.
/// </summary>
public class BuildLoadResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public bool BuildSucceeded { get; set; }
    public TimeSpan Duration { get; set; }
    public int ProjectCount { get; set; }
    public int ErrorCount { get; set; }
    public int WarningCount { get; set; }
}

/// <summary>
/// Summary information about a build.
/// </summary>
public class BuildSummary
{
    public string? BinlogPath { get; set; }
    public bool BuildSucceeded { get; set; }
    public TimeSpan Duration { get; set; }
    public string? DurationText { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public int ProjectCount { get; set; }
    public int ErrorCount { get; set; }
    public int WarningCount { get; set; }
    public string? MSBuildVersion { get; set; }
    public string? LogFilePath { get; set; }
}

/// <summary>
/// Information about a target in the build.
/// </summary>
public class TargetInfo
{
    public string Name { get; set; } = "";
    public TimeSpan Duration { get; set; }
    public string? DurationText { get; set; }
    public string? ProjectName { get; set; }
    public string? ProjectFile { get; set; }
    public bool Succeeded { get; set; }
    public bool Skipped { get; set; }
}

/// <summary>
/// Summary information about a task aggregated across invocations.
/// </summary>
public class TaskSummaryInfo
{
    public string Name { get; set; } = "";
    public TimeSpan TotalDuration { get; set; }
    public string? TotalDurationText { get; set; }
    public int InvocationCount { get; set; }
    public TimeSpan AverageDuration { get; set; }
    public string? AverageDurationText { get; set; }
}

/// <summary>
/// Information about a double write issue.
/// </summary>
public class DoubleWriteInfo
{
    public string Destination { get; set; } = "";
    public List<string> Sources { get; set; } = new();
}

/// <summary>
/// Information about an error in the build.
/// </summary>
public class ErrorInfo
{
    public string? Code { get; set; }
    public string? Message { get; set; }
    public string? File { get; set; }
    public int? LineNumber { get; set; }
    public string? ProjectName { get; set; }
    public string? ProjectFile { get; set; }
}

/// <summary>
/// Information about a warning in the build.
/// </summary>
public class WarningInfo
{
    public string? Code { get; set; }
    public string? Message { get; set; }
    public string? File { get; set; }
    public int? LineNumber { get; set; }
    public string? ProjectName { get; set; }
    public string? ProjectFile { get; set; }
}

/// <summary>
/// Information about a search result.
/// </summary>
public class SearchResultInfo
{
    public string NodeType { get; set; } = "";
    public string Text { get; set; } = "";
    public string Path { get; set; } = "";
}

/// <summary>
/// Node in the project tree structure.
/// </summary>
public class ProjectTreeNode
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public string? Duration { get; set; }
    public List<ProjectTreeNode> Children { get; set; } = new();
}

/// <summary>
/// Information about a property value.
/// </summary>
public class PropertyValueInfo
{
    public string PropertyName { get; set; } = "";
    public string? Value { get; set; }
    public string? ProjectName { get; set; }
    public string? Context { get; set; }
}

/// <summary>
/// Information about an item value.
/// </summary>
public class ItemValueInfo
{
    public string ItemType { get; set; } = "";
    public string Include { get; set; } = "";
    public Dictionary<string, string> Metadata { get; set; } = new();
    public string? ProjectName { get; set; }
}

/// <summary>
/// Result of checking for suspicious patterns.
/// </summary>
public class SuspiciousPatternsResult
{
    public List<SuspiciousPatternInfo> Issues { get; set; } = new();
}

/// <summary>
/// Information about a suspicious pattern detected in the build.
/// </summary>
public class SuspiciousPatternInfo
{
    public string Category { get; set; } = "";
    public string Severity { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public List<string> Details { get; set; } = new();
}

/// <summary>
/// Information about a compiler invocation.
/// </summary>
public class CompilerInvocationInfo
{
    public string Language { get; set; } = "";
    public string TaskName { get; set; } = "";
    public string? ProjectName { get; set; }
    public string? ProjectFile { get; set; }
    public TimeSpan Duration { get; set; }
    public string? DurationText { get; set; }
    public string? CommandLine { get; set; }
}

/// <summary>
/// Information about target dependencies.
/// </summary>
public class TargetDependencyInfo
{
    public string TargetName { get; set; } = "";
    public string? ProjectName { get; set; }
    public List<string> DependsOnTargets { get; set; } = new();
    public string? ParentTarget { get; set; }
    public string? TargetBuiltReason { get; set; }
}

/// <summary>
/// Information about evaluation profile.
/// </summary>
public class EvaluationProfileInfo
{
    public string? ProjectName { get; set; }
    public TimeSpan Duration { get; set; }
    public string? DurationText { get; set; }
    public int EvaluationId { get; set; }
}

/// <summary>
/// Information about a file copy operation.
/// </summary>
public class FileCopyInfo
{
    public string Source { get; set; } = "";
    public string Destination { get; set; } = "";
    public bool Copied { get; set; }
}
