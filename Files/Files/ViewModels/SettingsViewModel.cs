using Files.Common;
using Files.Controllers;
using Files.DataModels;
using Files.Enums;
using Files.Filesystem;
using Files.Helpers;
using Files.Views;
using Microsoft.AppCenter.Analytics;
using Microsoft.Toolkit.Mvvm.ComponentModel;
using Microsoft.Toolkit.Mvvm.Input;
using Microsoft.Toolkit.Uwp.Extensions;
using Microsoft.Toolkit.Uwp.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Windows.Globalization;
using Windows.Storage;
using Windows.System;

namespace Files.ViewModels
{
    public class SettingsViewModel : ObservableObject
    {
        private readonly ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;

        public CloudDrivesManager CloudDrivesManager { get; private set; }

        public TerminalController TerminalController { get; set; }

        private async Task<SettingsViewModel> Initialize()
        {
            DetectDateTimeFormat();
            PinSidebarLocationItems();
            DetectRecycleBinPreference();
            DetectQuickLook();

            // Load the supported languages
            var supportedLang = ApplicationLanguages.ManifestLanguages;
            DefaultLanguages = new ObservableCollection<DefaultLanguageModel> { new DefaultLanguageModel(null) };
            foreach (var lang in supportedLang)
            {
                DefaultLanguages.Add(new DefaultLanguageModel(lang));
            }

            //DetectWSLDistros();
            TerminalController = await TerminalController.CreateInstance();

            // Send analytics to AppCenter
            TrackAnalytics();

            return this;
        }

        public static Task<SettingsViewModel> CreateInstance()
        {
            var settings = new SettingsViewModel();
            return settings.Initialize();
        }

        private SettingsViewModel()
        {
        }

        private void TrackAnalytics()
        {
            Analytics.TrackEvent($"{nameof(DisplayedTimeStyle)} {DisplayedTimeStyle}");
            Analytics.TrackEvent($"{nameof(ThemeHelper.RootTheme)} {ThemeHelper.RootTheme}");
            Analytics.TrackEvent($"{nameof(PinRecycleBinToSideBar)} {PinRecycleBinToSideBar}");
            Analytics.TrackEvent($"{nameof(ShowFileExtensions)} {ShowFileExtensions}");
            Analytics.TrackEvent($"{nameof(ShowConfirmDeleteDialog)} {ShowConfirmDeleteDialog}");
            Analytics.TrackEvent($"{nameof(IsAcrylicDisabled)} {IsAcrylicDisabled}");
            Analytics.TrackEvent($"{nameof(ShowFileOwner)} {ShowFileOwner}");
            Analytics.TrackEvent($"{nameof(IsHorizontalTabStripEnabled)} {IsHorizontalTabStripEnabled}");
            Analytics.TrackEvent($"{nameof(IsDualPaneEnabled)} {IsDualPaneEnabled}");
            Analytics.TrackEvent($"{nameof(AlwaysOpenDualPaneInNewTab)} {AlwaysOpenDualPaneInNewTab}");
            Analytics.TrackEvent($"{nameof(IsVerticalTabFlyoutEnabled)} {IsVerticalTabFlyoutEnabled}");
            Analytics.TrackEvent($"{nameof(AreHiddenItemsVisible)} {AreHiddenItemsVisible}");
            Analytics.TrackEvent($"{nameof(ShowDrivesWidget)} {ShowDrivesWidget}");
            Analytics.TrackEvent($"{nameof(ListAndSortDirectoriesAlongsideFiles)} {ListAndSortDirectoriesAlongsideFiles}");
            Analytics.TrackEvent($"{nameof(AreRightClickContentMenuAnimationsEnabled)} {AreRightClickContentMenuAnimationsEnabled}");
        }

        public static async void OpenLogLocation()
        {
            await Launcher.LaunchFolderAsync(ApplicationData.Current.LocalFolder);
        }

        public static async void ReportIssueOnGitHub()
        {
            await Launcher.LaunchUriAsync(new Uri(@"https://github.com/files-community/files-uwp/issues/new/choose"));
        }

        public GridLength SidebarWidth
        {
            get => new GridLength(Math.Min(Math.Max(Get(200d), 200d), 500d), GridUnitType.Pixel);
            set => Set(value.Value);
        }

        public void DetectQuickLook()
        {
            // TODO: Detect QuickLook with in-project code
            //try
            //{
            //    ApplicationData.Current.LocalSettings.Values["Arguments"] = "StartupTasks";
            //    await FullTrustProcessLauncher.LaunchFullTrustProcessForCurrentAppAsync();
            //}
            //catch (Exception ex)
            //{
            //    NLog.LogManager.GetCurrentClassLogger().Warn(ex, ex.Message);
            //}
        }

        private void DetectRecycleBinPreference()
        {
            if (localSettings.Values["PinRecycleBin"] == null)
            {
                localSettings.Values["PinRecycleBin"] = true;
            }

            if ((bool)localSettings.Values["PinRecycleBin"] == true)
            {
                PinRecycleBinToSideBar = true;
            }
            else
            {
                PinRecycleBinToSideBar = false;
            }
        }

        private void PinSidebarLocationItems()
        {
            AddDefaultLocations();
        }

        private void AddDefaultLocations()
        {
            MainWindow.SideBarItems.Add(new LocationItem
            {
                Text = "SidebarHome".GetLocalized(),
                Font = Application.Current.Resources["FluentUIGlyphs"] as FontFamily,
                Glyph = "\uea80",
                IsDefaultLocation = true,
                Path = "Home"
            });
        }

        private async void DetectWSLDistros()
        {
            try
            {
                var distroFolder = await StorageFolder.GetFolderFromPathAsync(@"\\wsl$\");
                if ((await distroFolder.GetFoldersAsync()).Count > 0)
                {
                    AreLinuxFilesSupported = false;
                }

                foreach (StorageFolder folder in await distroFolder.GetFoldersAsync())
                {
                    Uri logoURI = null;
                    if (folder.DisplayName.Contains("ubuntu", StringComparison.OrdinalIgnoreCase))
                    {
                        logoURI = new Uri("ms-appx:///Assets/WSL/ubuntupng.png");
                    }
                    else if (folder.DisplayName.Contains("kali", StringComparison.OrdinalIgnoreCase))
                    {
                        logoURI = new Uri("ms-appx:///Assets/WSL/kalipng.png");
                    }
                    else if (folder.DisplayName.Contains("debian", StringComparison.OrdinalIgnoreCase))
                    {
                        logoURI = new Uri("ms-appx:///Assets/WSL/debianpng.png");
                    }
                    else if (folder.DisplayName.Contains("opensuse", StringComparison.OrdinalIgnoreCase))
                    {
                        logoURI = new Uri("ms-appx:///Assets/WSL/opensusepng.png");
                    }
                    else if (folder.DisplayName.Contains("alpine", StringComparison.OrdinalIgnoreCase))
                    {
                        logoURI = new Uri("ms-appx:///Assets/WSL/alpinepng.png");
                    }
                    else
                    {
                        logoURI = new Uri("ms-appx:///Assets/WSL/genericpng.png");
                    }

                    MainWindow.SideBarItems.Add(new WSLDistroItem()
                    {
                        Text = folder.DisplayName,
                        Path = folder.Path,
                        Logo = logoURI
                    });
                }
            }
            catch (Exception)
            {
                // WSL Not Supported/Enabled
                AreLinuxFilesSupported = false;
            }
        }

        private void DetectDateTimeFormat()
        {
            if (localSettings.Values[Constants.LocalSettings.DateTimeFormat] != null)
            {
                if (localSettings.Values[Constants.LocalSettings.DateTimeFormat].ToString() == "Application")
                {
                    DisplayedTimeStyle = TimeStyle.Application;
                }
                else if (localSettings.Values[Constants.LocalSettings.DateTimeFormat].ToString() == "System")
                {
                    DisplayedTimeStyle = TimeStyle.System;
                }
            }
            else
            {
                localSettings.Values[Constants.LocalSettings.DateTimeFormat] = "Application";
            }
        }

        private TimeStyle displayedTimeStyle = TimeStyle.Application;

        public TimeStyle DisplayedTimeStyle
        {
            get => displayedTimeStyle;
            set
            {
                SetProperty(ref displayedTimeStyle, value);
                if (value.Equals(TimeStyle.Application))
                {
                    localSettings.Values[Constants.LocalSettings.DateTimeFormat] = "Application";
                }
                else if (value.Equals(TimeStyle.System))
                {
                    localSettings.Values[Constants.LocalSettings.DateTimeFormat] = "System";
                }
            }
        }

        #region DetailsView Column Settings

        /// <summary>
        /// Gets or sets a value indicating whether or not the date column should be visible.
        /// </summary>
        public bool ShowDateColumn
        {
            get => Get(true);
            set => Set(value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether or not the type column should be visible.
        /// </summary>
        public bool ShowTypeColumn
        {
            get => Get(true);
            set => Set(value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether or not the size column should be visible.
        /// </summary>
        public bool ShowSizeColumn
        {
            get => Get(true);
            set => Set(value);
        }

        #endregion DetailsView Column Settings

        #region CommonPaths

        public string OneDriveCommercialPath { get; set; } = Environment.GetEnvironmentVariable("OneDriveCommercial");
        public string OneDrivePath { get; set; } = Environment.GetEnvironmentVariable("OneDriveConsumer");
        public string DesktopPath { get; set; } = UserDataPaths.GetDefault().Desktop;
        public string DocumentsPath { get; set; } = UserDataPaths.GetDefault().Documents;
        public string DownloadsPath { get; set; } = UserDataPaths.GetDefault().Downloads;
        public string PicturesPath { get; set; } = UserDataPaths.GetDefault().Pictures;
        public string MusicPath { get; set; } = UserDataPaths.GetDefault().Music;
        public string VideosPath { get; set; } = UserDataPaths.GetDefault().Videos;

        private string tempPath = (string)Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\Environment", "TEMP", null);

        public string TempPath
        {
            get => tempPath;
            set => SetProperty(ref tempPath, value);
        }

        private string localAppDataPath = UserDataPaths.GetDefault().LocalAppData;

        public string LocalAppDataPath
        {
            get => localAppDataPath;
            set => SetProperty(ref localAppDataPath, value);
        }

        private string homePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        public string HomePath
        {
            get => homePath;
            set => SetProperty(ref homePath, value);
        }

        private string winDirPath = Environment.GetFolderPath(Environment.SpecialFolder.Windows);

        public string WinDirPath
        {
            get => winDirPath;
            set => SetProperty(ref winDirPath, value);
        }

        // Currently is the command to open the folder from cmd ("cmd /c start Shell:RecycleBinFolder")
        public string RecycleBinPath { get; set; } = @"Shell:RecycleBinFolder";

        #endregion CommonPaths

        #region FilesAndFolder

        /// <summary>
        /// Gets or sets a value indicating whether or not file extensions should be visible.
        /// </summary>
        public bool ShowFileExtensions
        {
            get => Get(true);
            set => Set(value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether or not hidden items should be visible.
        /// </summary>
        public bool AreHiddenItemsVisible
        {
            get => Get(false);
            set => Set(value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether or not system items should be visible.
        /// </summary>
        public bool AreSystemItemsHidden
        {
            get => Get(true);
            set => Set(value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether or not files should be sorted together with folders.
        /// </summary>
        public bool ListAndSortDirectoriesAlongsideFiles
        {
            get => Get(false);
            set => Set(value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether or not items should open with one click.
        /// </summary>
        public bool OpenItemsWithOneclick
        {
            get => Get(false);
            set => Set(value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether or not to search unindexed items.
        /// </summary>
        public bool SearchUnindexedItems
        {
            get => Get(false);
            set => Set(value);
        }

        /// <summary>
        /// Enables saving a unique layout mode, gridview size and sort direction per folder
        /// </summary>
        public bool AreLayoutPreferencesPerFolder
        {
            get => Get(true);
            set => Set(value);
        }

        #endregion FilesAndFolder

        #region Multitasking

        /// <summary>
        /// Gets or sets a value indicating whether or not to automatically switch between the horizontal and vertical tab layout.
        /// </summary>
        public bool IsMultitaskingExperienceAdaptive
        {
            get => Get(true);
            set => Set(value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether or not to enable the vertical tab layout.
        /// </summary>
        public bool IsVerticalTabFlyoutEnabled
        {
            get => Get(false);
            set => Set(value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether or not to enable the horizontal tab layout.
        /// </summary>
        public bool IsHorizontalTabStripEnabled
        {
            get => Get(false);
            set => Set(value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether or not to enable dual pane feature.
        /// </summary>
        public bool IsDualPaneEnabled
        {
            get => Get(false);
            set => Set(value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether or not to always open a second pane when opening a new tab.
        /// </summary>
        public bool AlwaysOpenDualPaneInNewTab
        {
            get => Get(false);
            set => Set(value);
        }

        #endregion Multitasking

        #region Widgets

        /// <summary>
        /// Gets or sets a value indicating whether or not the library cards widget should be visible.
        /// </summary>
        public bool ShowLibraryCardsWidget
        {
            get => Get(true);
            set => Set(value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether or not the recent files widget should be visible.
        /// </summary>
        public bool ShowRecentFilesWidget
        {
            get => Get(true);
            set => Set(value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether or not the drives widget should be visible.
        /// </summary>
        public bool ShowDrivesWidget
        {
            get => Get(true);
            set => Set(value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether or not the Bundles widget should be visible.
        /// </summary>
        public bool ShowBundlesWidget
        {
            get => Get(false);
            set => Set(value);
        }

        #endregion Widgets

        #region Preferences

        /// <summary>
        /// Gets or sets a value indicating whether or not the confirm delete dialog should show when deleting items.
        /// </summary>
        public bool ShowConfirmDeleteDialog
        {
            get => Get(true);
            set => Set(value);
        }

        /// <summary>
        /// Gets or sets a value indicating the application language.
        /// </summary>
        public DefaultLanguageModel CurrentLanguage { get; set; } = new DefaultLanguageModel(ApplicationLanguages.PrimaryLanguageOverride);

        /// <summary>
        /// Gets or sets an ObservableCollection of the support langauges.
        /// </summary>
        public ObservableCollection<DefaultLanguageModel> DefaultLanguages { get; private set; }

        /// <summary>
        /// Gets or sets a value indicating the default language.
        /// </summary>
        public DefaultLanguageModel DefaultLanguage
        {
            get
            {
                return DefaultLanguages.FirstOrDefault(dl => dl.ID == ApplicationLanguages.PrimaryLanguageOverride) ??
                           DefaultLanguages.FirstOrDefault();
            }
            set
            {
                ApplicationLanguages.PrimaryLanguageOverride = value.ID;
            }
        }

        //TODO: This shouldn't pin recycle bin to the sidebar, it should only hold the value whether it should or shouldn't be pinned
        /// <summary>
        /// Gets or sets a value indicating whether or not recycle bin should be pinned to the sidebar.
        /// </summary>
        private bool pinRecycleBinToSideBar;

        public bool PinRecycleBinToSideBar
        {
            get => pinRecycleBinToSideBar;
            set
            {
                if (value != pinRecycleBinToSideBar)
                {
                    SetProperty(ref pinRecycleBinToSideBar, value);
                    if (value == true)
                    {
                        localSettings.Values["PinRecycleBin"] = true;
                        var recycleBinItem = new LocationItem
                        {
                            Text = localSettings.Values.Get("RecycleBin_Title", "Recycle Bin"),
                            Font = Application.Current.Resources["RecycleBinIcons"] as FontFamily,
                            Glyph = "\uEF87",
                            IsDefaultLocation = true,
                            Path = RecycleBinPath
                        };
                        // Add recycle bin to sidebar, title is read from LocalSettings (provided by the fulltrust process)
                        // TODO: the very first time the app is launched localized name not available
                        MainWindow.SideBarItems.Insert(MainWindow.SideBarItems.Where(item => item is LocationItem).Count(), recycleBinItem);
                    }
                    else
                    {
                        localSettings.Values["PinRecycleBin"] = false;
                        foreach (INavigationControlItem item in MainWindow.SideBarItems.ToList())
                        {
                            if (item is LocationItem && item.Path == RecycleBinPath)
                            {
                                MainWindow.SideBarItems.Remove(item);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Gets or sets the value indicating whether the preview pane should adapt to the width of the view.
        /// </summary>
        public bool EnableAdaptivePreviewPane
        {
            get => Get(true);
            set => Set(value);
        }

        #endregion Preferences

        #region Appearance

        /// <summary>
        /// Gets or sets a value indicating whether or not acrylic is enabled.
        /// </summary>
        public bool IsAcrylicDisabled
        {
            get => Get(true);
            set => Set(value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether or not right click context menu animations are enabled.
        /// </summary>
        public bool AreRightClickContentMenuAnimationsEnabled
        {
            get => Get(false);
            set => Set(value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether or not to move overflow menu items into a sub menu.
        /// </summary>
        public bool MoveOverflowMenuItemsToSubMenu
        {
            get => Get(true);
            set => Set(value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether or not the copy location menu item is shown in the right click context menu.
        /// </summary>
        public bool ShowCopyLocationMenuItem
        {
            get => Get(true);
            set => Set(value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether or not the open in new tab menu item is shown in the right click context menu.
        /// </summary>
        public bool ShowOpenInNewTabMenuItem
        {
            get => Get(true);
            set => Set(value);
        }

        #endregion Appearance

        #region Experimental

        /// <summary>
        /// Gets or sets a value indicating whether or not to show the item owner in the properties window.
        /// </summary>
        public bool ShowFileOwner
        {
            get => Get(false);
            set => Set(value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether or not to cache files and folders.
        /// </summary>
        public bool UseFileListCache
        {
            get => Get(true);
            set => Set(value);
        }

        #endregion Experimental

        #region Startup

        /// <summary>
        /// Gets or sets a value indicating whether or not to navigate to a specific location when launching the app.
        /// </summary>
        public bool OpenASpecificPageOnStartup
        {
            get => Get(false);
            set => Set(value);
        }

        /// <summary>
        /// Gets or sets a value indicating the default startup location.
        /// </summary>
        public string OpenASpecificPageOnStartupPath
        {
            get => Get("");
            set => Set(value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether or not continue the last session whenever the app is launched.
        /// </summary>
        public bool ContinueLastSessionOnStartUp
        {
            get => Get(false);
            set => Set(value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether or not to open a page when the app is launched.
        /// </summary>
        public bool OpenNewTabPageOnStartup
        {
            get => Get(true);
            set => Set(value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether or not opening the app from the jumplist should open the directory in a new instance.
        /// </summary>
        public bool AlwaysOpenANewInstance
        {
            get => Get(false);
            set => Set(value);
        }

        public string[] PagesOnStartupList
        {
            get => Get<string[]>(null);
            set => Set(value);
        }

        public string[] LastSessionPages
        {
            get => Get<string[]>(null);
            set => Set(value);
        }

        #endregion Startup

        /// <summary>
        /// Gets or sets a value indicating whether or not WSL is supported.
        /// </summary>
        public bool AreLinuxFilesSupported
        {
            get => Get(false);
            set => Set(value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether or not to show a teaching tip informing the user about the status center.
        /// </summary>
        public bool ShowStatusCenterTeachingTip
        {
            get => Get(true);
            set => Set(value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether or not to restore tabs after restarting the app.
        /// </summary>
        public bool ResumeAfterRestart
        {
            get => Get(false);
            set => Set(value);
        }

        public event EventHandler ThemeModeChanged;

        public RelayCommand UpdateThemeElements => new RelayCommand(() =>
        {
            ThemeModeChanged?.Invoke(this, EventArgs.Empty);
        });

        public AcrylicTheme AcrylicTheme { get; set; }

        public FolderLayoutModes DefaultLayoutMode
        {
            get => (FolderLayoutModes)Get((byte)FolderLayoutModes.DetailsView); // Details View
            set => Set((byte)value);
        }

        public int DefaultGridViewSize
        {
            get => Get(Constants.Browser.GridViewBrowser.GridViewSizeSmall);
            set => Set(value);
        }

        public SortDirection DefaultDirectorySortDirection
        {
            get => (SortDirection)Get((byte)SortDirection.Ascending);
            set => Set((byte)value);
        }

        public SortOption DefaultDirectorySortOption
        {
            get => (SortOption)Get((byte)SortOption.Name);
            set => Set((byte)value);
        }

        #region ReadAndSaveSettings

        public bool Set<TValue>(TValue value, [CallerMemberName] string propertyName = null)
        {
            propertyName = propertyName != null && propertyName.StartsWith("set_", StringComparison.InvariantCultureIgnoreCase)
                ? propertyName.Substring(4)
                : propertyName;

            TValue originalValue = default;

            if (localSettings.Values.ContainsKey(propertyName))
            {
                originalValue = Get(originalValue, propertyName);

                localSettings.Values[propertyName] = value;
                if (!base.SetProperty(ref originalValue, value, propertyName))
                {
                    return false;
                }
            }
            else
            {
                localSettings.Values[propertyName] = value;
            }

            return true;
        }

        public TValue Get<TValue>(TValue defaultValue, [CallerMemberName] string propertyName = null)
        {
            var name = propertyName ??
                       throw new ArgumentNullException(nameof(propertyName), "Cannot store property of unnamed.");

            name = name.StartsWith("get_", StringComparison.InvariantCultureIgnoreCase)
                ? propertyName.Substring(4)
                : propertyName;

            if (localSettings.Values.ContainsKey(name))
            {
                var value = localSettings.Values[name];

                if (!(value is TValue tValue))
                {
                    if (value is IConvertible)
                    {
                        tValue = (TValue)Convert.ChangeType(value, typeof(TValue));
                    }
                    else
                    {
                        var valueType = value.GetType();
                        var tryParse = typeof(TValue).GetMethod("TryParse", BindingFlags.Instance | BindingFlags.Public);

                        if (tryParse == null)
                        {
                            return default;
                        }

                        var stringValue = value.ToString();
                        tValue = default;

                        var tryParseDelegate =
                            (TryParseDelegate<TValue>)Delegate.CreateDelegate(valueType, tryParse, false);

                        tValue = (tryParseDelegate?.Invoke(stringValue, out tValue) ?? false) ? tValue : default;
                    }

                    Set(tValue, propertyName); // Put the corrected value in settings.
                    return tValue;
                }
                return tValue;
            }

            localSettings.Values[propertyName] = defaultValue;

            return defaultValue;
        }

        private delegate bool TryParseDelegate<TValue>(string inValue, out TValue parsedValue);

        #endregion ReadAndSaveSettings
    }
}