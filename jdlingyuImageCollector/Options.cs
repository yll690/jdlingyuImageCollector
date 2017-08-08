using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

class Options : Dictionary<string, string>
{
    private string FileName = "";
    private string LineFeedChar = "\r\n";

    public Options()
    {
        FileName = Directory.GetCurrentDirectory()+"\\options.ini";
        readAllOptions();
    }

    public Options(string fileName)
    {
        FileName = fileName;
        readAllOptions();
    }

    private void saveFile(string fileName,string content, FileMode mode)
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
        Clear();
        string key = "", value = "", optionsString = File.ReadAllText(FileName, Encoding.Default);
        int indexOfLine = 0, indexOfEqual = -1;

        if (optionsString.IndexOf("\r\n") < 0)
            LineFeedChar = "\n";
        while (true)
        {
            indexOfEqual = optionsString.IndexOf("=", indexOfLine);
            if (indexOfEqual < 0)
                break;
            key = optionsString.Substring(indexOfLine, indexOfEqual - indexOfLine);
            indexOfLine = optionsString.IndexOf("\r\n", indexOfLine + 1) + LineFeedChar.Length;
            value = optionsString.Substring(indexOfEqual + 1, indexOfLine - indexOfEqual - 1 - LineFeedChar.Length);
            Add(key, value);
        }
    }

    public void saveAllOptions()
    {
        saveFile(FileName,allOptions(), FileMode.Create);
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