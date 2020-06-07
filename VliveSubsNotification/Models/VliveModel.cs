using Avalonia.Media.Imaging;
using DynamicData;
using DynamicData.Annotations;
using LiteDB;
using MoreLinq;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace VliveSubsNotification.Models
{
    public class VliveModel : ReactiveObject
    {
        static LiteDatabase OpenDatabase() => new LiteDatabase("vlive.db");

        public ObservableCollection<VliveEntryModel> Entries { get; } = new ObservableCollection<VliveEntryModel>();

        internal void OpenEntryCommand(VliveEntryModel entry) =>
            Process.Start(new ProcessStartInfo($"https://www.vlive.tv/video/{entry.VideoId}")
            {
                UseShellExecute = true,
            });

        static VliveModel()
        {
            BsonMapper.Global.Entity<VliveEntryModel>()
                .Ignore(x => x.ThrownExceptions).Ignore(x => x.Changed).Ignore(x => x.Changing).Ignore(x => x.PreviewImage);
        }

        public VliveModel()
        {
            static void AttachChangeHandler(VliveEntryModel entry) =>
                entry.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName != nameof(VliveEntryModel.PreviewImage))
                        using (var db = OpenDatabase())
                        {
                            var collection = db.GetCollection<VliveEntryModel>();
                            collection.Update((VliveEntryModel)s);
                        }
                };

            using (var db = OpenDatabase())
            {
                var collection = db.GetCollection<VliveEntryModel>();
                Entries.AddRange(collection.FindAll());
                Entries.ForEach(AttachChangeHandler);
            }

            Entries.CollectionChanged += (s, e) =>
            {
                switch (e!.Action)
                {
                    case NotifyCollectionChangedAction.Add:
                        using (var db = OpenDatabase())
                        {
                            var collection = db.GetCollection<VliveEntryModel>();
                            collection.InsertBulk(e.NewItems.Cast<VliveEntryModel>());
                            e.NewItems.Cast<VliveEntryModel>().ForEach(AttachChangeHandler);
                        }
                        break;
                }
            };
        }

        public bool AddOrUpdateEntry(ref VliveEntryModel entry)
        {
            var _entry = entry;
            var existingEntry = Entries.FirstOrDefault(w => w == _entry);
            if (existingEntry is null)
            {
                Entries.Add(entry);
                return true;
            }
            else
            {
                existingEntry.CopyLiveDataFrom(entry);
                entry = existingEntry;
                return false;
            }
        }
    }

    public class VliveEntryModel : ReactiveObject
    {
        public int Id { get; set; }

        string? channelName;
        public string? ChannelName { get => channelName; set => this.RaiseAndSetIfChanged(ref channelName, value); }

        string? title;
        public string? Title { get => title; set => this.RaiseAndSetIfChanged(ref title, value); }

        TimeSpan duration;
        public TimeSpan Duration { get => duration; set => this.RaiseAndSetIfChanged(ref duration, value); }

        DateTime date;
        public DateTime Date { get => date; set => this.RaiseAndSetIfChanged(ref date, value); }

        byte[]? previewImageBytes;
        public byte[]? PreviewImageBytes
        {
            get => previewImageBytes;
            set
            {
                if (value == previewImageBytes || (!(value is null) && !(previewImageBytes is null) && previewImageBytes.SequenceEqual(value)))
                    return;

                if (value is null)
                    PreviewImage = null;
                else
                {
                    using var mem = new MemoryStream(value);
                    PreviewImage = new Bitmap(mem);
                }

                this.RaiseAndSetIfChanged(ref previewImageBytes, value);
                this.RaisePropertyChanged(nameof(PreviewImage));
            }
        }

        public IBitmap? PreviewImage { get; private set; }

        bool hasEnglishSubs;
        public bool HasEnglishSubs { get => hasEnglishSubs; set => this.RaiseAndSetIfChanged(ref hasEnglishSubs, value); }

        bool isWatched;
        public bool IsWatched { get => isWatched; set => this.RaiseAndSetIfChanged(ref isWatched, value); }

        bool isIgnored;
        public bool IsIgnored { get => isIgnored; set => this.RaiseAndSetIfChanged(ref isIgnored, value); }

        public int VideoId { get; set; }
        public int ChannelId { get; set; }

        public void CopyLiveDataFrom(VliveEntryModel other) =>
            (ChannelName, Title, Duration, Date, HasEnglishSubs) =
                (other.ChannelName, other.Title, other.Duration, other.Date, other.HasEnglishSubs);

        public override bool Equals(object? obj) => obj is VliveEntryModel model && VideoId == model.VideoId && ChannelId == model.ChannelId;
        public override int GetHashCode() => HashCode.Combine(VideoId, ChannelId);

        public static bool operator ==(VliveEntryModel left, VliveEntryModel right) => EqualityComparer<VliveEntryModel>.Default.Equals(left, right);
        public static bool operator !=(VliveEntryModel left, VliveEntryModel right) => !(left == right);
    }
}