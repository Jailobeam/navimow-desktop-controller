using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Web.Script.Serialization;

namespace NavimowDesktopController
{
    internal static class JsonUtils
    {
        private static readonly JavaScriptSerializer Serializer = new JavaScriptSerializer();

        public static Dictionary<string, object> ParseObject(string json)
        {
            var result = Serializer.DeserializeObject(json) as Dictionary<string, object>;
            return result ?? new Dictionary<string, object>();
        }

        public static string ToJson(object value)
        {
            return Serializer.Serialize(value);
        }

        public static object[] GetArray(Dictionary<string, object> dictionary, string key)
        {
            object value;
            if (dictionary == null || !dictionary.TryGetValue(key, out value))
            {
                return new object[0];
            }

            var array = value as object[];
            if (array != null)
            {
                return array;
            }

            var list = value as ArrayList;
            if (list != null)
            {
                return list.ToArray();
            }

            return new object[0];
        }

        public static Dictionary<string, object> GetObject(Dictionary<string, object> dictionary, string key)
        {
            object value;
            if (dictionary == null || !dictionary.TryGetValue(key, out value))
            {
                return new Dictionary<string, object>();
            }

            var result = value as Dictionary<string, object>;
            return result ?? new Dictionary<string, object>();
        }

        public static string GetString(Dictionary<string, object> dictionary, string key)
        {
            object value;
            if (dictionary == null || !dictionary.TryGetValue(key, out value) || value == null)
            {
                return string.Empty;
            }

            return Convert.ToString(value);
        }

        public static int GetInt(Dictionary<string, object> dictionary, string key, int defaultValue)
        {
            object value;
            if (dictionary == null || !dictionary.TryGetValue(key, out value) || value == null)
            {
                return defaultValue;
            }

            int number;
            if (int.TryParse(Convert.ToString(value), out number))
            {
                return number;
            }

            double decimalValue;
            if (double.TryParse(Convert.ToString(value), NumberStyles.Any, CultureInfo.InvariantCulture, out decimalValue))
            {
                return Convert.ToInt32(decimalValue);
            }

            return defaultValue;
        }

        public static double? GetDouble(Dictionary<string, object> dictionary, string key)
        {
            object value;
            if (dictionary == null || !dictionary.TryGetValue(key, out value) || value == null)
            {
                return null;
            }

            double number;
            if (double.TryParse(Convert.ToString(value), NumberStyles.Any, CultureInfo.InvariantCulture, out number))
            {
                return number;
            }

            return null;
        }

        public static List<KeyValuePair<string, string>> FlattenObject(Dictionary<string, object> dictionary)
        {
            var values = new List<KeyValuePair<string, string>>();
            FlattenRecursive(string.Empty, dictionary, values);
            return values;
        }

        public static string ToDisplayString(object value)
        {
            if (value == null)
            {
                return "-";
            }

            var dictionary = value as Dictionary<string, object>;
            if (dictionary != null)
            {
                return ToJson(dictionary);
            }

            var arrayList = value as ArrayList;
            if (arrayList != null)
            {
                return ToJson(arrayList);
            }

            var array = value as object[];
            if (array != null)
            {
                return ToJson(array);
            }

            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        private static void FlattenRecursive(string prefix, object value, List<KeyValuePair<string, string>> values)
        {
            var dictionary = value as Dictionary<string, object>;
            if (dictionary != null)
            {
                foreach (var pair in dictionary)
                {
                    var childKey = string.IsNullOrWhiteSpace(prefix) ? pair.Key : prefix + "." + pair.Key;
                    FlattenRecursive(childKey, pair.Value, values);
                }

                return;
            }

            var array = value as object[];
            if (array != null)
            {
                for (int i = 0; i < array.Length; i++)
                {
                    FlattenRecursive(prefix + "[" + i + "]", array[i], values);
                }

                return;
            }

            var list = value as ArrayList;
            if (list != null)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    FlattenRecursive(prefix + "[" + i + "]", list[i], values);
                }

                return;
            }

            values.Add(new KeyValuePair<string, string>(prefix, ToDisplayString(value)));
        }
    }
}
