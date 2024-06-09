using System.Text.RegularExpressions;
using UnityEngine;

namespace Duplicationer
{
    internal static class UnityExtensions
    {
        internal static void DestroyAllChildren(this Transform transform)
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                child.SetParent(null, false);
                Object.Destroy(child.gameObject);
            }
        }

        internal static T[] RemoveAt<T>(this T[] source, int index)
        {
            T[] dest = new T[source.Length - 1];
            if (index > 0)
                System.Array.Copy(source, 0, dest, 0, index);

            if (index < source.Length - 1)
                System.Array.Copy(source, index + 1, dest, index, source.Length - index - 1);

            return dest;
        }

        public static int NthIndexOf(this string target, string value, int n)
        {
            Match m = Regex.Match(target, "((" + Regex.Escape(value) + ").*?){" + n + "}");

            if (m.Success)
                return m.Groups[2].Captures[n - 1].Index;
            else
                return -1;
        }
    }
}
