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
            int index = 1, lengthOfCatalog = 0, startPosition = -1, endPosition = -1, retrytimes = 5, temp = 0;
            bool finished = false;
            List<int> newCatalog = new List<int>();
            StringBuilder catalogSB = new StringBuilder();
            while (!finished && retrytimes > 0)
            {
                string catalogUrl = domain + "page/" + index + "/";
                clog("正在加载目录页" + index + ":" + catalogUrl);
                try
                {
                    HTML = GetWebClient(catalogUrl);
                    startPosition = HTML.IndexOf("<div id=\"primary-home\"");//对应18年5月之后的新网页
                    if(startPosition<=0)
                        if (HTML.IndexOf("你所找的文章不存在! 我们强烈的为你推荐以下文章") >= 0)
                        {
                            clog("\n识别目录列表完毕");
                            finished = true;
                            break;
                        }
                    endPosition = HTML.IndexOf("<div class=\"widget-area-in\">", startPosition);
                    //startPosition = HTML.IndexOf("<div id=\"postlist\" class=\"clx\">");
                    //endPosition = HTML.IndexOf("<div id=\"pagenavi-fixed\">", startPosition);
                    if (endPosition <= 0)
                        throw new Exception("目录页提取出错:endPosition <= 0");


                    HTML = HTML.Substring(startPosition, endPosition - startPosition);
                    matches = indexRE.Matches(HTML);
                    for (int i = 0; i < matches.Count; i += 2)
                    {
                        temp = Convert.ToInt32(matches[i].ToString().Substring(domain.Length, matches[i].ToString().IndexOf("/", domain.Length) - domain.Length));
                        if (ignoreList.Contains(temp))
                            continue;
                        else if (catalog.Contains(temp))
                        {
                            clog("\n识别目录列表完毕");
                            finished = true;
                            break;
                        }
                        newCatalog.Add(temp);
                        lengthOfCatalog++;
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

                    if(retrytimes>1)
                        Console.WriteLine(retrytimes - 1 + "次重试后将结束识别目录列表\n");
                    else
                        Console.WriteLine("\n识别目录列表完毕\n");
                    retrytimes--;
                    index--;
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

        //收集元数据
        static void collectMetaData(string HTML, ref Pictures res)
        {
            int indexOfTitle = -1, endOfTitle = -1, indexOfDatetime = -1, endOfDatetime = -1;
            log("正在收集元数据");

            indexOfTitle = HTML.IndexOf("<h1 class=\"entry-title\" ref=\"postTitle\">");
            if (indexOfTitle < 0)
                log("查找标题出错", true);
            endOfTitle = HTML.IndexOf("</h1>", indexOfTitle + 1);
            if (endOfTitle <= 0)
                log("查找标题出错", true);
            res.title = HTML.Substring(indexOfTitle + 40, endOfTitle - indexOfTitle - 40);

            indexOfDatetime = HTML.IndexOf("<time class=\"timeago\"");
            if (indexOfDatetime < 0)
                log("查找日期出错", true);
            indexOfDatetime = HTML.IndexOf(">",indexOfDatetime);
            endOfDatetime = HTML.IndexOf("</time>", indexOfDatetime + 1);
            if (endOfDatetime < 0)
                log("查找日期出错", true);
            res.datetime = HTML.Substring(indexOfDatetime + 1, endOfDatetime - indexOfDatetime - 1);
            log("收集元数据完成");
        }

        //收集标签
        static void collectTags(string HTML,ref Pictures res)
        {
            int indexOfCategory = -1, endOfCategory = -1, indexOfTag = -1, endOfTag = -1;
            log("正在收集标签");

            indexOfCategory = HTML.IndexOf("<a class=\"list-category bg-blue-light color\"");
            indexOfCategory = HTML.IndexOf("\">", indexOfCategory);
            if (indexOfCategory < 0)
                log("查找分类出错", true);
            endOfCategory = HTML.IndexOf("</a>", indexOfCategory + 1);
            if (endOfCategory < 0)
                log("查找分类出错", true);

            res.category = HTML.Substring(indexOfCategory+2, endOfCategory - indexOfCategory-2);

            res.tags = "";
            while (true)
            {
                indexOfTag = HTML.IndexOf("\"># ", indexOfTag + 1);
                if (indexOfTag < 0)
                    break;
                endOfTag = HTML.IndexOf("<span>", indexOfTag + 1);
                if (endOfTag < 0)
                    break;
                else if (res.tags != "")
                    res.tags += " ";
                res.tags += HTML.Substring(indexOfTag + 4, endOfTag - indexOfTag - 4);
            }
            if (res.tags == "")
                log("查找标签出错", true);
            log("收集标签完成");
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
            Regex pictureLinks = new Regex("src=\"[A-Za-z0-9:/.\\-_]{10,300}.(jpg|png)\"");
            //Regex pictureLinks = new Regex("<a href=\"[A-Za-z0-9:/.\\-_]{10,}.(jpg|png)\">");
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
                url = domain + index.ToString() + "/";
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

                    //查找元数据所在HTML段
                    int indexOfMetaData = -1, endOfMetaData = -1;
                    indexOfMetaData = HTML.IndexOf("<div class=\"post-meta\">");
                    endOfMetaData = HTML.IndexOf("<div class=\"clearfix post-meta-read\"", indexOfMetaData);
                    if (indexOfMetaData < 0 || endOfMetaData <= 0)
                    {
                        log("查找元数据所在HTML段出错:endPosition < 0", true);
                        addToFile(currentDirectory + "errorlist.txt", url + "\n\n");
                        continue;
                    }
                    string metaData = HTML.Substring(indexOfMetaData, endOfMetaData - indexOfMetaData);
                    collectMetaData(metaData, ref pics);

                    //查找标签所在HTML段
                    int indexOfTags = -1, endOfTags = -1;
                    indexOfTags = HTML.IndexOf("<div class=\"zrz-post-tags l1 fs12 fl\"");
                    endOfTags = HTML.IndexOf("</div>", indexOfTags);
                    if (indexOfTags < 0 || endOfTags <= 0)
                    {
                        log("查找标签所在HTML段出错:endPosition < 0", true);
                        addToFile(currentDirectory + "errorlist.txt", url + "\n\n");
                        continue;
                    }
                    string tags = HTML.Substring(indexOfTags, endOfTags - indexOfTags);
                    collectTags(tags, ref pics);

                    log("\nIndex:" + pics.index.ToString() + "\nUrl:" + pics.url + "\nTitle:" + pics.title + "\nDatetime:" + pics.datetime + "\nCategory:" + pics.category + "\nTags:" + pics.tags + "\n");
                    
                    string newDirectory = formatFileName(pics.index + "_" + pics.title + "_" + pics.category + "_" + pics.tags);
                    if (!Directory.Exists(pictureLocation + newDirectory))
                        Directory.CreateDirectory(pictureLocation + newDirectory);

                    //查找图片所在HTML段
                    int indexOfPicture = -1, endOfPicture = -1;
                    indexOfPicture = HTML.IndexOf("<div id=\"entry-content\"",endOfMetaData);
                    endOfPicture = HTML.IndexOf("<div class=\"share-box fs12\"", indexOfPicture);
                    if (indexOfPicture < 0 || endOfPicture <= 0)
                    {
                        log("查找图片所在HTML段出错:endPosition < 0", true);
                        addToFile(currentDirectory + "errorlist.txt", url + "\n\n");
                        continue;
                    }
                    string pictureHtml= HTML.Substring(indexOfPicture, endOfPicture - indexOfPicture);
                    //查找图片
                    matches = pictureLinks.Matches(pictureHtml);
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
