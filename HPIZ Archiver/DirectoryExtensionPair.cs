using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HPIZArchiver
{
    static class DirectoryExtensionPair
    {
        static readonly Dictionary<string, string[]> folderExtensionPairs = new Dictionary<string, string[]>()
        {
            { @"ae", new string[] { "txt" } },
            { @"ai", new string[] { "txt" } },
            { @"anim3d", new string[] { "bos" } },
            { @"anims", new string[] { "gaf" } },
            { @"bitmaps", new string[] { "pcx" } },
            { @"bitmaps\glamour", new string[] { "pcx" } },
            { @"camps", new string[] { "tdf" } },
            { @"camps\briefs", new string[] { "txt", "wav" } },
            { @"camps\briefs-french", new string[] { "txt", "wav" } },
            { @"camps\briefs-german", new string[] { "txt", "wav" } },
            { @"camps\briefs-italian", new string[] { "txt", "wav" } },
            { @"camps\briefs-spanish", new string[] { "txt", "wav" } },
            { @"camps\useonly", new string[] { "tdf" } },
            { @"download", new string[] { "tdf" } },
            { @"features", new string[] { "tdf" } },
            { @"fonts", new string[] { "fnt" } },
            { @"gamedata", new string[] { "tdf" } },
            { @"gamedate", new string[] { "tdf" } },
            { @"guie", new string[] { "gui" } },
            { @"guis", new string[] { "gui" } },
            { @"maps", new string[] { "ota", "tnt" } },
            { @"objects3d", new string[] { "3do" } },
            { @"palettes", new string[] { "pal", "alp", "lht", "shd" } },
            { @"scripts", new string[] { "bos", "cob" } },
            { @"sections", new string[] { "sct" } },
            { @"sounds", new string[] { "wav" } },
            { @"textures", new string[] { "gaf" } },
            { @"unitpice", new string[] { "pcx" } },
            { @"unitpics", new string[] { "pcx" } },
            { @"units", new string[] { "fbi" } },
            { @"unitse", new string[] { "fbi" } },
            { @"weapone", new string[] { "tdf" } },
            { @"weapons", new string[] { "tdf" } }
        };

        public static bool IsDirectoryExtensionKnow(string path)
        {
            path = path.ToLower();
            foreach (var folder in folderExtensionPairs.Keys)
                if (path.StartsWith(folder, StringComparison.OrdinalIgnoreCase))
                    switch (path.Replace(folder, string.Empty).Split('\\').Length)
                    {
                        case 4:
                            if (folder == "sections") goto case 2;
                            break;
                        case 3:
                            if (folder == "features") goto case 2;
                            break;
                        case 2:
                            foreach (var extension in folderExtensionPairs[folder])
                                if (path.EndsWith("." + extension, StringComparison.OrdinalIgnoreCase))
                                    return true;
                            break;
                    }
            return false;
        }
    }
}
