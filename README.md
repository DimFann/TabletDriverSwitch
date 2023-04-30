# TabletDriverSwitch
----
Simple tool for switching between drivers for graphics tablets.
This is only for Wacom & Huion for now, I don't have any XP-Pen tablets I need to worry about.

Seems to work for the most part, requires some setup. There may be some edge cases that break but works on my machine™

GUI maybe if anyone cares? Send me a message on twitter if you want me to fix something.

<h2>Setup</h2>

1. Create a folder somewhere called "Drivers"
2. Create two folders in that folder called "Huion" and "Wacom"
3. For each of those folders create two folders inside, "x64" and "x86"
4. Install the latest copy of your Wacom driver
5. Navigate to C:\Windows\System32 and copy wintab32.dll to Drivers\Wacom\x86
6. Navigate to C:\Windows\SysWOW64 and copy wintab32.dll to Drivers\Wacom\x64
7. Install the latest copy of your Huion driver
8. Repeat steps 5 and 6 but place the copies of wintab32.dll in the respective Huion folder.
9. Update Driver:Vendor:SourcePath for Wacom and Huion in appsettings.json to point to the named root of the folders you created.
   - See the default values for an example
10. Update any paths in appsettings.json that don't match your system.
11. You're done, you can test by running TabletDriverSwitch.exe, try to use your Wacom tablet afterwards.

<h2>Notes</h2>

- I have Origin in the kill list because it was using SysWOW64\wintab32.dll for some reason.
- If you have any other process that grab that dll add them to the kill list.
  - It supports RegEx
- The "Art" section in the config is an attempt to find and warn the user of any wintab32.dll using programs that may be open.
  - I added some that I have to worry about but it's all ReGex so add whatever you like.
- Sometimes _sometimes_ this doesn't work and what fixes it is unplugging and re-plugging your tablet.
  - Trying to find a windowsey way to automate that so I can just reset the device every time but beats me for now.
