## Installing

Photo Calendar Maker runs on Windows 10 and 11. Download the Setup file for your PC from the links below (they're also under **Assets**):

- [`CalendarMaker-win-x64-Setup.exe`](https://github.com/SpeakingInBits/CalendarMaker-MAUI/releases/download/v{{VERSION}}/CalendarMaker-win-x64-Setup.exe) for most PCs.
- [`CalendarMaker-win-arm64-Setup.exe`](https://github.com/SpeakingInBits/CalendarMaker-MAUI/releases/download/v{{VERSION}}/CalendarMaker-win-arm64-Setup.exe) for PCs with an Arm processor, such as Snapdragon.

1. Double-click the Setup file. The app isn't code-signed yet, so if Windows says "Windows protected your PC", click **More info**, then **Run anyway**.
2. The app installs for your Windows account (no administrator password needed) and opens. Next time, open **Photo Calendar Maker** from the Start menu or the desktop.

If you can't run installers, download [`CalendarMaker-win-x64-Portable.zip`](https://github.com/SpeakingInBits/CalendarMaker-MAUI/releases/download/v{{VERSION}}/CalendarMaker-win-x64-Portable.zip) (or [`CalendarMaker-win-arm64-Portable.zip`](https://github.com/SpeakingInBits/CalendarMaker-MAUI/releases/download/v{{VERSION}}/CalendarMaker-win-arm64-Portable.zip) for Arm) instead, right-click it, choose **Extract All**, and double-click `Photo Calendar Maker.exe` in the extracted folder.

After that, the app keeps itself up to date. When a new version is out, it downloads it in the background and installs it the next time you open the app (or right away with **Restart now**). If you're using a version from before automatic updates (0.8 or earlier), download this version once by hand; it updates itself from then on.

The other files in **Assets** (`.nupkg`, `.json`) are what installed apps download to update. You don't need them.
