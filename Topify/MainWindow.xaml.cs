using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Topify.Common;
using SpotifyAPI.Web;
using SpotifyAPI.Web.Auth;
using Newtonsoft.Json;
using static SpotifyAPI.Web.Scopes;
using System.Timers;
using System.Drawing;
using System.IO;
using Image = System.Drawing.Image;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Net;
using System.Windows.Threading;
using System.Windows.Forms;

namespace Topify
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private static SpotifyClient? spClient;
        private static readonly EmbedIOAuthServer _server = new(new("http://localhost:5000/callback"), 5000);
        public System.Timers.Timer statusTime;
        public System.Timers.Timer ensureTimer;
        public OffsetWatch ow;
        public int trackLength;
        public string spotifyUrl;
        public bool isResume = false;
        public int resumeFrom;
        public int seekedLength;
        public int lastKnownSeek;
        public int i = 0;
        
        public MainWindow()
        {
            InitializeComponent();
            InitializeWindowSettings();
        }
        
        public void InitializeWindowSettings()
        {
            AuthifyNotice.Visibility = Visibility.Visible;
            IntPtr hWnd = new WindowInteropHelper(this).EnsureHandle();
            var attribute = DWMWINDOWATTRIBUTE.DWMWA_WINDOW_CORNER_PREFERENCE;
            var preference = DWM_WINDOW_CORNER_PREFERENCE.DWMWCP_ROUND;
            DwmSetWindowAttribute(hWnd, attribute, ref preference, sizeof(uint));
            DragContainer.Visibility = Visibility.Hidden;
            this.Left = 25;
            this.Top = 25;
            GetClientOuath();
        }
        
        public async void GetClientOuath()
        {
            await StartAuthentication();
        }
        
        private static string? json;

        private async Task StartAuthentication()
        {
            var (verifier, challenge) = PKCEUtil.GenerateCodes();

            await _server.Start();
            _server.AuthorizationCodeReceived += async (sender, response) =>
            {
                await _server.Stop();
                PKCETokenResponse token = await new OAuthClient().RequestToken(
                  new PKCETokenRequest("2aa7adaf3dd745d2a5da9ebb12588afe", response.Code, _server.BaseUri, verifier)
                );

                json = JsonConvert.SerializeObject(token);
                await Start();
            };

            var request = new LoginRequest(_server.BaseUri, "2aa7adaf3dd745d2a5da9ebb12588afe", LoginRequest.ResponseType.Code)
            {
                CodeChallenge = challenge,
                CodeChallengeMethod = "S256",
                Scope = new List<string> { UserReadPlaybackState, UserModifyPlaybackState, UserReadPlaybackPosition }
            };

            Uri uri = request.ToUri();
            try
            {
                BrowserUtil.Open(uri);
            }
            catch (Exception)
            {
                Console.WriteLine("Unable to open URL, manually open: {0}", uri);
            }
        }
        
        private Task Start()
        {
            var _token = JsonConvert.DeserializeObject<PKCETokenResponse>(json!);
            var authenticator = new PKCEAuthenticator("2aa7adaf3dd745d2a5da9ebb12588afe", _token!);
            authenticator.TokenRefreshed += (sender, token) => token = _token!;
            var config = SpotifyClientConfig.CreateDefault()
        .WithAuthenticator(authenticator);
            spClient = new SpotifyClient(config);
            var me = spClient.UserProfile.Current();
            System.Windows.Application.Current.Dispatcher.Invoke(AccessAcquired, DispatcherPriority.SystemIdle);
            _server.Dispose();
            return Task.CompletedTask;
        }
        
        public async void AccessAcquired()
        {
            await RefreshDynamicContent();
            StartCheckPlaybackChanged();
            AnimationHandler.FadeAnimation(AuthifyNotice, 0.2, AuthifyNotice.Opacity, 0);
            await Task.Delay(205);
            AuthifyNotice.Visibility = Visibility.Hidden;
        }
        
        private async Task<Task> RefreshDynamicContent()
        {
            if (spClient!.Player.GetCurrentPlayback().Result.IsPlaying)
            {
                PlaybackPlay();
                CurrentlyPlaying track = await spClient!.Player.GetCurrentlyPlaying(new PlayerCurrentlyPlayingRequest { Market = "from_token" });
                FullTrack ft = (FullTrack)track.Item;
                trackLength = ft.DurationMs;
                seekedLength = spClient!.Player.GetCurrentPlayback().Result.ProgressMs;
                if (trackLength - seekedLength > 5000)
                {
                    isResume = true;
                    resumeFrom = seekedLength;
                }
                else if (trackLength - seekedLength < 5000)
                {
                    isResume = false;
                    resumeFrom = 0;
                }
                TickStart();
                StartStatusBarTimer();
                spotifyUrl = ft.Uri;
                NowPlayingName.Content = ft.Name;
                NowPlayingArtist.Content = ft.Artists[0].Name;
                WebClient wc = new();
                await wc.DownloadFileTaskAsync(ft.Album.Images[0].Url, "i.tmp");
                BitmapImage canvas = new();
                canvas.BeginInit();
                canvas.StreamSource = new MemoryStream(File.ReadAllBytes("i.tmp"));
                canvas.UriSource = new Uri(ft.Album.Images[0].Url);
                canvas.EndInit();
                File.Delete("i.tmp");
                Bitmap canvasAsBitmap = ConvertBitmapImageToBitmap(canvas);
                Bitmap resizedCanvas = ResizeBitmap(canvasAsBitmap, 96, 96);
                BitmapImage canvasResized = BitmapToBitmapImage(resizedCanvas);
                AlbumCanvas.ImageSource = canvasResized;
                SolidColorBrush dominantColor = new(GetDominantColor(ConvertBitmapImageToBitmap(canvas)));
                MainGrid.Background = dominantColor;
            }
            else
            {
                PlaybackPause();
            }
            return Task.CompletedTask;
        }
        
        public enum DWMWINDOWATTRIBUTE
        {
            DWMWA_WINDOW_CORNER_PREFERENCE = 33
        }

        public enum DWM_WINDOW_CORNER_PREFERENCE
        {
            DWMWCP_DEFAULT = 0,
            DWMWCP_DONOTROUND = 1,
            DWMWCP_ROUND = 2,
            DWMWCP_ROUNDSMALL = 3
        }
        
        [DllImport("dwmapi.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
        internal static extern void DwmSetWindowAttribute(IntPtr hwnd,
                                                         DWMWINDOWATTRIBUTE attribute,
                                                         ref DWM_WINDOW_CORNER_PREFERENCE pvAttribute,
                                                         uint cbAttribute);

        private void DragContainer_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.MouseDevice.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void PreviousSong_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            SkipBackGeometry.Brush = System.Windows.Media.Brushes.White;
            SkipBackDrawingImage.Opacity = 1;
        }

        private void PreviousSong_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            SkipBackDrawingImage.Opacity = 0.3;
            SkipBackGeometry.Brush = System.Windows.Media.Brushes.Black;
        }

        private async void PreviousSong_MouseUp(object sender, MouseButtonEventArgs e)
        {
            await spClient!.Player.SkipPrevious();
            NowPlayingProgress.Value = 0;
            await Task.Delay(200);
            await RefreshDynamicContent();
            TickStart();
            StartStatusBarTimer();
        }

        private void NextSong_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            SkipForwardGeometry.Brush = System.Windows.Media.Brushes.White;
            SkipForwardDrawingImage.Opacity = 1;
        }

        private void NextSong_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            SkipForwardDrawingImage.Opacity = 0.3;
            SkipForwardGeometry.Brush = System.Windows.Media.Brushes.Black;
        }

        private async void NextSong_MouseUp(object sender, MouseButtonEventArgs e)
        {
            await spClient!.Player.SkipNext();
            NowPlayingProgress.Value = 0;
            await Task.Delay(200);
            await RefreshDynamicContent();
            TickStart();
            StartStatusBarTimer();
        }

        public static Bitmap ResizeBitmap(Bitmap bmp, int width, int height)
        {
            Bitmap result = new(width, height);
            using (Graphics g = Graphics.FromImage(result))
            {
                g.DrawImage(bmp, 0, 0, width, height);
            }
            return result;
        }

        public static Bitmap ConvertBitmapImageToBitmap(BitmapImage bitmapImage)
        {
            using MemoryStream outStream = new();
            BitmapEncoder enc = new BmpBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(bitmapImage));
            enc.Save(outStream);
            Bitmap bitmap = new(outStream);

            return new Bitmap(bitmap);
        }

        public System.Windows.Media.Color GetDominantColor(Bitmap bmp)
        {
            BitmapData srcData = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

            int stride = srcData.Stride;

            IntPtr Scan0 = srcData.Scan0;

            int[] totals = new int[] { 0, 0, 0 };

            int width = bmp.Width;
            int height = bmp.Height;

            unsafe
            {
                byte* p = (byte*)(void*)Scan0;

                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        for (int color = 0; color < 3; color++)
                        {
                            int idx = (y * stride) + x * 4 + color;
                            totals[color] += p[idx];
                        }
                    }
                }
            }

            int avgB = totals[0] / (width * height);
            int avgG = totals[1] / (width * height);
            int avgR = totals[2] / (width * height);

            bmp.UnlockBits(srcData);
            return System.Windows.Media.Color.FromRgb(Convert.ToByte(avgR), Convert.ToByte(avgG), Convert.ToByte(avgB));
        }

        public static Bitmap ResizeImage(Image image, int width, int height)
        {
            var destRect = new Rectangle(0, 0, width, height);
            var destImage = new Bitmap(width, height);

            destImage.SetResolution(image.HorizontalResolution, image.VerticalResolution);

            using (var graphics = Graphics.FromImage(destImage))
            {
                graphics.CompositingMode = CompositingMode.SourceCopy;
                graphics.CompositingQuality = CompositingQuality.HighQuality;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = SmoothingMode.HighQuality;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

                using var wrapMode = new ImageAttributes();
                wrapMode.SetWrapMode(WrapMode.TileFlipXY);
                graphics.DrawImage(image, destRect, 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, wrapMode);
            }

            return destImage;
        }

        public static BitmapImage BitmapToBitmapImage(Bitmap bitmap)
        {
            using MemoryStream memory = new();
            bitmap.Save(memory, ImageFormat.Png);
            memory.Position = 0;
            BitmapImage bitmapImage = new();
            bitmapImage.BeginInit();
            bitmapImage.StreamSource = memory;
            bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
            bitmapImage.EndInit();
            return bitmapImage;
        }

        private void StartStatusBarTimer()
        {
            statusTime = new()
            {
                Interval = 2000
            };
            statusTime.Elapsed += new(StatusTimeElapsed);
            statusTime.Enabled = true;
        }
        private void StatusTimeElapsed(object sender, ElapsedEventArgs e)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(UpdateProgress, DispatcherPriority.SystemIdle);
        }
        
        private void UpdateProgress()
        {
            NowPlayingProgress.Value = ow.ElapsedTimeSpan.TotalMilliseconds / trackLength * 100;
            int seeked = spClient!.Player.GetCurrentPlayback().Result.ProgressMs;
            if (lastKnownSeek - seeked > 4000 || trackLength - seeked < 0)
            {
                ow = new OffsetWatch(TimeSpan.FromMilliseconds(seeked));
                ow.Start();
            }
            if (seeked > trackLength || seeked < 0 || seeked == lastKnownSeek)
            {
                ow.Stop();
                RefreshDynamicContent();
            }
            lastKnownSeek = seeked;
        }

        private void StartCheckPlaybackChanged()
        {
            ensureTimer = new()
            {
                Interval = 2500
            };
            ensureTimer.Elapsed += new(PlaybackchangedElapsed);
            ensureTimer.Enabled = true;
        }
        private async void PlaybackchangedElapsed(object? sender, ElapsedEventArgs e)
        {
            CurrentlyPlaying track = await spClient!.Player.GetCurrentlyPlaying(new PlayerCurrentlyPlayingRequest { Market = "from_token" });
            FullTrack ft = (FullTrack)track.Item;
            bool isPlaying = spClient!.Player.GetCurrentPlayback().Result.IsPlaying;
            if (ft.Uri == null || ft.Uri != spotifyUrl)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(RefreshDynamicContent, DispatcherPriority.SystemIdle);
            }
            if (isPlaying)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(PlaybackPlay, DispatcherPriority.SystemIdle);
            }
            else if (!isPlaying)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(PlaybackPause, DispatcherPriority.SystemIdle);
            }
        }

        private void PlaybackPause()
        {
            PlayButtonImage.Visibility = Visibility.Visible;
            PauseButtonImage.Visibility = Visibility.Hidden;
            PlayButtonCircle.Visibility = Visibility.Visible;
            PauseButtonCircle.Visibility = Visibility.Hidden;
            PlayButtonHandler.Visibility = Visibility.Visible;
            PauseButtonHandler.Visibility = Visibility.Hidden;
        }

        private void PlaybackPlay()
        {
            PlayButtonImage.Visibility = Visibility.Hidden;
            PauseButtonImage.Visibility = Visibility.Visible;
            PlayButtonCircle.Visibility = Visibility.Hidden;
            PauseButtonCircle.Visibility = Visibility.Visible;
            PlayButtonHandler.Visibility = Visibility.Hidden;
            PauseButtonHandler.Visibility = Visibility.Visible;
        }

        private void TickStart()
        {
            ow = new(TimeSpan.FromMilliseconds(resumeFrom));
            ow.Start();
        }
        
        private void Image_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            PlayButtonImage.Margin = new Thickness(332, 45, 13, 13);
            PauseButtonImage.Margin = new Thickness(332, 45, 13, 13);
        }

        private void Image_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            PlayButtonImage.Margin = new Thickness(333, 46, 14, 14);
            PauseButtonImage.Margin = new Thickness(333, 46, 14, 14);
        }

        public static string CutStart(string s, string what)
        {
            if (s.StartsWith(what))
                return s[what.Length..];
            else
                return s;
        }

        private void Image_MouseEnter_1(object sender, System.Windows.Input.MouseEventArgs e)
        {
            AnimationHandler.FadeIn(SpotifyIconHoverBorder, 0.3);
        }

        private void Image_MouseLeave_1(object sender, System.Windows.Input.MouseEventArgs e)
        {
            AnimationHandler.FadeOut(SpotifyIconHoverBorder, 0.3);
        }

        private void Image_MouseUp(object sender, MouseButtonEventArgs e)
        {
            BrowserUtil.Open(new Uri(spotifyUrl));
        }

        private async void Image_MouseUp_1(object sender, MouseButtonEventArgs e)
        {
            PlaybackPlay();
            int seeked = spClient!.Player.GetCurrentPlayback().Result.ProgressMs;
            lastKnownSeek = seeked;
            await spClient!.Player.ResumePlayback();
            ow = new(TimeSpan.FromMilliseconds(seeked));
            ow.Start();
            StartStatusBarTimer();
        }

        private async void PauseButtonCircle_MouseUp(object sender, MouseButtonEventArgs e)
        {
            PlaybackPause();
            await spClient!.Player.PausePlayback();
            statusTime.Enabled = false;
            statusTime = null;
            ow.Stop();
        }

        private void AlbumCoverMouseDown(object sender, MouseButtonEventArgs e)
        {
            WindowPositionOptionHost.Visibility = Visibility.Visible;
        }

        private void PlacementOption1_MouseUp(object sender, MouseButtonEventArgs e)
        {
            this.Top = 25;
            this.Left = 25;
            WindowPositionOptionHost.Visibility = Visibility.Hidden;
        }

        private void PlacementOption2_MouseUp(object sender, MouseButtonEventArgs e)
        {
            int scrHeight = Screen.PrimaryScreen.WorkingArea.Height;
            this.Top = scrHeight - 95;
            this.Left = 25;
            WindowPositionOptionHost.Visibility = Visibility.Hidden;
        }

        private void PlacementOption3_MouseUp(object sender, MouseButtonEventArgs e)
        {
            int scrWidth = Screen.PrimaryScreen.WorkingArea.Width;
            this.Top = 25;
            this.Left = scrWidth - 385;
            WindowPositionOptionHost.Visibility = Visibility.Hidden;
        }

        private void PlacementOption4_MouseUp(object sender, MouseButtonEventArgs e)
        {
            int scrHeight = Screen.PrimaryScreen.WorkingArea.Height;
            int scrWidth = Screen.PrimaryScreen.WorkingArea.Width;
            this.Top = scrHeight - 95;
            this.Left = scrWidth - 385;
            WindowPositionOptionHost.Visibility = Visibility.Hidden;
        }

        private void TrafficLightClose_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            TrafficLightCloseEllipse.Fill = (SolidColorBrush)new BrushConverter().ConvertFromString("#5e5e5e")!;
        }

        private void TrafficLightClose_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            TrafficLightCloseEllipse.Fill = (SolidColorBrush)new BrushConverter().ConvertFromString("#181818")!;
        }

        private void TrafficLightClose_MouseUp(object sender, MouseButtonEventArgs e)
        {
            System.Windows.Application.Current.Shutdown(1);
        }
    }
}
