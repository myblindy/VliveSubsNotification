using DynamicData;
using MoreLinq;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VliveSubsNotification.Models;
using VliveSubsNotification.Services;

namespace VliveSubsNotification.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        private readonly VliveService VliveService = new VliveService();
        public VliveModel VliveModel { get; } = new VliveModel();

        bool refreshing;
        public bool Refreshing
        {
            get => refreshing;
            set => this.RaiseAndSetIfChanged(ref refreshing, value);
        }

        bool onlyInteresting;
        public bool OnlyInteresting
        {
            get => onlyInteresting;
            set
            {
                if (value) OnlyActive = false;
                this.RaiseAndSetIfChanged(ref onlyInteresting, value);
                ClearSelectedEntries();
                AddIfInteresting(VliveModel.Entries);
            }
        }

        bool onlyActive;
        public bool OnlyActive
        {
            get => onlyActive;
            set
            {
                if (value) OnlyInteresting = false;
                this.RaiseAndSetIfChanged(ref onlyActive, value);
                ClearSelectedEntries();
                AddIfInteresting(VliveModel.Entries);
            }
        }

        void SelectedEntriesChangeHandler(object sender, PropertyChangedEventArgs e)
        {
            var entry = (VliveEntryModel)sender;
            if (!IsInteresting(entry))
            {
                entry.PropertyChanged -= SelectedEntriesChangeHandler;
                SelectedEntries.Remove(entry);
            }
        }

        private void ClearSelectedEntries()
        {
            SelectedEntries.ForEach(e => e.PropertyChanged -= SelectedEntriesChangeHandler);
            SelectedEntries.Clear();
        }

        public ObservableCollection<VliveEntryModel> SelectedEntries { get; } = new ObservableCollection<VliveEntryModel>();

        public Task RefreshCommand() => VliveService.RefreshAsync(this);

        bool IsInteresting(VliveEntryModel entry) =>
            OnlyInteresting ? entry.HasEnglishSubs && !entry.IsWatched && !entry.IsIgnored :
            OnlyActive ? !entry.HasEnglishSubs && !entry.IsIgnored && !entry.IsWatched :
            true;

        void AddIfInteresting(IEnumerable<VliveEntryModel> entries) =>
            entries.Where(IsInteresting).ForEach(entry =>
                {
                    SelectedEntries.Add(entry);
                    entry.PropertyChanged += SelectedEntriesChangeHandler;
                });

        public MainWindowViewModel()
        {
            AddIfInteresting(VliveModel.Entries);
            VliveModel.Entries.CollectionChanged += (s, e) =>
            {
                switch (e!.Action)
                {
                    case NotifyCollectionChangedAction.Add:
                        AddIfInteresting(e.NewItems.Cast<VliveEntryModel>());
                        break;
                    default:
                        throw new InvalidOperationException();
                }
            };
        }
    }
}
