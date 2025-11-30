using Microsoft.Build.Logging.StructuredLogger;
using StructuredLogViewer;
using MSBuildTask = Microsoft.Build.Logging.StructuredLogger.Task;

namespace BinlogMcpServer;

/// <summary>
/// Service that manages binary log analysis state and operations.
/// </summary>
public class BinlogAnalysisService
{
    private Build? _currentBuild;
    private string? _currentBinlogPath;

    /// <summary>
    /// Gets the currently loaded build, or null if no build is loaded.
    /// </summary>
    public Build? CurrentBuild => _currentBuild;

    /// <summary>
    /// Gets the path to the currently loaded binlog file.
    /// </summary>
    public string? CurrentBinlogPath => _currentBinlogPath;

    /// <summary>
    /// Gets whether a build is currently loaded.
    /// </summary>
    public bool IsBuildLoaded => _currentBuild != null;

    /// <summary>
    /// Loads a binary log file for analysis.
    /// </summary>
    public BuildLoadResult LoadBinlog(string path)
    {
        if (!File.Exists(path))
        {
            return new BuildLoadResult
            {
                Success = false,
                Error = $"File not found: {path}"
            };
        }

        try
        {
            var build = BinaryLog.ReadBuild(path);
            BuildAnalyzer.AnalyzeBuild(build);

            _currentBuild = build;
            _currentBinlogPath = path;

            return new BuildLoadResult
            {
                Success = true,
                BuildSucceeded = build.Succeeded,
                Duration = build.Duration,
                ProjectCount = CountProjects(build),
                ErrorCount = CountErrors(build),
                WarningCount = CountWarnings(build)
            };
        }
        catch (Exception ex)
        {
            return new BuildLoadResult
            {
                Success = false,
                Error = $"Failed to load binlog: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Gets a summary of the currently loaded build.
    /// </summary>
    public BuildSummary? GetBuildSummary()
    {
        if (_currentBuild == null)
            return null;

        var build = _currentBuild;

        return new BuildSummary
        {
            BinlogPath = _currentBinlogPath,
            BuildSucceeded = build.Succeeded,
            Duration = build.Duration,
            DurationText = build.DurationText,
            StartTime = build.StartTime,
            EndTime = build.EndTime,
            ProjectCount = CountProjects(build),
            ErrorCount = CountErrors(build),
            WarningCount = CountWarnings(build),
            MSBuildVersion = build.MSBuildVersion,
            LogFilePath = build.LogFilePath
        };
    }

    /// <summary>
    /// Gets the slowest targets in the build.
    /// </summary>
    public List<TargetInfo> GetSlowestTargets(int count = 10, string? projectFilter = null)
    {
        if (_currentBuild == null)
            return new List<TargetInfo>();

        var targets = new List<TargetInfo>();

        _currentBuild.VisitAllChildren<Target>(target =>
        {
            if (projectFilter != null)
            {
                var project = target.GetNearestParent<Project>();
                if (project == null || !project.Name.Contains(projectFilter, StringComparison.OrdinalIgnoreCase))
                    return;
            }

            targets.Add(new TargetInfo
            {
                Name = target.Name,
                Duration = target.Duration,
                DurationText = target.DurationText,
                ProjectName = target.Project?.Name ?? "Unknown",
                ProjectFile = target.Project?.ProjectFile,
                Succeeded = target.Succeeded,
                Skipped = target.Skipped
            });
        });

        return targets
            .OrderByDescending(t => t.Duration)
            .Take(count)
            .ToList();
    }

    /// <summary>
    /// Gets the slowest tasks in the build, aggregated by task name.
    /// </summary>
    public List<TaskSummaryInfo> GetSlowestTasks(int count = 10, string? taskNameFilter = null)
    {
        if (_currentBuild == null)
            return new List<TaskSummaryInfo>();

        var taskStats = new Dictionary<string, TaskSummaryInfo>(StringComparer.OrdinalIgnoreCase);

        _currentBuild.VisitAllChildren<MSBuildTask>(task =>
        {
            if (taskNameFilter != null && !task.Name.Contains(taskNameFilter, StringComparison.OrdinalIgnoreCase))
                return;

            if (!taskStats.TryGetValue(task.Name, out var info))
            {
                info = new TaskSummaryInfo { Name = task.Name };
                taskStats[task.Name] = info;
            }

            info.InvocationCount++;
            info.TotalDuration += task.Duration;
        });

        foreach (var info in taskStats.Values)
        {
            info.AverageDuration = TimeSpan.FromTicks(info.TotalDuration.Ticks / info.InvocationCount);
            info.TotalDurationText = TextUtilities.DisplayDuration(info.TotalDuration);
            info.AverageDurationText = TextUtilities.DisplayDuration(info.AverageDuration);
        }

        return taskStats.Values
            .OrderByDescending(t => t.TotalDuration)
            .Take(count)
            .ToList();
    }

    /// <summary>
    /// Gets double writes detected in the build.
    /// </summary>
    public List<DoubleWriteInfo> GetDoubleWrites()
    {
        if (_currentBuild == null)
            return new List<DoubleWriteInfo>();

        var doubleWrites = DoubleWritesAnalyzer.GetDoubleWrites(_currentBuild);

        return doubleWrites.Select(dw => new DoubleWriteInfo
        {
            Destination = dw.Key,
            Sources = dw.Value.ToList()
        }).ToList();
    }

    /// <summary>
    /// Gets all errors from the build.
    /// </summary>
    public List<ErrorInfo> GetErrors(string? projectFilter = null)
    {
        if (_currentBuild == null)
            return new List<ErrorInfo>();

        var errors = new List<ErrorInfo>();

        _currentBuild.VisitAllChildren<Error>(error =>
        {
            var project = error.GetNearestParent<Project>();
            if (projectFilter != null && project != null &&
                !project.Name.Contains(projectFilter, StringComparison.OrdinalIgnoreCase))
                return;

            errors.Add(new ErrorInfo
            {
                Code = error.Code,
                Message = error.Text,
                File = error.File,
                LineNumber = error.LineNumber,
                ProjectName = project?.Name,
                ProjectFile = project?.ProjectFile
            });
        });

        return errors;
    }

    /// <summary>
    /// Gets all warnings from the build.
    /// </summary>
    public List<WarningInfo> GetWarnings(string? projectFilter = null, string? warningCodeFilter = null)
    {
        if (_currentBuild == null)
            return new List<WarningInfo>();

        var warnings = new List<WarningInfo>();

        _currentBuild.VisitAllChildren<Warning>(warning =>
        {
            var project = warning.GetNearestParent<Project>();

            if (projectFilter != null && project != null &&
                !project.Name.Contains(projectFilter, StringComparison.OrdinalIgnoreCase))
                return;

            if (warningCodeFilter != null && warning.Code != null &&
                !warning.Code.Contains(warningCodeFilter, StringComparison.OrdinalIgnoreCase))
                return;

            warnings.Add(new WarningInfo
            {
                Code = warning.Code,
                Message = warning.Text,
                File = warning.File,
                LineNumber = warning.LineNumber,
                ProjectName = project?.Name,
                ProjectFile = project?.ProjectFile
            });
        });

        return warnings;
    }

    /// <summary>
    /// Searches the build using the query syntax.
    /// </summary>
    public List<SearchResultInfo> Search(string query, int maxResults = 100)
    {
        if (_currentBuild == null)
            return new List<SearchResultInfo>();

        var search = new Search(
            new[] { _currentBuild },
            _currentBuild.StringTable.Instances,
            maxResults,
            markResultsInTree: false);

        var results = search.FindNodes(query, CancellationToken.None);

        return results.Select(r => new SearchResultInfo
        {
            NodeType = r.Node?.GetType().Name ?? "Unknown",
            Text = GetNodeText(r.Node),
            Path = GetNodePath(r.Node)
        }).ToList();
    }

    /// <summary>
    /// Gets the project tree structure.
    /// </summary>
    public ProjectTreeNode? GetProjectTree(int depth = 2)
    {
        if (_currentBuild == null)
            return null;

        return BuildProjectTree(_currentBuild, depth, 0);
    }

    /// <summary>
    /// Gets property values across the build.
    /// </summary>
    public List<PropertyValueInfo> GetPropertyValues(string propertyName, string? projectFilter = null)
    {
        if (_currentBuild == null)
            return new List<PropertyValueInfo>();

        var values = new List<PropertyValueInfo>();

        // Search in evaluations
        _currentBuild.VisitAllChildren<Property>(prop =>
        {
            if (!prop.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase))
                return;

            var project = prop.GetNearestParent<Project>();
            var evaluation = prop.GetNearestParent<ProjectEvaluation>();

            if (projectFilter != null)
            {
                var name = project?.Name ?? evaluation?.Name;
                if (name == null || !name.Contains(projectFilter, StringComparison.OrdinalIgnoreCase))
                    return;
            }

            values.Add(new PropertyValueInfo
            {
                PropertyName = prop.Name,
                Value = prop.Value,
                ProjectName = project?.Name ?? evaluation?.Name,
                Context = project != null ? "Build" : "Evaluation"
            });
        });

        return values;
    }

    /// <summary>
    /// Gets item values.
    /// </summary>
    public List<ItemValueInfo> GetItemValues(string itemType, string? projectFilter = null)
    {
        if (_currentBuild == null)
            return new List<ItemValueInfo>();

        var items = new List<ItemValueInfo>();

        _currentBuild.VisitAllChildren<AddItem>(addItem =>
        {
            if (!addItem.Name.Equals(itemType, StringComparison.OrdinalIgnoreCase))
                return;

            var project = addItem.GetNearestParent<Project>();
            if (projectFilter != null && project != null &&
                !project.Name.Contains(projectFilter, StringComparison.OrdinalIgnoreCase))
                return;

            foreach (var child in addItem.Children)
            {
                if (child is Item item)
                {
                    var metadata = new Dictionary<string, string>();
                    foreach (var metadataChild in item.Children.OfType<Metadata>())
                    {
                        metadata[metadataChild.Name] = metadataChild.Value;
                    }

                    items.Add(new ItemValueInfo
                    {
                        ItemType = itemType,
                        Include = item.Text,
                        Metadata = metadata,
                        ProjectName = project?.Name
                    });
                }
            }
        });

        return items;
    }

    /// <summary>
    /// Checks for suspicious patterns in the build.
    /// </summary>
    public SuspiciousPatternsResult CheckSuspiciousPatterns()
    {
        if (_currentBuild == null)
            return new SuspiciousPatternsResult { Issues = new List<SuspiciousPatternInfo>() };

        var issues = new List<SuspiciousPatternInfo>();

        // Check for double writes
        var doubleWrites = GetDoubleWrites();
        if (doubleWrites.Count > 0)
        {
            issues.Add(new SuspiciousPatternInfo
            {
                Category = "Non-Determinism",
                Severity = "Warning",
                Title = "Double Writes Detected",
                Description = $"Found {doubleWrites.Count} files being written from multiple sources. This can cause non-deterministic builds.",
                Details = doubleWrites.Take(5).Select(d => $"{d.Destination}: {d.Sources.Count} sources").ToList()
            });
        }

        // Check for targets that always run (never skipped)
        var targetStats = new Dictionary<string, (int Total, int Skipped)>(StringComparer.OrdinalIgnoreCase);
        _currentBuild.VisitAllChildren<Target>(target =>
        {
            if (!targetStats.ContainsKey(target.Name))
                targetStats[target.Name] = (0, 0);

            var stats = targetStats[target.Name];
            stats.Total++;
            if (target.Skipped)
                stats.Skipped++;
            targetStats[target.Name] = stats;
        });

        var neverSkippedTargets = targetStats
            .Where(t => t.Value.Total > 2 && t.Value.Skipped == 0)
            .Where(t => !IsExpectedAlwaysRunTarget(t.Key))
            .Take(10)
            .ToList();

        if (neverSkippedTargets.Count > 0)
        {
            issues.Add(new SuspiciousPatternInfo
            {
                Category = "Performance",
                Severity = "Info",
                Title = "Targets That Never Skip",
                Description = "These targets ran multiple times and were never skipped. They may benefit from incremental build support.",
                Details = neverSkippedTargets.Select(t => $"{t.Key}: {t.Value.Total} runs, 0 skipped").ToList()
            });
        }

        // Check for expensive tasks running multiple times
        var slowTasks = GetSlowestTasks(count: 5);
        var expensiveRepeatedTasks = slowTasks
            .Where(t => t.InvocationCount > 5 && t.TotalDuration.TotalSeconds > 10)
            .ToList();

        if (expensiveRepeatedTasks.Count > 0)
        {
            issues.Add(new SuspiciousPatternInfo
            {
                Category = "Performance",
                Severity = "Warning",
                Title = "Expensive Tasks Running Multiple Times",
                Description = "These tasks are expensive and run many times. Consider if they can be cached or run fewer times.",
                Details = expensiveRepeatedTasks.Select(t =>
                    $"{t.Name}: {t.InvocationCount} invocations, total {t.TotalDurationText}").ToList()
            });
        }

        return new SuspiciousPatternsResult { Issues = issues };
    }

    /// <summary>
    /// Gets compiler invocations from the build.
    /// </summary>
    public List<CompilerInvocationInfo> GetCompilerInvocations(string? languageFilter = null)
    {
        if (_currentBuild == null)
            return new List<CompilerInvocationInfo>();

        var invocations = new List<CompilerInvocationInfo>();
        var taskNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Csc", "Vbc", "Fsc" };

        if (languageFilter != null)
        {
            taskNames = languageFilter.ToLowerInvariant() switch
            {
                "csharp" or "c#" => new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Csc" },
                "vb" or "visualbasic" => new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Vbc" },
                "fsharp" or "f#" => new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Fsc" },
                _ => taskNames
            };
        }

        _currentBuild.VisitAllChildren<MSBuildTask>(task =>
        {
            if (!taskNames.Contains(task.Name))
                return;

            var project = task.GetNearestParent<Project>();

            invocations.Add(new CompilerInvocationInfo
            {
                Language = task.Name switch
                {
                    "Csc" => "C#",
                    "Vbc" => "VB",
                    "Fsc" => "F#",
                    _ => "Unknown"
                },
                TaskName = task.Name,
                ProjectName = project?.Name,
                ProjectFile = project?.ProjectFile,
                Duration = task.Duration,
                DurationText = task.DurationText,
                CommandLine = task.CommandLineArguments
            });
        });

        return invocations;
    }

    /// <summary>
    /// Gets the target dependencies for a specific target.
    /// </summary>
    public TargetDependencyInfo? GetTargetDependencies(string targetName, string? projectFilter = null)
    {
        if (_currentBuild == null)
            return null;

        Target? targetNode = null;

        _currentBuild.VisitAllChildren<Target>(target =>
        {
            if (targetNode != null)
                return;

            if (!target.Name.Equals(targetName, StringComparison.OrdinalIgnoreCase))
                return;

            if (projectFilter != null)
            {
                var project = target.GetNearestParent<Project>();
                if (project == null || !project.Name.Contains(projectFilter, StringComparison.OrdinalIgnoreCase))
                    return;
            }

            targetNode = target;
        });

        if (targetNode == null)
            return null;

        var project = targetNode.GetNearestParent<Project>();

        return new TargetDependencyInfo
        {
            TargetName = targetNode.Name,
            ProjectName = project?.Name,
            DependsOnTargets = targetNode.DependsOnTargets?.Split(',').Select(s => s.Trim()).ToList() ?? new List<string>(),
            ParentTarget = targetNode.ParentTarget,
            TargetBuiltReason = targetNode.TargetBuiltReason.ToString()
        };
    }

    /// <summary>
    /// Gets evaluation profile information.
    /// </summary>
    public List<EvaluationProfileInfo> GetEvaluationProfile(string? projectFilter = null)
    {
        if (_currentBuild == null)
            return new List<EvaluationProfileInfo>();

        var profiles = new List<EvaluationProfileInfo>();

        _currentBuild.VisitAllChildren<ProjectEvaluation>(evaluation =>
        {
            if (projectFilter != null && !evaluation.Name.Contains(projectFilter, StringComparison.OrdinalIgnoreCase))
                return;

            profiles.Add(new EvaluationProfileInfo
            {
                ProjectName = evaluation.Name,
                Duration = evaluation.Duration,
                DurationText = evaluation.DurationText,
                EvaluationId = evaluation.Id
            });
        });

        return profiles
            .OrderByDescending(p => p.Duration)
            .ToList();
    }

    /// <summary>
    /// Gets file copy operations from the build.
    /// </summary>
    public List<FileCopyInfo> GetFileCopies(string? sourceFilter = null, string? destinationFilter = null)
    {
        if (_currentBuild == null)
            return new List<FileCopyInfo>();

        var copies = new List<FileCopyInfo>();

        _currentBuild.VisitAllChildren<CopyTask>(copyTask =>
        {
            foreach (var operation in copyTask.FileCopyOperations)
            {
                if (sourceFilter != null && !operation.Source.Contains(sourceFilter, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (destinationFilter != null && !operation.Destination.Contains(destinationFilter, StringComparison.OrdinalIgnoreCase))
                    continue;

                copies.Add(new FileCopyInfo
                {
                    Source = operation.Source,
                    Destination = operation.Destination,
                    Copied = operation.Copied
                });
            }
        });

        return copies;
    }

    // Helper methods

    private int CountProjects(Build build)
    {
        int count = 0;
        build.VisitAllChildren<Project>(_ => count++);
        return count;
    }

    private int CountErrors(Build build)
    {
        int count = 0;
        build.VisitAllChildren<Error>(_ => count++);
        return count;
    }

    private int CountWarnings(Build build)
    {
        int count = 0;
        build.VisitAllChildren<Warning>(_ => count++);
        return count;
    }

    private string GetNodeText(BaseNode? node)
    {
        return node switch
        {
            null => "",
            NamedNode named => named.Name,
            TextNode text => text.Text ?? "",
            _ => node.ToString() ?? ""
        };
    }

    private string GetNodePath(BaseNode? node)
    {
        if (node == null)
            return "";

        var parts = new List<string>();
        var current = node;

        while (current != null)
        {
            var text = GetNodeText(current);
            if (!string.IsNullOrEmpty(text))
            {
                parts.Insert(0, $"{current.GetType().Name}:{text}");
            }

            current = current.Parent as BaseNode;
        }

        return string.Join(" > ", parts);
    }

    private ProjectTreeNode BuildProjectTree(TreeNode node, int maxDepth, int currentDepth)
    {
        var treeNode = new ProjectTreeNode
        {
            Name = node switch
            {
                Project p => p.Name,
                Build b => "Build",
                _ => node.ToString() ?? "Unknown"
            },
            Type = node.GetType().Name,
            Duration = (node as TimedNode)?.DurationText
        };

        if (currentDepth < maxDepth && node.HasChildren)
        {
            treeNode.Children = node.Children
                .OfType<Project>()
                .Select(p => BuildProjectTree(p, maxDepth, currentDepth + 1))
                .ToList();
        }

        return treeNode;
    }

    private bool IsExpectedAlwaysRunTarget(string targetName)
    {
        // These targets are expected to always run
        var expectedTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Build", "Clean", "Rebuild", "Restore", "Pack", "Publish",
            "BeforeBuild", "AfterBuild", "CoreBuild", "PrepareForBuild",
            "_InitializeAssemblyVersion", "_GetRestoreProjectStyle"
        };

        return expectedTargets.Contains(targetName);
    }
}
