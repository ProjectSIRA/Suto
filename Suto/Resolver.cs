namespace Suto;

internal class Resolver
{
    public static string[] DLLs(string directory, string[] fileList, bool invertFiles = false)
    {
        IEnumerable<string> files = Directory.GetFiles(directory).Where(path =>
        {
            FileInfo info = new(path);
            if (info.Extension != ".dll")
                return false;

            foreach (var sub in fileList)
                if (info.Name.Contains(sub))
                    return !invertFiles;

            return invertFiles;
        });
        return files.ToArray();
    }
}