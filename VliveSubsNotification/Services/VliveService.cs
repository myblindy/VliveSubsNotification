using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using VliveSubsNotification.Models;
using VliveSubsNotification.ViewModels;

namespace VliveSubsNotification.Services
{
    class VliveService
    {
        const string ChannelJsonUrlFormat = @"https://api-vfan.vlive.tv/vproxy/channelplus/getChannelVideoList?app_id=8c6cc7b45d2568fb668be6e05b6e5a3b&gcc=CA&locale=en&channelSeq={0}&maxNumOfRows=300";
        const string VideoHtmlUrlFormat = @"https://www.vlive.tv/video/{0}";
        const string VideoJsonUrlFormat = @"https://apis.naver.com/rmcnmv/rmcnmv/vod/play/v2.0/{0}?key={1}&pid=rmcPlayer_15914868245172346&sid=2024&ver=2.0&devt=html5_pc&doct=json&ptc=https&sptc=https&cpt=vtt&ctls=%7B%22visible%22%3A%7B%22fullscreen%22%3Atrue%2C%22logo%22%3Afalse%2C%22playbackRate%22%3Afalse%2C%22scrap%22%3Afalse%2C%22playCount%22%3Atrue%2C%22commentCount%22%3Atrue%2C%22title%22%3Atrue%2C%22writer%22%3Atrue%2C%22expand%22%3Atrue%2C%22subtitles%22%3Atrue%2C%22thumbnails%22%3Atrue%2C%22quality%22%3Atrue%2C%22setting%22%3Atrue%2C%22script%22%3Afalse%2C%22logoDimmed%22%3Atrue%2C%22badge%22%3Atrue%2C%22seekingTime%22%3Atrue%2C%22muted%22%3Atrue%2C%22muteButton%22%3Afalse%2C%22viewerNotice%22%3Afalse%2C%22linkCount%22%3Afalse%2C%22createTime%22%3Afalse%2C%22thumbnail%22%3Atrue%7D%2C%22clicked%22%3A%7B%22expand%22%3Afalse%2C%22subtitles%22%3Afalse%7D%7D&pv=4.18.40&dr=2048x1152&cpl=en_US&lc=en_US&adi=%5B%7B%22adSystem%22%3A%22TPA%22%7D%5D&adu=dummy&videoId={0}&cc=CA";
        const int MaxThumbnailSize = 150;

        static readonly (string channelCode, int channelId)[] FollowedChannels = new[]
        {
            ("F73FF", 128),          // gfriend
            ("E8D2CB", 358),         // DC
        };

        readonly HttpClient HttpClient = new HttpClient(new HttpClientHandler
        {
            AutomaticDecompression = System.Net.DecompressionMethods.All,
        });

        public async Task RefreshAsync(MainWindowViewModel vm)
        {
            try
            {
                vm.Refreshing = true;

                foreach (var (channelCode, channelId) in FollowedChannels)
                {
                    // get the list of videos in the channel
                    HttpResponseMessage response;
                    using (var request = new HttpRequestMessage(HttpMethod.Get, string.Format(ChannelJsonUrlFormat, channelId))
                    {
                        Headers =
                        {
                            { "Host", "api-vfan.vlive.tv" },
                            { "User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:76.0) Gecko/20100101 Firefox/76.0" },
                            { "Accept", "application/json, text/plain, */*" },
                            { "Accept-Language", "en-US,en;q=0.5" },
                            { "Accept-Encoding", "gzip, deflate, br" },
                            { "Origin", "https://channels.vlive.tv" },
                            { "Connection", "keep-alive" },
                            { "Referer", $"https://channels.vlive.tv/{channelCode}/video" },
                            { "Pragma", "no-cache" },
                            { "Cache-Control", "no-cache" },
                        }
                    })
                    {
                        response = await HttpClient.SendAsync(request);
                    }

                    var responseText = await response.Content.ReadAsStringAsync();
                    var responseJson = JObject.Parse(responseText);

                    var channelName = responseJson["result"]!["channelInfo"]!["channelName"]!.Value<string>();

                    var videos = responseJson["result"]!["videoList"]
                        .Select(r => (videoId: r["videoSeq"]!.Value<int>(), title: r["title"]!.Value<string>(), date: r["createdAt"]!.Value<DateTime>(), thumbnail: r["thumbnail"]!.Value<string>()))
                        .ToList();

                    foreach (var (videoId, title, date, thumbnail) in videos)
                    {
                        // skip if seen or already known to have subs
                        var existingModelEntry = vm.VliveModel.Entries.FirstOrDefault(w => w.ChannelId == channelId && w.VideoId == videoId);
                        if (!(existingModelEntry is null) && !(existingModelEntry.PreviewImageBytes is null) && (existingModelEntry.IsWatched || existingModelEntry.HasEnglishSubs || existingModelEntry.IsIgnored))
                            continue;

                        // get the html of the video
                        var htmlText = await HttpClient.GetStringAsync(string.Format(VideoHtmlUrlFormat, videoId));
                        var reMatch = Regex.Match(htmlText, @"vlive\.video\.init\(.*\n\s+""([^""]+)"".*\n\s+""([^""]+)", RegexOptions.Singleline);
                        var (videoHexId, videoHexKey) = (reMatch.Groups[1].Value, reMatch.Groups[2].Value);

                        HttpResponseMessage videoJsonResponse;
                        using (var videoJsonRequest = new HttpRequestMessage(HttpMethod.Get, string.Format(VideoJsonUrlFormat, videoHexId, videoHexKey))
                        {
                            Headers =
                            {
                                { "Host", "apis.naver.com" },
                                { "User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:76.0) Gecko/20100101 Firefox/76.0" },
                                { "Accept", "*/*" },
                                { "Accept-Language", "en-US,en;q=0.5" },
                                { "Accept-Encoding", "gzip, deflate, br" },
                                { "Referer", $"https://www.vlive.tv/video/{videoId}" },
                                { "Origin", "https://www.vlive.tv" },
                                { "Connection", "keep-alive" },
                                { "Pragma", "no-cache" },
                                { "Cache-Control", "no-cache" },
                                { "TE", "Trailers" },
                            }
                        })
                        {
                            videoJsonResponse = await HttpClient.SendAsync(videoJsonRequest);
                        }

                        var videoJson = JObject.Parse(await videoJsonResponse.Content.ReadAsStringAsync());
                        var englishSubs = videoJson.ContainsKey("captions") && videoJson["captions"]!["list"].Select(r => r["language"]!.Value<string>()).Any(l => l == "en");
                        var videoDuration = TimeSpan.FromSeconds(videoJson["videos"]!["list"]!.First!["duration"]!.Value<double>());

                        var entry = new VliveEntryModel
                        {
                            ChannelId = channelId,
                            VideoId = videoId,

                            Title = title,
                            ChannelName = channelName,
                            Duration = videoDuration,
                            Date = date,
                            HasEnglishSubs = englishSubs,
                        };

                        var isNewEntry = vm.VliveModel.AddOrUpdateEntry(ref entry);

                        if ((entry.PreviewImageBytes is null || entry.PreviewImageBytes.Length == 0) && !string.IsNullOrWhiteSpace(thumbnail))
                            _ = Task.Run(async () =>
                              {
                                  var origBytes = await HttpClient.GetByteArrayAsync(thumbnail).ConfigureAwait(false);

                                  using var img = Image.Load(origBytes);

                                  var destW = img.Width > img.Height ? MaxThumbnailSize : 0;
                                  var destH = img.Width > img.Height ? 0 : MaxThumbnailSize;
                                  img.Mutate(x => x.Resize(destW, destH, KnownResamplers.Lanczos3));

                                  using var ms = new MemoryStream();
                                  img.SaveAsJpeg(ms);

                                  var bytes = ms.ToArray();

                                  await Dispatcher.UIThread.InvokeAsync(() => entry.PreviewImageBytes = bytes).ConfigureAwait(false);
                              });
                    }
                }
            }
            finally
            {
                vm.Refreshing = false;
            }
        }
    }
}