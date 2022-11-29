using System;
using System.IO;

namespace SWTORCombatParser.Utilities
{
    public static class ConvertToAppData
    {
        private static string oldPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "DubaTech", "SWTORCombatParser");
        private static string newPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DubaTech", "SWTORCombatParser");
        public static void ConvertFromProgramDataToAppData()
        {
            if (Directory.Exists(newPath))
                return;
            Directory.CreateDirectory(newPath);

            if (!Directory.Exists(oldPath))
                return;
            var currentDirectory = new DirectoryInfo(oldPath);
            currentDirectory.DeepCopy(newPath);
        }
    }
    public static class DirectoryInfoExtensions
    {
        public static void DeepCopy(this DirectoryInfo directory, string destinationDir)
        {
            foreach (string dir in Directory.GetDirectories(directory.FullName, "*", SearchOption.AllDirectories))
            {
                string dirToCreate = dir.Replace(directory.FullName, destinationDir);
                Directory.CreateDirectory(dirToCreate);
            }

            foreach (string newPath in Directory.GetFiles(directory.FullName, "*.*", SearchOption.AllDirectories))
            {
                File.Copy(newPath, newPath.Replace(directory.FullName, destinationDir), true);
            }
        }
    }
}
