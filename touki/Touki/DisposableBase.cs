// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Threading;

namespace Touki;

/// <summary>
///  Base class for implementing <see cref="IDisposable"/> with double disposal protection.
/// </summary>
public abstract class DisposableBase : IDisposable
{
    private int _disposedValue;

    /// <summary>
    ///  Gets a value indicating whether the object has been disposed.
    /// </summary>
    /// <value>
    ///  <see langword="true"/> if the object has been disposed; otherwise, <see langword="false"/>.
    /// </value>
    protected bool Disposed => _disposedValue != 0;

    /// <summary>
    ///  Called when the component is being disposed or finalized.
    /// </summary>
    /// <param name="disposing">
    ///  <see langword="false"/> if called via a destructor on the finalizer queue. Do not access object fields
    ///  unless <see langword="true"/>.
    /// </param>
    protected abstract void Dispose(bool disposing);

    private void DisposeInternal(bool disposing)
    {
        // Want to ensure both paths are guarded against double disposal.
        if (Interlocked.Exchange(ref _disposedValue, 1) == 1)
        {
            return;
        }

        Dispose(disposing);
    }

    /// <summary>
    ///  Disposes the component.
    /// </summary>
    public void Dispose()
    {
        DisposeInternal(disposing: true);
        GC.SuppressFinalize(this);
    }

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
