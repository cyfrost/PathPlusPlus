using System;
using Microsoft.Win32;

namespace PathPlusPlus.App
{
    /// <summary>
    /// Registers the Explorer right-click menu as plain registry verbs (no COM DLL is loaded into Explorer).
    /// Per-user installs write HKCU\Software\Classes (no admin needed); all-users installs write HKLM.
    /// On Windows 11 the menu appears under "Show more options" (Shift+F10).
    /// </summary>
    internal static class ShellIntegration
    {
        private const string VerbName = "PathPlusPlus";
        private const string FolderMenu = "PathPlusPlus.FolderMenu";
        private const string FileMenu = "PathPlusPlus.FileMenu";
        private const int SeparatorBefore = 0x20; // ECF_SEPARATORBEFORE

        private static readonly string[] FolderTargets = { @"Directory\shell", @"Directory\Background\shell", @"Drive\shell" };
        private const string FileTarget = @"*\shell";

        private sealed class Item
        {
            public Item(string key, string label, string arguments, bool separatorBefore = false)
            {
                Key = key;
                Label = label;
                Arguments = arguments;
                SeparatorBefore = separatorBefore;
            }

            public string Key { get; }
            public string Label { get; }
            public string Arguments { get; }
            public bool SeparatorBefore { get; }
        }

        private static readonly Item[] FolderItems =
        {
            new Item("1-add", "Add to PATH…", "--add-to-path \"%V\""),
            new Item("2-remove", "Remove from PATH…", "--remove-from-path \"%V\""),
            new Item("3-variable", "Create environment variable…", "--new-variable \"%V\"", separatorBefore: true),
            new Item("4-editor", "Open PATH editor", string.Empty, separatorBefore: true),
            new Item("5-windows", "Windows Environment Variables…", "--env-dialog"),
        };

        private static readonly Item[] FileItems =
        {
            new Item("1-add", "Add containing folder to PATH…", "--add-to-path --parent \"%1\""),
            new Item("2-remove", "Remove containing folder from PATH…", "--remove-from-path --parent \"%1\""),
            new Item("3-variable", "Create variable from this file's path…", "--new-variable \"%1\"", separatorBefore: true),
            new Item("4-variable-folder", "Create variable from containing folder…", "--new-variable --parent \"%1\""),
            new Item("5-editor", "Open PATH editor", string.Empty, separatorBefore: true),
            new Item("6-windows", "Windows Environment Variables…", "--env-dialog"),
        };

        public static void Register(bool allUsers, string exePath)
        {
            Unregister(allUsers, notify: false);
            using (RegistryKey classes = OpenClasses(allUsers))
            {
                foreach (string target in FolderTargets) WriteVerb(classes, target, FolderMenu, exePath);
                WriteVerb(classes, FileTarget, FileMenu, exePath);
                WriteMenu(classes, FolderMenu, FolderItems, exePath);
                WriteMenu(classes, FileMenu, FileItems, exePath);
            }
            Notify();
        }

        public static void Unregister(bool allUsers, bool notify = true)
        {
            using (RegistryKey classes = OpenClasses(allUsers))
            {
                foreach (string target in FolderTargets) classes.DeleteSubKeyTree(target + "\\" + VerbName, throwOnMissingSubKey: false);
                classes.DeleteSubKeyTree(FileTarget + "\\" + VerbName, throwOnMissingSubKey: false);
                classes.DeleteSubKeyTree(FolderMenu, throwOnMissingSubKey: false);
                classes.DeleteSubKeyTree(FileMenu, throwOnMissingSubKey: false);
            }
            if (notify) Notify();
        }

        private static RegistryKey OpenClasses(bool allUsers)
        {
            RegistryHive hive = allUsers ? RegistryHive.LocalMachine : RegistryHive.CurrentUser;
            using (RegistryKey root = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64))
            {
                return root.CreateSubKey(@"Software\Classes");
            }
        }

        private static void WriteVerb(RegistryKey classes, string target, string menuKey, string exePath)
        {
            using (RegistryKey verb = classes.CreateSubKey(target + "\\" + VerbName))
            {
                verb.SetValue("MUIVerb", "Path++");
                verb.SetValue("Icon", "\"" + exePath + "\",0");
                verb.SetValue("ExtendedSubCommandsKey", menuKey);
                verb.SetValue("MultiSelectModel", "Single");
            }
        }

        private static void WriteMenu(RegistryKey classes, string menuKey, Item[] items, string exePath)
        {
            foreach (Item item in items)
            {
                using (RegistryKey key = classes.CreateSubKey(menuKey + @"\shell\" + item.Key))
                {
                    key.SetValue("MUIVerb", item.Label);
                    if (item.SeparatorBefore) key.SetValue("CommandFlags", SeparatorBefore, RegistryValueKind.DWord);
                    using (RegistryKey command = key.CreateSubKey("command"))
                    {
                        string args = item.Arguments.Length == 0 ? string.Empty : " " + item.Arguments;
                        command.SetValue(string.Empty, "\"" + exePath + "\"" + args);
                    }
                }
            }
        }

        private static void Notify()
        {
            try
            {
                NativeMethods.SHChangeNotify(NativeMethods.SHCNE_ASSOCCHANGED, NativeMethods.SHCNF_IDLIST, IntPtr.Zero, IntPtr.Zero);
            }
            catch (Exception)
            {
                // Explorer picks the change up on its own eventually.
            }
        }
    }
}
