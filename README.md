# WebView2Utilities

WebView2Utilities help you develop and debug Microsoft Edge WebView2 apps.

![Screenshot of WebView2 Utilities](screenshot.png)

## Install & Run

* Install via the [ClickOnce install page](https://david-risney.github.io/WebView2Utilities/install/wv2util.application).
* Or copy the binaries off the [releases page](https://github.com/david-risney/WebView2Utilities/releases/) to somewhere on your machine.
* Or build [the code](https://github.com/david-risney/WebView2Utilities) yourself.

## UI details

The app has three tabs for the different main WebView2Utilities features. Each tab has a `Refresh` button in the tab which you can use to force the information in that to reload.

### Override tab

This tab helps you set the [loader override policy registry keys](https://docs.microsoft.com/en-us/microsoft-edge/webview2/reference/win32/webview2-idl?view=webview2-1.0.774.44#createcorewebview2environmentwithoptions).

* `Host app exe` is the name of the host app's executable that the rest of the settings will apply to. It applies to future webview2 creations. The '* (All other apps)' entry applies to all apps that don't have a specific entry in this list.
* `Runtime` contains three options for forcing apps to pick a WebView2 Runtime.
  * `Evergreen` is the usual manner of finding the WebView2 Runtime as described in the WebView2 documentation. First the WebView2 Runtime, then Beta, then Dev, then Canary channels.
  * `Evergreen with preview build` reverses the usual order of discovering installed WebView2 Runtimes looking for the least stable channel first.
  * `Fixed Version` lets you select an explicit path for a WebView2 Runtime. The path should have the msedgewebview2.exe in it. If set, the host app will use this runtime instead of whatever they requested.
* `User data path` is the path to a user data folder. If set the host app will use this user data folder instead of whatever they requested.
* `Browser arguments` is additional command line switches to be passed to the browser process created for the WebView2. If set, this is merged in with whatever the app sets. See the [list of chromium command line switches](https://peter.sh/experiments/chromium-command-line-switches/) to see what switches exist.

You can use the `Add New` and `Remove` buttons to add and remove entries to the list. Additionally the Host app exe has a drop down of running host app executables for your convenience. Similarly, the Fixed Version has a drop down of found WebView Runtimes.

### Runtimes tab

The Runtimes tab lists the found installed WebView2 Runtimes and non-stable Microsoft Edge browser installations. These are paths that you might use with the `Fixed Version` field in the Overrides tab.

There's a section at the bottom with links to install additional versions of the WebView2 Runtime.

### Host Apps tab

This tab lists the running processes that have a WebView2 SDK DLL or WebView2 Runtime client DLL loaded. This includes the SDK version and which runtime the process is using. Loading the list of processes takes a while. You can use the Refresh button to refresh the list.

### About tab

Application version and helpful links.
