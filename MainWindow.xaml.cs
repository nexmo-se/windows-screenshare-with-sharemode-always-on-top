using OpenTok;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Media;
using Point = System.Drawing.Point;

namespace ScreenSharing
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public const string API_KEY = "47464991";
        public const string SESSION_ID = "2_MX40NzQ2NDk5MX5-MTY2MjM0Nzk0NDExN35jYmFjbng4UG1lVGNZYVQrVGNHRStXdUt-fg";
        public const string TOKEN = "T1==cGFydG5lcl9pZD00NzQ2NDk5MSZzaWc9OTEzZTg5NDlmYzYzN2ZhMzFhMGVjMDM2NWFlNmVkNjU1YTdjZTc0YjpzZXNzaW9uX2lkPTJfTVg0ME56UTJORGs1TVg1LU1UWTJNak0wTnprME5ERXhOMzVqWW1GamJuZzRVRzFsVkdOWllWUXJWR05IUlN0WGRVdC1mZyZjcmVhdGVfdGltZT0xNjYyMzQ3OTU2Jm5vbmNlPTAuNDM4NzQ0ODMxMDMxMzg4ODQmcm9sZT1wdWJsaXNoZXImZXhwaXJlX3RpbWU9MTY2MjQzNDM1NiZpbml0aWFsX2xheW91dF9jbGFzc19saXN0PQ==";

        ScreenSharingCapturer Capturer;
        Session Session;
        Publisher Publisher;
        bool Disconnect = false;
        bool ShareModeOn = false;
        Dictionary<Stream, Subscriber> SubscriberByStream = new Dictionary<Stream, Subscriber>();

        //This will store the Current Dimensions and sizes before we go to sharing mode
        double CurrentWidth;
        double CurrentHeight;
        double[] CurrentPos = { 0, 0 };

        public MainWindow()
        {
            InitializeComponent();
            Capturer = new ScreenSharingCapturer();

            // We create the publisher here to show the preview when application starts
            // Please note that the PublisherVideo component is added in the xaml file
            Publisher = new Publisher.Builder(Context.Instance)
            {
                Renderer = PublisherVideo,
                Capturer = Capturer,
                HasAudioTrack = false
            }.Build();
            
            // We set the video source type to screen to disable the downscaling of the video
            // in low bandwidth situations, instead the frames per second will drop.
            Publisher.VideoSourceType = VideoSourceType.Screen;
            
            if (API_KEY == "" || SESSION_ID == "" || TOKEN == "")
            {
                System.Windows.MessageBox.Show("Please fill out the API_KEY, SESSION_ID and TOKEN variables in the source code " +
                    "in order to connect to the session", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                ConnectDisconnectButton.IsEnabled = false;
            }
            else
            {
                Session = new Session.Builder(Context.Instance, API_KEY, SESSION_ID).Build();

                Session.Connected += Session_Connected;
                Session.Disconnected += Session_Disconnected;
                Session.Error += Session_Error;
                Session.StreamReceived += Session_StreamReceived;
                Session.StreamDropped += Session_StreamDropped;
            }

            Closing += MainWindow_Closing;
        }


        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            //if we are on Sharing Mode, we don't close the window, but we exit Share Mode
            //Needed when Toolbars are present
            if (ShareModeOn)
            {
                this.Share_Mode_Toggle(sender, new System.Windows.RoutedEventArgs());
                e.Cancel = true;
            }
            else
            {
                foreach (var subscriber in SubscriberByStream.Values)
                {
                    subscriber.Dispose();
                }
                Publisher?.Dispose();
                Session?.Dispose();
            }
            
        }

        private void Session_Connected(object sender, EventArgs e)
        {
            try
            {
                Session.Publish(Publisher);
            }
            catch (OpenTokException ex)
            {
                Trace.WriteLine("OpenTokException " + ex.ToString());
            }
        }

        private void Session_Disconnected(object sender, EventArgs e)
        {
            Trace.WriteLine("Session disconnected");
            SubscriberByStream.Clear();
            SubscriberGrid.Children.Clear();
        }

        private void Session_Error(object sender, Session.ErrorEventArgs e)
        {
            System.Windows.MessageBox.Show("Session error:" + e.ErrorCode, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private void UpdateGridSize(int numberOfSubscribers)
        {
            int rows = Convert.ToInt32(Math.Round(Math.Sqrt(numberOfSubscribers)));
            int cols = rows == 0 ? 0 : Convert.ToInt32(Math.Ceiling(((double)numberOfSubscribers) / rows));
            SubscriberGrid.Columns = cols;
            SubscriberGrid.Rows = rows;
        }

        private void Session_StreamReceived(object sender, Session.StreamEventArgs e)
        {
            Trace.WriteLine("Session stream received");
            VideoRenderer renderer = new VideoRenderer();
            SubscriberGrid.Children.Add(renderer);
            UpdateGridSize(SubscriberGrid.Children.Count);
            Subscriber subscriber = new Subscriber.Builder(Context.Instance, e.Stream)
            {
                Renderer = renderer
            }.Build();
            SubscriberByStream.Add(e.Stream, subscriber);

            try
            {
                Session.Subscribe(subscriber);
            }
            catch (OpenTokException ex)
            {
                Trace.WriteLine("OpenTokException " + ex.ToString());
            }
        }

        private void Session_StreamDropped(object sender, Session.StreamEventArgs e)
        {
            Trace.WriteLine("Session stream dropped");
            var subscriber = SubscriberByStream[e.Stream];
            if (subscriber != null)
            {
                SubscriberByStream.Remove(e.Stream);
                try
                {
                    Session.Unsubscribe(subscriber);
                }
                catch (OpenTokException ex)
                {
                    Trace.WriteLine("OpenTokException " + ex.ToString());
                }

                SubscriberGrid.Children.Remove((UIElement)subscriber.VideoRenderer);
                UpdateGridSize(SubscriberGrid.Children.Count);
            }
        }

        private void Connect_Click(object sender, RoutedEventArgs e)
        {
            if (Disconnect)
            {
                Trace.WriteLine("Disconnecting session");
                try
                {
                    Session.Unpublish(Publisher);
                    Session.Disconnect();
                }
                catch (OpenTokException ex)
                {
                    Trace.WriteLine("OpenTokException " + ex.ToString());
                }
            }
            else
            {
                Trace.WriteLine("Connecting session");
                try
                {
                    Session.Connect(TOKEN);
                }
                catch (OpenTokException ex)
                {
                    Trace.WriteLine("OpenTokException " + ex.ToString());
                }
            }
            Disconnect = !Disconnect;
            ConnectDisconnectButton.Content = Disconnect ? "Disconnect" : "Connect";
        }

        private void Share_Mode_Toggle(object sender, RoutedEventArgs e)
        {
            if (ShareModeOn)
            {
                this.PublisherVideo.Visibility = Visibility.Visible;
                this.Topmost = false;
                this.Width = this.CurrentWidth;
                this.Height = this.CurrentHeight;
                this.Left = this.CurrentPos[0];
                this.Top = this.CurrentPos[1];
                this.ConnectDisconnectButton.Visibility = Visibility.Visible;
                this.ShareMode.Content = "Enter Share Mode";
                this.WindowStyle = WindowStyle.SingleBorderWindow;
                this.ResizeMode = ResizeMode.CanResize;
            }
            else
            {
                //store current dimensions and position
                this.CurrentHeight = this.Height;
                this.CurrentWidth = this.Width;
                this.CurrentPos = new double[2] {this.Left, this.Top};

                this.PublisherVideo.Visibility = Visibility.Hidden;
                this.Topmost = true;
                this.Width = 640;
                this.Height = 480;
                this.ConnectDisconnectButton.Visibility = Visibility.Hidden;
                this.ShareMode.Content = "Exit Share Mode";

                //Only Show ToolWindow
                this.WindowStyle = WindowStyle.ToolWindow;
                //Use WindowStyle None if you don't want it to be movable
                //this.WindowStyle = WindowStyle.None;

                //Set this window to be not resizeable
                this.ResizeMode = ResizeMode.NoResize;
                //Use This is you want the window to be resized
                //this.ResizeMode = ResizeMode.CanResize;
                
                var screen = Screen.FromPoint(new Point((int)this.Left, (int)this.Top));
                var dpiScale = VisualTreeHelper.GetDpi(this);
                Rect dpi_scaled_screen = new Rect { Width = screen.WorkingArea.Width / dpiScale.DpiScaleX, Height = screen.WorkingArea.Height / dpiScale.DpiScaleY };
                this.Left = dpi_scaled_screen.Right - this.Width;
                this.Top = dpi_scaled_screen.Bottom - this.Height;
            }
            ShareModeOn = !ShareModeOn;
            Trace.WriteLine(ShareModeOn);
        }
    }
}