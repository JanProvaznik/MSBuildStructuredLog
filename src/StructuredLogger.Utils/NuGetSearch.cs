﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NuGet.Frameworks;
using NuGet.ProjectModel;
using StructuredLogViewer;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public class AssetsFile
    {
        public string AssetsFilePath { get; set; }
        public string ProjectFilePath { get; set; }
        public string Text { get; set; }

        private LockFile lockFile;
        public LockFile LockFile
        {
            get
            {
                lock (this)
                {
                    lockFile ??= new LockFileFormat().Parse(Text, AssetsFilePath);
                }

                return lockFile;
            }
        }
    }

    public class NuGetSearch : ISearchExtension
    {
        private Regex projectFilePathRegex = new Regex(@"\""projectPath\""\: \""(?<Path>[^\""]+)\"",", RegexOptions.Compiled);
        private List<AssetsFile> assetsFiles;

        public Build Build { get; }

        public NuGetSearch(Build build)
        {
            Build = build;
        }

        public bool TryGetResults(NodeQueryMatcher matcher, IList<SearchResult> resultCollector, int maxResults)
        {
            if (!string.Equals(matcher.TypeKeyword, "nuget", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var underProjectMatcher = matcher.IncludeMatchers.FirstOrDefault(m => m.UnderProject);
            if (underProjectMatcher == null || underProjectMatcher.Terms.Count == 0)
            {
                resultCollector.Add(new SearchResult(new Error { Text = "Add a 'project(...)' clause to filter which project(s) to search." }));
                resultCollector.Add(new SearchResult(new Note { Text = "Specify '$nuget project(.csproj)' to search all projects (expensive)." }));
                return true;
            }

            PopulateAssetsFiles();

            var files = FindAssetsFiles(underProjectMatcher, maxResults);
            if (files.Count == 0)
            {
                resultCollector.Add(new SearchResult(new Error { Text = "No matching project.assets.json files found" }));
                return true;
            }

            foreach (var file in files)
            {
                var project = new Project
                {
                    ProjectFile = file.ProjectFilePath,
                    Name = Path.GetFileName(file.ProjectFilePath),
                    IsExpanded = true
                };

                PopulateProject(project, matcher, file);

                if (project.HasChildren)
                {
                    resultCollector.Add(new SearchResult(project));
                }
            }

            return true;
        }

        private void PopulateProject(Project project, NodeQueryMatcher matcher, AssetsFile file)
        {
            var lockFile = file.LockFile;

            bool expand = matcher.Terms.Count > 0;
            bool addedAnything = false;

            Dictionary<string, Folder> targetsByDependencyHash = new(StringComparer.OrdinalIgnoreCase);

            foreach (var framework in lockFile.ProjectFileDependencyGroups)
            {
                string shortFrameworkName = ShortenFrameworkName(framework.FrameworkName);
                var frameworkDependencies = framework.Dependencies.Select(d => ParsePackageId(d)).ToArray();

                var target = lockFile.Targets.FirstOrDefault(t => string.Equals(t.Name, framework.FrameworkName, StringComparison.OrdinalIgnoreCase));
                if (target == null)
                {
                    continue;
                }

                var libraries = target.Libraries.ToDictionary(l => l.Name, StringComparer.OrdinalIgnoreCase);

                var frameworkNode = new Folder
                {
                    Name = $"Dependencies for {shortFrameworkName}",
                    IsExpanded = true
                };

                HashSet<string> expandedPackages = new(frameworkDependencies.Select(d => d.name), StringComparer.OrdinalIgnoreCase);

                var topLevelLibraries = new List<LockFileTargetLibrary>();

                foreach (var name in expandedPackages)
                {
                    if (!libraries.TryGetValue(name, out var topLibrary))
                    {
                        continue;
                    }

                    topLevelLibraries.Add(topLibrary);
                }

                var topLevelLibrariesSorted = topLevelLibraries.OrderByDescending(l => l.Type);

                string dependenciesHash = string.Join(",", topLevelLibrariesSorted.Select(t => $"{t.Name}/{t.Version}"));
                if (targetsByDependencyHash.TryGetValue(dependenciesHash, out var existingFolder))
                {
                    existingFolder.Name = $"{existingFolder.Name}; {shortFrameworkName}";
                    continue;
                }

                targetsByDependencyHash[dependenciesHash] = frameworkNode;

                foreach (var topLibrary in topLevelLibrariesSorted)
                {
                    var dependency = frameworkDependencies.FirstOrDefault(d => string.Equals(d.name, topLibrary.Name, StringComparison.OrdinalIgnoreCase)).version;
                    (TreeNode topLevelNode, SearchResult match) = CreateNode(lockFile, dependency, topLibrary, expand, matcher);

                    bool added = AddDependencies(lockFile, topLibrary.Name, topLevelNode, libraries, expandedPackages, matcher);
                    if (match != null || added)
                    {
                        frameworkNode.AddChild(topLevelNode);
                        addedAnything = true;
                    }
                }

                if (addedAnything)
                {
                    project.AddChild(frameworkNode);
                }
            }

            addedAnything |= PopulatePackageContents(project, lockFile, matcher);

            if (!addedAnything)
            {
                return;
            }

            PopulatePackageFolders(project, lockFile);
            PopulateLogs(project, lockFile);
        }

        private bool AddDependencies(
            LockFile lockFile,
            string id,
            TreeNode dependencyNode,
            Dictionary<string, LockFileTargetLibrary> libraries,
            HashSet<string> expandedPackages,
            NodeQueryMatcher matcher)
        {
            if (!libraries.TryGetValue(id, out var library))
            {
                return false;
            }

            bool result = false;
            bool expand = matcher.Terms.Count > 0;

            var dependencyLibraries = new List<LockFileTargetLibrary>();

            foreach (var name in library.Dependencies.Select(d => d.Id))
            {
                if (!libraries.TryGetValue(name, out var dependencyLibrary))
                {
                    continue;
                }

                dependencyLibraries.Add(dependencyLibrary);
            }

            var dependencyLibrariesSorted = dependencyLibraries.OrderByDescending(l => l.Type);

            HashSet<LockFileTargetLibrary> needToAddChildren = new();

            foreach (var dependencyLibrary in dependencyLibrariesSorted)
            {
                if (expandedPackages.Add(dependencyLibrary.Name))
                {
                    needToAddChildren.Add(dependencyLibrary);
                }
            }

            foreach (var dependencyLibrary in dependencyLibrariesSorted)
            {
                var dependency = library.Dependencies
                    .FirstOrDefault(d => string.Equals(d.Id, dependencyLibrary.Name, StringComparison.OrdinalIgnoreCase))
                    .VersionRange
                    .ToString();
                var (node, match) = CreateNode(lockFile, dependency, dependencyLibrary, expand, matcher);

                bool added = false;
                if (needToAddChildren.Contains(dependencyLibrary))
                {
                    added = AddDependencies(lockFile, dependencyLibrary.Name, node, libraries, expandedPackages, matcher);
                }

                if (match != null || added)
                {
                    dependencyNode.AddChild(node);
                    result = true;
                }
            }

            return result;
        }

        private bool PopulatePackageContents(Project project, LockFile lockFile, NodeQueryMatcher matcher)
        {
            bool addedAnything = false;

            if (matcher.Terms.Count == 0)
            {
                return false;
            }

            var nodesByTarget = new Dictionary<string, (List<string> targets, TreeNode node)>(StringComparer.OrdinalIgnoreCase);

            foreach (var target in lockFile.Targets)
            {
                string targetName = ShortenFrameworkName(target.Name);

                foreach (var package in target.Libraries)
                {
                    if (string.Equals(package.Type, "project", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var lockFileLibrary = lockFile.GetLibrary(package.Name, package.Version);

                    var node = AddPackage(package, matcher, lockFileLibrary);
                    if (node == null)
                    {
                        continue;
                    }

                    string contentHash = StringWriter.GetString(node);
                    if (nodesByTarget.TryGetValue(contentHash, out var existing))
                    {
                        existing.targets.Add(targetName);
                    }
                    else
                    {
                        nodesByTarget[contentHash] = (new List<string> { targetName }, node);
                    }
                }
            }

            if (nodesByTarget.Count == 1)
            {
                var node = nodesByTarget.FirstOrDefault().Value.node;
                node.IsExpanded = true;
                project.AddChild(node);
                addedAnything = true;
            }
            else
            {
                foreach (var kvp in nodesByTarget)
                {
                    var folderName = "Packages for " + string.Join("; ", kvp.Value.targets);
                    var folder = project.GetOrCreateNodeWithName<Folder>(folderName);
                    var node = kvp.Value.node;
                    folder.IsExpanded = true;
                    folder.AddChild(node);
                    addedAnything = true;
                }
            }

            return addedAnything;
        }

        private SearchResult IsMatch(LockFileTargetLibrary package, NodeQueryMatcher matcher)
        {
            var match = matcher.IsMatch(package.Name, package.Version.ToString());
            if (match != null)
            {
                return match;
            }

            foreach (var dependency in package.Dependencies)
            {
                match = matcher.IsMatch(dependency.Id, dependency.VersionRange.ToString());
                if (match != null)
                {
                    return match;
                }
            }

            match =
                IsMatch(package.Build, matcher) ??
                IsMatch(package.BuildMultiTargeting, matcher) ??
                IsMatch(package.FrameworkAssemblies, matcher) ??
                IsMatch(package.CompileTimeAssemblies, matcher) ??
                IsMatch(package.ContentFiles.OfType<LockFileItem>().ToArray(), matcher) ??
                IsMatch(package.EmbedAssemblies, matcher) ??
                IsMatch(package.NativeLibraries, matcher) ??
                IsMatch(package.ResourceAssemblies, matcher) ??
                IsMatch(package.RuntimeAssemblies, matcher) ??
                IsMatch(package.RuntimeTargets.OfType<LockFileItem>().ToArray(), matcher) ??
                IsMatch(package.ToolsAssemblies, matcher) ??
                IsMatch(package.FrameworkReferences, matcher);

            return match;
        }

        private SearchResult IsMatch(IList<LockFileItem> list, NodeQueryMatcher matcher)
        {
            if (list == null || list.Count == 0)
            {
                return null;
            }

            foreach (var item in list)
            {
                var match = matcher.IsMatch(item.Path);
                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }

        private SearchResult IsMatch(IList<string> list, NodeQueryMatcher matcher)
        {
            if (list == null || list.Count == 0)
            {
                return null;
            }

            foreach (var item in list)
            {
                var match = matcher.IsMatch(item);
                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }

        private TreeNode AddPackage(LockFileTargetLibrary package, NodeQueryMatcher matcher, LockFileLibrary lockFileLibrary)
        {
            var packageNode = new Package
            {
                Name = package.Name,
                Version = package.Version.ToString()
            };
            TreeNode node = packageNode;

            bool expand = false;
            bool hasMatch = false;

            (node, var match) = WrapWithProxy(node, matcher, packageNode.Name, packageNode.Version);
            hasMatch |= match != null;

            if (package.Dependencies.Count > 0)
            {
                var folder = new Folder { Name = "Dependencies" };
                node.AddChild(folder);

                foreach (var item in package.Dependencies)
                {
                    packageNode = new Package
                    {
                        Name = item.Id, VersionSpec = item.VersionRange.ToString()
                    };
                    TreeNode itemNode = packageNode;

                    (itemNode, match) = WrapWithProxy(itemNode, matcher, packageNode.Name, packageNode.VersionSpec);
                    if (match != null)
                    {
                        folder.IsExpanded = true;
                        expand = true;
                        hasMatch = true;
                    }

                    folder.AddChild(itemNode);
                }
            }

            HashSet<string> files = new(lockFileLibrary.Files, StringComparer.OrdinalIgnoreCase);
            files.Remove(".nupkg.metadata");
            files.Remove(".signature.p7s");
            files.Remove(package.Name + ".nuspec");
            files.RemoveWhere(f => f.EndsWith(".nupkg.sha512", StringComparison.OrdinalIgnoreCase));

            expand |= AddItems(node, matcher, files, package.Build, "build");
            expand |= AddItems(node, matcher, files, package.BuildMultiTargeting, "buildMultitargeting");
            expand |= AddItems(node, matcher, files, package.FrameworkAssemblies, "frameworkAssemblies");
            expand |= AddItems(node, matcher, files, package.CompileTimeAssemblies, "compile");
            expand |= AddItems(node, matcher, files, package.ContentFiles.OfType<LockFileItem>().ToArray(), "contentFiles");
            expand |= AddItems(node, matcher, files, package.EmbedAssemblies, "embed");
            expand |= AddItems(node, matcher, files, package.NativeLibraries, "native");
            expand |= AddItems(node, matcher, files, package.ResourceAssemblies, "resource");
            expand |= AddItems(node, matcher, files, package.RuntimeAssemblies, "runtime");
            expand |= AddItems(node, matcher, files, package.RuntimeTargets.OfType<LockFileItem>().ToArray(), "runtimeTargets");
            expand |= AddItems(node, matcher, files, package.ToolsAssemblies, "tools");
            expand |= AddItems(node, matcher, files, package.FrameworkReferences, "frameworkReferences");
            expand |= AddItems(node, matcher, files, files.ToArray(), "Files");

            if (expand)
            {
                node.IsExpanded = true;
                hasMatch = true;
            }

            if (!hasMatch)
            {
                return null;
            }

            return node;
        }

        private bool AddItems(TreeNode node, NodeQueryMatcher matcher, HashSet<string> files, IList<string> items, string itemName)
        {
            if (items == null || items.Count == 0)
            {
                return false;
            }

            var folder = new Folder { Name = itemName };
            node.AddChild(folder);

            foreach (var item in items)
            {
                TreeNode itemNode = new Item { Name = item };
                files.Remove(item);

                (itemNode, var match) = WrapWithProxy(itemNode, matcher, item);
                if (match != null)
                {
                    folder.IsExpanded = true;
                }

                folder.AddChild(itemNode);
            }

            return folder.IsExpanded;
        }

        private bool AddItems(TreeNode node, NodeQueryMatcher matcher, HashSet<string> files, IList<LockFileItem> items, string itemName)
        {
            if (items == null || items.Count == 0)
            {
                return false;
            }

            var folder = new Folder { Name = itemName };
            node.AddChild(folder);

            foreach (var item in items)
            {
                TreeNode itemNode = new Item { Name = item.Path };
                files.Remove(item.Path);

                (itemNode, var match) = WrapWithProxy(itemNode, matcher, item.Path);
                if (match != null)
                {
                    folder.IsExpanded = true;
                }

                if (item.Properties is { } properties && properties.Count > 0)
                {
                    foreach (var property in properties)
                    {
                        var metadata = new Metadata { Name = property.Key, Value = property.Value };
                        itemNode.AddChild(metadata);
                    }
                }

                folder.AddChild(itemNode);
            }

            return folder.IsExpanded;
        }

        private void PopulateLogs(Project project, LockFile lockFile)
        {
            foreach (var logMessage in lockFile.LogMessages)
            {
                string text = logMessage.Message;

                TextNode node;
                if (logMessage.Level == NuGet.Common.LogLevel.Error)
                {
                    node = new Error
                    {
                        Code = logMessage.Code.ToString()
                    };
                }
                else if (logMessage.Level == NuGet.Common.LogLevel.Warning)
                {
                    node = new Warning
                    {
                        Code = logMessage.Code.ToString()
                    };
                }
                else
                {
                    node = new MessageWithLocation();
                    text = $"{logMessage.Code}: {text}";
                }

                node.Text = text;

                project.AddChild(node);
            }
        }

        private static void PopulatePackageFolders(Project project, LockFile lockFile)
        {
            var packageFoldersNode = new Folder
            {
                Name = "PackageFolders",
                IsLowRelevance = true
            };
            foreach (var packageFolder in lockFile.PackageFolders)
            {
                var item = new Item { Name = packageFolder.Path };
                packageFoldersNode.AddChild(item);
            }

            if (packageFoldersNode.HasChildren)
            {
                project.AddChild(packageFoldersNode);
            }
        }

        private (TreeNode node, SearchResult match) CreateNode(
            LockFile lockFile,
            string dependency,
            LockFileTargetLibrary library,
            bool expand,
            NodeQueryMatcher matcher)
        {
            string name = library.Name;
            string version = library.Version.ToString();

            TreeNode node;
            string[] fields;

            if (string.Equals(library.Type, "project", StringComparison.OrdinalIgnoreCase))
            {
                var libraryInfo = lockFile.GetLibrary(name, library.Version);
                name = Path.GetFileName(libraryInfo.MSBuildProject);
                node = new Project
                {
                    Name = name,
                    ProjectFile = libraryInfo.MSBuildProject,
                    IsExpanded = expand
                };
                fields = new[] { name };
            }
            else
            {
                var package = new Package
                {
                    Name = name,
                    Version = version,
                    VersionSpec = dependency,
                    IsExpanded = expand
                };
                node = package;
                fields = new[] { name, version, dependency };
            }

            var result = WrapWithProxy(node, matcher, fields);
            return result;
        }

        private (TreeNode node, SearchResult match) WrapWithProxy(TreeNode original, NodeQueryMatcher matcher, params string[] fields)
        {
            var match = matcher.IsMatch(fields);
            if (match == null || match == SearchResult.EmptyQueryMatch)
            {
                return (original, match);
            }

            match.FieldsToDisplay = fields;

            var proxy = new ProxyNode();
            proxy.Original = original;
            proxy.Populate(match);
            proxy.Text = original.ToString();
            proxy.IsExpanded = original.IsExpanded;

            return (proxy, match);
        }

        private (string name, string version) ParsePackageId(string dependency)
        {
            return dependency.GetFirstAndRest(' ');
        }

        private IReadOnlyList<AssetsFile> FindAssetsFiles(NodeQueryMatcher underProjectMatcher, int maxResults)
        {
            var files = new List<AssetsFile>();
            foreach (var assetFile in assetsFiles)
            {
                foreach (var term in underProjectMatcher.Terms)
                {
                    if (term.IsMatch(assetFile.ProjectFilePath) ||
                        term.IsMatch(assetFile.AssetsFilePath))
                    {
                        files.Add(assetFile);
                        if (files.Count >= maxResults)
                        {
                            return files;
                        }
                    }
                }
            }

            return files;
        }

        private void PopulateAssetsFiles()
        {
            if (assetsFiles != null)
            {
                return;
            }

            assetsFiles = new List<AssetsFile>();

            if (Build.SourceFiles == null || Build.SourceFiles.Count == 0)
            {
                return;
            }

            foreach (var file in Build.SourceFiles)
            {
                if (!file.FullPath.EndsWith("project.assets.json", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var assetFile = new AssetsFile
                {
                    AssetsFilePath = file.FullPath,
                    Text = file.Text
                };

                var match = projectFilePathRegex.Match(file.Text);
                if (!match.Success)
                {
                    continue;
                }

                string projectFilePath = match.Groups["Path"].Value;
                if (string.IsNullOrEmpty(projectFilePath))
                {
                    continue;
                }

                projectFilePath = projectFilePath.Replace(@"\\", @"\");

                assetFile.ProjectFilePath = projectFilePath;

                assetsFiles.Add(assetFile);
            }
        }

        private string ShortenFrameworkName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return name;
            }

            int slash = name.IndexOf('/');
            if (slash >= 0)
            {
                string framework = name.Substring(0, slash);
                string suffix = name.Substring(slash + 1);

                name = ParseFrameworkName(framework) + "/" + suffix;
            }
            else
            {
                name = ParseFrameworkName(name);
            }

            return name;
        }

        private static string ParseFrameworkName(string name)
        {
            try
            {
                var parsed = NuGetFramework.Parse(name);
                return parsed?.GetShortFolderName() ?? name;
            }
            catch
            {
                return name;
            }
        }
    }
}