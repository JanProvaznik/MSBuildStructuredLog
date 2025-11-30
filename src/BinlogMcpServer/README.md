# MSBuild Binary Log MCP Server

An MCP (Model Context Protocol) server for analyzing MSBuild binary log files (`.binlog`). This server enables AI assistants like GitHub Copilot to help with build performance optimization, troubleshooting, and analysis.

## Installation

```bash
dotnet tool install -g MSBuild.BinlogMcpServer
```

## Usage

### With GitHub Copilot

Add the following to your `.copilot/mcp.json`:

```json
{
  "servers": {
    "binlog": {
      "type": "stdio",
      "command": "binlog-mcp-server"
    }
  }
}
```

### Standalone

Run the server directly:

```bash
binlog-mcp-server
```

## Available Tools

### Build Loading and Summary

- **load_binlog** - Load a binary log file for analysis
- **get_build_summary** - Get a comprehensive summary of the loaded build

### Performance Analysis

- **get_slowest_targets** - Identify the slowest targets in the build
- **get_slowest_tasks** - Identify the slowest tasks in the build
- **analyze_critical_path** - Analyze the critical path of the build
- **get_evaluation_profile** - Get evaluation profiling information

### Issue Detection

- **get_double_writes** - Detect files written from multiple sources
- **get_errors** - Get all build errors
- **get_warnings** - Get all build warnings
- **check_suspicious_patterns** - Run automated checks for common build issues

### Search and Query

- **search** - Search the build log using structured query syntax
- **get_property_value** - Get property values
- **get_item_values** - Get item values

### Build Structure

- **get_project_tree** - Get the project dependency/execution tree
- **get_target_dependencies** - Get target dependency information
- **get_compiler_invocations** - Get compiler invocations
- **get_file_copies** - Analyze file copy operations

### Comparison

- **compare_builds** - Compare two binary logs

## Example Conversations

### Performance Investigation

> "Load the build log and tell me what's making my build slow"

The AI will:
1. Load the binlog
2. Get a build summary
3. Identify the slowest tasks and targets
4. Check for suspicious patterns
5. Provide recommendations

### Build Failure Investigation

> "Why did my build fail?"

The AI will:
1. Load the binlog
2. Get all errors
3. Search for related context
4. Explain the failure and suggest fixes

## Query Syntax

The `search` tool supports the Structured Log Viewer query syntax:

- Simple text: `MyTarget`
- Node type: `$target MyTarget`, `$task Csc`
- Under constraint: `under(MyProject) $error`
- Time filters: `duration>1s`
- Combinations: `$task Csc and duration>5s`

## License

MIT License - see the main repository for details.
