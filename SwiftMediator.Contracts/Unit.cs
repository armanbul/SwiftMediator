#nullable enable

using System;

namespace SwiftMediator.Core
{
    /// <summary>
    /// Represents a unit of execution that returns no value.
    /// </summary>
    public readonly struct Unit : IEquatable<Unit>
    {
        /// <summary>The singleton value of <see cref="Unit"/>.</summary>
        public static readonly Unit Value = new Unit();

        /// <inheritdoc/>
        public bool Equals(Unit other) => true;
        /// <inheritdoc/>
        public override bool Equals(object? obj) => obj is Unit;
        /// <inheritdoc/>
        public override int GetHashCode() => 0;
        /// <inheritdoc/>
        public static bool operator ==(Unit left, Unit right) => true;
        /// <inheritdoc/>
        public static bool operator !=(Unit left, Unit right) => false;
    }
}
