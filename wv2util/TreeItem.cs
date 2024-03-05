using PInvoke;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;

namespace wv2util
{
    public interface ITreeItem
    {
        string Name { get; }

        BitmapSource IconAsBitmapSource { get; }

        ObservableCollection<ITreeItem> Children { get; }

        Object Model { get; }
    }

    public class HostAppRootTreeItem : ITreeItem
    {
        private HostAppList m_hostAppList;
        public HostAppRootTreeItem(HostAppList hostAppList)
        {
            m_hostAppList = hostAppList;
        }

        public string Name => "Host Apps";

        public BitmapSource IconAsBitmapSource => null;

        private ObservableCollection<ITreeItem> m_children = null;
        public ObservableCollection<ITreeItem> Children
        {
            get
            {
                if (m_children == null)
                {
                    m_children = new ObservableCollectionProjection<HostAppEntry, ITreeItem>(
                        m_hostAppList,
                        i => new HostAppEntryTreeItem(i));
                }
                return m_children;
            }
        }

        public Object Model => m_hostAppList;
    }

    public class HostAppEntryTreeItem : ITreeItem
    {
        private HostAppEntry m_hostAppEntry;
        public HostAppEntryTreeItem(HostAppEntry hostAppEntry)
        {
            m_hostAppEntry = hostAppEntry;
        }

        public string Name => m_hostAppEntry.ToString();

        public BitmapSource IconAsBitmapSource => null;

        public ObservableCollection<ITreeItem> Children { get; private set; } = new ObservableCollection<ITreeItem>();

        public Object Model => m_hostAppEntry;
    }
}