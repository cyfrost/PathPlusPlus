using System.Collections.Generic;

namespace PathPlusPlus.Core
{
    public interface IBackupStore
    {
        /// <summary>Saves the state of <paramref name="name"/> (null = did not exist). Returns the backup file path.</summary>
        string Save(EnvScope scope, string name, RawValue before);

        IList<BackupInfo> List();

        BackupInfo Load(string file);

        /// <summary>True when <paramref name="file"/> is inside the backup folder for <paramref name="scope"/>.</summary>
        bool IsInStore(string file, EnvScope scope);

        string GetDirectory(EnvScope scope);
    }
}
