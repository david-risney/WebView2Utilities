# WebView2Utilities

WebView2Utilities help you develop and debug Microsoft Edge WebView2 apps.

![image](https://github.com/david-risney/WebView2Utilities/assets/5773562/d4d3ccb9-62fe-4476-98e2-51e6f70a0054)


## Install & Run

* Install via the [ClickOnce install page](https://david-risney.github.io/WebView2Utilities/install/WebView2Utilities.application).
* Or copy the binaries off the [releases page](https://github.com/david-risney/WebView2Utilities/releases/) to somewhere on your machine.
* Or build [the code](https://github.com/david-risney/WebView2Utilities) yourself.

## UI details

The app has three tabs for the different main WebView2Utilities features. Each tab has a `Refresh` button in the tab which you can use to force the information in that to reload.

### Host Apps tab

This tab lists the running processes that are using WebView2. By default `Discover more Host Apps information (slower)` is unchecked and the processes listed will be those with a WebView2 mojo connection. Their HWND trees are examined to try to find their corresponding WebView2 runtime browser process. This may not work in some cases and you can try checking the `Discover more` checkbox. This will examine all processes, not just those with a WebView2 mojo connection, walk all HWND trees, and examine process parents. This finds more information but is slower.

When selecting a Host App from the list on the left you can see details about the Host App on the right:

* `IL` will report if the Host App process is running as admin, or in an app container.
* `SDK version` is the version of the SDK DLLs that the Host App process has loaded.
* `Probable UI framework` is WinForms, WPF, WinUI2, or WinUI3 and based on what DLLs the Host App process has loaded.
* Similarly `Probable API kind` reports Win32, WinRT, or .NET also based on the DLLs loaded by the Host App process.
* Both are 'probable' because its likely based on what DLLs the Host App process has loaded but not definitive.
* The `Runtime path`, version, and channel are based on the WebView2 runtime DLL loaded by the Host App process. If these are Unknown then the host app is using WebView2 SDK DLLs but has not created a WebView2 yet.
* The `User data folder` and `Browser process PID` are based on the browser process used by the host app process. As mentioned above, WebView2Utilities may not always be able to discover the browser process used. In that case the `Runtime path` will have a valid value, but the `User data folder` and `Browser process PID` will be listed as Unknown. You can try checking the `Discover more` checkbox in that case.

There are some buttons below the detail information:

* `Open Override` will create an entry for the selected app, if one doesn't already exit, in the Override tab, switch to the Override tab, and select the corresponding override entry.
* `Create Report` will create a zip file containing information displayed in WebView2Utilities as well as any crash dumps or chromium logs for the selected host app. Note that personal information may be stored in the zip file as a part of the crash dump or elsewhere in the file. Only share the zip with people you trust.

The `Watch for changes` checkbox is checked by default. When checked WebView2Utilities will check for changes to the set of processes with a WebView2 mojo connection every three seconds and if there is a change, the tab will be refreshed automatically. Otherwise, you can use the Refresh button in the tab title to refresh the list manually.

### Runtimes tab

The Runtimes tab lists the found installed WebView2 Runtimes and non-stable Microsoft Edge browser installations. These are paths that you might use with the `Fixed Version` field in the Overrides tab.

There's a section at the bottom with links to install additional versions of the WebView2 Runtime.

### Override tab

This tab helps you set the [loader override policy registry keys](https://docs.microsoft.com/en-us/microsoft-edge/webview2/reference/win32/webview2-idl?view=webview2-1.0.774.44#createcorewebview2environmentwithoptions).

* `Host app exe` is the name of the host app's executable that the rest of the settings will apply to. It applies to future webview2 creations. The '* (All other apps)' entry applies to all apps that don't have a specific entry in this list.
* `Runtime` contains three options for forcing apps to pick a WebView2 Runtime.
  * `Evergreen` is the usual manner of finding the WebView2 Runtime as described in the WebView2 documentation. First the WebView2 Runtime, then Beta, then Dev, then Canary channels.
  * `Evergreen with preview build` reverses the usual order of discovering installed WebView2 Runtimes looking for the least stable channel first.
  * `Fixed Version` lets you select an explicit path for a WebView2 Runtime. The path should have the msedgewebview2.exe in it. If set, the host app will use this runtime instead of whatever they requested.
* `Browser arguments` is additional command line switches to be passed to the browser process created for the WebView2. If set, this is merged in with whatever the app sets. See the [list of chromium command line switches](https://peter.sh/experiments/chromium-command-line-switches/) to see what switches exist.
  * Common browser argument checkboxes follow. Checking these will alter the `Browser Arguments` text box to include or exclude these common browser arguments.
  * `Auto open DevTools` when set will cause the WebView2 to automatically open DevTools when the WebView2 is first created.
  * `Logging` when set will enable chromium logging to a log file in the user data folder. This log will be captured by the `Create Report` button on the Host Apps tab.
* `User data path` is the path to a user data folder. If set the host app will use this user data folder instead of whatever they requested.
* `Launch RegEdit` will open regedit.exe to the registry path of the override keys.

You can use the `Add New` and `Remove` buttons to add and remove entries to the list. Additionally the Host app exe has a drop down of running host app executables for your convenience. Similarly, the Fixed Version has a drop down of found WebView Runtimes.

### About tab

Application version and helpful links.
