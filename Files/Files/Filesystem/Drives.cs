using Files.Common;
using Files.Enums;
using Files.UserControls.Widgets;
using Files.Views;
using Microsoft.System;
using Microsoft.Toolkit.Mvvm.ComponentModel;
using Microsoft.Toolkit.Uwp.Extensions;
using Microsoft.UI.Xaml;
using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Devices.Portable;
using Windows.Storage;

namespace Files.Filesystem
{
    public class DrivesManager : ObservableObject
    {
        private static readonly Task<DrivesManager> _instanceTask = CreateSingleton();
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private List<DriveItem> drivesList = new List<DriveItem>();

        public IReadOnlyList<DriveItem> Drives
        {
            get
            {
                lock (drivesList)
                {
                    return drivesList.ToList().AsReadOnly();
                }
            }
        }

        private bool showUserConsentOnInit = false;

        public bool ShowUserConsentOnInit
        {
            get => showUserConsentOnInit;
            set => SetProperty(ref showUserConsentOnInit, value);
        }

        private DeviceWatcher deviceWatcher;
        private bool driveEnumInProgress;

        //Private as we want to prevent CloudDriveManager being constructed manually
        private DrivesManager()
        {
            SetupDeviceWatcher();
        }

        private static async Task<DrivesManager> CreateSingleton()
        {
            var drives = new DrivesManager();
            return await drives.EnumerateDrivesAsync();
        }

        public static Task<DrivesManager> Instance => _instanceTask;

        private async Task<DrivesManager> EnumerateDrivesAsync()
        {
            driveEnumInProgress = true;

            if (await GetDrivesAsync())
            {
                if (!Drives.Any(d => d.Type != DriveType.Removable))
                {
                    // Only show consent dialog if the exception is UnauthorizedAccessException
                    // and the drives list is empty (except for Removable drives which don't require FileSystem access)
                    ShowUserConsentOnInit = true;
                }
            }

            StartDeviceWatcher();

            driveEnumInProgress = false;

            return this;
        }

        private void SetupDeviceWatcher()
        {
            deviceWatcher = DeviceInformation.CreateWatcher(StorageDevice.GetDeviceSelector());
            deviceWatcher.Added += DeviceAdded;
            deviceWatcher.Removed += DeviceRemoved;
            deviceWatcher.EnumerationCompleted += DeviceWatcher_EnumerationCompleted;
        }

        private void StartDeviceWatcher()
        {
            if (deviceWatcher.Status == DeviceWatcherStatus.Created
                || deviceWatcher.Status == DeviceWatcherStatus.Stopped
                || deviceWatcher.Status == DeviceWatcherStatus.Aborted)
            {
                deviceWatcher?.Start();
            }
        }

        private void DeviceWatcher_EnumerationCompleted(DeviceWatcher sender, object args)
        {
            System.Diagnostics.Debug.WriteLine("DeviceWatcher_EnumerationCompleted");
            RefreshUI();
        }

        private void RefreshUI()
        {
            try
            {
                SyncSideBarItemsUI();
            }
            catch (Exception) // UI Thread not ready yet, so we defer the pervious operation until it is.
            {
                System.Diagnostics.Debug.WriteLine($"RefreshUI Exception");
                // Defer because UI-thread is not ready yet (and DriveItem requires it?)
                MainWindow.Current.Activated += RefreshUI;
            }
        }

        private void RefreshUI(object sender, WindowActivatedEventArgs args)
        {
            SyncSideBarItemsUI();
            MainWindow.Current.Activated -= RefreshUI;
        }

        private void SyncSideBarItemsUI()
        {
            App.mainWindow.DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Normal, () =>
            {
                lock (MainWindow.SideBarItems)
                {
                    var drivesSection = MainWindow.SideBarItems.FirstOrDefault(x => x is HeaderTextItem && x.Text == "SidebarDrives".GetLocalized());

                    if (drivesSection != null && Drives.Count == 0)
                    {
                        //No drives - remove the header
                        MainWindow.SideBarItems.Remove(drivesSection);
                    }

                    if (drivesSection == null && Drives.Count > 0)
                    {
                        drivesSection = new HeaderTextItem()
                        {
                            Text = "SidebarDrives".GetLocalized()
                        };

                        MainWindow.SideBarItems.Add(drivesSection);
                    }

                    var sectionStartIndex = MainWindow.SideBarItems.IndexOf(drivesSection);

                    //Remove all existing drives from the sidebar
                    foreach (var item in MainWindow.SideBarItems
                    .Where(x => x.ItemType == NavigationControlItemType.Drive)
                    .ToList())
                    {
                        MainWindow.SideBarItems.Remove(item);
                        DrivesWidget.ItemsAdded.Remove(item);
                    }

                    //Add all drives to the sidebar
                    var insertAt = sectionStartIndex + 1;
                    foreach (var drive in Drives)
                    {
                        MainWindow.SideBarItems.Insert(insertAt, drive);
                        insertAt++;

                        if (drive.Type != DriveType.VirtualDrive)
                        {
                            DrivesWidget.ItemsAdded.Add(drive);
                        }
                    }
                }
            });
        }

        private void MainView_Activated(object sender, WindowActivatedEventArgs args)
        {
            SyncSideBarItemsUI();
            MainWindow.Current.Activated -= MainView_Activated;
        }

        private void DeviceAdded(DeviceWatcher sender, DeviceInformation args)
        {
            var deviceId = args.Id;
            StorageFolder root = null;
            try
            {
                root = StorageDevice.FromId(deviceId);
            }
            catch (Exception ex) when (
                ex is UnauthorizedAccessException
                || ex is ArgumentException)
            {
                Logger.Warn($"{ex.GetType()}: Attemting to add the device, {args.Name}, failed at the StorageFolder initialization step. This device will be ignored. Device ID: {args.Id}");
                return;
            }

            DriveType type;
            try
            {
                // Check if this drive is associated with a drive letter
                var driveAdded = new DriveInfo(root.Path);
                type = GetDriveType(driveAdded);
            }
            catch (ArgumentException)
            {
                type = DriveType.Removable;
            }

            lock (drivesList)
            {
                // If drive already in list, skip.
                if (drivesList.Any(x => x.DeviceID == deviceId ||
                    string.IsNullOrEmpty(root.Path) ? x.Path.Contains(root.Name) : x.Path == root.Path))
                {
                    return;
                }

                var driveItem = new DriveItem(root, deviceId, type);

                Logger.Info($"Drive added: {driveItem.Path}, {driveItem.Type}");

                drivesList.Add(driveItem);
            }
            // Update the collection on the ui-thread.
            DeviceWatcher_EnumerationCompleted(null, null);
        }

        private void DeviceRemoved(DeviceWatcher sender, DeviceInformationUpdate args)
        {
            Logger.Info($"Drive removed: {args.Id}");
            lock (drivesList)
            {
                drivesList.RemoveAll(x => x.DeviceID == args.Id);
            }
            // Update the collection on the ui-thread.
            DeviceWatcher_EnumerationCompleted(null, null);
        }

        private async Task<bool> GetDrivesAsync()
        {
            // Flag set if any drive throws UnauthorizedAccessException
            bool unauthorizedAccessDetected = false;

            var drives = DriveInfo.GetDrives().ToList();

            var remDevices = await DeviceInformation.FindAllAsync(StorageDevice.GetDeviceSelector());
            List<string> supportedDevicesNames = new List<string>();
            foreach (var item in remDevices)
            {
                try
                {
                    supportedDevicesNames.Add(StorageDevice.FromId(item.Id).Name);
                }
                catch (Exception e)
                {
                    Logger.Warn($"Can't get storage device name: {e.Message}, skipping...");
                }
            }

            foreach (DriveInfo driveInfo in drives.ToList())
            {
                if (!supportedDevicesNames.Contains(driveInfo.Name) && driveInfo.DriveType == System.IO.DriveType.Removable)
                {
                    drives.Remove(driveInfo);
                }
            }

            foreach (var drive in drives)
            {
                var res = await FilesystemTasks.Wrap(() => StorageFolder.GetFolderFromPathAsync(drive.Name).AsTask());
                if (res == FileSystemStatusCode.Unauthorized)
                {
                    unauthorizedAccessDetected = true;
                    Logger.Warn($"{res.ErrorCode}: Attemting to add the device, {drive.Name}, failed at the StorageFolder initialization step. This device will be ignored.");
                    continue;
                }
                else if (!res)
                {
                    Logger.Warn($"{res.ErrorCode}: Attemting to add the device, {drive.Name}, failed at the StorageFolder initialization step. This device will be ignored.");
                    continue;
                }

                lock (drivesList)
                {
                    // If drive already in list, skip.
                    if (drivesList.Any(x => x.Path == drive.Name))
                    {
                        continue;
                    }

                    var type = GetDriveType(drive);

                    var driveItem = new DriveItem(res.Result, drive.Name.TrimEnd('\\'), type);

                    Logger.Info($"Drive added: {driveItem.Path}, {driveItem.Type}");

                    drivesList.Add(driveItem);
                }
            }

            return unauthorizedAccessDetected;
        }

        private DriveType GetDriveType(DriveInfo drive)
        {
            DriveType type = DriveType.Unknown;

            switch (drive.DriveType)
            {
                case System.IO.DriveType.CDRom:
                    type = DriveType.CDRom;
                    break;

                case System.IO.DriveType.Fixed:
                    type = DriveType.Fixed;
                    break;

                case System.IO.DriveType.Network:
                    type = DriveType.Network;
                    break;

                case System.IO.DriveType.NoRootDirectory:
                    type = DriveType.NoRootDirectory;
                    break;

                case System.IO.DriveType.Ram:
                    type = DriveType.Ram;
                    break;

                case System.IO.DriveType.Removable:
                    type = DriveType.Removable;
                    break;

                case System.IO.DriveType.Unknown:
                    if (Helpers.PathNormalization.NormalizePath(drive.Name) != Helpers.PathNormalization.NormalizePath("A:") &&
                            Helpers.PathNormalization.NormalizePath(drive.Name) != Helpers.PathNormalization.NormalizePath("B:"))
                    {
                        type = DriveType.Unknown;
                    }
                    else
                    {
                        type = DriveType.FloppyDisk;
                    }
                    break;

                default:
                    type = DriveType.Unknown;
                    break;
            }

            return type;
        }

        public static async Task<StorageFolderWithPath> GetRootFromPathAsync(string devicePath)
        {
            if (!Path.IsPathRooted(devicePath))
            {
                return null;
            }
            var rootPath = Path.GetPathRoot(devicePath);
            if (devicePath.StartsWith("\\\\?\\")) // USB device
            {
                // Check among already discovered drives
                StorageFolder matchingDrive = App.DrivesManager.Drives.FirstOrDefault(x =>
                    Helpers.PathNormalization.NormalizePath(x.Path) == Helpers.PathNormalization.NormalizePath(rootPath))?.Root;
                if (matchingDrive == null)
                {
                    // Check on all removable drives
                    var remDevices = await DeviceInformation.FindAllAsync(StorageDevice.GetDeviceSelector());
                    foreach (var item in remDevices)
                    {
                        try
                        {
                            var root = StorageDevice.FromId(item.Id);
                            if (Helpers.PathNormalization.NormalizePath(rootPath).Replace("\\\\?\\", "") == root.Name.ToUpperInvariant())
                            {
                                matchingDrive = root;
                                break;
                            }
                        }
                        catch (Exception)
                        {
                            // Ignore this..
                        }
                    }
                }
                if (matchingDrive != null)
                {
                    return new StorageFolderWithPath(matchingDrive, rootPath);
                }
            }
            else if (devicePath.StartsWith("\\\\")) // Network share
            {
                rootPath = rootPath.LastIndexOf("\\") > 1 ? rootPath.Substring(0, rootPath.LastIndexOf("\\")) : rootPath; // Remove share name
                return new StorageFolderWithPath(await StorageFolder.GetFolderFromPathAsync(rootPath), rootPath);
            }
            // It's ok to return null here, on normal drives StorageFolder.GetFolderFromPathAsync works
            return null;
        }

        public async Task HandleWin32DriveEvent(DeviceEvent eventType, string deviceId)
        {
            switch (eventType)
            {
                case DeviceEvent.Added:
                    var driveAdded = new DriveInfo(deviceId);
                    var rootAdded = await FilesystemTasks.Wrap(() => StorageFolder.GetFolderFromPathAsync(deviceId).AsTask());
                    if (!rootAdded)
                    {
                        Logger.Warn($"{rootAdded.ErrorCode}: Attemting to add the device, {deviceId}, failed at the StorageFolder initialization step. This device will be ignored.");
                        return;
                    }
                    lock (drivesList)
                    {
                        // If drive already in list, skip.
                        var matchingDrive = drivesList.FirstOrDefault(x => x.DeviceID == deviceId ||
                            string.IsNullOrEmpty(rootAdded.Result.Path) ? x.Path.Contains(rootAdded.Result.Name) : x.Path == rootAdded.Result.Path);
                        if (matchingDrive != null)
                        {
                            // Update device id to match drive letter
                            matchingDrive.DeviceID = deviceId;
                            return;
                        }
                        var type = GetDriveType(driveAdded);
                        var driveItem = new DriveItem(rootAdded, deviceId, type);
                        Logger.Info($"Drive added from fulltrust process: {driveItem.Path}, {driveItem.Type}");
                        drivesList.Add(driveItem);
                    }
                    DeviceWatcher_EnumerationCompleted(null, null);
                    break;

                case DeviceEvent.Removed:
                    lock (drivesList)
                    {
                        drivesList.RemoveAll(x => x.DeviceID == deviceId);
                    }
                    DeviceWatcher_EnumerationCompleted(null, null);
                    break;

                case DeviceEvent.Inserted:
                case DeviceEvent.Ejected:
                    var rootModified = await FilesystemTasks.Wrap(() => StorageFolder.GetFolderFromPathAsync(deviceId).AsTask());
                    DriveItem matchingDriveEjected = Drives.FirstOrDefault(x => x.DeviceID == deviceId);
                    if (rootModified && matchingDriveEjected != null)
                    {
                        _ = App.mainWindow.DispatcherQueue.TryEnqueue(async () =>
                        {
                            matchingDriveEjected.Root = rootModified.Result;
                            matchingDriveEjected.Text = rootModified.Result.DisplayName;
                            await matchingDriveEjected.UpdatePropertiesAsync();
                        });
                    }
                    break;
            }
        }

        public void Dispose()
        {
            if (deviceWatcher != null)
            {
                if (deviceWatcher.Status == DeviceWatcherStatus.Started || deviceWatcher.Status == DeviceWatcherStatus.EnumerationCompleted)
                {
                    deviceWatcher.Stop();
                }
            }
        }

        public void ResumeDeviceWatcher()
        {
            if (!driveEnumInProgress)
            {
                this.StartDeviceWatcher();
            }
        }
    }
}