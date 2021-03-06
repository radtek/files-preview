using Files.Filesystem;
using Files.UserControls.MultitaskingControl;
using Files.ViewModels;
using Files.Common;
using Files.Views;
using Microsoft.Toolkit.Uwp.Extensions;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.System;
using static Files.Helpers.PathNormalization;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Files
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window, INotifyPropertyChanged
    {
        public SettingsViewModel AppSettings => App.AppSettings;
        public static IMultitaskingControl MultitaskingControl { get; set; }

        private TabItem selectedTabItem;

        public TabItem SelectedTabItem
        {
            get
            {
                return selectedTabItem;
            }
            set
            {
                selectedTabItem = value;
                NotifyPropertyChanged(nameof(SelectedTabItem));
            }
        }

        public static ObservableCollection<TabItem> AppInstances = new ObservableCollection<TabItem>();
        public static ObservableCollection<INavigationControlItem> SideBarItems = new ObservableCollection<INavigationControlItem>();

        public MainWindow()
        {
            this.InitializeComponent();
            //ApplicationView.PreferredLaunchWindowingMode = ApplicationViewWindowingMode.Auto;
            //var CoreTitleBar = CoreApplication.GetCurrentView().TitleBar;

            // TODO: Add Layout RTL when implemented
            //var flowDirectionSetting = ResourceContext.GetForCurrentView().QualifierValues["LayoutDirection"];

            //if (flowDirectionSetting == "RTL")
            //{
            //    FlowDirection = FlowDirection.RightToLeft;
            //}
            horizontalMultitaskingControl.Loaded += HorizontalMultitaskingControl_Loaded1;
        }

        private void HorizontalMultitaskingControl_Loaded1(object sender, RoutedEventArgs e)
        {
            MultitaskingControl = horizontalMultitaskingControl;
            horizontalMultitaskingControl.Loaded -= HorizontalMultitaskingControl_Loaded1;
        }

        private async void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
        {
            this.Activated -= MainWindow_Activated;
            await EnsureSettingsAndConfigurationAreBootstrapped();
            AddNewTab();
            this.SelectedTabItem = AppInstances[App.InteractionViewModel.TabStripSelectedIndex];
        }

        internal async Task EnsureSettingsAndConfigurationAreBootstrapped()
        {

            if (App.CloudDrivesManager == null)
            {
                //Enumerate cloud drives on in the background. It will update the UI itself when finished
                var o = await Files.Filesystem.CloudDrivesManager.Instance;
                App.CloudDrivesManager = o;
            }

            //Start off a list of tasks we need to run before we can continue startup
            var tasksToRun = new List<Task>();

            if (App.AppSettings == null)
            {
                //We can't create AppSettings at the same time as everything else as other dependencies depend on AppSettings
                App.AppSettings = await SettingsViewModel.CreateInstance();
                if (App.AppSettings?.AcrylicTheme == null)
                {
                    Helpers.ThemeHelper.Initialize();
                }
            }

            if (App.SidebarPinnedController == null)
            {
                App.SidebarPinnedController = await Files.Controllers.SidebarPinnedController.CreateInstance();

            }

            if (App.DrivesManager == null)
            {
                var driveTask = new Func<Task>(async () =>
                {
                    var o = await DrivesManager.Instance;
                    App.DrivesManager = o;
                });
                //var drive = DrivesManager.Instance.ContinueWith(o => App.DrivesManager = o.Result)
                tasksToRun.Add(driveTask());
            }

            if (App.InteractionViewModel == null)
            {
                App.InteractionViewModel = new InteractionViewModel();
            }

            if (tasksToRun.Any())
            {
                //Only proceed when all tasks are completed
                await Task.WhenAll(tasksToRun);
            }
        }

        public static void AddNewTab()
        {
            AddNewTabByPath(typeof(PaneHolderPage), "NewTab".GetLocalized());
        }

        public static void AddNewTabAtIndex(object sender, RoutedEventArgs e)
        {
            AddNewTabByPath(typeof(PaneHolderPage), "NewTab".GetLocalized());
        }

        public static void DuplicateTabAtIndex(object sender, RoutedEventArgs e)
        {
            var tabItem = ((FrameworkElement)sender).DataContext as TabItem;
            var index = AppInstances.IndexOf(tabItem);

            if (AppInstances[index].TabItemArguments != null)
            {
                var tabArgs = AppInstances[index].TabItemArguments;
                AddNewTabByParam(tabArgs.InitialPageType, tabArgs.NavigationArg, index + 1);
            }
            else
            {
                AddNewTabByPath(typeof(PaneHolderPage), "NewTab".GetLocalized());
            }
        }

        public static async void MoveTabToNewWindow(object sender, RoutedEventArgs e)
        {
            var tabItem = ((FrameworkElement)sender).DataContext as TabItem;
            var index = AppInstances.IndexOf(tabItem);
            var tabItemArguments = AppInstances[index].TabItemArguments;

            MultitaskingControl.Items.RemoveAt(index);

            if (tabItemArguments != null)
            {
                await Interacts.Interaction.OpenTabInNewWindowAsync(tabItemArguments.Serialize());
            }
            else
            {
                await Interacts.Interaction.OpenPathInNewWindowAsync("NewTab".GetLocalized());
            }
        }

        public static void AddNewTabByParam(Type type, object tabViewItemArgs, int atIndex = -1)
        {
            Microsoft.UI.Xaml.Controls.FontIconSource fontIconSource = new Microsoft.UI.Xaml.Controls.FontIconSource();
            fontIconSource.FontFamily = App.Current.Resources["FluentUIGlyphs"] as FontFamily;

            TabItem tabItem = new TabItem()
            {
                Header = null,
                IconSource = fontIconSource,
                Description = null
            };
            tabItem.Control.NavigationArguments = new TabItemArguments()
            {
                InitialPageType = type,
                NavigationArg = tabViewItemArgs
            };
            tabItem.Control.ContentChanged += Control_ContentChanged;
            UpdateTabInfo(tabItem, tabViewItemArgs);
            AppInstances.Insert(atIndex == -1 ? AppInstances.Count : atIndex, tabItem);
        }

        public static void AddNewTabByPath(Type type, string path, int atIndex = -1)
        {
            Microsoft.UI.Xaml.Controls.FontIconSource fontIconSource = new Microsoft.UI.Xaml.Controls.FontIconSource();
            fontIconSource.FontFamily = App.Current.Resources["FluentUIGlyphs"] as FontFamily;

            if (string.IsNullOrEmpty(path))
            {
                path = "NewTab".GetLocalized();
            }

            TabItem tabItem = new TabItem()
            {
                Header = null,
                IconSource = fontIconSource,
                Description = null
            };
            tabItem.Control.NavigationArguments = new TabItemArguments()
            {
                InitialPageType = type,
                NavigationArg = path
            };
            tabItem.Control.ContentChanged += Control_ContentChanged;
            SetSelectedTabInfo(tabItem, path);
            AppInstances.Insert(atIndex == -1 ? AppInstances.Count : atIndex, tabItem);
        }

        private static void SetSelectedTabInfo(TabItem selectedTabItem, string currentPath, string tabHeader = null)
        {
            selectedTabItem.AllowStorageItemDrop = true;

            string tabLocationHeader;
            Microsoft.UI.Xaml.Controls.FontIconSource fontIconSource = new Microsoft.UI.Xaml.Controls.FontIconSource();
            Microsoft.UI.Xaml.Controls.IconSource tabIcon;
            fontIconSource.FontFamily = App.Current.Resources["FluentUIGlyphs"] as FontFamily;

            if (currentPath == null || currentPath == "SidebarSettings/Text".GetLocalized())
            {
                tabLocationHeader = "SidebarSettings/Text".GetLocalized();
                fontIconSource.Glyph = "\xeb5d";
            }
            else if (currentPath == null || currentPath == "NewTab".GetLocalized() || currentPath == "Home")
            {
                tabLocationHeader = "NewTab".GetLocalized();
                fontIconSource.Glyph = "\xe90c";
            }
            else if (currentPath.Equals(App.AppSettings.DesktopPath, StringComparison.OrdinalIgnoreCase))
            {
                tabLocationHeader = "SidebarDesktop".GetLocalized();
                fontIconSource.Glyph = "\xe9f1";
            }
            else if (currentPath.Equals(App.AppSettings.DownloadsPath, StringComparison.OrdinalIgnoreCase))
            {
                tabLocationHeader = "SidebarDownloads".GetLocalized();
                fontIconSource.Glyph = "\xe91c";
            }
            else if (currentPath.Equals(App.AppSettings.DocumentsPath, StringComparison.OrdinalIgnoreCase))
            {
                tabLocationHeader = "SidebarDocuments".GetLocalized();
                fontIconSource.Glyph = "\xEA11";
            }
            else if (currentPath.Equals(App.AppSettings.PicturesPath, StringComparison.OrdinalIgnoreCase))
            {
                tabLocationHeader = "SidebarPictures".GetLocalized();
                fontIconSource.Glyph = "\xEA83";
            }
            else if (currentPath.Equals(App.AppSettings.MusicPath, StringComparison.OrdinalIgnoreCase))
            {
                tabLocationHeader = "SidebarMusic".GetLocalized();
                fontIconSource.Glyph = "\xead4";
            }
            else if (currentPath.Equals(App.AppSettings.VideosPath, StringComparison.OrdinalIgnoreCase))
            {
                tabLocationHeader = "SidebarVideos".GetLocalized();
                fontIconSource.Glyph = "\xec0d";
            }
            else if (currentPath.Equals(App.AppSettings.RecycleBinPath, StringComparison.OrdinalIgnoreCase))
            {
                var localSettings = ApplicationData.Current.LocalSettings;
                tabLocationHeader = localSettings.Values.Get("RecycleBin_Title", "Recycle Bin");
                fontIconSource.FontFamily = Application.Current.Resources["RecycleBinIcons"] as FontFamily;
                fontIconSource.Glyph = "\xEF87";
            }
            else if (App.AppSettings.OneDrivePath != null && currentPath.Equals(App.AppSettings.OneDrivePath, StringComparison.OrdinalIgnoreCase))
            {
                tabLocationHeader = "OneDrive";
                fontIconSource.Glyph = "\xe9b7";
            }
            else if (App.AppSettings.OneDriveCommercialPath != null && currentPath.Equals(App.AppSettings.OneDriveCommercialPath, StringComparison.OrdinalIgnoreCase))
            {
                tabLocationHeader = "OneDrive Commercial";
                fontIconSource.Glyph = "\xe9b7";
            }
            else
            {
                // If path is a drive's root
                if (NormalizePath(Path.GetPathRoot(currentPath)) == NormalizePath(currentPath))
                {
                    try
                    {
                        List<DriveInfo> drives = DriveInfo.GetDrives().ToList();
                        DriveInfo matchingDrive = drives.FirstOrDefault(x => NormalizePath(currentPath).Contains(NormalizePath(x.Name)));

                        if (matchingDrive != null)
                        {
                            //Go through types and set the icon according to type
                            string type = GetDriveTypeIcon(matchingDrive);
                            if (!string.IsNullOrWhiteSpace(type))
                            {
                                fontIconSource.Glyph = type;
                            }
                            else
                            {
                                fontIconSource.Glyph = "\xeb8b";    //Drive icon
                            }
                        }
                        else
                        {
                            fontIconSource.Glyph = "\xeb4a";    //Floppy icon
                        }
                    }
                    catch (Exception)
                    {
                        fontIconSource.Glyph = "\xeb8b";    //Fallback
                    }

                    tabLocationHeader = NormalizePath(currentPath);
                }
                else
                {
                    fontIconSource.Glyph = "\xea55";    //Folder icon
                    tabLocationHeader = currentPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Split('\\', StringSplitOptions.RemoveEmptyEntries).Last();
                }
            }
            if (tabHeader != null)
            {
                tabLocationHeader = tabHeader;
            }
            tabIcon = fontIconSource;
            selectedTabItem.Header = tabLocationHeader;
            selectedTabItem.IconSource = tabIcon;
        }

        private static void Control_ContentChanged(object sender, TabItemArguments e)
        {
            var matchingTabItem = MainWindow.AppInstances.SingleOrDefault(x => x.Control == sender);
            if (matchingTabItem == null)
            {
                return;
            }
            UpdateTabInfo(matchingTabItem, e.NavigationArg);
        }

        private static void UpdateTabInfo(TabItem tabItem, object navigationArg)
        {
            if (navigationArg is PaneNavigationArguments paneArgs)
            {
                var leftHeader = !string.IsNullOrEmpty(paneArgs.LeftPaneNavPathParam) ? new DirectoryInfo(paneArgs.LeftPaneNavPathParam).Name : null;
                var rightHeader = !string.IsNullOrEmpty(paneArgs.RightPaneNavPathParam) ? new DirectoryInfo(paneArgs.RightPaneNavPathParam).Name : null;
                if (leftHeader != null && rightHeader != null)
                {
                    SetSelectedTabInfo(tabItem, paneArgs.LeftPaneNavPathParam, $"{leftHeader} | {rightHeader}");
                }
                else
                {
                    SetSelectedTabInfo(tabItem, paneArgs.LeftPaneNavPathParam);
                }
            }
            else if (navigationArg is string pathArgs)
            {
                SetSelectedTabInfo(tabItem, pathArgs);
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void NavigateToNumberedTabKeyboardAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            int indexToSelect = 0;

            switch (sender.Key)
            {
                case VirtualKey.Number1:
                    indexToSelect = 0;
                    break;

                case VirtualKey.Number2:
                    indexToSelect = 1;
                    break;

                case VirtualKey.Number3:
                    indexToSelect = 2;
                    break;

                case VirtualKey.Number4:
                    indexToSelect = 3;
                    break;

                case VirtualKey.Number5:
                    indexToSelect = 4;
                    break;

                case VirtualKey.Number6:
                    indexToSelect = 5;
                    break;

                case VirtualKey.Number7:
                    indexToSelect = 6;
                    break;

                case VirtualKey.Number8:
                    indexToSelect = 7;
                    break;

                case VirtualKey.Number9:
                    // Select the last tab
                    indexToSelect = AppInstances.Count - 1;
                    break;
            }

            // Only select the tab if it is in the list
            if (indexToSelect < AppInstances.Count)
            {
                App.InteractionViewModel.TabStripSelectedIndex = indexToSelect;
            }
            args.Handled = true;
        }

        private void CloseSelectedTabKeyboardAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            if (App.InteractionViewModel.TabStripSelectedIndex >= AppInstances.Count)
            {
                var tabItem = AppInstances[AppInstances.Count - 1];
                MultitaskingControl?.RemoveTab(tabItem);
            }
            else
            {
                var tabItem = AppInstances[App.InteractionViewModel.TabStripSelectedIndex];
                MultitaskingControl?.RemoveTab(tabItem);
            }
            args.Handled = true;
        }

        private bool isRestoringClosedTab = false; // Avoid reopening two tabs

        private void AddNewInstanceAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            var shift = args.KeyboardAccelerator.Modifiers.HasFlag(VirtualKeyModifiers.Shift);
            if (!shift)
            {
                AddNewTabByPath(typeof(PaneHolderPage), "NewTab".GetLocalized());
            }
            else // ctrl + shif + t, restore recently closed tab
            {
                if (!isRestoringClosedTab && MultitaskingControl.RecentlyClosedTabs.Any())
                {
                    isRestoringClosedTab = true;
                    var lastTab = MultitaskingControl.RecentlyClosedTabs.Last();
                    MultitaskingControl.RecentlyClosedTabs.Remove(lastTab);
                    AddNewTabByParam(lastTab.TabItemArguments.InitialPageType, lastTab.TabItemArguments.NavigationArg);
                    isRestoringClosedTab = false;
                }
            }
            args.Handled = true;
        }

        private async void OpenNewWindowAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            var filesUWPUri = new Uri("files-uwp:");
            await Launcher.LaunchUriAsync(filesUWPUri);
        }

        private void HorizontalMultitaskingControl_Loaded(object sender, RoutedEventArgs e)
        {
            MultitaskingControl = horizontalMultitaskingControl;
        }

        private static string GetDriveTypeIcon(DriveInfo drive)
        {
            string type;

            switch (drive.DriveType)
            {
                case System.IO.DriveType.CDRom:
                    type = "\xec39";
                    break;

                case System.IO.DriveType.Fixed:
                    type = "\xeb8b";
                    break;

                case System.IO.DriveType.Network:
                    type = "\xeac2";
                    break;

                case System.IO.DriveType.NoRootDirectory:
                    type = "\xea5a";
                    break;

                case System.IO.DriveType.Ram:
                    type = "\xe9f2";
                    break;

                case System.IO.DriveType.Removable:
                    type = "\xec0a";
                    break;

                case System.IO.DriveType.Unknown:
                    if (NormalizePath(drive.Name) != NormalizePath("A:") && NormalizePath(drive.Name) != NormalizePath("B:"))
                    {
                        type = "\xeb8b";
                    }
                    else
                    {
                        type = "\xeb4a";    //Floppy icon
                    }
                    break;

                default:
                    type = "\xeb8b";    //Drive icon
                    break;
            }

            return type;
        }
    }
}
