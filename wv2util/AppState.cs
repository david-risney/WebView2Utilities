using System.Collections.ObjectModel;

namespace wv2util
{
    public class AppState
    {
        private static AppOverrideList s_AppOverrideList = new AppOverrideList();
        public static AppOverrideList GetAppOverrideList() => s_AppOverrideList;

        private static RuntimeList s_RuntimeList = new RuntimeList();
        public static RuntimeList GetRuntimeList() => s_RuntimeList;

        private static HostAppList s_HostAppList = new HostAppList();
        public static HostAppList GetHostAppList() => s_HostAppList;

        private static ObservableCollection<ITreeItem> s_hostAppTreeItems = new ObservableCollectionProjection<HostAppEntry, ITreeItem>(
                        GetHostAppList(),
                        i => new HostAppEntryTreeItem(null, GetHostAppList(), i));
        public static ObservableCollection<ITreeItem> GetHostAppTree() => s_hostAppTreeItems;

        private static ExperimentalFeatureList s_ExperimentalFeatureList = new ExperimentalFeatureList();
        public static ExperimentalFeatureList GetExperimentalFeatureList() => s_ExperimentalFeatureList;
    }
}