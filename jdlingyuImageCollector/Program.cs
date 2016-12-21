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
    class pictures
    {
        public int index;
        public string url;
        public string title;
        public string datetime;
        public string author;
        public string tags;
        public string[] picUrls = new string[50];

    }
    class Program
    {
        public static string pictureLocation = Directory.GetCurrentDirectory() + "\\";
        public static string fileLocation = Directory.GetCurrentDirectory() + "\\";
        public static string url = "";

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
        static void saveFile(string fileName, string content)
        {
            FileStream file = new FileStream(fileName, FileMode.Append);
            byte[] data = System.Text.Encoding.Default.GetBytes(content);
            file.Write(data, 0, data.Length);
            file.Flush();
            file.Close();
        }

        static void log(string logText)
        {
            Console.WriteLine(logText);
            saveFile(fileLocation + "log.txt", DateTime.Now + " " + logText + "\n");
        }

        static void clog(string logText)
        {
            Console.WriteLine(logText);
            saveFile(fileLocation + "catalogLog.txt", DateTime.Now + " " + logText + "\n");
        }

        //收集目录
        static void collectIndex()
        {
            string oldCatalog = "";
            if (File.Exists(fileLocation + "catalog.txt"))
                oldCatalog = File.ReadAllText(fileLocation + "catalog.txt", Encoding.Default);

            Regex indexRE = new Regex("http://www.jdlingyu.moe/[0-9]{1,6}/");
            MatchCollection matches;
            string HTML = "", catalog = "", temp="";
            int index = 1, lengthOfCatalog = 0, startPosition = -1, endPosition = -1, retrytimes = 5;
            bool finished = false;
            int[] indexs = new int[100000];
            while (!finished && retrytimes > 0)
            {
                clog("正在加载目录页" + index);
                try
                {
                    HTML = GetWebClient("http://www.jdlingyu.moe/page/" + index + "/");
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
                        clog("目录页提取出错" + index);
                        continue;
                    }

                    HTML = HTML.Substring(startPosition, endPosition - startPosition);
                    matches = indexRE.Matches(HTML);
                    for (int i = 0; i < matches.Count; i += 2)
                    {
                        temp = matches[i].ToString().Substring(24, matches[i].ToString().IndexOf("/", 24)-24);
                        if (oldCatalog.IndexOf(temp) >= 0)
                        {
                            clog("识别目录列表完毕");
                            finished = true;
                            break;
                        }
                        indexs[lengthOfCatalog] = Convert.ToInt32(temp);
                        lengthOfCatalog++;
                        clog(matches[i].ToString());
                    }
                    Console.WriteLine();

                }
                catch (WebException error)
                {
                    clog("加载失败，" + error.Message);
                    Console.WriteLine(retrytimes + "次重试后将结束识别目录列表");
                    retrytimes--;
                }
                index++;
            }
            
            int[] indexList = new int[lengthOfCatalog];
            for (int i = 0; i < lengthOfCatalog; i++)
                indexList[i] = indexs[i];
            Array.Sort(indexList);
            for (int i = 0; i < lengthOfCatalog; i++)
                catalog += indexList[i].ToString() + "\n";
            saveFile(fileLocation + "catalog.txt", catalog);
        }

        //收集信息
        static void collectInformation(string HTML, ref pictures res)
        {
            int indexOfTitle = -1, endOfTitle = -1, indexOfDatetime = -1, endOfDatetime = -1, indexOfAuthor = -1, endOfAuthor = -1, indexOfTag = -1, endOfTag = -1;
            log("正在收集网页信息");

            indexOfTitle = HTML.IndexOf("<h2 class=\"main-title\">");
            endOfTitle = HTML.IndexOf("</h2>", indexOfTitle + 1);
            if (indexOfTitle < 0 || endOfTitle <= 0)
                log("查找标题出错");
            res.title = HTML.Substring(indexOfTitle + 23, endOfTitle - indexOfTitle - 23);

            indexOfDatetime = HTML.IndexOf("<span class=\"post-span\">");
            endOfDatetime = HTML.IndexOf("</span>", indexOfDatetime + 1);
            if (indexOfDatetime < 0 | endOfDatetime < 0)
                log("查找日期出错");
            res.datetime = HTML.Substring(indexOfDatetime + 24, endOfDatetime - indexOfDatetime - 24);

            indexOfAuthor = HTML.IndexOf("rel=\"category tag\">");
            endOfAuthor = HTML.IndexOf("</a>", indexOfAuthor + 1);
            if (indexOfAuthor < 0 | endOfAuthor < 0)
                log("查找作者出错");

            res.author = HTML.Substring(indexOfAuthor + 19, endOfAuthor - indexOfAuthor - 19);

            res.tags = "";
            while (true)
            {
                indexOfTag = HTML.IndexOf("rel=\"tag\">", indexOfTag + 1);
                endOfTag = HTML.IndexOf("</a>", indexOfTag + 1);
                if (indexOfTag < 0 | endOfTag < 0)
                    break;
                else if (res.tags != "")
                    res.tags += " ";
                res.tags += HTML.Substring(indexOfTag + 10, endOfTag - indexOfTag - 10);
            }
            if (res.tags == "")
                log("查找标签出错");
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
                log("下载失败，分析文件名称错误");
                return;
            }
            fileName = picUrl.Substring(lastIndexOfSlash, picUrl.Length - lastIndexOfSlash);
            try
            {
                WebClient mywebclient = new WebClient();
                mywebclient.DownloadFile(picUrl, filePath + fileName);
            }
            catch (WebException error)
            {
                log("下载图片" + picUrl + "失败，" + error.Message);
                saveFile(fileLocation + "errorlist.txt", url + "\n" + picUrl + "\n\n");
            }
        }

        //收集百度盘资源
        static void collectBaidupan(string HTML)
        {
            string baidupan = url + "\n";
            int indexOfBaidupan = -1, endOfBaidupan = -1;
            indexOfBaidupan = HTML.IndexOf("链接：http://pan.baidu.com/s/");
            endOfBaidupan = HTML.IndexOf("\" />", indexOfBaidupan);
            if (indexOfBaidupan < 0 || endOfBaidupan < 0)
                return;
            baidupan += HTML.Substring(indexOfBaidupan, endOfBaidupan - indexOfBaidupan) + "\n\n";
            saveFile(fileLocation + "baidupan.txt", baidupan);
        }

        //收集图片
        static void collectPictures()
        {
            string catalog = "", downloaded = "";
            string HTML = "";
            int index = 0,indexOfCatalog = -1,endOfCatalog=-1 ,
                indexOfPostImage = -1, endOfMainNavi = -1;
            Regex pictureLinks = new Regex("<a href=\"[A-Za-z0-9:/.\\-_]{10,}.(jpg|png)\">");
            MatchCollection matches;

            if (File.Exists(fileLocation + "catalog.txt"))
                catalog = File.ReadAllText(fileLocation + "catalog.txt", Encoding.Default);
            else
                log("没有找到目录文件！");
            if (File.Exists(fileLocation + "downloaded.txt"))
                downloaded = File.ReadAllText(fileLocation + "downloaded.txt", Encoding.Default);

            while (true)
            {
                endOfCatalog = catalog.IndexOf('\n', indexOfCatalog + 1);
                if (endOfCatalog < 0)
                    break;
                index = Convert.ToInt32(catalog.Substring(indexOfCatalog + 1, endOfCatalog- indexOfCatalog - 1));
                indexOfCatalog = catalog.IndexOf('\n', indexOfCatalog + 1);
                if (downloaded.IndexOf(index.ToString()) >= 0)
                    continue;

                pictures pics = new pictures();
                url = "http://www.jdlingyu.moe/" + index.ToString() + "/";
                
                if (indexOfCatalog < 0)
                    break;
                pics.url = url;
                pics.index = index;

                log("正在加载内容页：" + url);
                try
                {
                    HTML = GetWebClient(url);
                    HTML = HTML.Replace("\\\"", "\"");
                    log("内容页加载成功");
                    if (HTML.IndexOf("链接：http://pan.baidu.com/s/") > 0)
                    {
                        log("检测到百度盘链接");
                        collectBaidupan(HTML);
                    }
                    indexOfPostImage = HTML.IndexOf("<div class=\"post image\">");
                    endOfMainNavi = HTML.IndexOf("<div class=\"main-navi clx\">", indexOfPostImage);
                    if (indexOfPostImage < 0 || endOfMainNavi <= 0)
                    {
                        log("查找图片所在HTML段出错");
                        saveFile(fileLocation + "errorlist.txt", url + "\n\n");
                        continue;
                    }
                    HTML = HTML.Substring(indexOfPostImage, endOfMainNavi - indexOfPostImage);

                    collectInformation(HTML, ref pics);
                    log("\nIndex:" + pics.index.ToString() + "\nUrl:" + pics.url + "\nTitle:" + pics.title + "\nDatetime:" + pics.datetime + "\nAuthor:" + pics.author + "\nTags:" + pics.tags + "\n");

                    string newDirectory = formatFileName(pics.index + "_" + pics.title + "_" + pics.tags);
                    if (!Directory.Exists(pictureLocation + newDirectory))
                        Directory.CreateDirectory(pictureLocation + newDirectory);

                    matches = pictureLinks.Matches(HTML);
                    for (int i = 0; i < matches.Count; i++)
                    {
                        pics.picUrls[i] = matches[i].ToString().Substring(9, matches[i].ToString().Length - 11);
                        log("正在下载图片：" + pics.picUrls[i]);
                        download(pics.picUrls[i], pictureLocation + newDirectory + "\\");
                    }
                    Console.WriteLine();
                    log("\n");

                    downloaded += pics.index + "\n";
                    saveFile(fileLocation + "downloaded.txt", pics.index + "\n");
                }
                catch (WebException error)
                {
                    log("加载内容页失败，" + error.Message + "\n");
                    saveFile(fileLocation + "errorlist.txt", url + "\n\n");
                }
            }
        }

        static void input()
        {
            string input;
            bool inputCorrect = false;
            Console.WriteLine("是否自定义文件保存位置？输入y或n，默认保存在程序所在位置:" + fileLocation);
            while (!inputCorrect)
            {
                input = Console.ReadLine();
                switch (input)
                {
                    case "y":
                        {
                            inputCorrect = true;
                            Console.WriteLine("输入文件保存位置，例如E:\\jdlingyu\\");
                            input = Console.ReadLine();
                            if (input.LastIndexOf("\\") != input.Length - 1)
                                input += "\\";
                            pictureLocation = input;
                            if (!Directory.Exists(pictureLocation))
                                Directory.CreateDirectory(pictureLocation);
                            saveFile(fileLocation + "\\pictureLocation.ini", input);
                            break;
                        }
                    case "n":
                        {
                            inputCorrect = true;
                            saveFile(fileLocation + "\\pictureLocation.ini", fileLocation);
                            break;
                        }
                    default:
                        {
                            Console.WriteLine("请重新输入");
                            break;
                        }
                }
            }
        }

        static void Main(string[] args)
        {
            Console.WriteLine("程序所在位置：" + fileLocation);
            if (File.Exists(fileLocation + "pictureLocation.ini"))
            {
                pictureLocation = File.ReadAllText(fileLocation + "pictureLocation.ini", Encoding.Default);
                Console.WriteLine("文件保存位置：" + pictureLocation);
            }
            else
                input();
            collectIndex();
            collectPictures();
            Console.WriteLine("\n下载完成\n");
            Console.ReadKey();
        }
    }
}
