using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

namespace jdlingyuImageCollector
{
    class Pictures
    {
        public int index;
        public string url;
        public string title;
        public string datetime;
        public string category;
        public string tags;
        public List<string> picUrls = new List<string>();

    }

    class Program
    {
        static string currentDirectory = Directory.GetCurrentDirectory() + "\\";
        static Options options;
        static string url = "";
        static string domain = "http://www.jdlingyu.wang/";
        static string pictureLocation = currentDirectory;
        static bool logToFile = true;
        static List<int> ignoreList = new List<int>();
        static List<int> catalog = new List<int>();

        static string defaultOptionString = "";


        //获取HTML
        static string GetWebClient(string url)
        {
            string strHTML = "";
            WebClient myWebClient = new WebClient();
            Stream myStream = myWebClient.OpenRead(url);
            StreamReader sr = new StreamReader(myStream, System.Text.Encoding.GetEncoding("utf-8"));
            strHTML = sr.ReadToEnd();
            myStream.Close();
            return strHTML;
        }

        //保存文件
        static void addToFile(string fileName, string content)
        {
            FileStream file = new FileStream(fileName, FileMode.Append);
            byte[] data = System.Text.Encoding.Default.GetBytes(content);
            file.Write(data, 0, data.Length);
            file.Flush();
            file.Close();
        }

        static void log(string logText, bool error, bool catalog)
        {
            if (error)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(logText);
                Console.ForegroundColor = ConsoleColor.Gray;
            }
            else
                Console.WriteLine(logText);
            if (logToFile)
                if (catalog)
                    addToFile(currentDirectory + "catalogLog.txt", DateTime.Now + " " + logText + "\n");
                else
                    addToFile(currentDirectory + "log.txt", DateTime.Now + " " + logText + "\n");
        }

        static void log(string logText)
        {
            log(logText, false, false);
        }

        static void log(string logText, bool error)
        {
            log(logText, error, false);
        }

        static void clog(string logText, bool error)
        {
            log(logText, error, true);
        }

        static void clog(string logText)
        {
            log(logText, false, true);
        }

        static List<int> getIndexes(string indexesString, char separator)
        {
            List<int> indexes = new List<int>();
            foreach (string s in indexesString.Split(separator))
            {
                if (s != "")
                    indexes.Add(Convert.ToInt32(s));
            }
            return indexes;
        }

        static List<int> getIndexes(string indexesString)
        {
            return getIndexes(indexesString, '\n');
        }

        //收集目录
        static void collectIndex()
        {
            if (File.Exists(currentDirectory + "catalog.txt"))
                catalog = getIndexes(File.ReadAllText(currentDirectory + "catalog.txt", Encoding.Default));

            Regex indexRE = new Regex(domain + "[0-9]{1,6}/");
            MatchCollection matches;
            string HTML = "";
            int index = 0, lengthOfCatalog = 0, startPosition = -1, endPosition = -1, retrytimes = 5, temp = 0;
            bool finished = false;
            List<int> newCatalog = new List<int>();
            StringBuilder catalogSB = new StringBuilder();
            while (!finished && retrytimes > 0)
            {
                clog("正在加载目录页" + index);
                try
                {
                    HTML = GetWebClient(domain + "page/" + index + "/");
                    retrytimes = 5;
                    startPosition = HTML.IndexOf("<div id=\"postlist\" class=\"clx\">");
                    endPosition = HTML.IndexOf("<div id=\"pagenavi-fixed\">", startPosition);
                    if (startPosition <= 0 || endPosition <= 0)
                    {
                        if (HTML.IndexOf("你所找的文章不存在! 我们强烈的为你推荐以下文章") >= 0)
                        {
                            clog("识别目录列表完毕");
                            finished = true;
                            break;
                        }
                        clog("目录页提取出错" + index, true);
                        continue;
                    }

                    HTML = HTML.Substring(startPosition, endPosition - startPosition);
                    matches = indexRE.Matches(HTML);
                    for (int i = 0; i < matches.Count; i += 2)
                    {
                        temp = Convert.ToInt32(matches[i].ToString().Substring(domain.Length, matches[i].ToString().IndexOf("/", domain.Length) - domain.Length));
                        if (ignoreList.Contains(temp))
                            continue;
                        else if (catalog.Contains(temp))
                        {
                            clog("识别目录列表完毕");
                            finished = true;
                            break;
                        }
                        newCatalog.Add(temp);
                        lengthOfCatalog++;
                        clog(matches[i].ToString());
                    }
                    Console.WriteLine();
                }
                catch (WebException error)
                {
                    clog("加载失败，" + error.Message, true);
                    Console.WriteLine(retrytimes + "次重试后将结束识别目录列表\n");
                    retrytimes--;
                }
                index++;
            }
            catalog.Sort();
            newCatalog.Sort();
            foreach (int i in newCatalog)
                catalog.Add(i);
            catalogSB.Clear();
            foreach (int i in newCatalog)
                catalogSB.AppendLine(i.ToString());
            addToFile(currentDirectory + "catalog.txt", catalogSB.ToString());
        }

        //收集信息
        static void collectInformation(string HTML, ref Pictures res)
        {
            int indexOfTitle = -1, endOfTitle = -1, indexOfDatetime = -1, endOfDatetime = -1, indexOfCategory = -1, endOfCategory = -1, indexOfTag = -1, endOfTag = -1;
            log("正在收集网页信息");

            indexOfTitle = HTML.IndexOf("<h2 class=\"main-title\">");
            endOfTitle = HTML.IndexOf("</h2>", indexOfTitle + 1);
            if (indexOfTitle < 0 || endOfTitle <= 0)
                log("查找标题出错", true);
            res.title = HTML.Substring(indexOfTitle + 23, endOfTitle - indexOfTitle - 23);

            indexOfDatetime = HTML.IndexOf("<span class=\"post-span\">");
            endOfDatetime = HTML.IndexOf("</span>", indexOfDatetime + 1);
            if (indexOfDatetime < 0 || endOfDatetime < 0)
                log("查找日期出错", true);
            res.datetime = HTML.Substring(indexOfDatetime + 24, endOfDatetime - indexOfDatetime - 24);

            indexOfCategory = HTML.IndexOf("rel=\"category tag\">");
            endOfCategory = HTML.IndexOf("</a>", indexOfCategory + 1);
            if (indexOfCategory < 0 || endOfCategory < 0)
                log("查找分类出错", true);

            res.category = HTML.Substring(indexOfCategory + 19, endOfCategory - indexOfCategory - 19);

            res.tags = "";
            while (true)
            {
                indexOfTag = HTML.IndexOf("rel=\"tag\">", indexOfTag + 1);
                endOfTag = HTML.IndexOf("</a>", indexOfTag + 1);
                if (indexOfTag < 0 || endOfTag < 0)
                    break;
                else if (res.tags != "")
                    res.tags += " ";
                res.tags += HTML.Substring(indexOfTag + 10, endOfTag - indexOfTag - 10);
            }
            if (res.tags == "")
                log("查找标签出错", true);
            log("收集网页信息完成");

        }

        //规范文件名
        static string formatFileName(string fileName)
        {
            Regex specialChar = new Regex("[/\\:\\*\\?\"<>\\|]");
            MatchCollection matches = specialChar.Matches(fileName);
            for (int i = 0; i < matches.Count; i++)
                fileName = fileName.Replace(matches[i].ToString(), " ");
            return fileName;
        }

        //下载图片
        static void download(string picUrl, string filePath)
        {
            int lastIndexOfSlash = -1;
            string fileName;
            lastIndexOfSlash = picUrl.LastIndexOf("/");
            if (lastIndexOfSlash < 0)
            {
                log("下载失败，分析文件名称错误", true);
                return;
            }
            fileName = picUrl.Substring(lastIndexOfSlash, picUrl.Length - lastIndexOfSlash);
            try
            {
                WebClient downloader = new WebClient();
                downloader.DownloadFile(picUrl, filePath + fileName);
            }
            catch (WebException error)
            {
                log("下载图片" + picUrl + "失败，" + error.Message, true);
                addToFile(currentDirectory + "errorlist.txt", url + "\n" + picUrl + "\n\n");
            }
        }

        //收集百度盘资源
        static void collectBaidupan(string HTML)
        {
            string baidupan = url + "\n";
            int indexOfBaidupan = -1, endOfBaidupan = -1;
            indexOfBaidupan = HTML.IndexOf("http://pan.baidu.com/s/");
            endOfBaidupan = HTML.IndexOf("\" />", indexOfBaidupan);
            if (indexOfBaidupan < 0 || endOfBaidupan < 0)
                return;
            baidupan += HTML.Substring(indexOfBaidupan, endOfBaidupan - indexOfBaidupan) + "\n\n";
            addToFile(currentDirectory + "baidupan.txt", baidupan);
        }

        //收集图片
        static void collectPictures()
        {
            List<int> downloaded = new List<int>();
            string HTML = "";
            Regex pictureLinks = new Regex("<a href=\"[A-Za-z0-9:/.\\-_]{10,}.(jpg|png)\">");
            MatchCollection matches;

            if (File.Exists(currentDirectory + "downloaded.txt"))
                downloaded = getIndexes(File.ReadAllText(currentDirectory + "downloaded.txt", Encoding.Default));
            int catalogIndex = 0;
            if (Convert.ToBoolean(options["fromFirstIndex"]) || downloaded.Count == 0)
                catalogIndex = 0;
            else
                catalogIndex = catalog.IndexOf(downloaded[downloaded.Count - 1]);

            for (; catalogIndex < catalog.Count; catalogIndex++)
            {
                int index = catalog[catalogIndex];
                if (downloaded.Contains(index))
                    continue;
                Pictures pics = new Pictures();
                url = "http://www.jdlingyu.moe/" + index.ToString() + "/";
                pics.url = url;
                pics.index = index;

                log("正在加载内容页：" + url);
                try
                {
                    HTML = GetWebClient(url);
                    HTML = HTML.Replace("\\\"", "\"");
                    log("内容页加载成功");
                    if (HTML.IndexOf("http://pan.baidu.com/s/") > 0)
                    {
                        log("检测到百度盘链接");
                        collectBaidupan(HTML);
                    }
                    int indexOfPostImage = -1, endOfMainNavi = -1;
                    indexOfPostImage = HTML.IndexOf("<div class=\"post image\">");
                    endOfMainNavi = HTML.IndexOf("<div class=\"main-navi clx\">", indexOfPostImage);
                    if (indexOfPostImage < 0 || endOfMainNavi <= 0)
                    {
                        log("查找图片所在HTML段出错", true);
                        addToFile(currentDirectory + "errorlist.txt", url + "\n\n");
                        continue;
                    }
                    HTML = HTML.Substring(indexOfPostImage, endOfMainNavi - indexOfPostImage);

                    collectInformation(HTML, ref pics);
                    log("\nIndex:" + pics.index.ToString() + "\nUrl:" + pics.url + "\nTitle:" + pics.title + "\nDatetime:" + pics.datetime + "\nCategory:" + pics.category + "\nTags:" + pics.tags + "\n");

                    string newDirectory = formatFileName(pics.index + "_" + pics.title + "_" + pics.category + "_" + pics.tags);
                    if (!Directory.Exists(pictureLocation + newDirectory))
                        Directory.CreateDirectory(pictureLocation + newDirectory);

                    matches = pictureLinks.Matches(HTML);
                    for (int i = 0; i < matches.Count; i++)
                    {
                        pics.picUrls.Add(matches[i].ToString().Substring(9, matches[i].ToString().Length - 11));
                        log("正在下载图片：" + pics.picUrls[i]);
                        download(pics.picUrls[i], pictureLocation + newDirectory + "\\");
                    }
                    Console.WriteLine();
                    log("\n");

                    downloaded.Add(pics.index);
                    options["lastDownload"] = pics.index.ToString();
                    addToFile(currentDirectory + "downloaded.txt", pics.index + "\n");
                }
                catch (WebException error)
                {
                    log("加载内容页失败，" + error.Message + "\n", true);
                    addToFile(currentDirectory + "errorlist.txt", url + "\n\n");
                }
            }
        }

        static void initialize()
        {
            if (!File.Exists(currentDirectory + "options.ini"))
            {
                string defaultOption = Properties.Resources.options;
                defaultOption = defaultOption.Replace("pictureLocation=", "pictureLocation=" + currentDirectory);
                addToFile(currentDirectory + "options.ini", defaultOption);
            }
            options = new Options();
            domain = options["domain"];
            pictureLocation = options["pictureLocation"];
            logToFile = Convert.ToBoolean(options["logToFile"]);
            ignoreList = getIndexes(options["ignoreList"], ',');
            Console.WriteLine(options.allOptions());
        }

        static void Main(string[] args)
        {
            try
            {
                initialize();
                collectIndex();
                collectPictures();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.StackTrace);
                Console.WriteLine(e.Message);
                Console.WriteLine("出现意外错误，按下任意按键以退出");
                Console.ReadKey();
                return;
            }
            Console.WriteLine("\n下载完成\n");
            Console.WriteLine("按下任意按键以退出");
            Console.ReadKey();
        }
    }
}