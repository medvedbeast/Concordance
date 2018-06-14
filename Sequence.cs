using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Concordance
{
    class Sequence
    {
        public string left1
        { get; set; }
        public string left0
        { get; set; }
        public string word
        { get; set; }
        public string msd
        { get; set; }
        public string right0
        { get; set; }
        public string right1
        { get; set; }
        public string wordBase
        { get; set; }

        public override string ToString()
        {
            if (left1 == "")
                left1 = " ";
            if (left0 == "")
                left0 = " ";
            if (word == "")
                word = " ";
            if (right0 == "")
                right0 = " ";
            if (right1 == "")
                right1 = " ";
            if (wordBase == "")
                wordBase = " ";
            if (msd == "")
                msd = " ";
            return "(\"" + left1 + "\", \"" + left0 + "\", \"" + word + "\", \"" + msd + "\", \"" + wordBase + "\", \"" + right0 + "\", \"" + right1 + "\");\n";
        }
        public void Replace(char[] symbols)
        {
            left1 = Remove(left1, symbols);
            left0 = Remove(left0, symbols);
            right0 = Remove(right0, symbols);
            right1 = Remove(right1, symbols);
        }

        string Remove(string source, char[] separators)
        {
            string result = "";
            for (int i = 0; i < source.Length; i++)
            {
                bool append = true;
                for (int j = 0; j < separators.Length; j++)
                {
                    if (source[i] == separators[j])
                    {
                        append = false;
                    }
                }
                if (append)
                {
                    result += source[i];
                }
            }
            return result;
        }
    }
}
