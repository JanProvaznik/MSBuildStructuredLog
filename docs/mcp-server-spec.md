# MSBuild Binary Log MCP Server Specification

## Overview

The MSBuild Binary Log MCP (Model Context Protocol) Server provides AI assistants with the ability to analyze MSBuild binary log files (`.binlog`) for build performance optimization, troubleshooting, and general build analysis.

## Use Cases

### Primary Use Case: Build Performance Optimization

The MCP server is designed to help developers identify and resolve build performance issues by:

1. **Identifying Slow Targets and Tasks** - Find the most time-consuming parts of the build
2. **Detecting Redundant Work** - Identify double writes, unnecessary rebuilds, and duplicate operations
3. **Analyzing Dependencies** - Understand project reference graphs and target dependency chains
4. **Profiling Evaluation** - Analyze MSBuild evaluation performance and property/item operations

### Secondary Use Cases

- **Build Troubleshooting** - Investigate build failures, warnings, and errors
- **Build Understanding** - Learn what happens during a build, which targets run, etc.
- **Configuration Analysis** - Examine properties, items, and their values during build

## Tools

### 1. `load_binlog`
Load a binary log file for analysis.

**Parameters:**
- `path` (string, required): Path to the .binlog file

**Returns:**
- Build summary including success/failure, duration, project count, error/warning counts

### 2. `get_build_summary`
Get a comprehensive summary of the loaded build.

**Parameters:**
- None

**Returns:**
- Overall build status
- Total duration
- Number of projects built
- Error and warning counts
- Top-level project information

### 3. `get_slowest_targets`
Identify the slowest targets in the build.

**Parameters:**
- `count` (number, optional, default: 10): Number of targets to return
- `project_filter` (string, optional): Filter by project name pattern

**Returns:**
- List of targets sorted by duration, including:
  - Target name
  - Duration
  - Project name
  - Whether it was skipped

### 4. `get_slowest_tasks`
Identify the slowest tasks in the build.

**Parameters:**
- `count` (number, optional, default: 10): Number of tasks to return
- `task_name_filter` (string, optional): Filter by task name

**Returns:**
- List of tasks sorted by duration, including:
  - Task name
  - Total duration across all invocations
  - Number of invocations
  - Average duration per invocation

### 5. `get_double_writes`
Detect files that were written to from multiple sources (non-determinism indicator).

**Parameters:**
- None

**Returns:**
- List of destination files with multiple sources
- Source file paths for each destination

### 6. `get_errors`
Get all build errors.

**Parameters:**
- `project_filter` (string, optional): Filter by project name pattern

**Returns:**
- List of errors with:
  - Error code
  - Message
  - File and line number
  - Project name

### 7. `get_warnings`
Get all build warnings.

**Parameters:**
- `project_filter` (string, optional): Filter by project name pattern
- `warning_code_filter` (string, optional): Filter by warning code

**Returns:**
- List of warnings with:
  - Warning code
  - Message
  - File and line number
  - Project name

### 8. `search`
Search the build log using the structured log query syntax.

**Parameters:**
- `query` (string, required): Search query (supports the same syntax as the Structured Log Viewer)
- `max_results` (number, optional, default: 100): Maximum number of results

**Returns:**
- List of matching nodes with:
  - Node type
  - Text/Name
  - Path in the build tree

### 9. `get_project_tree`
Get the project dependency/execution tree.

**Parameters:**
- `depth` (number, optional, default: 2): Maximum depth to traverse

**Returns:**
- Hierarchical tree of projects showing build order and dependencies

### 10. `get_target_dependencies`
Get the dependency graph for a specific target.

**Parameters:**
- `target_name` (string, required): Name of the target
- `project_filter` (string, optional): Filter by project name

**Returns:**
- Target dependency information including:
  - DependsOnTargets
  - BeforeTargets relationships
  - AfterTargets relationships

### 11. `get_property_value`
Get the value of a property at a specific point in the build.

**Parameters:**
- `property_name` (string, required): Name of the property
- `project_filter` (string, optional): Filter by project name

**Returns:**
- Property values across different projects/evaluations

### 12. `get_item_values`
Get the values of an item type.

**Parameters:**
- `item_type` (string, required): Name of the item type (e.g., "Compile", "Reference")
- `project_filter` (string, optional): Filter by project name

**Returns:**
- Item values with their metadata

### 13. `get_evaluation_profile`
Get evaluation profiling information.

**Parameters:**
- `project_filter` (string, optional): Filter by project name

**Returns:**
- Evaluation time breakdown by:
  - Property evaluations
  - Item evaluations
  - Import processing
  - Glob expansions

### 14. `check_suspicious_patterns`
Run automated checks for common build issues.

**Parameters:**
- None

**Returns:**
- List of detected issues including:
  - Double writes (non-determinism)
  - Targets that always run (never up-to-date)
  - Expensive tasks that run repeatedly
  - Missing incremental build support
  - Unnecessary rebuilds

### 15. `get_compiler_invocations`
Get C#/VB/F# compiler invocations with their command lines.

**Parameters:**
- `language` (string, optional): Filter by language ("csharp", "vb", "fsharp")

**Returns:**
- List of compiler invocations with:
  - Project name
  - Command line arguments
  - Duration

### 16. `get_file_copies`
Analyze file copy operations in the build.

**Parameters:**
- `source_filter` (string, optional): Filter by source path pattern
- `destination_filter` (string, optional): Filter by destination path pattern

**Returns:**
- List of file copy operations with source, destination, and timing

## Planned Future Tools

The following tools are planned for future implementation:

### `analyze_critical_path` (Planned)
Analyze the critical path of the build (longest sequential chain).

### `compare_builds` (Planned)
Compare two binary logs to identify differences.

## Query Syntax

The `search` tool supports the Structured Log Viewer query syntax:

- Simple text search: `MyTarget`
- Node type prefix: `$target MyTarget`, `$task Csc`, `$message some text`
- Property search: `$property Configuration`
- Under constraint: `under(MyProject) $error`
- Project filter: `project(*.Tests) $target`
- Time range: `start<10s`, `duration>1s`
- Negation: `not $warning`
- Combination: `$task Csc and duration>5s`

## Response Format

All tools return JSON objects with the following structure:

```json
{
  "success": true,
  "data": { ... },
  "error": null
}
```

Or in case of error:

```json
{
  "success": false,
  "data": null,
  "error": "Error message describing what went wrong"
}
```

## Example Workflows

### Performance Investigation

1. Load the binlog: `load_binlog(path: "msbuild.binlog")`
2. Get overview: `get_build_summary()`
3. Find slow spots: `get_slowest_tasks(count: 10)`
4. Check for issues: `check_suspicious_patterns()`
5. Analyze critical path: `analyze_critical_path()`

### Build Failure Investigation

1. Load the binlog: `load_binlog(path: "msbuild.binlog")`
2. Get errors: `get_errors()`
3. Search for context: `search(query: "under(FailingProject) $error")`
4. Check properties: `get_property_value(property_name: "TargetFramework", project_filter: "FailingProject")`

### Incremental Build Analysis

1. Load the binlog: `load_binlog(path: "msbuild.binlog")`
2. Check for double writes: `get_double_writes()`
3. Find non-incremental targets: `search(query: "$target and not Skipped")`
4. Analyze target inputs/outputs: `get_target_dependencies(target_name: "CoreCompile")`

## Implementation Notes

- The server maintains state for one loaded binlog at a time
- Large binlogs may take significant time to load and analyze
- Search operations use the existing StructuredLogger search infrastructure
- All duration values are in milliseconds unless otherwise specified
