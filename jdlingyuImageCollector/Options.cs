using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace jdlingyuImageCollector
{
    class Options : Dictionary<string, string>
    {
        private string FileName = "";
        private string LineFeedChar = "\r\n";

        public Options()
        {
            FileName = Directory.GetCurrentDirectory() + "\\options.ini";
            readAllOptions();
        }

        public Options(string fileName)
        {
            FileName = fileName;
            readAllOptions();
        }

        private void saveFile(string fileName, string content, FileMode mode)
        {
            FileStream file = new FileStream(fileName, mode);
            byte[] data = Encoding.Default.GetBytes(content);
            file.Write(data, 0, data.Length);
            file.Flush();
            file.Close();
        }

        public void readAllOptions()
        {
            if (!File.Exists(FileName))
                return;
            int indexOfEqual = -1;
            string key = "", value = "";
            string[] lines = File.ReadAllLines(FileName, Encoding.Default);
            foreach (string line in lines)
            {
                if (line.StartsWith(";")) continue;
                indexOfEqual = line.IndexOf("=");
                key = line.Substring(0,indexOfEqual);
                value = line.Substring(indexOfEqual+1);
                Add(key, value);
            }
        }

        public void saveAllOptions()
        {
            saveFile(FileName, allOptions(), FileMode.Create);
        }

        public string allOptions()
        {
            string optionsString = "";
            foreach (KeyValuePair<string, string> keyValuePair in this)
            {
                optionsString += keyValuePair.Key + "=" + keyValuePair.Value + LineFeedChar;
            }
            return optionsString;
        }
    }
}