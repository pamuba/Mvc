// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace System.Collections.Generic
{
    internal static class DictionaryExtensions
    {
        /// <summary>
        /// Remove entries from dictionary that match the removeCondition.
        /// </summary>
        public static void RemoveFromDictionary<TKey, TValue, TState>(
            this IDictionary<TKey, TValue> dictionary,
            Func<KeyValuePair<TKey, TValue>, TState, bool> removeCondition,
            TState state)
        {
            // Because it is not possible to delete while enumerating, a copy of the keys must be taken.
            // Use the size of the dictionary as an upper bound to avoid creating more than one copy of the keys.
            var removeCount = 0;
            var keys = new TKey[dictionary.Count];
            foreach (var entry in dictionary)
            {
                if (removeCondition(entry, state))
                {
                    keys[removeCount] = entry.Key;
                    removeCount++;
                }
            }
            for (var i = 0; i < removeCount; i++)
            {
                dictionary.Remove(keys[i]);
            }
        }
    }
}