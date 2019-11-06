using System;
using System.IO;
using System.Net;
using System.Reflection;
using System.Linq;
using System.Windows.Forms;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using System.Globalization;

namespace SpotifyAdsBlocker
{
    public partial class Main : Form
    {
        private bool muted = false;
        private string lastMessage = "";
        private ToolTip artistTooltip = new ToolTip();

        private readonly string spotifyPath = Environment.GetEnvironmentVariable("APPDATA") + @"\Spotify\spotify.exe";
        private readonly string volumeMixerPath = Environment.GetEnvironmentVariable("WINDIR") + @"\System32\SndVol.exe";
        private readonly string hostsPath = Environment.GetEnvironmentVariable("WINDIR") + @"\System32\drivers\etc\hosts";

        private readonly string[] adHosts = { "pubads.g.doubleclick.net", "securepubads.g.doubleclick.net", "www.googletagservices.com", "gads.pubmatic.com", "ads.pubmatic.com", "tpc.googlesyndication.com", "pagead2.googlesyndication.com", "googleads.g.doubleclick.net" };

        public const string website = @"https://www.ericzhang.me/projects/spotify-ad-blocker-SpotifyAdsBlocker/";

        private Analytics a;
        private DateTime lastRequest;
        private string lastAction = "";
        private SpotifyPatcher patcher;
        private Listener listener;
        private SpotifyHook hook;

        public Main()
        {
            Thread.CurrentThread.CurrentUICulture = Thread.CurrentThread.CurrentCulture;
            InitializeComponent();
        }

        /**
         * Contains the logic for when to mute Spotify
         **/
        private void MainTimer_Tick(object sender, EventArgs e)
        {
            try {
                if (hook.IsRunning())
                {
                    if (hook.IsAdPlaying())
                    {
                        if (MainTimer.Interval != 1000) MainTimer.Interval = 1000;
                        if (!muted) Mute(true);
                        if (!hook.IsPlaying())
                        {
                            AudioUtils.SendNextTrack(hook.Handle == IntPtr.Zero ? Handle : hook.Handle);
                            Thread.Sleep(500);
                        }

                        string artist = hook.GetArtist();
                        string message = Properties.strings.StatusMuting + " " + Truncate(artist);
                        if (lastMessage != message)
                        {
                            lastMessage = message;
                            StatusLabel.Text = message;
                            artistTooltip.SetToolTip(StatusLabel, artist);
                            LogAction("/mute/" + artist);
                        }
                    }
                    else if (hook.IsPlaying() && !hook.WindowName.Equals("Spotify")) // Normal music
                    {
                        if (muted)
                        {
                            Thread.Sleep(500); // Give extra time for ad to change out
                            Mute(false);
                        }
                        if (MainTimer.Interval != 400) MainTimer.Interval = 400;

                        string artist = hook.GetArtist();
                        string message = Properties.strings.StatusPlaying + " " + Truncate(artist);
                        if (lastMessage != message)
                        {
                            lastMessage = message;
                            StatusLabel.Text = message;
                            artistTooltip.SetToolTip(StatusLabel, artist);
                            LogAction("/play/" + artist);
                        }
                    }
                    else if (hook.WindowName.Equals("Spotify"))
                    {
                        string message = Properties.strings.StatusPaused;
                        if (lastMessage != message)
                        {
                            lastMessage = message;
                            StatusLabel.Text = message;
                            artistTooltip.SetToolTip(StatusLabel, "");
                        }
                    }
                }
                else
                {
                    if (MainTimer.Interval != 1000) MainTimer.Interval = 1000;
                    string message = Properties.strings.StatusNotFound;
                    if (lastMessage != message)
                    {
                        lastMessage = message;
                        StatusLabel.Text = message;
                        artistTooltip.SetToolTip(StatusLabel, "");
                    };
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }

            string aktifdil = textBox1.Text;
            label1.Text = aktifdil;

            switch (aktifdil)
            {
                case "Türkçe":
                    VolumeMixerButton.Text = "Ses Kontrolünü Aç";
                    BlockBannersCheckbox.Text = "Banner Reklamlarını Engelle";
                    undoPatchToolStripMenuItem.Text = "Oturum Açarken Spotify Ads Blocker'ı Başlat";
                    SpotifyCheckbox.Text = "Spotify Ads Blocker ile Spotify'ı başlatın";
                    // StatusLabel
                    if (StatusLabel.Text == "Loading..."){
                        StatusLabel.Text = "Yükleniyor...";
                    }
                    else if (StatusLabel.Text == "Spotify is paused"){
                        StatusLabel.Text = "Spotify duraklatıldı";
                    }
                    else if (StatusLabel.Text == "Spotify not found"){
                        StatusLabel.Text = "Spotify bulunamadı";
                    }
                    openToolStripMenuItem.Text = "Spotify Reklam Engelleyici Penceresini Göster";
                    websiteToolStripMenuItem.Text = "Spotify Ads Blocker'ı Web Sitesini Açın";
                    undoPatchToolStripMenuItem.Text = "Spotify Ads Blocker'ı Spotify'dan kaldır";
                    exitToolStripMenuItem.Text = "Çıkış";
                    break;
                case "English":
                    VolumeMixerButton.Text = "Open Volume Control";
                    BlockBannersCheckbox.Text = "Block Banner Ads";
                    StartupCheckbox.Text = "Start Spotify Ads Blocker on Login";
                    SpotifyCheckbox.Text = "Start Spotify with Spotify Ads Blocker";
                    // StatusLabel
                    if (StatusLabel.Text == "Loading...")
                    {
                        StatusLabel.Text = "Loading...";
                    }
                    else if (StatusLabel.Text == "Spotify is paused")
                    {
                        StatusLabel.Text = "Spotify is paused";
                    }
                    else if (StatusLabel.Text == "Spotify not found")
                    {
                        StatusLabel.Text = "Spotify not found";
                    }
                    openToolStripMenuItem.Text = "Show SpotifyAdsBlocker Window";
                    websiteToolStripMenuItem.Text = "Open SpotifyAdsBlocker Website";
                    undoPatchToolStripMenuItem.Text = "Remove SpotifyAdsBlocker from Spotify";
                    exitToolStripMenuItem.Text = "Exit";
                    break;
                case "Русские":
                    VolumeMixerButton.Text = "Открыть регулятор громкости";
                    BlockBannersCheckbox.Text = "Блокировка баннерной рекламы";
                    StartupCheckbox.Text = "Запустить Spotify Ads Blocker при входе в систему";
                    SpotifyCheckbox.Text = "Начните Spotify с блокировщика рекламы Spotify";
                    // StatusLabel
                    if (StatusLabel.Text == "Loading...")
                    {
                        StatusLabel.Text = "загрузка...";
                    }
                    else if (StatusLabel.Text == "Spotify is paused")
                    {
                        StatusLabel.Text = "Spotify приостановлен";
                    }
                    else if (StatusLabel.Text == "Spotify not found")
                    {
                        StatusLabel.Text = "Spotify не найден";
                    }
                    openToolStripMenuItem.Text = "Показать окно блокировщика рекламы Spotify";
                    websiteToolStripMenuItem.Text = "Откройте сайт SpotifyAdsBlocker";
                    undoPatchToolStripMenuItem.Text = "Удалить SpotifyAdsBlocker из Spotify";
                    exitToolStripMenuItem.Text = "Выход";
                    break;

            }

        }
       
        /**
         * Mutes/Unmutes Spotify.
         
         * i: false = unmute, true = mute
         **/
        private void Mute(bool mute)
        {
            AudioUtils.SetMute(hook.VolumeControl.Control, mute);
            muted = AudioUtils.IsMuted(hook.VolumeControl.Control) != null ? (bool)AudioUtils.IsMuted(hook.VolumeControl.Control) : false;
        }

        private string Truncate(string name)
        {
            if (name.Length > 10)
            {
                return name.Substring(0, 10) + "...";
            }
            return name;
        }

        /**
         * Checks if the current installation is the latest version. Prompts user if not.
         **/
        private void CheckUpdate()
        {
            try
            {
                WebClient w = new WebClient();
                w.Headers.Add("user-agent", "SpotifyAdsBlocker " + Assembly.GetExecutingAssembly().GetName().Version.ToString() + " " + System.Environment.OSVersion);
                string s = w.DownloadString("https://www.ericzhang.me/dl/?file=SpotifyAdsBlocker-version.txt");
                int latest = Convert.ToInt32(s);
                int current = Convert.ToInt32(Assembly.GetExecutingAssembly().GetName().Version.ToString().Replace(".", ""));
                if (latest <= current)
                    return;
                if (MessageBox.Show(Properties.strings.UpgradeMessageBox, "SpotifyAdsBlocker", MessageBoxButtons.YesNo) == DialogResult.Yes)
                {
                    Process.Start(website);
                    Application.Exit();
                }
            }
            catch (Exception)
            {
               /* Update
                * MessageBox.Show(Properties.strings.UpgradeErrorMessageBox, "SpotifyAdsBlocker");
                * */
            }
        }

        private void LogAction(string action)
        {
            if (lastAction.Equals(action) && DateTime.Now - lastRequest < TimeSpan.FromMinutes(5)) return;
            Task.Run(() => a.LogAction(action));
            lastAction = action;
            lastRequest = DateTime.Now;
        }

        /**
         * Send a request every 5 minutes to keep session alive
         **/
        private void Heartbeat_Tick(object sender, EventArgs e)
        {
            if (DateTime.Now - lastRequest > TimeSpan.FromMinutes(5))
            {
                LogAction("/heartbeat");
            }
        }

        string AnaSeçiliDil;

        string TR = "Türkçe";
        string En_US = "English";
        string RU = "Русские";

        
        // Dil Ekle
        private void Dil_Ekle(string hangidil){
            var dil = ComboBox_Dil.Items;
            dil.Add(hangidil);
        }



        private void Main_Load(object sender, EventArgs e)
        {
            StartupCheckbox.Checked = true;
            BlockBannersCheckbox.Checked = true;

            /* yazma
            StreamWriter SW = new StreamWriter(Application.StartupPath + @"\language.txt");
            SW.WriteLine(textBox1.Text);
            SW.Close();
            */

            // Dil Okuma
            string dildosyasivarmı = AppDomain.CurrentDomain.BaseDirectory.ToString() + @"\language";

            textBox1.Visible = false;
            if(File.Exists(dildosyasivarmı) == true){
            StreamReader SR = new StreamReader(Application.StartupPath + @"\language");
            textBox1.Text = SR.ReadLine();
            SR.Close();
            }
            else{ /* dilexe çıkarma ve açma yakında ekle */
                StreamWriter SW = new StreamWriter(Application.StartupPath + @"\language");
                SW.WriteLine("Türkçe");
                SW.Close();

                IsMdiContainer = true;
                    Main yeniform = new Main();
                    yeniform.MdiParent = this;
                    yeniform.Show();
            }


            Dil_Ekle(TR);
            Dil_Ekle(En_US);
            Dil_Ekle(RU);
            ComboBox_Dil.Visible = false;



            if (Properties.Settings.Default.UpdateSettings) // If true, then first launch of latest SpotifyAdsBlocker
            {
                Properties.Settings.Default.Upgrade();
                Properties.Settings.Default.UpdateSettings = false;
                Properties.Settings.Default.Save();
            }

            // Start Spotify and give SpotifyAdsBlocker higher priority
            try
            {
                if (Properties.Settings.Default.StartSpotify && File.Exists(spotifyPath) && Process.GetProcessesByName("spotify").Length < 1)
                {
                    Process.Start(spotifyPath);
                }
                Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.High; // Windows throttles down when minimized to task tray, so make sure SpotifyAdsBlocker runs smoothly
            }
            catch (Exception) {}

            // Set up UI
            if (File.Exists(hostsPath))
            {
                string hostsFile = File.ReadAllText(hostsPath);
                BlockBannersCheckbox.Checked = adHosts.All(host => hostsFile.Contains("0.0.0.0 " + host));
            }
            RegistryKey startupKey = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
            if (startupKey.GetValue("SpotifyAdsBlocker") != null)
            {
                if (startupKey.GetValue("SpotifyAdsBlocker").ToString() == "\"" + Application.ExecutablePath + "\"")
                {
                    StartupCheckbox.Checked = true;
                    this.WindowState = FormWindowState.Minimized;
                }
                else // Reg value exists, but not in right path
                {
                    startupKey.DeleteValue("SpotifyAdsBlocker");
                }
            }
            SpotifyCheckbox.Checked = Properties.Settings.Default.StartSpotify;
            
            // Set up Analytics
            if (String.IsNullOrEmpty(Properties.Settings.Default.CID))
            {
                Properties.Settings.Default.CID = Analytics.GenerateCID();
                Properties.Settings.Default.Save();
            }
            a = new Analytics(Properties.Settings.Default.CID, Assembly.GetExecutingAssembly().GetName().Version.ToString());

            // Start Spotify hook
            hook = new SpotifyHook();

            /* Start SpotifyAdsBlocker listener
            listener = new Listener();
            Task.Run(() => listener.Listen()); */

            MainTimer.Enabled = true;

            LogAction("/launch");

            Task.Run(() => CheckUpdate());
        }

        private void CheckPatch(bool launch)
        {
            string currentVersion = FileVersionInfo.GetVersionInfo(spotifyPath).FileVersion;
            if (!Properties.Settings.Default.LastPatched.Equals(currentVersion) || launch) // Always attempt to patch on launch
            {
                // MessageBox.Show("SpotifyAdsBlocker needs to modify Spotify.\r\n\r\nTo return to the original, right click the SpotifyAdsBlocker icon in your task tray and choose 'Remove Patch'.", "SpotifyAdsBlocker");
                if (!patcher.Patch())
                {
                    MessageBox.Show(Properties.strings.PatchErrorMessageBox, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                else
                {
                    Properties.Settings.Default.LastPatched = currentVersion;
                    Properties.Settings.Default.Save();
                }
            }
        }

        private void RestoreFromTray()
        {
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.WindowState = FormWindowState.Normal;
            this.ShowInTaskbar = true;
        }
        
        private void Notify(String message)
        {
            NotifyIcon.ShowBalloonTip(5000, "SpotifyAdsBlocker", message, ToolTipIcon.None);
        }

        private void NotifyIcon_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (!this.ShowInTaskbar && e.Button == MouseButtons.Left)
            {
                RestoreFromTray();
            }
        }

        private void NotifyIcon_BalloonTipClicked(object sender, EventArgs e)
        {
            RestoreFromTray();
        }

        private void Form_Resize(object sender, EventArgs e)
        {
            if (this.WindowState == FormWindowState.Minimized)
            {
                this.ShowInTaskbar = false;
                this.FormBorderStyle = FormBorderStyle.FixedToolWindow;


                string aktifdil = textBox1.Text;
                label1.Text = aktifdil;
                switch (aktifdil)
                {
                    case "Türkçe":
                        Notify(Properties.strings.HiddenNotifyTr);
                        break;
                    case "English":
                        Notify(Properties.strings.HiddenNotify);
                        break;
                    case "Русские":
                        Notify(Properties.strings.HiddenNotifyRu);
                        break;
                }
            }
        }

        private void SkipAdsCheckbox_Click(object sender, EventArgs e)
        {
            if (!MainTimer.Enabled) return; // Still setting up UI
            if (!IsUserAnAdmin())
            {
                MessageBox.Show(Properties.strings.BlockBannersUAC, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                BlockBannersCheckbox.Checked = !BlockBannersCheckbox.Checked;
                return;
            }
            try
            {
                if (!File.Exists(hostsPath))
                {
                    File.Create(hostsPath).Close();
                }
                // Always clear hosts
                string[] text = File.ReadAllLines(hostsPath);
                text = text.Where(line => !adHosts.Contains(line.Replace("0.0.0.0 ", "")) && line.Length > 0).ToArray();
                File.WriteAllLines(hostsPath, text);

                if (BlockBannersCheckbox.Checked)
                {
                    using (StreamWriter sw = File.AppendText(hostsPath))
                    {
                        sw.WriteLine();
                        foreach (string host in adHosts)
                        {
                            sw.WriteLine("0.0.0.0 " + host);
                        }
                    }
                }
                MessageBox.Show(Properties.strings.BlockBannersRestart, "SpotifyAdsBlocker", MessageBoxButtons.OK, MessageBoxIcon.Information);
                LogAction("/settings/blockBanners/" + BlockBannersCheckbox.Checked.ToString());
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }
        }

        private void StartupCheckbox_CheckedChanged(object sender, EventArgs e)
        {
            if (!MainTimer.Enabled) return; // Still setting up UI
            RegistryKey startupKey = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
            if (StartupCheckbox.Checked)
            {
                startupKey.SetValue("SpotifyAdsBlocker", "\"" + Application.ExecutablePath + "\"");
            }
            else
            {
                startupKey.DeleteValue("SpotifyAdsBlocker");
            }
            LogAction("/settings/startup/" + StartupCheckbox.Checked.ToString());
        }


        private void SpotifyCheckbox_CheckedChanged(object sender, EventArgs e)
        {
            if (!MainTimer.Enabled) return; // Still setting up UI
            Properties.Settings.Default.StartSpotify = SpotifyCheckbox.Checked;
            Properties.Settings.Default.Save();
            LogAction("/settings/startSpotify/" + SpotifyCheckbox.Checked.ToString());
        }

        private void VolumeMixerButton_Click(object sender, EventArgs e)
        {
            try
            {
                Process.Start(volumeMixerPath);
                LogAction("/button/volumeMixer");
            }
            catch (Exception)
            {
                MessageBox.Show(Properties.strings.VolumeMixerOpenError, "SpotifyAdsBlocker");
            }
        }

        private void WebsiteLink_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            MessageBox.Show(Properties.strings.ReportProblemMessageBox.Replace("{0}", Assembly.GetExecutingAssembly().GetName().Version.ToString()).Replace("{1}", FileVersionInfo.GetVersionInfo(spotifyPath).FileVersion), "SpotifyAdsBlocker");
            Clipboard.SetText(Properties.strings.ReportProblemClipboard.Replace("{0}", Assembly.GetExecutingAssembly().GetName().Version.ToString()).Replace("{1}", FileVersionInfo.GetVersionInfo(spotifyPath).FileVersion));
            Process.Start(website);
            LogAction("/button/website");
        }

        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            RestoreFromTray();
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void websiteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Process.Start(website);
        }

        private void undoPatchToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Properties.Settings.Default.LastPatched = "";
            Properties.Settings.Default.Save();

            if (patcher.Restore())
            {
                MessageBox.Show(Properties.strings.UndoPatchOKMessageBox, "SpotifyAdsBlocker");
            }
            else
            {
                MessageBox.Show(Properties.strings.UndoPatchFailMessageBox, "SpotifyAdsBlocker");
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!MainTimer.Enabled) return; // Still setting up UI
            if (!Properties.Settings.Default.UserEducated)
            {
                var result = MessageBox.Show(Properties.strings.OnExitMessageBox, "SpotifyAdsBlocker",
                                 MessageBoxButtons.YesNo,
                                 MessageBoxIcon.Warning);

                e.Cancel = (result == DialogResult.No);

                if (result == DialogResult.Yes)
                {
                    Properties.Settings.Default.UserEducated = true;
                    Properties.Settings.Default.Save();
                }
            }
        }

        [DllImport("shell32.dll")]
        public static extern bool IsUserAnAdmin();
    }
}
