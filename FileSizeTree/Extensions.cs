using WindowsAPICodePack.Dialogs;

namespace FileSizeTree
{
    public static class Extensions
    {
        public const string TITLE_OPEN_DIR = "Open Directory";

        public static string OpenDirectory(string title = TITLE_OPEN_DIR, string currentDirectory = null)
        {
            var dlg = new CommonOpenFileDialog
            {
                Title = title,
                IsFolderPicker = true,
                InitialDirectory = currentDirectory,
                AddToMostRecentlyUsedList = false,
                AllowNonFileSystemItems = false,
                DefaultDirectory = currentDirectory,
                EnsureFileExists = true,
                EnsurePathExists = true,
                EnsureReadOnly = false,
                EnsureValidNames = true,
                Multiselect = false,
                ShowPlacesList = true
            };

            if (dlg.ShowDialog() != CommonFileDialogResult.Ok)
            {
                return null;
            }

            return dlg.FileName;
        }
    }
}
