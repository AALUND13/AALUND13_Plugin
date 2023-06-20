using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System;
using System.Timers;
using NLog;
using System.Linq;

namespace AALUND13_Plugin
{
    public partial class AALUND13_PluginControl : UserControl
    {
        public HashSet<string> blacklistTags = new HashSet<string>();
        public ObservableCollection<string> observableBlacklistTags = new ObservableCollection<string>();
        public HashSet<string> copyBlacklistTags = new HashSet<string>();
        public static readonly Logger Log = LogManager.GetCurrentClassLogger();
        private Timer timer;

        private AALUND13_Plugin Plugin { get; }

        private AALUND13_PluginControl()
        {
            InitializeComponent();
        }

        private void StartTimer()
        {
            timer = new Timer(1000); // 1 second interval
            timer.Elapsed += TimerElapsed;
            timer.Start();
        }

        private void TimerElapsed(object sender, ElapsedEventArgs e)
        {
            // Perform the desired operation
            //Log.Info("Executing every second...");
            if (!HashSetEqual(Plugin.Config.blacklistedTags, copyBlacklistTags))
            {
                Dispatcher.Invoke(() =>
                {
                    var newTags = Plugin.Config.blacklistedTags.Except(copyBlacklistTags);
                    var removedTags = copyBlacklistTags.Except(Plugin.Config.blacklistedTags).ToList();

                    foreach (var tag in newTags)
                    {
                        blacklistTags.Add(tag);
                        observableBlacklistTags.Add(tag);
                    }

                    foreach (var tag in removedTags)
                    {
                        blacklistTags.Remove(tag);
                        observableBlacklistTags.Remove(tag);
                    }

                    copyBlacklistTags.Clear();
                    foreach (var tag in Plugin.Config.blacklistedTags)
                    {
                        copyBlacklistTags.Add(tag);
                    }
                });
            }
        }


        private bool HashSetEqual<T>(HashSet<T> set1, HashSet<T> set2)
        {
            if (set1.Count != set2.Count)
                return false;

            foreach (var item in set1)
            {
                if (!set2.Contains(item))
                    return false;
            }

            return true;
        }


        public AALUND13_PluginControl(AALUND13_Plugin plugin) : this()
        {
            Plugin = plugin;
            DataContext = plugin.Config;

            blacklistTags = new HashSet<string>(Plugin.Config.blacklistedTags);
            copyBlacklistTags = new HashSet<string>(Plugin.Config.blacklistedTags);
            observableBlacklistTags = new ObservableCollection<string>(Plugin.Config.blacklistedTags);

            Tag_Listbox.ItemsSource = observableBlacklistTags;

            StartTimer();
        }

        private void SaveButton_OnClick(object sender, RoutedEventArgs e)
        {
            Plugin.Config.blacklistedTags = blacklistTags;
            Plugin.Save();
        }

        private void Add_Button_Click(object sender, RoutedEventArgs e)
        {
            if (Tags_Dropbox.SelectedItem != null)
            {
                string selectedItem = (Tags_Dropbox.SelectedItem as ComboBoxItem)?.Content?.ToString();
                if (!blacklistTags.Contains(selectedItem))
                {
                    blacklistTags.Add(selectedItem);
                }
            }
        }

        private void Remove_Button_Click(object sender, RoutedEventArgs e)
        {
            if (Tag_Listbox.SelectedItem != null)
            {
                string selectedItem = Tag_Listbox.SelectedItem.ToString();
                blacklistTags.Remove(selectedItem);
            }
        }
    }
}
