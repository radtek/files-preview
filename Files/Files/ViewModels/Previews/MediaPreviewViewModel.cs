using Files.Filesystem;
using Microsoft.UI.Xaml;
using System.Collections.Generic;
using Windows.Media.Core;

namespace Files.ViewModels.Previews
{
    public class MediaPreviewViewModel : BasePreviewModel
    {
        private MediaSource source;

        public MediaPreviewViewModel(ListedItem item) : base(item)
        {
        }

        public static List<string> Extensions => new List<string>() {
            // Video
            ".mp4", ".webm", ".ogg", ".mov", ".qt", ".mp4", ".m4v", ".mp4v", ".3g2", ".3gp2", ".3gp", ".3gpp", ".mkv",
            // Audio
            ".mp3", ".m4a", ".wav", ".wma", ".aac", ".adt", ".adts", ".cda",
        };

        public MediaSource Source
        {
            get => source;
            set => SetProperty(ref source, value);
        }

        public override void LoadPreviewAndDetails()
        {
            base.LoadSystemFileProperties();
            Source = MediaSource.CreateFromStorageFile(ItemFile);
        }

        public override void PreviewControlBase_Unloaded(object sender, RoutedEventArgs e)
        {
            Source = null;
            base.PreviewControlBase_Unloaded(sender, e);
        }
    }
}