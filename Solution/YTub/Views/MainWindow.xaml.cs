﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
//using System.Windows.Forms;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using YoutubeExtractor;
using YTub.Common;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace YTub.Views
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void Exit_OnClick(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void RemoveChanelOnClick(object sender, RoutedEventArgs e)
        {
            ViewModelLocator.MvViewModel.Model.MySubscribe.RemoveChanel(null);
        }

        private void SyncChanelOnClick(object sender, RoutedEventArgs e)
        {
            ViewModelLocator.MvViewModel.Model.MySubscribe.SyncChanel("SyncChanelSelected");
        }

        private void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
        {
            ViewModelLocator.MvViewModel.Model.MySubscribe.GetChanelsFromDb();
        }

        private void EditChanelOnClick(object sender, RoutedEventArgs e)
        {
            ViewModelLocator.MvViewModel.Model.MySubscribe.AddChanel("edit");
        }

        private void MainWindow_OnKeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Insert)
            {
                //ViewModelLocator.MvViewModel.Model.MySubscribe.AddChanel(null);
                ViewModelLocator.MvViewModel.Model.AddLink(null);
            }
        }

        private void DownloadVideoOnClick(object sender, RoutedEventArgs e)
        {
            var sndr = sender as MenuItem;
            if (sndr == null)
                ViewModelLocator.MvViewModel.Model.MySubscribe.CurrentChanel.DownloadVideo("Internal");
            else
                ViewModelLocator.MvViewModel.Model.MySubscribe.CurrentChanel.DownloadVideo(sndr.CommandParameter.ToString());
        }

        //private void AddToQueueOnClick(object sender, RoutedEventArgs e)
        //{
        //    throw new System.NotImplementedException();
        //}

        private void PlayOnClick(object sender, RoutedEventArgs e)
        {
            var sndr = sender as MenuItem;
            if (sndr == null)
            {
                ViewModelLocator.MvViewModel.Model.MySubscribe.PlayFile("Online");
            }
            else
            {
                switch (sndr.CommandParameter.ToString())
                {
                    case "Local":
                        ViewModelLocator.MvViewModel.Model.MySubscribe.PlayFile(sndr.CommandParameter.ToString());
                        break;

                    case "Online":
                        ViewModelLocator.MvViewModel.Model.MySubscribe.PlayFile(sndr.CommandParameter.ToString());
                        break;
                }    
            }
        }

        private void PlayLocalButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (ViewModelLocator.MvViewModel.Model.MySubscribe.CurrentChanel.CurrentVideoItem.IsHasFile)
                ViewModelLocator.MvViewModel.Model.MySubscribe.PlayFile("Local");
            else
                ViewModelLocator.MvViewModel.Model.MySubscribe.CurrentChanel.DownloadVideo("MaxQuality");
        }

        private void CopyLinkOnClick(object sender, RoutedEventArgs e)
        {
            if (ViewModelLocator.MvViewModel.Model.MySubscribe.CurrentChanel != null &&
                ViewModelLocator.MvViewModel.Model.MySubscribe.CurrentChanel.CurrentVideoItem != null)
            {
                try
                {
                    Clipboard.SetText(
                        ViewModelLocator.MvViewModel.Model.MySubscribe.CurrentChanel.CurrentVideoItem.VideoLink);
                }
                catch
                {
                    //MessageBox.Show("SS");
                }
            }
        }

        private void DeleteOnClick(object sender, RoutedEventArgs e)
        {
            if (ViewModelLocator.MvViewModel.Model.MySubscribe.CurrentChanel != null &&
                ViewModelLocator.MvViewModel.Model.MySubscribe.CurrentChanel.CurrentVideoItem != null)
            {
                ViewModelLocator.MvViewModel.Model.MySubscribe.CurrentChanel.DeleteFiles();
            }
        }
    }
}
