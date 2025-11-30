using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;

namespace BinlogMcpServer;

/// <summary>
/// MCP tools for binary log analysis.
/// </summary>
public static class BinlogTools
{
    private static BinlogAnalysisService? _service;

    /// <summary>
    /// Gets or creates the analysis service instance.
    /// </summary>
    private static BinlogAnalysisService Service => _service ??= new BinlogAnalysisService();

    [McpServerTool(Name = "load_binlog")]
    [Description("Load a binary log file (.binlog) for analysis. This must be called before using other analysis tools.")]
    public static string LoadBinlog(
        [Description("The path to the .binlog file to load")] string path)
    {
        var result = Service.LoadBinlog(path);
        return FormatResponse(result.Success, result.Success ? new
        {
            message = result.BuildSucceeded ? "Build succeeded" : "Build failed",
            duration = result.Duration.ToString(),
            projectCount = result.ProjectCount,
            errorCount = result.ErrorCount,
            warningCount = result.WarningCount
        } : null, result.Error);
    }

    [McpServerTool(Name = "get_build_summary")]
    [Description("Get a comprehensive summary of the currently loaded build including duration, project count, errors, and warnings.")]
    public static string GetBuildSummary()
    {
        if (!Service.IsBuildLoaded)
        {
            return FormatResponse(false, null, "No build loaded. Use load_binlog first.");
        }

        var summary = Service.GetBuildSummary();
        return FormatResponse(true, summary);
    }

    [McpServerTool(Name = "get_slowest_targets")]
    [Description("Identify the slowest targets in the build. Useful for finding performance bottlenecks.")]
    public static string GetSlowestTargets(
        [Description("Number of targets to return (default: 10)")] int count = 10,
        [Description("Optional filter by project name pattern")] string? projectFilter = null)
    {
        if (!Service.IsBuildLoaded)
        {
            return FormatResponse(false, null, "No build loaded. Use load_binlog first.");
        }

        var targets = Service.GetSlowestTargets(count, projectFilter);
        return FormatResponse(true, new
        {
            count = targets.Count,
            targets = targets.Select(t => new
            {
                name = t.Name,
                duration = t.DurationText,
                project = t.ProjectName,
                skipped = t.Skipped
            })
        });
    }

    [McpServerTool(Name = "get_slowest_tasks")]
    [Description("Identify the slowest tasks in the build, aggregated by task name. Shows total time and invocation count.")]
    public static string GetSlowestTasks(
        [Description("Number of tasks to return (default: 10)")] int count = 10,
        [Description("Optional filter by task name")] string? taskNameFilter = null)
    {
        if (!Service.IsBuildLoaded)
        {
            return FormatResponse(false, null, "No build loaded. Use load_binlog first.");
        }

        var tasks = Service.GetSlowestTasks(count, taskNameFilter);
        return FormatResponse(true, new
        {
            count = tasks.Count,
            tasks = tasks.Select(t => new
            {
                name = t.Name,
                totalDuration = t.TotalDurationText,
                invocations = t.InvocationCount,
                averageDuration = t.AverageDurationText
            })
        });
    }

    [McpServerTool(Name = "get_double_writes")]
    [Description("Detect files that were written to from multiple sources, which can cause non-deterministic builds.")]
    public static string GetDoubleWrites()
    {
        if (!Service.IsBuildLoaded)
        {
            return FormatResponse(false, null, "No build loaded. Use load_binlog first.");
        }

        var doubleWrites = Service.GetDoubleWrites();
        return FormatResponse(true, new
        {
            count = doubleWrites.Count,
            doubleWrites = doubleWrites.Select(d => new
            {
                destination = d.Destination,
                sourceCount = d.Sources.Count,
                sources = d.Sources
            })
        });
    }

    [McpServerTool(Name = "get_errors")]
    [Description("Get all build errors from the loaded build.")]
    public static string GetErrors(
        [Description("Optional filter by project name pattern")] string? projectFilter = null)
    {
        if (!Service.IsBuildLoaded)
        {
            return FormatResponse(false, null, "No build loaded. Use load_binlog first.");
        }

        var errors = Service.GetErrors(projectFilter);
        return FormatResponse(true, new
        {
            count = errors.Count,
            errors = errors.Select(e => new
            {
                code = e.Code,
                message = e.Message,
                file = e.File,
                line = e.LineNumber,
                project = e.ProjectName
            })
        });
    }

    [McpServerTool(Name = "get_warnings")]
    [Description("Get all build warnings from the loaded build.")]
    public static string GetWarnings(
        [Description("Optional filter by project name pattern")] string? projectFilter = null,
        [Description("Optional filter by warning code")] string? warningCodeFilter = null)
    {
        if (!Service.IsBuildLoaded)
        {
            return FormatResponse(false, null, "No build loaded. Use load_binlog first.");
        }

        var warnings = Service.GetWarnings(projectFilter, warningCodeFilter);
        return FormatResponse(true, new
        {
            count = warnings.Count,
            warnings = warnings.Select(w => new
            {
                code = w.Code,
                message = w.Message,
                file = w.File,
                line = w.LineNumber,
                project = w.ProjectName
            })
        });
    }

    [McpServerTool(Name = "search")]
    [Description("Search the build log using the structured query syntax. Supports patterns like '$task Csc', '$error', 'under(ProjectName)', 'duration>1s'.")]
    public static string Search(
        [Description("The search query using structured log syntax")] string query,
        [Description("Maximum number of results (default: 100)")] int maxResults = 100)
    {
        if (!Service.IsBuildLoaded)
        {
            return FormatResponse(false, null, "No build loaded. Use load_binlog first.");
        }

        var results = Service.Search(query, maxResults);
        return FormatResponse(true, new
        {
            query,
            count = results.Count,
            results = results.Select(r => new
            {
                type = r.NodeType,
                text = r.Text,
                path = r.Path
            })
        });
    }

    [McpServerTool(Name = "get_project_tree")]
    [Description("Get the project dependency and execution tree showing how projects are structured in the build.")]
    public static string GetProjectTree(
        [Description("Maximum depth to traverse (default: 2)")] int depth = 2)
    {
        if (!Service.IsBuildLoaded)
        {
            return FormatResponse(false, null, "No build loaded. Use load_binlog first.");
        }

        var tree = Service.GetProjectTree(depth);
        return FormatResponse(true, tree);
    }

    [McpServerTool(Name = "get_property_value")]
    [Description("Get the value of a property across the build. Useful for checking configuration values.")]
    public static string GetPropertyValue(
        [Description("Name of the property to find")] string propertyName,
        [Description("Optional filter by project name pattern")] string? projectFilter = null)
    {
        if (!Service.IsBuildLoaded)
        {
            return FormatResponse(false, null, "No build loaded. Use load_binlog first.");
        }

        var values = Service.GetPropertyValues(propertyName, projectFilter);
        return FormatResponse(true, new
        {
            propertyName,
            count = values.Count,
            values = values.Select(v => new
            {
                value = v.Value,
                project = v.ProjectName,
                context = v.Context
            })
        });
    }

    [McpServerTool(Name = "get_item_values")]
    [Description("Get the values of an item type (like 'Compile', 'Reference', 'PackageReference') from the build.")]
    public static string GetItemValues(
        [Description("Name of the item type (e.g., 'Compile', 'Reference', 'PackageReference')")] string itemType,
        [Description("Optional filter by project name pattern")] string? projectFilter = null)
    {
        if (!Service.IsBuildLoaded)
        {
            return FormatResponse(false, null, "No build loaded. Use load_binlog first.");
        }

        var items = Service.GetItemValues(itemType, projectFilter);
        return FormatResponse(true, new
        {
            itemType,
            count = items.Count,
            items = items.Select(i => new
            {
                include = i.Include,
                metadata = i.Metadata,
                project = i.ProjectName
            })
        });
    }

    [McpServerTool(Name = "check_suspicious_patterns")]
    [Description("Run automated checks for common build issues like double writes, non-incremental targets, and expensive repeated tasks.")]
    public static string CheckSuspiciousPatterns()
    {
        if (!Service.IsBuildLoaded)
        {
            return FormatResponse(false, null, "No build loaded. Use load_binlog first.");
        }

        var result = Service.CheckSuspiciousPatterns();
        return FormatResponse(true, new
        {
            issueCount = result.Issues.Count,
            issues = result.Issues.Select(i => new
            {
                category = i.Category,
                severity = i.Severity,
                title = i.Title,
                description = i.Description,
                details = i.Details
            })
        });
    }

    [McpServerTool(Name = "get_compiler_invocations")]
    [Description("Get compiler invocations (Csc, Vbc, Fsc) from the build with their command lines and duration.")]
    public static string GetCompilerInvocations(
        [Description("Optional filter by language: 'csharp', 'vb', or 'fsharp'")] string? languageFilter = null)
    {
        if (!Service.IsBuildLoaded)
        {
            return FormatResponse(false, null, "No build loaded. Use load_binlog first.");
        }

        var invocations = Service.GetCompilerInvocations(languageFilter);
        return FormatResponse(true, new
        {
            count = invocations.Count,
            invocations = invocations.Select(i => new
            {
                language = i.Language,
                project = i.ProjectName,
                duration = i.DurationText,
                commandLine = i.CommandLine?.Length > 500 ? i.CommandLine[..500] + "..." : i.CommandLine
            })
        });
    }

    [McpServerTool(Name = "get_target_dependencies")]
    [Description("Get dependency information for a specific target including DependsOnTargets and relationships.")]
    public static string GetTargetDependencies(
        [Description("Name of the target")] string targetName,
        [Description("Optional filter by project name pattern")] string? projectFilter = null)
    {
        if (!Service.IsBuildLoaded)
        {
            return FormatResponse(false, null, "No build loaded. Use load_binlog first.");
        }

        var deps = Service.GetTargetDependencies(targetName, projectFilter);
        if (deps == null)
        {
            return FormatResponse(false, null, $"Target '{targetName}' not found.");
        }

        return FormatResponse(true, new
        {
            target = deps.TargetName,
            project = deps.ProjectName,
            dependsOnTargets = deps.DependsOnTargets,
            parentTarget = deps.ParentTarget,
            builtReason = deps.TargetBuiltReason
        });
    }

    [McpServerTool(Name = "get_evaluation_profile")]
    [Description("Get evaluation profiling information showing how long each project took to evaluate.")]
    public static string GetEvaluationProfile(
        [Description("Optional filter by project name pattern")] string? projectFilter = null)
    {
        if (!Service.IsBuildLoaded)
        {
            return FormatResponse(false, null, "No build loaded. Use load_binlog first.");
        }

        var profiles = Service.GetEvaluationProfile(projectFilter);
        return FormatResponse(true, new
        {
            count = profiles.Count,
            evaluations = profiles.Select(p => new
            {
                project = p.ProjectName,
                duration = p.DurationText,
                evaluationId = p.EvaluationId
            })
        });
    }

    [McpServerTool(Name = "get_file_copies")]
    [Description("Analyze file copy operations in the build.")]
    public static string GetFileCopies(
        [Description("Optional filter by source path pattern")] string? sourceFilter = null,
        [Description("Optional filter by destination path pattern")] string? destinationFilter = null)
    {
        if (!Service.IsBuildLoaded)
        {
            return FormatResponse(false, null, "No build loaded. Use load_binlog first.");
        }

        var copies = Service.GetFileCopies(sourceFilter, destinationFilter);
        return FormatResponse(true, new
        {
            count = copies.Count,
            copies = copies.Take(100).Select(c => new
            {
                source = c.Source,
                destination = c.Destination,
                copied = c.Copied
            }),
            truncated = copies.Count > 100
        });
    }

    private static string FormatResponse(bool success, object? data, string? error = null)
    {
        var response = new
        {
            success,
            data,
            error
        };

        return JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
    }
}
