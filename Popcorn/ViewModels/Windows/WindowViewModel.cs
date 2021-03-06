﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Navigation;
using Akavache;
using Enterwell.Clients.Wpf.Notifications;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.CommandWpf;
using GalaSoft.MvvmLight.Ioc;
using GalaSoft.MvvmLight.Messaging;
using GalaSoft.MvvmLight.Threading;
using MahApps.Metro.Controls.Dialogs;
using Microsoft.Win32;
using NLog;
using Polly.Timeout;
using Popcorn.Dialogs;
using Popcorn.Extensions;
using Popcorn.Helpers;
using Popcorn.Messaging;
using Popcorn.Models.Subtitles;
using Popcorn.Services.Application;
using Popcorn.Services.Cache;
using Popcorn.Services.Chromecast;
using Popcorn.Services.User;
using Popcorn.Utils;
using Popcorn.Utils.Exceptions;
using Popcorn.ViewModels.Dialogs;
using Popcorn.ViewModels.Pages.Home;
using Popcorn.ViewModels.Pages.Home.Movie;
using Popcorn.ViewModels.Pages.Home.Show;
using Popcorn.ViewModels.Pages.Player;
using Popcorn.ViewModels.Windows.Settings;
using Popcorn.Services.Subtitles;

namespace Popcorn.ViewModels.Windows
{
    /// <summary>
    /// Window applcation's viewmodel
    /// </summary>
    public class WindowViewModel : ViewModelBase
    {
        /// <summary>
        /// Logger of the class
        /// </summary>
        private static Logger Logger { get; } = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Holds the async message relative to <see cref="ShowCustomSubtitleMessage"/>
        /// </summary>
        private IDisposable _customSubtitleMessage;

        /// <summary>
        /// Holds the async message relative to <see cref="ShowSubtitleDialogMessage"/>
        /// </summary>
        private IDisposable _showSubtitleDialogMessage;

        /// <summary>
        /// Holds the async message relative to <see cref="ShowCastMediaMessage"/>
        /// </summary>
        private IDisposable _castMediaMessage;

        /// <summary>
        /// Holds the async message relative to <see cref="ShowDownloadSettingsDialogMessage"/>
        /// </summary>
        private IDisposable _showDownloadSettingsMessage;

        /// <summary>
        /// Used to define the dialog context
        /// </summary>
        private readonly IDialogCoordinator _dialogCoordinator;

        /// <summary>
        /// Specify if movie flyout is open
        /// </summary>
        private bool _isMovieFlyoutOpen;

        /// <summary>
        /// Specify if cast flyout is open
        /// </summary>
        private bool _isCastFlyoutOpen;

        /// <summary>
        /// Specify if show flyout is open
        /// </summary>
        private bool _isShowFlyoutOpen;

        /// <summary>
        /// Specify if settings flyout is open
        /// </summary>
        private bool _isSettingsFlyoutOpen;

        /// <summary>
        /// If an update is available
        /// </summary>
        private bool _updateAvailable;

        /// <summary>
        /// Toggle fullscreen mode
        /// </summary>
        private bool _toggleFullscreen;

        /// <summary>
        /// Application state
        /// </summary>
        private IApplicationService _applicationService;

        /// <summary>
        /// Subtitles service
        /// </summary>
        private readonly ISubtitlesService _subtitlesService;

        /// <summary>
        /// The movie history service
        /// </summary>
        private readonly IUserService _userService;

        /// <summary>
        /// The chromecast service
        /// </summary>
        private readonly IChromecastService _chromecastService;

        /// <summary>
        /// <see cref="MediaPlayer"/>
        /// </summary>
        private MediaPlayerViewModel _mediaPlayer;

        /// <summary>
        /// Ignore taskbar on maximize 
        /// </summary>
        private bool _ignoreTaskbarOnMaximize;

        /// <summary>
        /// Main frame navigation service
        /// </summary>
        public NavigationService NavigationService { get; set; }

        /// <summary>
        /// The notification manager
        /// </summary>
        private readonly NotificationMessageManager _manager;

        /// <summary>
        /// The cache service
        /// </summary>
        private readonly ICacheService _cacheService;

        /// <summary>
        /// Initializes a new instance of the WindowViewModel class.
        /// </summary>
        /// <param name="applicationService">Instance of Application state</param>
        /// <param name="userService">Instance of movie history service</param>
        /// <param name="subtitlesService">Instance of subtitles service</param>
        /// <param name="chromecastService">Instance of Chromecast service</param>
        /// <param name="cacheService">Instance of cache service</param>
        /// <param name="manager">The notification manager</param>
        public WindowViewModel(IApplicationService applicationService, IUserService userService,
            ISubtitlesService subtitlesService, IChromecastService chromecastService,
            ICacheService cacheService, NotificationMessageManager manager)
        {
            _chromecastService = chromecastService;
            _cacheService = cacheService;
            _manager = manager;
            _subtitlesService = subtitlesService;
            _userService = userService;
            _dialogCoordinator = DialogCoordinator.Instance;
            _applicationService = applicationService;
            RegisterMessages();
            RegisterCommands();
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        }

        /// <summary>
        /// Application state
        /// </summary>
        public IApplicationService ApplicationService
        {
            get => _applicationService;
            set { Set(() => ApplicationService, ref _applicationService, value); }
        }

        /// <summary>
        /// Ignore taskbar on maximize 
        /// </summary>
        public bool IgnoreTaskbarOnMaximize
        {
            get => _ignoreTaskbarOnMaximize;
            set { Set(() => IgnoreTaskbarOnMaximize, ref _ignoreTaskbarOnMaximize, value); }
        }

        /// <summary>
        /// Specify if settings flyout is open
        /// </summary>
        public bool IsSettingsFlyoutOpen
        {
            get => _isSettingsFlyoutOpen;
            set { Set(() => IsSettingsFlyoutOpen, ref _isSettingsFlyoutOpen, value); }
        }

        /// <summary>
        /// Specify if movie flyout is open
        /// </summary>
        public bool IsMovieFlyoutOpen
        {
            get => _isMovieFlyoutOpen;
            set { Set(() => IsMovieFlyoutOpen, ref _isMovieFlyoutOpen, value); }
        }

        /// <summary>
        /// Specify if cast flyout is open
        /// </summary>
        public bool IsCastFlyoutOpen
        {
            get => _isCastFlyoutOpen;
            set { Set(() => IsCastFlyoutOpen, ref _isCastFlyoutOpen, value); }
        }

        /// <summary>
        /// Specify if show flyout is open
        /// </summary>
        public bool IsShowFlyoutOpen
        {
            get => _isShowFlyoutOpen;
            set { Set(() => IsShowFlyoutOpen, ref _isShowFlyoutOpen, value); }
        }

        /// <summary>
        /// If an update is available
        /// </summary>
        public bool UpdateAvailable
        {
            get => _updateAvailable;
            set { Set(() => UpdateAvailable, ref _updateAvailable, value); }
        }

        /// <summary>
        /// Media player
        /// </summary>
        public MediaPlayerViewModel MediaPlayer
        {
            get => _mediaPlayer;
            set { Set(() => MediaPlayer, ref _mediaPlayer, value); }
        }

        /// <summary>
        /// Command used to close movie page
        /// </summary>
        public ICommand CloseMoviePageCommand { get; private set; }

        /// <summary>
        /// Command used to close show page
        /// </summary>
        public ICommand CloseShowPageCommand { get; private set; }

        /// <summary>
        /// Command used to close the application
        /// </summary>
        public ICommand MainWindowClosingCommand { get; private set; }

        /// <summary>
        /// Command used to open application settings
        /// </summary>
        public ICommand OpenSettingsCommand { get; private set; }

        /// <summary>
        /// Command used to open about dialog
        /// </summary>
        public ICommand OpenAboutCommand { get; private set; }

        /// <summary>
        /// Command used to switch fullscreen mode
        /// </summary>
        public ICommand SwitchFullScreenCommand { get; private set; }

        /// <summary>
        /// Command used to open Github
        /// </summary>
        public ICommand GoToGitHubCommand { get; private set; }

        /// <summary>
        /// Command used to load tabs
        /// </summary>
        public ICommand InitializeAsyncCommand { get; private set; }

        /// <summary>
        /// Command used to drop files
        /// </summary>
        public ICommand DropFileCommand { get; private set; }

        /// <summary>
        /// Command used to manage drag enter
        /// </summary>
        public ICommand DragEnterFileCommand { get; private set; }

        /// <summary>
        /// Command used to manage drag leave
        /// </summary>
        public ICommand DragLeaveFileCommand { get; private set; }

        /// <summary>
        /// Register messages
        /// </summary>
        private void RegisterMessages()
        {
            Messenger.Default.Register<ManageExceptionMessage>(this, e => ManageException(e.UnHandledException));

            Messenger.Default.Register<LoadMovieMessage>(this, e =>
            {
                IsCastFlyoutOpen = false;
                IsMovieFlyoutOpen = true;
            });

            Messenger.Default.Register<LoadShowMessage>(this, e => IsShowFlyoutOpen = true);

            Messenger.Default.Register<PlayShowEpisodeMessage>(this, message => DispatcherHelper.CheckBeginInvokeOnUI(
                async () =>
                {
                    MediaPlayer = new MediaPlayerViewModel(_chromecastService, _subtitlesService, _cacheService,
                        message.Episode.FilePath,
                        message.Episode.Title,
                        MediaType.Show,
                        () =>
                        {
                            Messenger.Default.Send(new StopPlayingEpisodeMessage());
                        },
                        () =>
                        {
                            Messenger.Default.Send(new StopPlayingEpisodeMessage());
                        },
                        message.PlayingProgress,
                        message.BufferProgress,
                        message.PieceAvailability,
                        message.BandwidthRate,
                        message.Episode.SelectedSubtitle,
                        message.Episode.AvailableSubtitles);

                    ApplicationService.IsMediaPlaying = true;
                    IsShowFlyoutOpen = false;
                    if (NavigationService.CurrentSource.OriginalString == "Popcorn;component/Pages/PlayerPage.xaml")
                    {
                        NavigationService.Refresh();
                    }
                    else
                    {
                        NavigationService.Navigate(new Uri("Popcorn;component/Pages/PlayerPage.xaml",
                            UriKind.Relative));
                    }

                    await Task.Delay(500);
                    IgnoreTaskbarOnMaximize = true;
                }));

            Messenger.Default.Register<PlayMediaMessage>(this, message => DispatcherHelper.CheckBeginInvokeOnUI(
                async () =>
                {
                    MediaPlayer = new MediaPlayerViewModel(_chromecastService, _subtitlesService, _cacheService,
                        message.MediaPath,
                        message.MediaPath,
                        MediaType.Unkown,
                        () =>
                        {
                            Messenger.Default.Send(new NavigateToHomePageMessage());
                        },
                        () =>
                        {
                            Messenger.Default.Send(new NavigateToHomePageMessage());
                        },
                        message.PlayingProgress,
                        message.BufferProgress,
                        message.PieceAvailability,
                        message.BandwidthRate, subtitles: new List<Subtitle>
                        {
                            new Subtitle
                            {
                                Sub = new OSDB.Subtitle
                                {
                                    LanguageName = LocalizationProviderHelper.GetLocalizedValue<string>("NoneLabel"),
                                    SubtitleId = "none"
                                }
                            },
                            new Subtitle
                            {
                                Sub = new OSDB.Subtitle
                                {
                                    LanguageName = LocalizationProviderHelper.GetLocalizedValue<string>("CustomLabel"),
                                    SubtitleId = "custom"
                                }
                            }
                        });

                    ApplicationService.IsMediaPlaying = true;
                    IsShowFlyoutOpen = false;
                    IsMovieFlyoutOpen = false;
                    if (NavigationService.CurrentSource.OriginalString == "Popcorn;component/Pages/PlayerPage.xaml")
                    {
                        NavigationService.Refresh();
                    }
                    else
                    {
                        NavigationService.Navigate(new Uri("Popcorn;component/Pages/PlayerPage.xaml",
                            UriKind.Relative));
                    }

                    await Task.Delay(500);
                    IgnoreTaskbarOnMaximize = true;
                }));

            Messenger.Default.Register<PlayMovieMessage>(this, message => DispatcherHelper.CheckBeginInvokeOnUI(
                async () =>
                {
                    MediaPlayer = new MediaPlayerViewModel(_chromecastService, _subtitlesService, _cacheService,
                        message.Movie.FilePath, message.Movie.Title,
                        MediaType.Movie,
                        () =>
                        {
                            Messenger.Default.Send(new StopPlayingMovieMessage());
                        },
                        () =>
                        {
                            _userService.SetMovie(message.Movie);
                            Messenger.Default.Send(new ChangeSeenMovieMessage());
                            Messenger.Default.Send(new StopPlayingMovieMessage());
                        },
                        message.PlayingProgress,
                        message.BufferProgress,
                        message.PieceAvailability,
                        message.BandwidthRate,
                        message.Movie.SelectedSubtitle,
                        message.Movie.AvailableSubtitles);

                    ApplicationService.IsMediaPlaying = true;
                    IsMovieFlyoutOpen = false;
                    if (NavigationService.CurrentSource.OriginalString == "Popcorn;component/Pages/PlayerPage.xaml")
                    {
                        NavigationService.Refresh();
                    }
                    else
                    {
                        NavigationService.Navigate(new Uri("Popcorn;component/Pages/PlayerPage.xaml",
                            UriKind.Relative));
                    }

                    await Task.Delay(500);
                    IgnoreTaskbarOnMaximize = true;
                }));

            Messenger.Default.Register<PlayTrailerMessage>(this, message => DispatcherHelper.CheckBeginInvokeOnUI(
                async () =>
                {
                    MediaPlayer = new MediaPlayerViewModel(_chromecastService, _subtitlesService, _cacheService,
                        message.TrailerUrl,
                        message.MovieTitle,
                        MediaType.Trailer,
                        message.TrailerStoppedAction, message.TrailerEndedAction);
                    ApplicationService.IsMediaPlaying = true;
                    IsMovieFlyoutOpen = false;
                    IsShowFlyoutOpen = false;
                    if (NavigationService.CurrentSource.OriginalString == "Popcorn;component/Pages/PlayerPage.xaml")
                    {
                        NavigationService.Refresh();
                    }
                    else
                    {
                        NavigationService.Navigate(new Uri("Popcorn;component/Pages/PlayerPage.xaml",
                            UriKind.Relative));
                    }

                    await Task.Delay(500);
                    IgnoreTaskbarOnMaximize = true;
                }));

            Messenger.Default.Register<StopPlayingTrailerMessage>(this, message =>
            {
                if (!_toggleFullscreen)
                    IgnoreTaskbarOnMaximize = false;
                if (ApplicationService.IsMediaPlaying)
                {
                    if (!_toggleFullscreen && ApplicationService.IsFullScreen)
                    {
                        ApplicationService.IsFullScreen = false;
                        ApplicationService.IsFullScreen = true;
                    }

                    ApplicationService.IsMediaPlaying = false;
                    if (message.Type == MediaType.Movie)
                    {
                        IsMovieFlyoutOpen = true;
                    }
                    else
                    {
                        IsShowFlyoutOpen = true;
                    }
                }
            });

            Messenger.Default.Register<StopPlayMediaMessage>(this, message =>
            {
                if (!_toggleFullscreen)
                {
                    IgnoreTaskbarOnMaximize = false;
                    if (ApplicationService.IsMediaPlaying && ApplicationService.IsFullScreen)
                    {
                        ApplicationService.IsFullScreen = false;
                        ApplicationService.IsFullScreen = true;
                    }
                }

                ApplicationService.IsMediaPlaying = false;
            });

            Messenger.Default.Register<NavigateToHomePageMessage>(this, async message =>
            {
                await Task.Delay(500);
                if (!_toggleFullscreen)
                {
                    IgnoreTaskbarOnMaximize = false;
                    if (ApplicationService.IsMediaPlaying && ApplicationService.IsFullScreen)
                    {
                        ApplicationService.IsFullScreen = false;
                        ApplicationService.IsFullScreen = true;
                    }
                }

                ApplicationService.IsMediaPlaying = false;
                IsMovieFlyoutOpen = false;
                IsShowFlyoutOpen = false;

                if (NavigationService.CurrentSource.OriginalString != "Pages/HomePage.xaml" &&
                    NavigationService.CanGoBack)
                {
                    NavigationService.GoBack();
                }
            });

            Messenger.Default.Register<StopPlayingEpisodeMessage>(
                this,
                message =>
                {
                    if (!_toggleFullscreen)
                    {
                        IgnoreTaskbarOnMaximize = false;
                        if (ApplicationService.IsMediaPlaying && ApplicationService.IsFullScreen)
                        {
                            ApplicationService.IsFullScreen = false;
                            ApplicationService.IsFullScreen = true;
                        }
                    }

                    ApplicationService.IsMediaPlaying = false;
                    IsShowFlyoutOpen = true;
                });

            Messenger.Default.Register<StopPlayingMovieMessage>(
                this,
                message =>
                {
                    if (!_toggleFullscreen)
                    {
                        IgnoreTaskbarOnMaximize = false;
                        if (ApplicationService.IsMediaPlaying && ApplicationService.IsFullScreen)
                        {
                            ApplicationService.IsFullScreen = false;
                            ApplicationService.IsFullScreen = true;
                        }
                    }

                    ApplicationService.IsMediaPlaying = false;
                    IsMovieFlyoutOpen = true;
                });

            Messenger.Default.Register<ChangeLanguageMessage>(
                this,
                message =>
                {
                    var pages = SimpleIoc.Default.GetInstance<PagesViewModel>();
                    foreach (var page in pages.Pages)
                    {
                        if (page is MoviePageViewModel)
                        {
                            page.Caption = LocalizationProviderHelper.GetLocalizedValue<string>("MoviesLabel");
                        }
                        else if (page is ShowPageViewModel)
                        {
                            page.Caption = LocalizationProviderHelper.GetLocalizedValue<string>("ShowsLabel");
                        }
                    }
                });

            Messenger.Default.Register<UnhandledExceptionMessage>(this, message => ManageException(message.Exception));

            Messenger.Default.Register<UpdateAvailableMessage>(this, message =>
            {
                UpdateAvailable = true;
            });

            Messenger.Default.Register<SearchCastMessage>(this, message =>
            {
                IsCastFlyoutOpen = true;
            });

            Messenger.Default.Register<DownloadMagnetLinkMessage>(this, async message =>
            {
                await HandleTorrentDownload(message.MagnetLink);
            });

            _castMediaMessage = Messenger.Default.RegisterAsyncMessage<ShowCastMediaMessage>(async message =>
            {
                var vm = new ChromecastDialogViewModel(message, _chromecastService);
                var castDialog = new CastDialog
                {
                    DataContext = vm
                };
                var cts = new TaskCompletionSource<object>();
                vm.OnCloseAction = async () =>
                {
                    try
                    {
                        var dialog = await _dialogCoordinator.GetCurrentDialogAsync<CastDialog>(
                            this);
                        if (dialog != null)
                            await _dialogCoordinator.HideMetroDialogAsync(this, dialog);
                        cts.TrySetResult(null);
                    }
                    catch (Exception ex)
                    {
                        cts.TrySetException(ex);
                    }
                };

                try
                {
                    await _dialogCoordinator.ShowMetroDialogAsync(this, castDialog);
                    await cts.Task;
                }
                catch (Exception ex)
                {
                    Logger.Error(ex);
                }
            });

            _showSubtitleDialogMessage = Messenger.Default.RegisterAsyncMessage<ShowSubtitleDialogMessage>(
                async message =>
                {
                    var vm = new SubtitleDialogViewModel(message.Subtitles, message.CurrentSubtitle);
                    var subtitleDialog = new SubtitleDialog
                    {
                        DataContext = vm
                    };

                    var cts = new TaskCompletionSource<object>();
                    vm.OnCloseAction = async () =>
                    {
                        try
                        {
                            message.SelectedSubtitle = vm.SelectedSubtitle?.Sub;
                            var dialog = await _dialogCoordinator.GetCurrentDialogAsync<SubtitleDialog>(
                                this);
                            if (dialog != null)
                                await _dialogCoordinator.HideMetroDialogAsync(this, dialog);
                            cts.TrySetResult(null);
                        }
                        catch (Exception ex)
                        {
                            cts.TrySetException(ex);
                        }
                    };

                    try
                    {
                        await _dialogCoordinator.ShowMetroDialogAsync(this, subtitleDialog);
                        await cts.Task;
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(ex);
                    }
                });

            _showDownloadSettingsMessage = Messenger.Default.RegisterAsyncMessage<ShowDownloadSettingsDialogMessage>(
                async message =>
                {
                    var vm = new DownloadSettingsDialogViewModel(message.Media, _subtitlesService);
                    var subtitleDialog = new DownloadSettingsDialog
                    {
                        DataContext = vm
                    };

                    var cts = new TaskCompletionSource<object>();
                    vm.OnCloseAction = async result =>
                    {
                        try
                        {
                            message.Download = result;
                            var dialog = await _dialogCoordinator.GetCurrentDialogAsync<DownloadSettingsDialog>(
                                this);
                            if (dialog != null)
                                await _dialogCoordinator.HideMetroDialogAsync(this, dialog);
                            cts.TrySetResult(null);
                        }
                        catch (Exception ex)
                        {
                            cts.TrySetException(ex);
                        }
                    };

                    try
                    {
                        await _dialogCoordinator.ShowMetroDialogAsync(this, subtitleDialog);
                        await cts.Task;
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(ex);
                    }
                });

            _customSubtitleMessage = Messenger.Default.RegisterAsyncMessage<ShowCustomSubtitleMessage>(
                async message =>
                {
                    var fileDialog = new OpenFileDialog
                    {
                        Title = "Open Sub File",
                        Filter = "SUB files (*.sub,*srt,*sbv)|*.sub;*.srt;*.sbv",
                        InitialDirectory = Path.GetPathRoot(Environment.SystemDirectory)
                    };

                    if (fileDialog.ShowDialog() == true)
                    {
                        try
                        {
                            message.FileName = fileDialog.FileName;
                            await Task.FromResult(message.FileName);
                        }
                        catch (Exception)
                        {
                            message.Error = true;
                        }
                    }
                });
        }

        /// <summary>
        /// Register commands
        /// </summary>
        private void RegisterCommands()
        {
            CloseMoviePageCommand = new RelayCommand(() =>
            {
                Messenger.Default.Send(new StopPlayingTrailerMessage(MediaType.Movie));
                IsMovieFlyoutOpen = false;
                if (NavigationService.CurrentSource.OriginalString != "Pages/HomePage.xaml" &&
                    NavigationService.CanGoBack)
                {
                    NavigationService.GoBack();
                }
            });

            CloseShowPageCommand = new RelayCommand(() =>
            {
                Messenger.Default.Send(new StopPlayingTrailerMessage(MediaType.Show));
                IsShowFlyoutOpen = false;
                if (NavigationService.CurrentSource.OriginalString != "Pages/HomePage.xaml" &&
                    NavigationService.CanGoBack)
                {
                    NavigationService.GoBack();
                }
            });

            MainWindowClosingCommand = new RelayCommand(async () =>
            {
                try
                {
                    FileHelper.ClearFolders();
                    await _userService.UpdateUser();
                    await SaveCacheOnExit();
                }
                catch (Exception ex)
                {
                    Logger.Error(ex);
                }
            });

            OpenSettingsCommand = new RelayCommand(() => IsSettingsFlyoutOpen = !IsSettingsFlyoutOpen);

            DropFileCommand = new RelayCommand<DragEventArgs>(async e =>
            {
                try
                {
                    if (e.Data.GetDataPresent(DataFormats.FileDrop))
                    {
                        var files = (string[]) e.Data.GetData(DataFormats.FileDrop);
                        var torrentFile = files?.FirstOrDefault(a => a.Contains("torrent"));
                        if (torrentFile != null)
                        {
                            var vm = new DropTorrentDialogViewModel(_cacheService, torrentFile);
                            var dropTorrentDialog = new DropTorrentDialog
                            {
                                DataContext = vm
                            };

                            Messenger.Default.Send(new DropFileMessage(DropFileMessage.DropFileEvent.Leave));
                            await _dialogCoordinator.ShowMetroDialogAsync(this, dropTorrentDialog);
                            var settings = SimpleIoc.Default.GetInstance<ApplicationSettingsViewModel>();
                            await vm.Download(settings.UploadLimit, settings.DownloadLimit,
                                async () =>
                                {
                                    try
                                    {
                                        var dialog =
                                            await _dialogCoordinator.GetCurrentDialogAsync<DropTorrentDialog>(
                                                this);
                                        if (dialog != null)
                                            await _dialogCoordinator.HideMetroDialogAsync(this, dialog);
                                    }
                                    catch (Exception ex)
                                    {
                                        Logger.Error(ex);
                                    }
                                }, async () =>
                                {
                                    try
                                    {
                                        var dialog =
                                            await _dialogCoordinator.GetCurrentDialogAsync<DropTorrentDialog>(
                                                this);
                                        if (dialog != null)
                                            await _dialogCoordinator.HideMetroDialogAsync(this, dialog);
                                    }
                                    catch (Exception ex)
                                    {
                                        Logger.Error(ex);
                                    }
                                });
                        }
                    }
                    else
                    {
                        Messenger.Default.Send(new DropFileMessage(DropFileMessage.DropFileEvent.Leave));
                        Messenger.Default.Send(
                            new UnhandledExceptionMessage(
                                new NoDataInDroppedFileException(
                                    LocalizationProviderHelper.GetLocalizedValue<string>("NoMediaInDroppedTorrent"))));
                    }
                }
                catch (Exception)
                {
                    Messenger.Default.Send(new DropFileMessage(DropFileMessage.DropFileEvent.Leave));
                    Messenger.Default.Send(
                        new UnhandledExceptionMessage(
                            new PopcornException(
                                LocalizationProviderHelper.GetLocalizedValue<string>("DroppedFileIssue"))));
                }
            });

            SwitchFullScreenCommand = new RelayCommand(() =>
            {
                ToggleFullscren = !ToggleFullscren;
                if (ApplicationService.IsFullScreen && !IgnoreTaskbarOnMaximize)
                    IgnoreTaskbarOnMaximize = true;
                else if (ApplicationService.IsFullScreen && IgnoreTaskbarOnMaximize)
                {
                    IgnoreTaskbarOnMaximize = false;
                    ApplicationService.IsFullScreen = false;
                }
                else if (!ApplicationService.IsFullScreen && IgnoreTaskbarOnMaximize)
                {
                    IgnoreTaskbarOnMaximize = false;
                }
                else
                {
                    IgnoreTaskbarOnMaximize = true;
                    ApplicationService.IsFullScreen = true;
                }
            });

            OpenAboutCommand = new RelayCommand(async () =>
            {
                var aboutDialog = new AboutDialog();
                var vm = new AboutDialogViewModel(async () =>
                {
                    try
                    {
                        var dialog = await _dialogCoordinator.GetCurrentDialogAsync<AboutDialog>(
                            this);
                        if (dialog != null)
                            await _dialogCoordinator.HideMetroDialogAsync(this, dialog);
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(ex);
                    }
                });

                aboutDialog.DataContext = vm;
                await _dialogCoordinator.ShowMetroDialogAsync(this, aboutDialog);
            });

            GoToGitHubCommand = new RelayCommand(() =>
            {
                Process.Start(new ProcessStartInfo("https://github.com/bbougot/Popcorn"));
            });

            DragEnterFileCommand = new RelayCommand<DragEventArgs>(e =>
            {
                Messenger.Default.Send(new DropFileMessage(DropFileMessage.DropFileEvent.Enter));
            });

            DragLeaveFileCommand = new RelayCommand<DragEventArgs>(e =>
            {
                Messenger.Default.Send(new DropFileMessage(DropFileMessage.DropFileEvent.Leave));
            });

            InitializeAsyncCommand = new RelayCommand(async () =>
            {
                var cmd = Environment.GetCommandLineArgs();
                if (cmd.Any())
                {
                    if (cmd.Contains("updated"))
                    {
                        OpenAboutCommand.Execute(null);
                    }
                    else if (cmd.Length == 2 && (cmd[1].StartsWith("magnet") || cmd[1].EndsWith("torrent")))
                    {
                        var path = cmd[1];
                        await HandleTorrentDownload(path);
                    }
                }
            });
        }

        private async Task HandleTorrentDownload(string path)
        {
            var filePath = string.Empty;
            if (path.StartsWith("magnet"))
            {
                filePath = $"{_cacheService.DropFilesDownloads}{Guid.NewGuid()}.torrent";
                using (var file = File.Create(filePath))
                using (var stream = new StreamWriter(file))
                {
                    await stream.WriteLineAsync(path);
                }
            }
            else if (path.EndsWith("torrent"))
            {
                filePath = path;
            }

            var vm = new DropTorrentDialogViewModel(_cacheService, filePath);
            var dropTorrentDialog = new DropTorrentDialog
            {
                DataContext = vm
            };

            try
            {
                await _dialogCoordinator.ShowMetroDialogAsync(this, dropTorrentDialog);
                var settings = SimpleIoc.Default.GetInstance<ApplicationSettingsViewModel>();
                await vm.Download(settings.UploadLimit, settings.DownloadLimit,
                    async () =>
                    {
                        var dialog = await _dialogCoordinator.GetCurrentDialogAsync<DropTorrentDialog>(
                            this);
                        if (dialog != null)
                            await _dialogCoordinator.HideMetroDialogAsync(this, dialog);
                    },
                    async () =>
                    {
                        var dialog = await _dialogCoordinator.GetCurrentDialogAsync<DropTorrentDialog>(
                            this);
                        if (dialog != null)
                            await _dialogCoordinator.HideMetroDialogAsync(this, dialog);
                    });
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
            }
        }

        public bool ToggleFullscren
        {
            get => _toggleFullscreen;
            set => Set(ref _toggleFullscreen, value);
        }

        /// <summary>
        /// Flush cache on disk
        /// </summary>
        private async Task SaveCacheOnExit()
        {
            await BlobCache.Shutdown();
        }

        /// <summary>
        /// Display a dialog on unhandled exception
        /// </summary>
        /// <param name="sender">Sender</param>
        /// <param name="e">Event args</param>
        private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
            {
                Logger.Fatal(ex);
            }
        }

        /// <summary>
        /// Manage an exception
        /// </summary>
        /// <param name="exception">The exception to manage</param>
        private void ManageException(Exception exception)
        {
            DispatcherHelper.CheckBeginInvokeOnUI(() =>
            {
                if (exception is WebException || exception is SocketException || exception is TimeoutRejectedException)
                {
                    _applicationService.IsConnectionInError = true;
                    _manager.CreateMessage()
                        .Accent("#E82C0C")
                        .Background("#333")
                        .HasBadge("Error")
                        .HasMessage(
                            LocalizationProviderHelper.GetLocalizedValue<string>("ConnectionErrorDescriptionPopup"))
                        .Dismiss().WithButton(LocalizationProviderHelper.GetLocalizedValue<string>("Ignore"),
                            button => { })
                        .Queue();
                }
                else if (exception is TrailerNotAvailableException)
                {
                    _manager.CreateMessage()
                        .Accent("#E0A030")
                        .Background("#333")
                        .HasBadge("Warning")
                        .HasMessage(exception.Message)
                        .Dismiss().WithButton(LocalizationProviderHelper.GetLocalizedValue<string>("Dismiss"),
                            button => { })
                        .Queue();
                }
                else if (exception is NoDataInDroppedFileException)
                {
                    _manager.CreateMessage()
                        .Accent("#E0A030")
                        .Background("#333")
                        .HasBadge("Warning")
                        .HasMessage(exception.Message)
                        .Dismiss().WithButton(LocalizationProviderHelper.GetLocalizedValue<string>("Dismiss"),
                            button => { })
                        .Queue();
                }
                else if (exception is PopcornException)
                {
                    _manager.CreateMessage()
                        .Accent("#E82C0C")
                        .Background("#333")
                        .HasBadge("Error")
                        .HasMessage(exception.Message)
                        .Dismiss().WithButton(LocalizationProviderHelper.GetLocalizedValue<string>("Ignore"),
                            button => { })
                        .Queue();
                }
                else
                {
                    Logger.Fatal(exception);
                }
            });
        }
    }
}