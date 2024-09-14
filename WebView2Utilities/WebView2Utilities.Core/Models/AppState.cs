namespace WebView2Utilities.Core.Models;

public class AppState
{
    private static readonly AppOverrideList s_AppOverrideList = new();
    public static AppOverrideList GetAppOverrideList() => s_AppOverrideList;

    private static readonly RuntimeList s_RuntimeList = new();
    public static RuntimeList GetRuntimeList() => s_RuntimeList;

    private static readonly HostAppList s_HostAppList = new();
    public static HostAppList GetHostAppList() => s_HostAppList;

    /* TODO
    private static ObservableCollection<ITreeItem> s_hostAppTreeItems = new ObservableCollectionProjection<HostAppEntry, ITreeItem>(
                    GetHostAppList(),
                    i => new HostAppEntryTreeItem(GetHostAppList(), i));
    public static ObservableCollection<ITreeItem> GetHostAppTree() => s_hostAppTreeItems;
    */

    private static readonly ExperimentalFeatureList s_ExperimentalFeatureList = new();
    public static ExperimentalFeatureList GetExperimentalFeatureList() => s_ExperimentalFeatureList;
}