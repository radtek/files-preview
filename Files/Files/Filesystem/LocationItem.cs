using Microsoft.UI.Xaml.Media;

namespace Files.Filesystem
{
    public class LocationItem : INavigationControlItem
    {
        public string Glyph { get; set; }
        public string Text { get; set; }

        private string path;
        public string Path
        {
            get => path;
            set
            {
                path = value;
                HoverDisplayText = Path.Contains("?") ? Text : Path;
            }
        }

        public string HoverDisplayText { get; private set; }
        public FontFamily Font { get; set; } = new FontFamily("Segoe MDL2 Assets");
        public NavigationControlItemType ItemType => NavigationControlItemType.Location;
        public bool IsDefaultLocation { get; set; }
    }

    public class HeaderTextItem : INavigationControlItem
    {
        public string Glyph { get; set; } = null;

        public string Text { get; set; }

        private string path;
        public string Path
        {
            get => path;
            set
            {
                path = value;
                HoverDisplayText = Path.Contains("?") ? Text : Path;
            }
        }

        public string HoverDisplayText { get; private set; }

        public NavigationControlItemType ItemType => NavigationControlItemType.Header;
    }
}