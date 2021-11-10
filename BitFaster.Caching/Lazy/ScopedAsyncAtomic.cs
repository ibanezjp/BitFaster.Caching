﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BitFaster.Caching
{
    // Enable caching an AsyncLazy disposable object - guarantee single instance, safe disposal
    public class ScopedAsyncAtomic<K, V> : IDisposable 
        where V : IDisposable
    {
        private ReferenceCount<AsyncAtomic<K, V>> refCount;
        private bool isDisposed;

        private readonly AsyncAtomic<K, V> asyncAtomic;

        public ScopedAsyncAtomic()
        {
            this.asyncAtomic = new AsyncAtomic<K, V>();
        }

        public ScopedAsyncAtomic(V value)
        {
            this.asyncAtomic = new AsyncAtomic<K, V>(value);
        }

        public bool TryCreateLifetime(out AsyncAtomicLifetime<K, V> lifetime)
        {
            if (!this.refCount.Value.IsValueCreated)
            {
                lifetime = default;
                return false;
            }

            // TODO: exact dupe
            while (true)
            {
                var oldRefCount = this.refCount;

                // If old ref count is 0, the scoped object has been disposed and there was a race.
                if (this.isDisposed || oldRefCount.Count == 0)
                {
                    lifetime = default;
                    return false;
                }

                if (oldRefCount == Interlocked.CompareExchange(ref this.refCount, oldRefCount.IncrementCopy(), oldRefCount))
                {
                    // When Lifetime is disposed, it calls DecrementReferenceCount
                    lifetime = new AsyncAtomicLifetime<K, V>(oldRefCount, this.DecrementReferenceCount);
                    return true;
                }
            }
        }

        public async Task<(bool succeeded, AsyncAtomicLifetime<K, V> lifetime)> TryCreateLifetimeAsync(K key, Func<K, Task<V>> valueFactory)
        { 
            // initialize - factory can throw so do this before we start counting refs
            await this.asyncAtomic.GetValueAsync(key, valueFactory).ConfigureAwait(false);

            // TODO: exact dupe
            while (true)
            {
                var oldRefCount = this.refCount;
               
                // If old ref count is 0, the scoped object has been disposed and there was a race.
                if (this.isDisposed || oldRefCount.Count == 0)
                { 
                    return (false, default);
                }

                if (oldRefCount == Interlocked.CompareExchange(ref this.refCount, oldRefCount.IncrementCopy(), oldRefCount))
                {
                    // When Lifetime is disposed, it calls DecrementReferenceCount
                    return (true,  new AsyncAtomicLifetime<K, V>(oldRefCount, this.DecrementReferenceCount));
                }
            }
        }

        public async Task<AsyncAtomicLifetime<K, V>> CreateLifetimeAsync(K key, Func<K, Task<V>> valueFactory)
        {
            var result = await TryCreateLifetimeAsync(key, valueFactory).ConfigureAwait(false);

            if (!result.succeeded)
            {
                throw new ObjectDisposedException($"{nameof(V)} is disposed.");
            }

            return result.lifetime;
        }

        private void DecrementReferenceCount()
        {
            while (true)
            {
                var oldRefCount = this.refCount;

                if (oldRefCount == Interlocked.CompareExchange(ref this.refCount, oldRefCount.DecrementCopy(), oldRefCount))
                {
                    if (this.refCount.Count == 0 && this.refCount.Value.IsValueCreated)
                    {
                        this.refCount.Value.ValueIfCreated?.Dispose();
                    }

                    break;
                }
            }
        }

        public void Dispose()
        {
            if (!this.isDisposed)
            {
                this.DecrementReferenceCount();
                this.isDisposed = true;
            }
        }
    }
}
