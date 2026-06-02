// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Reflection;
using Microsoft.Build.FileSystem;
using Microsoft.Build.Globbing;

namespace Touki.Io;

/// <summary>
///  Provides access to internal FileMatcher functionality through reflection.
/// </summary>
public static class FileMatcherWrapper
{
    /// <summary>
    ///  Mirror of MSBuild's internal <c>FileMatcher.SearchAction</c> enum. The integer values match
    ///  MSBuild's enum so the reflection-based mapping in <see cref="GetFiles(string, string, List{string}?)"/>
    ///  works by direct int cast.
    /// </summary>
    public enum SearchAction
    {
        /// <summary>No action.</summary>
        None = 0,

        /// <summary>Run the file-system search.</summary>
        RunSearch = 1,

        /// <summary>Return the file specification verbatim.</summary>
        ReturnFileSpec = 2,

        /// <summary>Return an empty list.</summary>
        ReturnEmptyList = 3,

        /// <summary>Fail because the wildcard would enumerate a drive root.</summary>
        FailOnDriveEnumeratingWildcard = 4,

        /// <summary>Log that the wildcard would enumerate a drive root.</summary>
        LogDriveEnumeratingWildcard = 5,
    }

    /// <summary>
    ///  Result of the GetFiles method containing all returned information.
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

    // Used to build a FileMatcher over an injected file system.
    private static readonly ConstructorInfo s_fileSystemConstructor;

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
            _ = fileMatcherType.Assembly.GetType("Microsoft.Build.Shared.SearchAction")
                ?? fileMatcherType.Assembly.GetType("Microsoft.Build.Shared.FileMatcher+SearchAction")
                ?? throw new InvalidOperationException("Could not find SearchAction enum type");

            // Get the Default static field
            s_defaultFieldInfo = fileMatcherType.GetField(
                "Default",
                BindingFlags.Public | BindingFlags.Static)
                ?? throw new InvalidOperationException("Could not find Default field on FileMatcher");

            // Get the Default instance
            s_defaultInstance = s_defaultFieldInfo.GetValue(null)
                ?? throw new InvalidOperationException("Default instance of FileMatcher is null");

            // Find the internal IFileSystem type and the constructor that accepts it, so a matcher
            // can be built over an injected (recording or playback) file system.
            Type fileSystemType = fileMatcherType.Assembly.GetType("Microsoft.Build.Shared.FileSystem.IFileSystem")
                ?? throw new InvalidOperationException("Could not find IFileSystem type");

            s_fileSystemConstructor = fileMatcherType.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(c =>
                {
                    ParameterInfo[] parameters = c.GetParameters();
                    return parameters.Length >= 1 && parameters[0].ParameterType == fileSystemType;
                })
                ?? throw new InvalidOperationException("Could not find FileMatcher(IFileSystem, ...) constructor");

            // Get the GetFiles method
            s_getFilesMethodInfo = fileMatcherType.GetMethod("GetFiles",
                BindingFlags.NonPublic | BindingFlags.Instance,
                null,
                [typeof(string), typeof(string), typeof(List<string>)],
                null)
                ?? throw new InvalidOperationException(
                    "Could not find GetFiles(string, string, List<string>) method on FileMatcher");

            // Cache the return type information
            s_returnTupleType = s_getFilesMethodInfo.ReturnType;

            // Cache tuple item property getters
            s_fileListField = s_returnTupleType.GetField("Item1")
                ?? throw new InvalidOperationException("Could not find Item1 property on return tuple");

            s_searchActionField = s_returnTupleType.GetField("Item2")
                ?? throw new InvalidOperationException("Could not find Item2 property on return tuple");

            s_excludeFileSpecField = s_returnTupleType.GetField("Item3")
                ?? throw new InvalidOperationException("Could not find Item3 property on return tuple");

            s_globFailureField = s_returnTupleType.GetField("Item4")
                ?? throw new InvalidOperationException("Could not find Item4 property on return tuple");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to initialize FileMatcher reflection wrapper", ex);
        }
    }

    /// <summary>
    ///  Wrapper for the internal FileMatcher.GetFiles method using the Default instance.
    /// </summary>
    /// <param name="directoryPath">The root directory to search in</param>
    /// <param name="filespec">The file specification (glob pattern)</param>
    /// <param name="excludeSpecs">Specs to exclude.</param>
    /// <returns>Result object containing files and additional information</returns>
    public static GetFilesResult GetFiles(string directoryPath, string filespec, List<string>? excludeSpecs = null) =>
        GetFiles(s_defaultInstance, directoryPath, filespec, excludeSpecs);

    /// <summary>
    ///  Builds a FileMatcher over <paramref name="fileSystem"/> and runs <c>GetFiles</c> on it.
    /// </summary>
    /// <param name="directoryPath">The root directory to search in</param>
    /// <param name="filespec">The file specification (glob pattern)</param>
    /// <param name="excludeSpecs">Specs to exclude.</param>
    /// <param name="fileSystem">The file system the matcher queries (recording or playback).</param>
    /// <returns>Result object containing files and additional information</returns>
    public static GetFilesResult GetFiles(
        string directoryPath,
        string filespec,
        List<string>? excludeSpecs,
        MSBuildFileSystemBase fileSystem)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        return GetFiles(CreateMatcher(fileSystem), directoryPath, filespec, excludeSpecs);
    }

    private static GetFilesResult GetFiles(object instance, string directoryPath, string filespec, List<string>? excludeSpecs)
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
            object? returnValue = s_getFilesMethodInfo.Invoke(instance, [directoryPath, filespec, excludeSpecs])
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
            if (tie.InnerException is not null)
            {
                throw tie.InnerException;
            }

            throw;
        }
    }

    /// <summary>
    ///  Creates a FileMatcher instance backed by <paramref name="fileSystem"/>.
    /// </summary>
    public static object CreateMatcher(MSBuildFileSystemBase fileSystem)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        object?[] arguments = new object?[s_fileSystemConstructor.GetParameters().Length];
        arguments[0] = fileSystem;
        return s_fileSystemConstructor.Invoke(arguments);
    }

    /// <summary>
    ///  Simplified version that returns just the file list.
    /// </summary>
    /// <inheritdoc cref="GetFiles(string, string, List{string}?)"/>
    public static string[] GetFilesSimple(string directoryPath, string filespec, List<string>? excludeSpecs = null) =>
        GetFiles(directoryPath, filespec, excludeSpecs).FileList;

    /// <summary>
    ///  Simplified version that returns just the file list, using an injected file system.
    /// </summary>
    /// <inheritdoc cref="GetFiles(string, string, List{string}?, MSBuildFileSystemBase)"/>
    public static string[] GetFilesSimple(
        string directoryPath,
        string filespec,
        List<string>? excludeSpecs,
        MSBuildFileSystemBase fileSystem) =>
        GetFiles(directoryPath, filespec, excludeSpecs, fileSystem).FileList;

    /// <summary>
    ///  Checks if FileMatcher reflection initialization was successful.
    /// </summary>
    public static bool IsAvailable => s_getFilesMethodInfo is not null && s_defaultInstance is not null;
}
