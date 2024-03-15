using Microsoft.VisualStudio.TestTools.UnitTesting;
using wv2util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Collections.Specialized;

namespace wv2util.Tests
{
    public class A
    {
        public int value;
    }

    public class B
    {
        public int value;
    }

    [TestClass()]
    public class ObservableCollectionProjectionTests
    {
        private ObservableCollection<A> GetTestOCA()
        {
            ObservableCollection<A> oca = new ObservableCollection<A>();
            oca.Add(new A { value = 1 });
            oca.Add(new A { value = 2 });
            oca.Add(new A { value = 3 });

            return oca;
        }

        [TestMethod()]
        public void TestCount()
        {
            var oca = GetTestOCA();
            ObservableCollection<B> ocb = new ObservableCollectionProjection<A, B>(oca, a => new B { value = a.value + 100 });

            Debug.Assert(ocb.Count == oca.Count);
        }


        [TestMethod()]
        public void TestWrapValues()
        {
            var oca = GetTestOCA();
            ObservableCollection<B> ocb = new ObservableCollectionProjection<A, B>(oca, a => new B { value = a.value + 100 });

            Debug.Assert(ocb[0].value == 101);
            Debug.Assert(ocb[1].value == 102);
            Debug.Assert(ocb[2].value == 103);
        }

        [TestMethod()]
        public void TestIdentity()
        {
            var oca = GetTestOCA();
            ObservableCollection<B> ocb = new ObservableCollectionProjection<A, B>(oca, a => new B { value = a.value + 100 });

            var ocb0_1 = ocb[0];
            var ocb0_2 = ocb[0];
            Debug.Assert(ocb0_1 == ocb0_2);
        }

        [TestMethod()]
        public void TestAddition()
        {
            var oca = GetTestOCA();
            ObservableCollection<B> ocb = new ObservableCollectionProjection<A, B>(oca, a => new B { value = a.value + 100 });

            var ocb0_1 = ocb[0];
            var ocb0_2 = ocb[0];
            Debug.Assert(ocb0_1 == ocb0_2);
        }

        [TestMethod()]
        public void TestRemoval()
        {
            var oca = GetTestOCA();
            ObservableCollection<B> ocb = new ObservableCollectionProjection<A, B>(oca, a => new B { value = a.value + 100 });

            Debug.Assert(oca[1].value == 2);
            Debug.Assert(ocb[1].value == 102);

            oca.RemoveAt(1);

            Debug.Assert(oca[1].value == 3);
            Debug.Assert(ocb[1].value == 103);
        }

        [TestMethod()]
        public async Task TestPropertyChangedEvent()
        {
            var oca = GetTestOCA();
            ObservableCollection<B> ocb = new ObservableCollectionProjection<A, B>(oca, a => new B { value = a.value + 100 });

            TaskCompletionSource<NotifyCollectionChangedEventArgs> tcsa = null;
            TaskCompletionSource<NotifyCollectionChangedEventArgs> tcsb = null;

            tcsa = new TaskCompletionSource<NotifyCollectionChangedEventArgs>();
            tcsb = new TaskCompletionSource<NotifyCollectionChangedEventArgs>();
            oca.CollectionChanged += (object sender, NotifyCollectionChangedEventArgs e) => tcsa.TrySetResult(e);
            oca.CollectionChanged += (object sender, NotifyCollectionChangedEventArgs e) => tcsb.TrySetResult(e);
            oca.RemoveAt(0);
            var eventArgs = await tcsa.Task;
            Debug.Assert(eventArgs != null);
            eventArgs = await tcsb.Task;
            Debug.Assert(eventArgs != null);
        }
    }
}