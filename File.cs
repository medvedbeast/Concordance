using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Concordance
{
    class File
    {
        public string fullPath;
        public string path;
        public FileStates state;

        public File(string fullPath)
        {
            this.fullPath = fullPath;
            string[] parts = fullPath.Split('\\');
            this.path = parts[parts.Length - 1];
            this.state = FileStates.NONE;
        }

        public string GetSubfile()
        {
            int position = fullPath.LastIndexOf("-hepple.xml");
            return fullPath.Substring(0, position) + ".txt";
        }

        public override string ToString()
        {
            return (state == FileStates.ERROR ? "ERROR       " : (state == FileStates.SUCCEED ? "SUCCEED   " : "")) + "..\\" + path;
        }
    }
}
