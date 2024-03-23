# Scum Bag - Save Scummer

![screenshot](screenshot.png)

## About
Scum Bag is a place to hold all of your save backups. It automatically backs up your save files at a given interval and lets you restore to any point.

### Features

* Create a new save profile with frequency and max backups
* Only makes a backup if the file actually changed
* Reads your steam libraries to find installed games
* Takes a screenshot of the game window when the save file changes (Windows only)
    * Supports windowed and fullscreen apps

## Requirements

* Windows 11
    * Should work natively
* Windows 10
    * Requires [Webview2 runtime](https://developer.microsoft.com/en-us/microsoft-edge/webview2/)
* Linux
    * Requires [WebKit2GTK](https://webkitgtk.org/)
    * `apt install libgtk-3-0 libwebkit2gtk-4.0-37`
* macOS
    * Requires [WebKit](https://webkit.org/downloads/)
