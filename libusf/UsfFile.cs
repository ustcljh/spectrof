using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace libusf
{
    public class UsfFile
    {
        public Dictionary<string, string> Header = new();

        public UsfDataTable DataTable = new();

        public void LoadFromString(string str)
        {
            TextReader reader = new StringReader(str);
            LoadFromStream(reader);
        }

        public void LoadFromStream(TextReader stream)
        {
            Header = new();

            while (true)
            {
                string line = stream.ReadLine() ?? "";
                if (line == null)
                {
                    continue;
                }

                string lineBegin = line.Substring(0, line.IndexOf(" "));
                if (lineBegin == "#")
                {
                    if (line.Substring(line.IndexOf(" ") + 1).Trim() == "ENDHEAD")
                    {
                        break;
                    }
                    else
                    {
                        throw new Exception("Unexpected control #");
                    }
                }
                else if (lineBegin == "*")
                {
                    line = line.Substring(line.IndexOf(" ") + 1);
                    var key = line.Substring(0, line.IndexOf(":"));
                    var value = line.Substring(line.IndexOf(":") + 1);

                    value = value.Replace("\\n", "\n").Replace("\\\\", "\\");

                    Header.Add(key, value);
                }
            }

            StringBuilder builder = new();
            while (true)
            {
                string line = stream.ReadLine() ?? "".Trim();

                if (line == "")
                {
                    continue;
                }

                if (line == "# ENDTABLE")
                {
                    break;
                }

                builder.AppendLine(line);
            }

            DataTable = UsfDataTable.ParseUsfTableString(builder.ToString());
        }

        public string SaveToString()
        {
            StringWriter sw = new StringWriter();
            SaveToStream(sw);

            return sw.ToString();
        }

        public void SaveToStream(TextWriter stream)
        {
            foreach (var entry in Header)
            {
                string escapedLine = $"* {entry.Key}:{(entry.Value.Replace("\\", "\\\\").Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", "\\n"))}";
                stream.WriteLine(escapedLine);
            }
            stream.WriteLine("# ENDHEAD");

            stream.WriteLine(DataTable.ToUsfTableString());
            stream.WriteLine("# ENDTABLE");
        }
    }
}
