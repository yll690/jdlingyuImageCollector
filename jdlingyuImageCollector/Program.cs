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
        static string[] collections;
        static bool logToFile = true;
        static int maxDupCount = 10;
        static List<int> ignoreList = new List<int>();
        static List<int> catalogs = new List<int>();
        static Regex picSrcRE = new Regex("src=\"[A-Za-z0-9:/.\\-_]{10,300}.(jpg|png)\"");
        static Regex indexRE;
        static WebClient webClient = new WebClient();


        //获取HTML
        static string GetHTML(string url)
        {
            using (Stream myStream = webClient.OpenRead(url))
            {
                StreamReader sr = new StreamReader(myStream, System.Text.Encoding.GetEncoding("utf-8"));
                return sr.ReadToEnd();
            }
        }

        //保存文件
        static void addToFile(string fileName, string content)
        {
            File.AppendAllText(fileName, content, Encoding.Default);
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
            string[] indexes = indexesString.Split(new char[] { separator }, StringSplitOptions.RemoveEmptyEntries);
            return indexes.Select(s => Convert.ToInt32(s)).ToList();
        }

        static List<int> getIndexes(string indexesString)
        {
            return getIndexes(indexesString, '\n');
        }

        //收集目录
        static void collectIndex(string collection)
        {
            if (File.Exists(currentDirectory + "catalog.txt"))
                catalogs = getIndexes(File.ReadAllText(currentDirectory + "catalog.txt", Encoding.Default));

            MatchCollection matches;
            int index = 1, retrytimes = 5, dupCount = 0;
            List<int> newCatalogs = new List<int>();
            bool finished = false;
            while (!finished && retrytimes > 0)
            {
                string catalogUrl = $"{domain}collection/{collection}/page/{index}/";
                clog("正在加载目录页" + index + ":" + catalogUrl);
                try
                {
                    string HTML = GetHTML(catalogUrl);
                    int startPosition = HTML.IndexOf("<div id=\"primary-home\"");//对应20年6月之后的新网页
                    if (startPosition <= 0)
                        if (HTML.IndexOf("你所找的文章不存在! 我们强烈的为你推荐以下文章") >= 0)
                        {
                            clog("\n识别目录列表完毕");
                            finished = true;
                            break;
                        }
                    int endPosition = HTML.IndexOf("class=\"b2-pagenav post-nav", startPosition);
                    if (endPosition <= 0)
                        throw new Exception("目录页提取出错:endPosition <= 0");


                    HTML = HTML.Substring(startPosition, endPosition - startPosition);
                    matches = indexRE.Matches(HTML);
                    for (int i = 0; i < matches.Count; i += 2)
                    {
                        int catalog = Convert.ToInt32(matches[i].Groups[1].ToString());
                        if (ignoreList.Contains(catalog))
                            continue;
                        else if (catalogs.Contains(catalog))
                        {
                            clog($"重复的目录：{catalog}，{maxDupCount - dupCount}次后将结束本次目录识别。");
                            dupCount++;
                            if (dupCount >= maxDupCount)
                            {
                                clog("\n识别目录列表完毕");
                                finished = true;
                                break;
                            }
                            else
                                continue;
                        }
                        else
                            dupCount = 0;
                        newCatalogs.Add(catalog);
                        clog(matches[i].ToString());
                    }
                    Console.WriteLine();
                    retrytimes = 5;
                }
                catch (Exception error)
                {
                    if (error.GetType() == typeof(WebException))
                    {
                        clog("加载失败，" + error.Message, true);
                    }
                    else
                    {
                        clog(error.Message, true);
                    }

                    if (retrytimes > 1)
                        Console.WriteLine(retrytimes - 1 + "次重试后将结束识别目录列表\n");
                    else
                        Console.WriteLine("\n识别目录列表完毕\n");
                    retrytimes--;
                    index--;
                }
                index++;
            }

            foreach (int i in newCatalogs)
                catalogs.Add(i);
            catalogs.Sort();

            StringBuilder catalogSB = new StringBuilder();
            foreach (int i in catalogs)
                catalogSB.AppendLine(i.ToString());
            File.WriteAllText(currentDirectory + "catalog.txt", catalogSB.ToString());
        }

        static bool SubString(string source, string startKeyword, string endKeyword, out string result)
        {
            bool succeeded = SubString(source, new string[] { startKeyword }, endKeyword, out string result1);
            result = result1;
            return succeeded;
        }

        static bool SubString(string source, string startKeyword1, string startKeyword2, string endKeyword, out string result)
        {
            bool succeeded = SubString(source, new string[] { startKeyword1, startKeyword2 }, endKeyword, out string result1);
            result = result1;
            return succeeded;
        }

        static bool SubString(string source, string[] startKeywords, string endKeyword, out string result)
        {
            result = null;

            int startIndex = 0;
            foreach (string s in startKeywords)
                startIndex = source.IndexOf(s, startIndex);
            if (startIndex < 0)
                return false;
            else
            {
                int lastLength = startKeywords.Last().Length;
                int endIndex = source.IndexOf(endKeyword, startIndex + lastLength);
                if (endIndex < 0)
                    return false;
                else
                {
                    result = source.Substring(startIndex + lastLength, endIndex - startIndex - lastLength);
                    return true;
                }
            }
        }

        //收集元数据
        static void collectMetaData(string HTML, Pictures res)
        {
            log("正在收集元数据");

            if (SubString(HTML, "<span class=\"post-3-cat\"", "></span>", "</a>", out string category))
                res.category = category;
            else
                log("查找分类出错", true);

            if (SubString(HTML, "<h1>", "</h1>", out string title))
                res.title = title;
            else
                log("查找标题出错", true);

            if (SubString(HTML, "datetime=\"", "\"", out string datetime))
                res.datetime = datetime;
            else
                log("查找日期出错", true);

            log("收集元数据完成");
        }

        //收集标签
        static void collectTags(string HTML, Pictures res)
        {
            log("正在收集标签");

            res.tags = "";
            int indexOfTag = -1;
            while (true)
            {
                indexOfTag = HTML.IndexOf("<span class=\"tag-text\">", indexOfTag + 1);
                if (indexOfTag < 0)
                    break;
                int endOfTag = HTML.IndexOf("</span>", indexOfTag + 1);
                if (endOfTag < 0)
                    break;
                else if (res.tags != "")
                    res.tags += " ";
                res.tags += HTML.Substring(indexOfTag + 23, endOfTag - indexOfTag - 23);
            }
            if (res.tags == "")
                log("查找标签出错", true);
            log("收集标签完成");
        }

        //规范文件名
        static string formatFileName(string fileName)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                fileName = fileName.Replace(c, '_');
            return fileName;
        }

        //下载图片
        static void download(string picUrl, string filePath)
        {
            int lastIndexOfSlash = picUrl.LastIndexOf("/");
            if (lastIndexOfSlash < 0)
            {
                log("下载失败，分析文件名称错误", true);
                addToFile(currentDirectory + "errorlist.txt", url + "\n" + picUrl + "\n\n");
                return;
            }
            string fileName = picUrl.Substring(lastIndexOfSlash, picUrl.Length - lastIndexOfSlash);
            try
            {
                webClient.DownloadFile(picUrl, filePath + formatFileName(fileName));
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
            int indexOfBaidupan = HTML.IndexOf("http://pan.baidu.com/s/");
            int endOfBaidupan = HTML.IndexOf("\" />", indexOfBaidupan);
            if (indexOfBaidupan < 0 || endOfBaidupan < 0)
                return;
            string baidupan = url + "\n" + HTML.Substring(indexOfBaidupan, endOfBaidupan - indexOfBaidupan) + "\n\n";
            addToFile(currentDirectory + "baidupan.txt", baidupan);
        }

        //收集图片
        static void collectPictures()
        {
            List<int> downloaded = new List<int>();
            //Regex pictureLinks = new Regex("<a href=\"[A-Za-z0-9:/.\\-_]{10,}.(jpg|png)\">");

            if (File.Exists(currentDirectory + "downloaded.txt"))
                downloaded = getIndexes(File.ReadAllText(currentDirectory + "downloaded.txt", Encoding.Default));
            int catalogIndex;
            if (Convert.ToBoolean(options["fromFirstIndex"]) || downloaded.Count == 0)
                catalogIndex = 0;
            else
                catalogIndex = catalogs.IndexOf(downloaded[downloaded.Count - 1]);

            for (; catalogIndex < catalogs.Count; catalogIndex++)
            {
                int index = catalogs[catalogIndex];
                if (downloaded.Contains(index))
                    continue;
                Pictures pics = new Pictures();
                url = domain + "mzitu/" + index.ToString() + ".html";
                pics.url = url;
                pics.index = index;

                log("正在加载内容页：" + url);
                try
                {
                    string HTML = GetHTML(url);
                    HTML = HTML.Replace("\\\"", "\"");
                    log("内容页加载成功");
                    if (HTML.IndexOf("http://pan.baidu.com/s/") > 0)
                    {
                        log("检测到百度盘链接");
                        collectBaidupan(HTML);
                    }

                    //查找元数据所在HTML段

                    if (SubString(HTML, "<header class=\"entry-header\">", "<div class=\"entry-content\"", out string metaData))
                        collectMetaData(metaData, pics);
                    else
                    {
                        log("查找元数据所在HTML段出错", true);
                        addToFile(currentDirectory + "errorlist.txt", url + "\n\n");
                    }

                    //查找标签所在HTML段
                    if (SubString(HTML, "<div class=\"post-tags-meat\"", "</div>", out string tags))
                        collectTags(tags, pics);
                    else
                    {
                        log("查找标签所在HTML段出错", true);
                        addToFile(currentDirectory + "errorlist.txt", url + "\n\n");
                    }

                    log("\nIndex:" + pics.index.ToString() + "\nUrl:" + pics.url + "\nTitle:" + pics.title + "\nDatetime:" + pics.datetime + "\nCategory:" + pics.category + "\nTags:" + pics.tags + "\n");

                    string newDirectory = formatFileName(pics.index + "_" + pics.title + "_" + pics.category + "_" + pics.tags);
                    if (!Directory.Exists(pictureLocation + newDirectory))
                        Directory.CreateDirectory(pictureLocation + newDirectory);

                    //查找图片所在HTML段
                    if (!SubString(HTML, "<div class=\"entry-content\"", "<div id=\"content-ds\"", out string pictureHtml))
                    {
                        log("查找图片所在HTML段出错", true);
                        addToFile(currentDirectory + "errorlist.txt", url + "\n\n");
                        continue;
                    }
                    //查找图片
                    MatchCollection matches = picSrcRE.Matches(pictureHtml);
                    for (int i = 0; i < matches.Count; i++)
                    {
                        pics.picUrls.Add(matches[i].ToString().Substring(5, matches[i].ToString().Length - 6));
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
            maxDupCount = Convert.ToInt32(options["maxDupCount"]);
            ignoreList = getIndexes(options["ignoreList"], ',');
            collections = options["collections"].Split(',');
            indexRE = new Regex(domain + "[a-z/]{1,40}/([0-9]{1,6}).html");
            Console.WriteLine(options.allOptions());
        }

        static void Main(string[] args)
        {
            try
            {
                initialize();
                foreach (string c in collections)
                {
                    clog("正在加载专题" + c);
                    collectIndex(c);
                }
                collectPictures();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
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