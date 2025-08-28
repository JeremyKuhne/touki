// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Xunit.Abstractions;

namespace Touki.Io;

internal sealed class EnumeratorMock
{
    private sealed class DirectoryNode
    {
        public Dictionary<string, DirectoryNode> Directories { get; } = [];
        public List<string> Files { get; } = [];
    }

    private readonly string _root;
    private readonly MatchMSBuild _spec;
    private readonly ITestOutputHelper _log;
    private readonly DirectoryNode _rootNode = new();
    private readonly List<string> _included = [];

    public EnumeratorMock(string root, IEnumerable<string> files, MatchMSBuild spec, ITestOutputHelper log)
    {
        _root = root;
        _spec = spec;
        _log = log;
        _log.WriteLine($"Evaluating {spec._spec} in {root}");
        foreach (string file in files)
        {
            AddFile(file.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar));
        }
    }

    private void AddFile(string relativePath)
    {
        DirectoryNode current = _rootNode;
        ReadOnlySpan<char> remaining = relativePath.AsSpan();
        while (true)
        {
            int separatorIndex = remaining.IndexOf(Path.DirectorySeparatorChar);
            if (separatorIndex < 0)
            {
                current.Files.Add(remaining.ToString());
                break;
            }

            string segment = remaining[..separatorIndex].ToString();
            if (!current.Directories.TryGetValue(segment, out DirectoryNode? next))
            {
                next = new DirectoryNode();
                current.Directories[segment] = next;
            }

            current = next;
            remaining = remaining[(separatorIndex + 1)..];
        }
    }

    public IReadOnlyList<string> Enumerate()
    {
        Queue<(DirectoryNode Node, string Path)> directoryQueue = new();
        directoryQueue.Enqueue((_rootNode, _root));

        while (directoryQueue.Count > 0)
        {
            (DirectoryNode currentNode, string currentPath) = directoryQueue.Dequeue();

            // Process files in current directory
            foreach (string file in currentNode.Files)
            {
                _log.WriteLine($"Considering {Path.Join(currentPath, file)}:");
                if (_spec.MatchesFile(currentPath.AsSpan(), file.AsSpan()))
                {
                    _log.WriteLine("\tmatches");
                    _included.Add(Path.GetRelativePath(_root, Path.Join(currentPath, file)));
                }
                else
                {
                    _log.WriteLine("\tdoes not match");
                }
            }

            // Add subdirectories to queue
            foreach (KeyValuePair<string, DirectoryNode> pair in currentNode.Directories)
            {
                string name = pair.Key;
                DirectoryNode child = pair.Value;
                _log.WriteLine($"Considering {Path.Join(currentPath, name)}:");
                if (_spec.MatchesDirectory(currentPath.AsSpan(), name.AsSpan()))
                {
                    _log.WriteLine("\tqueued");
                    directoryQueue.Enqueue((child, Path.Join(currentPath, name)));
                }
                else
                {
                    _log.WriteLine("\tskipped");
                }
            }

            _spec.DirectoryFinished();
        }

        return _included;
    }
}
