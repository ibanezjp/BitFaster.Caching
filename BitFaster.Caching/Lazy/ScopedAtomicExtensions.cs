﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitFaster.Caching
{
    public static class ScopedAtomicExtensions
    {
        // TODO: GetOrAddLifetime?
        public static AtomicLifetime<K, V> GetOrAdd<K, V>(this ICache<K, ScopedAtomic<K, V>> cache, K key, Func<K, V> valueFactory) where V : IDisposable
        {
            while (true)
            {
                var scope = cache.GetOrAdd(key, _ => new ScopedAtomic<K, V>());

                if (scope.TryCreateLifetime(key, valueFactory, out var lifetime))
                {
                    return lifetime;
                }
            }
        }

        public static void AddOrUpdate<K, V>(this ICache<K, ScopedAtomic<K, V>> cache, K key, V value) where V : IDisposable
        {
            cache.AddOrUpdate(key, new ScopedAtomic<K, V>(value));
        }

        public static bool TryUpdate<K, V>(this ICache<K, ScopedAtomic<K, V>> cache, K key, V value) where V : IDisposable
        {
            return cache.TryUpdate(key, new ScopedAtomic<K, V>(value));
        }

        // TODO: TryGetLifetime?
        public static bool TryGetLifetime<K, V>(this ICache<K, ScopedAtomic<K, V>> cache, K key, out AtomicLifetime<K, V> value) where V : IDisposable
        {
            if (cache.TryGet(key, out var scoped))
            {
                if (scoped.TryCreateLifetime(out var lifetime))
                {
                    value = lifetime;
                    return true;
                }
            }   
            
            value = default;
            return false;
        }
    }
}
