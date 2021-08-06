using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace wv2util
{
    public static class SortUtil
    {
        public class SortColumnContext
        {
            private int m_columnIdx = 0;
            public int SortDirection { get; private set; } = 1;
            public void SelectColumn(int columnIdx)
            {
                if (columnIdx == m_columnIdx)
                {
                    SortDirection *= -1;
                }
                else
                {
                    SortDirection = 1;
                    m_columnIdx = columnIdx;
                }
            }
        }
        public class ComparisonComparer<T> : IComparer<T>, IComparer
        {
            private readonly Comparison<T> _comparison;

            public ComparisonComparer(Comparison<T> comparison)
            {
                _comparison = comparison;
            }

            public int Compare(T x, T y)
            {
                return _comparison(x, y);
            }

            public int Compare(object o1, object o2)
            {
                return _comparison((T)o1, (T)o2);
            }
        }

        public static int CompareVersionStrings(string left, string right)
        {
            var leftParts = left.Split('.').Select(partAsString => int.Parse(partAsString));
            var rightParts = right.Split('.').Select(partAsString => int.Parse(partAsString));

            while (leftParts.Count() < rightParts.Count())
            {
                leftParts.Append(0);
            }
            while (leftParts.Count() > rightParts.Count())
            {
                rightParts.Append(0);
            }

            var leftEnum = leftParts.GetEnumerator();
            var rightEnum = rightParts.GetEnumerator();
            while (leftEnum.MoveNext() && rightEnum.MoveNext())
            {
                int diff = leftEnum.Current - rightEnum.Current;
                if (diff != 0)
                {
                    return diff;
                }
            }
            return 0;
        }

        public static int CompareChannelStrings(string left, string right)
        {
            string[] channels = { "Canary", "Dev", "Beta", "Stable", "Stable WebView2 Runtime" };
            int leftPos = Array.IndexOf(channels, left);
            int rightPos = Array.IndexOf(channels, right);
            return leftPos - rightPos;
        }
    }
}
