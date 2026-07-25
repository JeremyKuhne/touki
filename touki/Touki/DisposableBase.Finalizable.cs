// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki;

public abstract partial class DisposableBase
{
    /// <summary>
    ///  A derived class of <see cref="DisposableBase"/> that includes a finalizer to ensure resources
    ///  are released when the object is garbage collected if the consumer fails to call <see cref="Dispose()"/>.
    /// </summary>
    public abstract class Finalizable : DisposableBase
    {
        /// <summary>
        ///  Finalizes an instance of the <see cref="Finalizable"/> class.
        /// </summary>
        ~Finalizable() => DisposeInternal(disposing: false);
    }
}
