// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Reflection;
using BenchmarkDotNet.Attributes;
using Microsoft.Build.Globbing;
using Touki.Io;

namespace touki.perf;

[MemoryDiagnoser]
public class MsBuildEnumeratePerf
{
    private const string Directory = @"n:\repos\runtime\";
    // private const string Filespec = "**/*.cs";
    private const string Filespec = "**/src/**/*.cs";

    [Benchmark(Baseline = true)]
    public IReadOnlyList<string> MSBuild()
    {
        var results = FileMatcherWrapper.GetFilesSimple(Directory, Filespec);
        return results;
    }

    [Benchmark]
    public IReadOnlyList<string> MsBuildEnumerator()
    {
        using MSBuildEnumerator enumerator = MSBuildEnumerator.Create(Directory, Filespec);
        List<string> results = [];
        while (enumerator.MoveNext())
        {
            results.Add(enumerator.Current);
        }

        return results;
    }

    /// <summary>
    ///  Provides access to internal FileMatcher functionality through reflection.
    /// </summary>
    public static class FileMatcherWrapper
    {
        // Enum to match internal SearchAction enum
        public enum SearchAction
        {
            RunSearch = 0,
            StopSearching = 1,
            IncludeAllFiles = 2
        }

        /// <summary>
        /// Result of the GetFiles method containing all returned information.
        /// </summary>
        public readonly struct GetFilesResult
        {
            /// <summary>List of files found</summary>
            public string[] FileList { get; }

            /// <summary>Action returned by the search</summary>
            public SearchAction Action { get; }

            /// <summary>Exclude file specification if any</summary>
            public string ExcludeFileSpec { get; }

            /// <summary>Glob failure message if any</summary>
            public string GlobFailure { get; }

            internal GetFilesResult(string[] fileList, SearchAction action, string excludeFileSpec, string globFailure)
            {
                FileList = fileList;
                Action = action;
                ExcludeFileSpec = excludeFileSpec;
                GlobFailure = globFailure;
            }
        }

        // Cache the reflected members for performance
        private static readonly FieldInfo s_defaultFieldInfo;
        private static readonly MethodInfo s_getFilesMethodInfo;
        private static readonly object s_defaultInstance;
        private static readonly Type s_searchActionType;

        // Cache the return type information
        private static readonly Type s_returnTupleType;
        private static readonly FieldInfo s_fileListField;
        private static readonly FieldInfo s_searchActionField;
        private static readonly FieldInfo s_excludeFileSpecField;
        private static readonly FieldInfo s_globFailureField;

#pragma warning disable CA1810 // Initialize reference type static fields inline
        static FileMatcherWrapper()
#pragma warning restore CA1810 // Initialize reference type static fields inline
        {
            try
            {
                // Get the FileMatcher type
                Type fileMatcherType = typeof(MSBuildGlob).Assembly.GetType("Microsoft.Build.Shared.FileMatcher")
                    ?? throw new InvalidOperationException("Could not find FileMatcher type");

                // Find the SearchAction enum type
                s_searchActionType = fileMatcherType.Assembly.GetType("Microsoft.Build.Shared.SearchAction")
                    ?? fileMatcherType.Assembly.GetType("Microsoft.Build.Shared.FileMatcher+SearchAction")!;

                if (s_searchActionType is null)
                {
                    throw new InvalidOperationException("Could not find SearchAction enum type");
                }

                // Get the Default static field
                s_defaultFieldInfo = fileMatcherType.GetField("Default",
                    BindingFlags.Public | BindingFlags.Static)!;

                if (s_defaultFieldInfo is null)
                {
                    throw new InvalidOperationException("Could not find Default field on FileMatcher");
                }

                // Get the Default instance
                s_defaultInstance = s_defaultFieldInfo.GetValue(null)!;

                if (s_defaultInstance is null)
                {
                    throw new InvalidOperationException("Default instance of FileMatcher is null");
                }

                // Get the GetFiles method
                s_getFilesMethodInfo = fileMatcherType.GetMethod("GetFiles",
                    BindingFlags.NonPublic | BindingFlags.Instance,
                    null,
                    [typeof(string), typeof(string), typeof(List<string>)],
                    null)!;

                if (s_getFilesMethodInfo is null)
                {
                    throw new InvalidOperationException(
                        "Could not find GetFiles(string, string, List<string>) method on FileMatcher");
                }

                // Cache the return type information
                s_returnTupleType = s_getFilesMethodInfo.ReturnType;

                // Cache tuple item property getters
                s_fileListField = s_returnTupleType.GetField("Item1") ??
                    throw new InvalidOperationException("Could not find Item1 property on return tuple");

                s_searchActionField = s_returnTupleType.GetField("Item2") ??
                    throw new InvalidOperationException("Could not find Item2 property on return tuple");

                s_excludeFileSpecField = s_returnTupleType.GetField("Item3") ??
                    throw new InvalidOperationException("Could not find Item3 property on return tuple");

                s_globFailureField = s_returnTupleType.GetField("Item4") ??
                    throw new InvalidOperationException("Could not find Item4 property on return tuple");
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    "Failed to initialize FileMatcher reflection wrapper", ex);
            }
        }

        /// <summary>
        ///  Wrapper for the internal FileMatcher.GetFiles method using the Default instance.
        /// </summary>
        /// <param name="directoryPath">The root directory to search in</param>
        /// <param name="filespec">The file specification (glob pattern)</param>
        /// <returns>Result object containing files and additional information</returns>
        public static GetFilesResult GetFiles(string directoryPath, string filespec)
        {
#pragma warning disable CA1510 // Use ArgumentNullException throw helper
            if (directoryPath is null)
                throw new ArgumentNullException(nameof(directoryPath));
            if (filespec is null)
                throw new ArgumentNullException(nameof(filespec));
#pragma warning restore CA1510 // Use ArgumentNullException throw helper

            try
            {
                // Invoke the method and get the tuple return value
                object? returnValue = s_getFilesMethodInfo.Invoke(s_defaultInstance, [directoryPath, filespec, null])
                    ?? throw new InvalidOperationException("GetFiles method returned null");

                // Extract tuple values using cached property info

                // Get Item1 (FileList) - the actual results
                string[] fileList = (string[])s_fileListField.GetValue(returnValue)!;

                // Get Item2 (SearchAction)
                object actionValue = s_searchActionField.GetValue(returnValue)!;
                SearchAction action = (SearchAction)Enum.ToObject(typeof(SearchAction), Convert.ToInt32(actionValue));

                // Get Item3 (ExcludeFileSpec)
                string excludeFileSpec = (string?)s_excludeFileSpecField.GetValue(returnValue) ?? string.Empty;

                // Get Item4 (GlobFailure)
                string globFailure = (string?)s_globFailureField.GetValue(returnValue) ?? string.Empty;

                return new GetFilesResult(fileList, action, excludeFileSpec, globFailure);
            }
            catch (TargetInvocationException tie)
            {
                // Unwrap the inner exception
                if (tie.InnerException != null)
                {
                    throw tie.InnerException;
                }

                throw;
            }
        }

        /// <summary>
        /// Simplified version that returns just the file list.
        /// </summary>
        public static string[] GetFilesSimple(string directoryPath, string filespec)
        {
            return GetFiles(directoryPath, filespec).FileList;
        }

        /// <summary>
        /// Checks if FileMatcher reflection initialization was successful.
        /// </summary>
        public static bool IsAvailable => s_getFilesMethodInfo != null && s_defaultInstance != null;
    }
}
