using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Storage;

namespace SWTORCombatParser.DataStructures.ClassInfos
{
    public class AllSWTORClasses
    {
        public List<SWTORClass> AllClasses { get; set; }
    }
    public static class ClassLoader
    {
        public async static Task<List<SWTORClass>> LoadAllClasses()
        {
            StorageFolder installedLocation = null;


            installedLocation = Package.Current.InstalledLocation;


            //var folder = Windows.Storage.ApplicationData.Current.LocalFolder.GetFolderAsync("DataStructures/ClassInfos/").GetResults();
            var topMan = await installedLocation.GetFolderAsync("SWTORCombatParser");
            var folder = await topMan.GetFolderAsync("DataStructures");
            var subFolder = await folder.GetFolderAsync("ClassInfos");
            var file = await subFolder.GetFileAsync("Classes.json");
            var text = FileIO.ReadTextAsync(file).GetResults();
            //var allClasses = File.ReadAllText("DataStructures/ClassInfos/Classes.json");
            return JsonConvert.DeserializeObject<AllSWTORClasses>(text).AllClasses;
        }
    }
}
