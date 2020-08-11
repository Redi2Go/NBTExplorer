using System;
using System.Collections.Generic;

namespace NBTUtil.Utils
{
    class Utils
    {
        public static K GetKeyByValue<K, V>(Dictionary<K, V> dictionary, V value, K defaultKeyValue)
        {
            V dictionaryValue;
            foreach (K key in dictionary.Keys)
            {
                dictionary.TryGetValue(key, out dictionaryValue);

                if (dictionaryValue.Equals(value))
                    return key;
            }

            return defaultKeyValue;
        }
    }
}
