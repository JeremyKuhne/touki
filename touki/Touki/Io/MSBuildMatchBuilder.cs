// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

#pragma warning disable IDE0005 // Using directive is unnecessary.
using Touki.Collections;
#pragma warning restore IDE0005 // Using directive is unnecessary.

namespace Touki.Io;

/// <summary>
///  Helpers for working with MSBuild formatted strings.
/// </summary>
public static class MSBuildMatchBuilder
{
    // In a default .NET library project, here are the default ItemExcludes that are applied to the project.
    // When looking for all *.cs files only TWO of these are relevant (exclude bin and obj).
    // 
    // DefaultItemExcludes =
    //
    //  bin\Debug\/**;
    //  obj\Debug\/**;
    //  bin\/**;
    //  obj\/**;
    //  **/*.user;
    //  **/*.*proj;
    //  **/*.sln;
    //  **/*.slnx;
    //  **/*.vssscc;
    //  **/.DS_Store
    //
    // Ideally we'd dedupe some of these.

    // If there are no wildcards in the include or exclude specification, they are resolved as paths and excluded
    // or included as is. As such we'll need to process them separately.

    // Include and exclude specifications are expected to have had properties replaced before arriving at this method.

    // State questions:
    //
    // Are all specs under the project path?
    //   Yes? - Single pass
    //   No? - Multiple passes for each specification


}
