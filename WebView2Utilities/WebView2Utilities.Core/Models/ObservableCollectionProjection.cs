using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace WebView2Utilities.Core.Models;

// A read only ObservableCollection that wraps an inner ObservableCollection and 
// projects the contents of the inner ObservableCollection to a new type.
public class ObservableCollectionProjection<TInner, TOuter> : ObservableCollection<TOuter>
    where TOuter : class
    where TInner : class
{
    private ObservableCollection<TInner> m_innerCollection;
    private Func<TInner, TOuter> m_projectDelegate;

    private static WeakRefEqualityComparator s_WeakRefEqualityComparatorSingleton = new WeakRefEqualityComparator();
    private class WeakRefEqualityComparator : IEqualityComparer<WeakReference<TInner>>
    {
        public bool Equals(WeakReference<TInner> x, WeakReference<TInner> y)
        {
            if (x == y)
            {
                return true;
            }
            else if (x == null && y == null)
            {
                return true;
            }
            else if (x == null || y == null)
            {
                return false;
            }
            else
            {
                TInner xInner;
                var gotX = x.TryGetTarget(out xInner);

                TInner yInner;
                var gotY = y.TryGetTarget(out yInner);

                if (gotX && gotY)
                {
                    if (xInner == null && yInner == null)
                    {
                        return true;
                    }
                    else if (xInner == null || yInner == null)
                    {
                        return false;
                    }
                    else
                    {
                        return xInner.Equals(yInner);
                    }
                }
                else
                {
                    return false;
                }
            }
        }

        public int GetHashCode(WeakReference<TInner> obj)
        {
            TInner target;
            if (obj != null && obj.TryGetTarget(out target))
            {
                return target.GetHashCode();
            }
            return 0;
        }
    }

    private Dictionary<WeakReference<TInner>, TOuter> m_innerToOuterCache
        = new Dictionary<WeakReference<TInner>, TOuter>(s_WeakRefEqualityComparatorSingleton);

    private TOuter InnerToOuter(TInner inner)
    {
        var innerWeakRef = new WeakReference<TInner>(inner);
        TOuter result;
        // clean m_innerToOuterCache of all entries where the WeakReference is no long alive
        CleanWeakRefsFromCache();

        if (!m_innerToOuterCache.TryGetValue(innerWeakRef, out result))
        {
            result = m_projectDelegate.Invoke(inner);
            m_innerToOuterCache.Add(innerWeakRef, result);
        }

        return result;
    }

    public ObservableCollectionProjection(
        ObservableCollection<TInner> innerCollection,
        Func<TInner, TOuter> projectDelegate)
    {
        m_innerCollection = innerCollection;
        m_projectDelegate = projectDelegate;

        m_innerCollection.CollectionChanged += InnerCollectionChanged;

        var outerCollection = m_innerCollection.Select(i => InnerToOuter(i)).ToArray();
        for (var idx = 0; idx < outerCollection.Count(); ++idx)
        {
            InsertItem(idx, outerCollection[idx]);
        }
    }

    private void CleanWeakRefsFromCache()
    {
        if (m_innerToOuterCache != null)
        {
            var deadWeakRefs = m_innerToOuterCache.Keys.Where(r => !r.TryGetTarget(out _)).ToArray();
            foreach (var deadWeakRef in deadWeakRefs)
            {
                m_innerToOuterCache.Remove(deadWeakRef);
            }
        }
    }

    private void InnerCollectionChanged(object sender, NotifyCollectionChangedEventArgs innerEventArgs)
    {
        switch (innerEventArgs.Action)
        {
            case NotifyCollectionChangedAction.Add:
                {
                    var outerNewItems = innerEventArgs.NewItems.Cast<TInner>().Select(i => InnerToOuter(i)).ToList();
                    for (var idx = 0; idx < outerNewItems.Count(); ++idx)
                    {
                        Insert(innerEventArgs.NewStartingIndex + idx, outerNewItems[idx]);
                    }
                }
                break;

            case NotifyCollectionChangedAction.Remove:
                {
                    var count = innerEventArgs.OldItems.Count;
                    for (var idx = 0; idx < count; ++idx)
                    {
                        RemoveAt(innerEventArgs.OldStartingIndex);
                    }
                }
                break;

            case NotifyCollectionChangedAction.Move:
                {
                    var count = innerEventArgs.OldItems.Count;
                    var oldIdx = innerEventArgs.OldStartingIndex;
                    var newIdx = innerEventArgs.NewStartingIndex;

                    for (var idx = 0; idx < count; ++idx)
                    {
                        Move(oldIdx + idx, newIdx + idx);
                    }
                }
                break;

            case NotifyCollectionChangedAction.Replace:
                {
                    var outerNewItems = innerEventArgs.NewItems.Cast<TInner>().Select(i => InnerToOuter(i)).ToList();
                    for (var idx = 0; idx < outerNewItems.Count(); ++idx)
                    {
                        SetItem(innerEventArgs.NewStartingIndex + idx, outerNewItems[idx]);
                    }
                }
                break;

            case NotifyCollectionChangedAction.Reset:
                {
                    var outerNewItems = m_innerCollection.Cast<TInner>().Select(i => InnerToOuter(i)).ToList();
                    var commonCount = Math.Min(outerNewItems.Count, Count);
                    for (var idx = 0; idx < commonCount; ++idx)
                    {
                        SetItem(idx, outerNewItems[idx]);
                    }
                    // If there are extra items in the current collection trim the end
                    while (Count > outerNewItems.Count)
                    {
                        RemoveAt(Count - 1);
                    }
                    // If there isn't enough space in the current collection append to the end
                    for (var idx = Count; idx < outerNewItems.Count; ++idx)
                    {
                        InsertItem(idx, outerNewItems[idx]);
                    }
                }
                break;
        }
    }
}