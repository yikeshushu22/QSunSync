﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Forms;
using SunSync.Models;
using Qiniu.Storage;
using Qiniu.Util;
using Qiniu.Storage.Model;
namespace SunSync
{
    /// <summary>
    /// Interaction logic for SyncSettingPage.xaml
    /// </summary>
    public partial class SyncSettingPage : Page
    {
        
        //local dir to sync
        private string syncLocalDir;
        //target bucket
        private string syncTargetBucket;
        //prefix
        private string syncPrefix;
        //ignore dir
        private bool ignoreDir;
        //overwrite same file
        private bool overwriteFile;
        //default chunk size
        private int defaultChunkSize;
        //upload threshold
        private int chunkUploadThreshold;
        //sync thread count
        private int syncThreadCount;
        //upload entry domain
        private string uploadEntryDomain;

        private MainWindow mainWindow;
        private SyncSetting syncSetting;
        public SyncSettingPage(MainWindow mainWindow)
        {
            InitializeComponent();
            this.mainWindow = mainWindow;
        }

        private void BackToHome_EventHandler(object sender, MouseButtonEventArgs e)
        {
            this.mainWindow.GotoHomePage();
        }


        private void BrowseFolderButton_EventHandler(object sender, RoutedEventArgs e)
        {
            FolderBrowserDialog fbd = new FolderBrowserDialog();
            fbd.ShowNewFolderButton = false;
            DialogResult dr = fbd.ShowDialog();
            if (dr.Equals(DialogResult.OK))
            {
                this.SyncLocalFolderTextBox.Text = fbd.SelectedPath;
            }
        }

        private void StartSyncButton_EventHandler(object sender, RoutedEventArgs e)
        {
            //save config to job record
            if (this.SyncLocalFolderTextBox.Text.Trim().Length == 0)
            {
                //todo 
                return;
            }

            if (this.SyncTargetBucketTextBox.Text.Trim().Length == 0)
            {
                //todo
                return;
            }
            this.syncLocalDir = this.SyncLocalFolderTextBox.Text.Trim();
            if (!Directory.Exists(this.syncLocalDir))
            {
                //directory not found
                this.SettingsErrorTextBlock.Text = "本地待同步目录不存在";
                return;
            }

            this.syncTargetBucket = this.SyncTargetBucketTextBox.Text.Trim();
            //check ak & sk and bucket
            Mac mac = new Mac(SystemConfig.ACCESS_KEY, SystemConfig.SECRET_KEY);
            BucketManager bucketManager = new BucketManager(mac);
            StatResult statResult = bucketManager.stat(this.syncTargetBucket, "WHOCAREYOU");
            if (statResult.ResponseInfo.StatusCode == 401)
            {
                //ak & sk not right
                this.SettingsErrorTextBlock.Text = "AK 或 SK 不正确";
                return;
            }
            else if (statResult.ResponseInfo.StatusCode == 631)
            {
                //bucket not exist
                this.SettingsErrorTextBlock.Text = "指定空间不存在";
                return;
            }

            //optional settings
            this.syncPrefix = this.PrefixTextBox.Text.Trim();
            this.ignoreDir = this.IgnoreRelativePathCheckBox.IsChecked.Value;
            this.overwriteFile = this.OverwriteFileCheckBox.IsChecked.Value;
            switch (this.ChunkDefaultSizeComboBox.SelectedIndex)
            {
                case 0:
                    this.defaultChunkSize = 128 * 1024;//128KB
                    break;
                case 1:
                    this.defaultChunkSize = 256 * 1024;//256KB;
                    break;
                case 2:
                    this.defaultChunkSize = 512 * 1024;//512KB
                    break;
                case 3:
                    this.defaultChunkSize = 1024 * 1024;//1MB
                    break;
                case 4:
                    this.defaultChunkSize = 2 * 1024 * 1024;//2MB
                    break;
                case 5:
                    this.defaultChunkSize = 4 * 1024 * 1024; //4MB
                    break;
                default:
                    this.defaultChunkSize = 512 * 1024;//512KB
                    break;
            }
            this.chunkUploadThreshold = (int)this.ChunkUploadThresholdSlider.Value * 1024 * 1024;
            this.syncThreadCount = (int)this.ThreadCountSlider.Value;
            switch (this.UploadEntryDomainComboBox.SelectedIndex)
            {
                case 0:
                    this.uploadEntryDomain = "http://up.qiniu.com";
                    break;
                case 1:
                    this.uploadEntryDomain = "http://upload.qiniu.com";
                    break;
                case 2:
                    this.uploadEntryDomain = "http://up.qiniug.com";
                    break;
                default:
                    this.uploadEntryDomain = "http://upload.qiniu.com";
                    break;
            }

            SyncSetting syncSetting = new SyncSetting();
            syncSetting.SyncLocalDir = this.syncLocalDir;
            syncSetting.SyncTargetBucket = this.syncTargetBucket;
            syncSetting.SyncPrefix = this.syncPrefix;
            syncSetting.IgnoreDir = this.ignoreDir;
            syncSetting.OverwriteFile = this.overwriteFile;
            syncSetting.SyncThreadCount = this.syncThreadCount;
            syncSetting.ChunkUploadThreshold = this.chunkUploadThreshold;
            syncSetting.DefaultChunkSize = this.defaultChunkSize;
            syncSetting.ChunkUploadThreshold = this.chunkUploadThreshold;
            syncSetting.UploadEntryDomain = this.uploadEntryDomain;

            this.mainWindow.GotoSyncProgress(syncSetting);
        }

        private void ChunkUploadThresholdChange_EventHandler(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (this.ChunkUploadThresholdLabel != null)
            {
                this.ChunkUploadThresholdLabel.Content = this.ChunkUploadThresholdSlider.Value + "MB";
            }
        }

        private void ThreadCountChange_EventHandler(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (this.ThreadCountLabel != null)
            {
                this.ThreadCountLabel.Content = this.ThreadCountSlider.Value + "";
            }
        }


        private void SyncSettingPageLoaded_EventHandler(object sender, RoutedEventArgs e)
        {
            if (this.syncSetting == null)
            {
                //use defaults for ui
                this.SyncSettingTabControl.SelectedIndex = 0;
                this.makeBasicSettingsDefault();
                this.makeAdvancedSettingsDefault();
            }
        }

        private void makeBasicSettingsDefault()
        {
            this.SyncLocalFolderTextBox.Text = "";
            this.SyncTargetBucketTextBox.Text = "";
        }

        private void makeAdvancedSettingsDefault()
        {
            this.PrefixTextBox.Text = "";
            this.OverwriteFileCheckBox.IsChecked = false;
            this.IgnoreRelativePathCheckBox.IsChecked = false;
            this.ChunkDefaultSizeComboBox.SelectedIndex = 2; //512KB
            this.ChunkUploadThresholdSlider.Value = 10;//10MB
            this.ThreadCountSlider.Value = 1;
            this.UploadEntryDomainComboBox.SelectedIndex = 1;
            this.ThreadCountLabel.Content = "1";
        }

    }

}
