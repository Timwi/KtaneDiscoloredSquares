using System;
using System.Collections;

using Rnd = UnityEngine.Random;

namespace DiscoloredSquares
{
    static class Ut
    {
        /// <summary>
        ///     Similar to <see cref="string.Substring(int)"/>, only for arrays. Returns a new array containing all items from
        ///     the specified <paramref name="startIndex"/> onwards.</summary>
        /// <remarks>
        ///     Returns a new copy of the array even if <paramref name="startIndex"/> is 0.</remarks>
        public static T[] Subarray<T>(this T[] array, int startIndex)
        {
            if (array == null)
                throw new ArgumentNullException("array");
            return Subarray(array, startIndex, array.Length - startIndex);
        }

        /// <summary>
        ///     Similar to <see cref="string.Substring(int,int)"/>, only for arrays. Returns a new array containing <paramref
        ///     name="length"/> items from the specified <paramref name="startIndex"/> onwards.</summary>
        /// <remarks>
        ///     Returns a new copy of the array even if <paramref name="startIndex"/> is 0 and <paramref name="length"/> is
        ///     the length of the input array.</remarks>
        public static T[] Subarray<T>(this T[] array, int startIndex, int length)
        {
            if (array == null)
                throw new ArgumentNullException("array");
            if (startIndex < 0)
                throw new ArgumentOutOfRangeException("startIndex", "startIndex cannot be negative.");
            if (length < 0 || startIndex + length > array.Length)
                throw new ArgumentOutOfRangeException("length", "length cannot be negative or extend beyond the end of the array.");
            T[] result = new T[length];
            Array.Copy(array, startIndex, result, 0, length);
            return result;
        }

        public static T Shuffle<T>(this T list) where T : IList
        {
            if (list == null)
                throw new ArgumentNullException("list");
            for (int j = list.Count; j >= 1; j--)
            {
                int item = Rnd.Range(0, j);
                if (item < j - 1)
                {
                    var t = list[item];
                    list[item] = list[j - 1];
                    list[j - 1] = t;
                }
            }
            return list;
        }
    }
}
