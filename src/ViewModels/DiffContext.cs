﻿using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.ViewModels
{
    public class DiffContext : ObservableObject
    {
        public string RepositoryPath
        {
            get => _repo;
        }

        public Models.Change WorkingCopyChange
        {
            get => _option.WorkingCopyChange;
        }

        public bool IsUnstaged
        {
            get => _option.IsUnstaged;
        }

        public string Title
        {
            get => _title;
            private set => SetProperty(ref _title, value);
        }

        public string FileModeChange
        {
            get => _fileModeChange;
            private set => SetProperty(ref _fileModeChange, value);
        }

        public bool IsLoading
        {
            get => _isLoading;
            private set => SetProperty(ref _isLoading, value);
        }

        public bool IsTextDiff
        {
            get => _isTextDiff;
            private set => SetProperty(ref _isTextDiff, value);
        }

        public object Content
        {
            get => _content;
            private set => SetProperty(ref _content, value);
        }

        public Vector SyncScrollOffset
        {
            get => _syncScrollOffset;
            set => SetProperty(ref _syncScrollOffset, value);
        }

        public DiffContext(string repo, Models.DiffOption option, DiffContext previous = null)
        {
            _repo = repo;
            _option = option;

            if (previous != null)
            {
                _isTextDiff = previous._isTextDiff;
                _content = previous._content;
            }

            Task.Run(() =>
            {
                var latest = new Commands.Diff(repo, option).Result();
                var rs = null as object;

                if (latest.TextDiff != null)
                {
                    latest.TextDiff.File = _option.Path;
                    rs = latest.TextDiff;
                }
                else if (latest.IsBinary)
                {
                    var oldPath = string.IsNullOrEmpty(_option.OrgPath) ? _option.Path : _option.OrgPath;
                    var ext = Path.GetExtension(oldPath);

                    if (IMG_EXTS.Contains(ext))
                    {
                        var imgDiff = new Models.ImageDiff();
                        if (option.Revisions.Count == 2)
                        {
                            imgDiff.Old = BitmapFromRevisionFile(repo, option.Revisions[0], oldPath);
                            imgDiff.New = BitmapFromRevisionFile(repo, option.Revisions[1], oldPath);
                        }
                        else
                        {
                            var fullPath = Path.Combine(repo, _option.Path);
                            imgDiff.Old = BitmapFromRevisionFile(repo, "HEAD", oldPath);
                            imgDiff.New = File.Exists(fullPath) ? new Bitmap(fullPath) : null;
                        }
                        rs = imgDiff;
                    }
                    else
                    {
                        var binaryDiff = new Models.BinaryDiff();
                        if (option.Revisions.Count == 2)
                        {
                            binaryDiff.OldSize = new Commands.QueryFileSize(repo, oldPath, option.Revisions[0]).Result();
                            binaryDiff.NewSize = new Commands.QueryFileSize(repo, _option.Path, option.Revisions[1]).Result();
                        }
                        else
                        {
                            var fullPath = Path.Combine(repo, _option.Path);
                            binaryDiff.OldSize = new Commands.QueryFileSize(repo, oldPath, "HEAD").Result();
                            binaryDiff.NewSize = File.Exists(fullPath) ? new FileInfo(fullPath).Length : 0;
                        }
                        rs = binaryDiff;
                    }
                }
                else if (latest.IsLFS)
                {
                    rs = latest.LFSDiff;
                }
                else
                {
                    rs = new Models.NoOrEOLChange();
                }

                Dispatcher.UIThread.Post(() =>
                {
                    if (string.IsNullOrEmpty(_option.OrgPath))
                        Title = _option.Path;
                    else
                        Title = $"{_option.OrgPath} → {_option.Path}";

                    FileModeChange = latest.FileModeChange;
                    Content = rs;
                    IsTextDiff = latest.TextDiff != null;
                    IsLoading = false;
                });
            });
        }

        public void OpenExternalMergeTool()
        {
            var type = Preference.Instance.ExternalMergeToolType;
            var exec = Preference.Instance.ExternalMergeToolPath;

            var tool = Models.ExternalMerger.Supported.Find(x => x.Type == type);
            if (tool == null || !File.Exists(exec))
            {
                App.RaiseException(_repo, "Invalid merge tool in preference setting!");
                return;
            }

            var args = tool.Type != 0 ? tool.DiffCmd : Preference.Instance.ExternalMergeToolDiffCmd;
            Task.Run(() => Commands.MergeTool.OpenForDiff(_repo, exec, args, _option));
        }

        private Bitmap BitmapFromRevisionFile(string repo, string revision, string file)
        {
            var stream = Commands.QueryFileContent.Run(repo, revision, file);
            return stream.Length > 0 ? new Bitmap(stream) : null;
        }

        private static readonly HashSet<string> IMG_EXTS = new HashSet<string>()
        {
            ".ico", ".bmp", ".jpg", ".png", ".jpeg"
        };

        private readonly string _repo = string.Empty;
        private readonly Models.DiffOption _option = null;
        private string _title = string.Empty;
        private string _fileModeChange = string.Empty;
        private bool _isLoading = true;
        private bool _isTextDiff = false;
        private object _content = null;
        private Vector _syncScrollOffset = Vector.Zero;
    }
}
