using Microsoft.Extensions.Configuration;
using System.Diagnostics;
using System.ServiceProcess;
using System.Text.RegularExpressions;

var config = new ConfigurationBuilder()
    .AddJsonFile(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json"))
    .Build();

var art = config.GetSection("Art").Get<string[]>() ?? throw new Exception("");
var kill = config.GetSection("Stop").Get<string[]>() ?? throw new Exception("");

var startHuion = config.GetSection("Start:Huion").Get<StartObject[]>() ?? throw new Exception("");
var startWacom = config.GetSection("Start:Wacom").Get<StartObject[]>() ?? throw new Exception("");

var driverHuion = config.GetSection("Driver:Huion").Get<DriverObject>() ?? throw new Exception("");
var driverWacom = config.GetSection("Driver:Wacom").Get<DriverObject>() ?? throw new Exception("");

// Kill all running processes that match the regex filters in appsettings.json

var processes = Process.GetProcesses();
var artProcesses = processes.Where(x => art.Any(y => Regex.IsMatch(x.ProcessName, y, RegexOptions.IgnoreCase)));

if(artProcesses.Any())
{
    Console.WriteLine("Found running applications that may be using wintab32.dll, save and close your work before running this tool");
    foreach(var process in artProcesses)
    {
        Console.WriteLine($"\t{process.ProcessName}");
    }
    goto end;
}

var targetProcesses = processes.Where(x => kill.Any(y => Regex.IsMatch(x.ProcessName, y, RegexOptions.IgnoreCase)));

if (targetProcesses.Any())
{
    Console.WriteLine("Killing processes");
    foreach (var process in targetProcesses)
    {
        Console.WriteLine($"\t{process.ProcessName}");
        process.Kill();
    }
}
else
{
    Console.WriteLine("No target processes were running");
}

// Check wintab32 for "Huion" or "Wacom" strings to determine which driver is currently active.

bool FindVendorString(string vendor, string dll)
{
    if (!File.Exists(dll))
        return false;
    
    using StreamReader reader = new StreamReader(dll);
    string file = reader.ReadToEnd();
    return file.Contains(vendor, StringComparison.OrdinalIgnoreCase);
}

const string systemPath32 = @"C:\Windows\System32";
const string systemPath64 = @"C:\Windows\SysWOW64";

var sys32Huion = FindVendorString("Huion", Path.Combine(systemPath32, "wintab32.dll"));
var sys64Huion = FindVendorString("Huion", Path.Combine(systemPath64, "wintab32.dll"));

var sys32Wacom = FindVendorString("Wacom", Path.Combine(systemPath32, "wintab32.dll"));
var sys64Wacom = FindVendorString("Wacom", Path.Combine(systemPath64, "wintab32.dll"));

Console.WriteLine("\r\nCurrent driver vendor(s)");
Console.WriteLine(
    $"\tSystem32.Huion:{sys32Huion}\r\n" +
    $"\tSysWOW64.Huion:{sys64Huion}\r\n" +
    $"\tSystem32.Wacom:{sys32Wacom}\r\n" +
    $"\tSysWOW64.Wacom:{sys64Wacom}\r\n"
);

// Do a sanity check, there should only be one type of driver active unless something weird is happening.

var allHuion = sys32Huion && sys64Huion;
var allWacom = sys32Wacom && sys64Wacom;

var someHuion = sys32Huion || sys64Huion;
var someWacom = sys32Wacom || sys64Wacom;

// All of one vendor or some of one vendor are the only valid cases.

if ((allHuion && allWacom) || (allHuion && someWacom) || (allWacom && someHuion) || (someHuion && someWacom))
{
    // This is weird, the user should fix this.
    Console.WriteLine("Found a weird mix of drivers, reinstall either your wacom or huion drivers");
    goto end;
}

Vendor vendor = allHuion || someHuion ? Vendor.Huion : Vendor.Wacom;

Console.WriteLine($"Active driver vendor: {vendor}");

// Back up the current drivers

bool BackupDriver(Vendor vendor, string backupRootPath)
{
    string backupPath32 = Path.Combine(backupRootPath, "x86");
    string backupPath64 = Path.Combine(backupRootPath, "x64");
    try
    {
        Directory.CreateDirectory(backupRootPath);
        Directory.CreateDirectory(backupPath32);
        Directory.CreateDirectory(backupPath64);
    }
    catch (Exception e)
    {
        Console.WriteLine($"Failed to create {vendor} backup path(s)\r\n{e.ToString}");
        return false;
    }

    try
    {
        string sysFile32 = Path.Combine(systemPath32, "wintab32.dll");
        string bakFile32 = Path.Combine(backupPath32, "wintab32.dll");

        if (File.Exists(bakFile32))
            File.Delete(bakFile32);

        if(File.Exists(sysFile32))
            File.Copy(sysFile32, bakFile32);

        string sysFile64 = Path.Combine(systemPath64, "wintab32.dll");
        string bakFile64 = Path.Combine(backupPath64, "wintab32.dll");

        if (File.Exists(bakFile64))
            File.Delete(bakFile64);

        if(File.Exists(sysFile64))
            File.Copy(sysFile64, bakFile64);

        return true;
    }
    catch(Exception e)
    {
        Console.WriteLine($"Failed to back up driver for {vendor}\r\n{e.ToString()}");
        return false;
    }   
}

Console.WriteLine("Creating backup of current driver");

string pickedBackupPath = vendor == Vendor.Huion ? driverHuion.BackupPath : driverWacom.BackupPath;
if (string.IsNullOrWhiteSpace(pickedBackupPath))
    throw new Exception($"{vendor} backup path is not defined");

pickedBackupPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, pickedBackupPath);
bool backedUp = BackupDriver(vendor, pickedBackupPath);

if(!backedUp)
{
    Console.WriteLine("Driver backup failed");
    goto end;
}
else
{
    Console.WriteLine($"Driver was saved to {pickedBackupPath}");
}

// Remove the current drivers

try
{
    Console.WriteLine($"Deleting wintab32.dll from {systemPath32}");
    File.Delete(Path.Combine(systemPath32, "wintab32.dll"));

    Console.WriteLine($"Deleting wintab32.dll from {systemPath64}");
    File.Delete(Path.Combine(systemPath64, "wintab32.dll"));
}
catch(Exception e)
{
    Console.WriteLine($"Failed to delete present driver file\r\n{e.ToString}");
    goto end;
}

// Place the saved drivers

bool PlaceDriver(Vendor vendor, string driverPath)
{
    try
    {
        string sysFile32 = Path.Combine(systemPath32, "wintab32.dll");
        string srcFile32 = Path.Combine(driverPath,"x86", "wintab32.dll");

        File.Copy(srcFile32, sysFile32);

        string sysFile64 = Path.Combine(systemPath64, "wintab32.dll");
        string srcFile64 = Path.Combine(driverPath, "x64", "wintab32.dll");

        File.Copy(srcFile64, sysFile64);

        return true;
    }
    catch (Exception e)
    {
        Console.WriteLine($"Failed to place driver for {vendor}\r\n{e.ToString()}");
        return false;
    }
}

var newVendor = vendor == Vendor.Huion ? Vendor.Wacom : Vendor.Huion;

Console.WriteLine($"Placing driver file for {newVendor}");

string pickedDriverPath = newVendor == Vendor.Huion ? driverHuion.SourcePath : driverWacom.SourcePath;
if (string.IsNullOrWhiteSpace(pickedDriverPath))
    throw new Exception($"{vendor} driver source path is not defined");

if(!Directory.Exists(pickedDriverPath))
{
    pickedDriverPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, pickedDriverPath);
    if(!Directory.Exists(pickedDriverPath))
    {
        Console.WriteLine("Could not find driver path, check that SourcePath specified in appsettings.json is correct.");
        goto end;
    }    
}

bool placedDriver = PlaceDriver(vendor, pickedDriverPath);

if (!placedDriver)
{
    Console.WriteLine("Driver place failed");
    goto end;
}
else
{
    Console.WriteLine($"{newVendor} driver was placed");
}

// Start the appropriate services/programs for the active drivers.

Console.WriteLine($"Starting {newVendor} applications and services");
var startObjects = newVendor == Vendor.Huion ? startHuion : startWacom;
foreach(StartObject so in startObjects)
{
    Console.WriteLine($"\t{so.Type} {so.Target}");
    if(so.Type == StartType.Executable)
    {
        Process.Start(so.Target,string.Empty);
    }
    else if(so.Type == StartType.Service)
    {
#pragma warning disable CA1416 // Validate platform compatibility
        ServiceController controller = new()
        {
            MachineName = ".",
            ServiceName = so.Target
        };
        controller.Start();
#pragma warning restore CA1416 // Validate platform compatibility
    }
}

end:

Console.WriteLine("Press enter to continue...");
Console.ReadLine();

return;

enum Vendor
{
    None,
    Huion,
    Wacom
}

enum StartType
{
    Executable,
    Service
}

class StartObject
{
    public StartType Type { get; set; }
    public string Target { get; set; } = string.Empty;
}

class DriverObject
{
    public string SourcePath { get; set; } = string.Empty;
    public string BackupPath { get; set; } = string.Empty;
}