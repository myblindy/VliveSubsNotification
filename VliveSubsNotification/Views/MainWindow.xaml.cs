using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using VliveSubsNotification.Models;
using VliveSubsNotification.ViewModels;

namespace VliveSubsNotification.Views
{
    public class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
        }

        private void OnEntryDoubleTapped(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (((DataGrid)sender!).SelectedItem is VliveEntryModel entry)
                ((MainWindowViewModel)DataContext).VliveModel.OpenEntryCommand(entry);
        }


        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
