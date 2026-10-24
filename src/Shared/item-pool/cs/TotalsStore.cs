// Library copy of shared/item-pool 1.0.0. Do not edit: see src/Shared/item-pool/VERSION.
using System;

namespace SlShared.ItemPool
{
    /// <summary>
    /// Holds the item totals of the last calculation, and the dirty bit that an item change sets.
    ///
    /// The cost sits on an item change and never on a hover: the invalidate patches set the bit, the tick
    /// calculates at most once for a frame, and the push goes out only when the data version moved. The two windows
    /// that build their text in C# do not wait for the tick, so they read through Read, which calculates first when
    /// the bit is set.
    /// </summary>
    internal sealed class TotalsStore
    {
        private TotalSet _current = TotalSet.Empty;
        private bool _dirty = true;
        private int _version;

        /// <summary>True while an item changed since the last calculation.</summary>
        public bool IsDirty => _dirty;

        /// <summary>The item totals of the last calculation.</summary>
        public TotalSet Current => _current;

        /// <summary>
        /// The version of the data that the store holds. It moves only when a calculation gave other totals, so the
        /// push can tell whether the web page already holds this data.
        /// </summary>
        public int Version => _version;

        /// <summary>An item changed, so the next calculation reads the pool again.</summary>
        public void MarkDirty()
        {
            _dirty = true;
        }

        /// <summary>
        /// Calculates the item totals again when the bit is set, at most once for each call. True when it
        /// calculated. A failed calculation leaves the bit set, so the next tick tries again.
        /// </summary>
        public bool Refresh(Func<TotalSet> read)
        {
            if (!_dirty) return false;
            var next = read();
            if (next == null) next = TotalSet.Failure;
            _dirty = next.Failed;
            if (!next.SameAs(_current))
            {
                _current = next;
                _version++;
            }
            return true;
        }

        /// <summary>The item totals now, calculated again first when the bit is set.</summary>
        public TotalSet Read(Func<TotalSet> read)
        {
            Refresh(read);
            return _current;
        }
    }
}
