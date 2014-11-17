﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.Common;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using YoutubeExtractor;

namespace YTub.Common
{
    public class Chanel :INotifyPropertyChanged
    {
        private bool _isReady;

        private string _chanelName;

        private IList _selectedListVideoItems = new ArrayList();

        private VideoItem _currentVideoItem;
        
        private readonly BackgroundWorker _bgv = new BackgroundWorker();

        private Timer _timer;

        public TimeSpan Synctime;

        public int MaxResults { get; set; }

        public int MinRes { get; set; }

        public string ChanelOwner { get; set; }

        public string ChanelName
        {
            get { return _chanelName; }
            set
            {
                _chanelName = value;
                OnPropertyChanged("ChanelName");
            }
        }

        public bool IsReady
        {
            get { return _isReady; }
            set
            {
                _isReady = value;
                OnPropertyChanged("IsReady");
            }
        }

        public TrulyObservableCollection<VideoItem> ListVideoItems { get; set; }

        public IList SelectedListVideoItems
        {
            get { return _selectedListVideoItems; }
            set
            {
                _selectedListVideoItems = value;
                OnPropertyChanged("SelectedListVideoItems");
            }
        }

        public VideoItem CurrentVideoItem
        {
            get { return _currentVideoItem; }
            set
            {
                _currentVideoItem = value;
                OnPropertyChanged("CurrentVideoItem");
            }
        }

        public Chanel(string name, string user)
        {
            MaxResults = 25;
            MinRes = 1;
            if (string.IsNullOrEmpty(user))
                throw new ArgumentException("Chanel user must be set");

            if (string.IsNullOrEmpty(name))
                name = user;

            ChanelName = name;
            ChanelOwner = user;
            ListVideoItems = new TrulyObservableCollection<VideoItem>();
            _bgv.DoWork += _bgv_DoWork;
            _bgv.RunWorkerCompleted += _bgv_RunWorkerCompleted;
        }

        void _bgv_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Error != null)
            {
                if (e.Error is SQLiteException)
                {
                    MessageBox.Show(e.Error.Message, "Database exception", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show(e.Error.Message, "Common error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                MinRes = 1;
                var dir = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName);
                if (dir != null)
                {
                    int totalrow;
                    Sqllite.CreateOrConnectDb(Subscribe.ChanelDb, ChanelOwner, out totalrow);
                    if (totalrow == 0)
                    {
                        foreach (VideoItem videoItem in ListVideoItems)
                        {
                            Sqllite.InsertRecord(Subscribe.ChanelDb, videoItem.VideoID, ChanelOwner, ChanelName, videoItem.VideoLink, videoItem.Title, videoItem.ViewCount, videoItem.ViewCount, videoItem.Duration, videoItem.Published, videoItem.Description);
                            videoItem.IsHasFile = videoItem.IsFileExist(videoItem);
                        }
                    }
                    else
                    {
                        foreach (VideoItem item in ListVideoItems)
                        {
                            item.IsSynced = Sqllite.IsTableHasRecord(Subscribe.ChanelDb, item.VideoID);
                            item.IsHasFile = item.IsFileExist(item);
                            item.PrevViewCount = Sqllite.GetVideoIntValue(Subscribe.ChanelDb, "viewcount", item.VideoID);
                            item.Delta = item.ViewCount - item.PrevViewCount;
                            Sqllite.UpdateValue(Subscribe.ChanelDb, "previewcount", item.VideoID, item.PrevViewCount);
                            Sqllite.UpdateValue(Subscribe.ChanelDb, "viewcount", item.VideoID, item.ViewCount);
                        }
                        IsReady = !ListVideoItems.Select(x => x.IsSynced).Contains(false);
                        foreach (VideoItem videoItem in ListVideoItems.Where(x => x.IsSynced == false))
                        {
                            Sqllite.InsertRecord(Subscribe.ChanelDb, videoItem.VideoID, ChanelOwner, ChanelName, videoItem.VideoLink, videoItem.Title, videoItem.ViewCount, videoItem.ViewCount, videoItem.Duration, videoItem.Published, videoItem.Description);
                        }
                    }
                }
                _timer.Dispose();
                ViewModelLocator.MvViewModel.Model.MySubscribe.Synctime = ViewModelLocator.MvViewModel.Model.MySubscribe.Synctime.Add(Synctime.Duration());
                ViewModelLocator.MvViewModel.Model.MySubscribe.Result = string.Format("Total: {0}. {1} synced in {2}",
                    ViewModelLocator.MvViewModel.Model.MySubscribe.Synctime.ToString(@"mm\:ss"), ChanelName, Synctime.Duration().ToString(@"mm\:ss"));
            }
        }

        void _bgv_DoWork(object sender, DoWorkEventArgs e)
        {
            //var minres = (int) e.Argument;
            while (true)
            {
                var wc = new WebClient { Encoding = Encoding.UTF8 };
                var zap = string.Format("https://gdata.youtube.com/feeds/api/users/{0}/uploads?alt=json&start-index={1}&max-results={2}", ChanelOwner, MinRes, MaxResults);
                string s = wc.DownloadString(zap);
                var jsvideo = (JObject)JsonConvert.DeserializeObject(s);
                if (jsvideo == null)
                    return;
                int total;
                if (int.TryParse(jsvideo["feed"]["openSearch$totalResults"]["$t"].ToString(), out total))
                {
                    foreach (JToken pair in jsvideo["feed"]["entry"])
                    {
                        var v = new VideoItem(pair) { Num = ListVideoItems.Count + 1, VideoOwner = ChanelOwner };
                        Application.Current.Dispatcher.Invoke(() => ListVideoItems.Add(v));
                    }
                    if (total > ListVideoItems.Count)
                    {
                        MinRes = MinRes + MaxResults;
                        continue;
                    }
                }

                break;
            }
        }

        public void DeleteFiles()
        {
            if (SelectedListVideoItems.Count > 0)
            {
                var sb = new StringBuilder();
                foreach (VideoItem item in SelectedListVideoItems)
                {
                    if (item.IsHasFile)
                        sb.Append(item.Title).Append(Environment.NewLine);
                }
                var result = MessageBox.Show("Are you sure to delete:" + Environment.NewLine + sb + "?", "Confirm", MessageBoxButton.OKCancel, MessageBoxImage.Information);
                if (result == MessageBoxResult.OK)
                {
                    for (var i = SelectedListVideoItems.Count; i > 0; i--)
                    {
                        var video = SelectedListVideoItems[i - 1] as VideoItem;
                        if (video != null && video.IsHasFile)
                        {
                            var fn = new FileInfo(video.FilePath);
                            try
                            {
                                fn.Delete();
                                ViewModelLocator.MvViewModel.Model.MySubscribe.Result = "Deleted";
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show(ex.Message);
                            }
                            video.IsHasFile = false;
                            video.IsDownLoading = false;
                        }
                    }
                }
            }
            else
            {
                MessageBox.Show("Please select Video");
            }
        }

        public void GetChanelVideoItems()
        {
            var tcb = new TimerCallback(tmr_Tick);
            _timer = new Timer(tcb, null, 0, 1000);
            ListVideoItems.Clear();
            _bgv.RunWorkerAsync();
        }

        public void GetChanelVideoItemsFromDb(string dbfile)
        {
            var res = Sqllite.GetChanelVideos(dbfile, ChanelOwner);
            foreach (DbDataRecord record in res)
            {
                var v = new VideoItem(record) {Num = ListVideoItems.Count + 1};
                ListVideoItems.Add(v);
            }
            var lst = new List<VideoItem>(ListVideoItems.Count);
            lst.AddRange(ListVideoItems);
            lst = lst.OrderByDescending(x => x.Published).ToList();
            ListVideoItems.Clear();
            foreach (VideoItem item in lst)
            {
                ListVideoItems.Add(item);
                item.Num = ListVideoItems.Count;
                item.IsHasFile = item.IsFileExist(item);
                item.Delta = item.ViewCount - item.PrevViewCount;
            }
        }

        public async void DownloadVideoInternal()
        {
            var lst = new List<VideoItem>(SelectedListVideoItems.Count);
            lst.AddRange(SelectedListVideoItems.Cast<VideoItem>());
            foreach (VideoItem item in lst)
            {
                CurrentVideoItem = item;
                var dir = new DirectoryInfo(Path.Combine(Subscribe.DownloadPath, CurrentVideoItem.VideoOwner));
                if (!dir.Exists)
                    dir.Create();
                CurrentVideoItem.IsDownLoading = true;
                CurrentVideoItem.IsHasFile = false;
                await DownloadVideoAsync(CurrentVideoItem);
            }
            ViewModelLocator.MvViewModel.Model.MySubscribe.Result = "Download Completed";
        }

        private static Task DownloadVideoAsync(VideoItem item)
        {
            return Task.Run(() =>
            {
                IEnumerable<VideoInfo> videoInfos = DownloadUrlResolver.GetDownloadUrls(item.VideoLink).OrderByDescending(z => z.Resolution);
                VideoInfo videoInfo = videoInfos.First(info => info.VideoType == VideoType.Mp4 && info.AudioBitrate != 0);
                if (videoInfo != null)
                {
                    if (videoInfo.RequiresDecryption)
                    {
                        DownloadUrlResolver.DecryptDownloadUrl(videoInfo);
                    }

                    var downloader = new VideoDownloader(videoInfo, Path.Combine(Subscribe.DownloadPath, item.VideoOwner, VideoItem.MakeValidFileName(videoInfo.Title) + videoInfo.VideoExtension));

                    downloader.DownloadProgressChanged += (sender, args) => downloader_DownloadProgressChanged(args, item);
                    downloader.DownloadFinished += delegate {downloader_DownloadFinished(downloader, item);};
                    downloader.Execute();
                }
            });
        }

        private static void downloader_DownloadFinished(object sender, VideoItem o)
        {
            var vd = sender as VideoDownloader;
            if (vd != null)
            {
                Application.Current.Dispatcher.BeginInvoke((Action)(() =>
                {
                    o.IsHasFile = true;
                    o.FilePath = vd.SavePath;
                }));
            }
        }

        private static void downloader_DownloadProgressChanged(ProgressEventArgs e, VideoItem o)
        {
            if ((int)e.ProgressPercentage%5 == 0)
            {
                Application.Current.Dispatcher.BeginInvoke((Action) (() =>
                {
                    o.PercentDownloaded = e.ProgressPercentage;
                }));
            }
        }

        public void DownloadVideoExternal()
        {
            if (string.IsNullOrEmpty(Subscribe.YoudlPath))
            {
                MessageBox.Show("Please set path to Youtube-dl in the Settings", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            foreach (VideoItem item in SelectedListVideoItems)
            {
                var youwr = new YouWrapper(Subscribe.YoudlPath, Subscribe.FfmpegPath, Path.Combine(Subscribe.DownloadPath, item.VideoOwner), item);
                youwr.DownloadFile(false);
            }
        }

        void tmr_Tick(object o)
        {
            Synctime = Synctime.Add(TimeSpan.FromSeconds(1));
            //ViewModelLocator.MvViewModel.Model.MySubscribe.Result = Synctime.ToString(@"hh\:mm\:ss");
        }

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null) handler(this, new PropertyChangedEventArgs(propertyName));
        } 

        #endregion

        #region regex

        //public void GetChanelVideoItemsWithoutGoogle()
        //{
        //    ListVideoItems.Clear();
        //    var web = new HtmlWeb
        //    {
        //        AutoDetectEncoding = false,
        //        OverrideEncoding = Encoding.UTF8,
        //    };
        //    var chanelDoc = web.Load(ChanelLink.AbsoluteUri);
        //    if (chanelDoc == null)
        //        throw new HtmlWebException("Can't load page: " + Environment.NewLine + ChanelLink.AbsoluteUri);
        //    //var i = 0;
        //    foreach (HtmlNode link in chanelDoc.DocumentNode.SelectNodes("//a[@href]"))
        //    {
        //        var att = link.Attributes["href"];
        //        string parsed;
        //        if (!IsLinkCorrectYouTube(att.Value, out parsed))
        //            continue;
        //        var parsedtrim = parsed.TrimEnd('&');
        //        var sp = parsedtrim.Split('=');
        //        if (sp.Length == 2 && sp[1].Length == 11)
        //        {
        //            var v = new VideoItem(parsedtrim, sp[1]);
        //            //var removedBreaksname = link.InnerText.Trim().Replace("\r\n", string.Empty).Replace("\n", string.Empty).Replace("\r", string.Empty);
        //            //v.VideoName = removedBreaksname;
        //            if (!ListVideoItems.Select(x => x.RawUrl).ToList().Contains(v.RawUrl))
        //            {
        //                ListVideoItems.Add(v);
        //                //i++;
        //            }
        //        }
        //    }
        //}

        //private static bool IsLinkCorrectYouTube(string input, out string parsedres)
        //{
        //    var res = false;
        //    parsedres = string.Empty;
        //    var regExp = new Regex(@"(watch\?.)(.+?)(?:[^-a-zA-Z0-9]|$)");
        //    //var regExp = new Regex(@"(?:youtu\.be\/|youtube.com\/(?:watch\?.*\bv=|embed\/|v\/)|ytimg\.com\/vi\/)(.+?)(?:[^-a-zA-Z0-9]|$)");
        //    //var regExp = new Regex(@"/^.*((youtu.be\/)|(v\/)|(\/u\/\w\/)|(embed\/)|(watch\?))\??v?=?([^#\&\?]*).*/");
        //    //var regExp = new Regex(@"/^.*(youtu.be\/|v\/|u\/\w\/|embed\/|watch\?v=|\&v=)([^#\&\?]*).*/");
        //    //var regExp = new Regex(@"/(?:https?://)?(?:www\.)?youtu(?:be\.com/watch\?(?:.*?&(?:amp;)?)?v=|\.be/)([\w‌​\-]+)(?:&(?:amp;)?[\w\?=]*)?/");
        //    //var regExp = new Regex(@"http://(?:www\.)?youtu(?:be\.com/watch\?v=|\.be/)(\w*)(&(amp;)?[\w\?=]*)?");
        //    //var regExp = new Regex(@"(?:(?:watch\?.*\bv=|embed\/|v\/)|ytimg\.com\/vi\/)(.+?)(?:[^-a-zA-Z0-9]|$)");
        //    var match = regExp.Match(input);
        //    if (match.Success)
        //    {
        //        parsedres = match.Value;
        //        res = true;
        //    }
        //    return res;
        //}

        #endregion
    }
}
