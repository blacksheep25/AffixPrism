using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
namespace AffixPrism.Core;

// Compatibility names are intentionally retained for existing installations.
public static class LegacyMigration
{
    public const string PreviousName = "ExileLens";
    public const string PreviousRepository = "https://github.com/blacksheep25/" + PreviousName;
    public static bool Import(string localData)
    {
        string source=Path.Combine(localData,PreviousName), target=Path.Combine(localData,"AffixPrism");
        if(!Directory.Exists(source)||Directory.Exists(target))return false;
        if((File.GetAttributes(source)&FileAttributes.ReparsePoint)!=0)throw new IOException("Linked legacy settings folders cannot be migrated automatically.");
        string stage=Path.Combine(localData,".AffixPrism-migration-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stage);
        try
        {
            void Copy(string from,string to)
            {
                foreach(var path in Directory.EnumerateFileSystemEntries(from))
                {
                    var attributes=File.GetAttributes(path);
                    if((attributes&FileAttributes.ReparsePoint)!=0)continue;
                    string name=Path.GetFileName(path);
                    if(name.Equals("updates",StringComparison.OrdinalIgnoreCase))continue;
                    string destination=Path.Combine(to,name);
                    if((attributes&FileAttributes.Directory)!=0) { Directory.CreateDirectory(destination); Copy(path,destination); }
                    else File.Copy(path,destination);
                }
            }
            Copy(source,stage);
            File.WriteAllText(Path.Combine(stage,"migration.txt"),"Imported existing settings and history. The original data was not modified.");
            Directory.Move(stage,target);
            return true;
        }
        finally
        {
            // Only the unique staging folder created above is eligible for cleanup.
            if(Directory.Exists(stage) && Path.GetDirectoryName(stage)==Path.GetFullPath(localData).TrimEnd(Path.DirectorySeparatorChar))Directory.Delete(stage,true);
        }
    }
}
