﻿using System;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Diagnostics;
using System.Threading;
using System.IO;
using Discord;

using AnimeChanger.Ani;

namespace AnimeChanger
{
    public partial class MainForm : Form, ILogin
    {
        /// <summary>
        /// List of supported browsers.
        /// </summary>
        public Browser[] SupportedBrowsers =
        {
            new Browser { ProcessName = "chrome", RemoveBrowserTitles = new string[] { " - Google Chrome" } },
            new Browser { ProcessName = "firefox", RemoveBrowserTitles = new string[] { " - Mozilla Firefox", " - Firefox Developer Edition" } },
            new Browser { ProcessName = "waterfox", RemoveBrowserTitles = new string[] { " - Waterfox" } },
        };

        /// <summary>
        /// Last found title
        /// </summary>
        string lastTitle = null;

        /// <summary>
        /// An array of global filters.
        /// </summary>
        public Filter[] GlobalFilters;

        /// <summary>
        /// An array of website filters.
        /// </summary>
        public Website[] WebCache2;

        /// <summary>
        /// Used for disconnecting from main thread.
        /// </summary>
        internal DiscordClient Client;

        private byte RetryInt = 0;


        public MainForm()
        {
            InitializeComponent();

            Misc.CheckFolder();

            GlobalFilters = XML.GetGlobalFilters();
            WebCache2 = XML.GetWebsiteFilters();
        }

        #region Discord.Net
        /// <summary>
        /// Starts Discord.DiscordClient, logs in and starts the check loop.
        /// </summary>
        public void StartClient(Secrets secrets)
        {
            Secrets sec = secrets;
            Thread DiscordThread = new Thread(() =>
            {
                System.Timers.Timer CheckTimer = new System.Timers.Timer(7500);

                Client = new DiscordClient();

                Client.Ready += (s, e) =>
                {
                    ChangeStatusLabel("Logged in");
                    CheckTimer.Elapsed += (s1, e1) => TimerCheck();
                    CheckTimer.Start();
                };

                try
                {
                    Client.ExecuteAndWait(async () =>
                    {
                        await Client.Connect(sec.email, sec.password);
                        TimerCheck();

                        LoginBtn.Invoke((MethodInvoker)delegate() {
                            LoginBtn.Enabled = false;
                        });
                    });
                }
                catch (Discord.Net.HttpException ex)
                {
                    MessageBox.Show(string.Format("(Error: {0})\nCouldn't connect to Discord. Make sure you enter the correct email and password.", ex.GetType().ToString()), "Error");
                    if (File.Exists(Path.Combine(Misc.FolderPath, "secrets.xml"))) 
                    {
                        File.Delete(Path.Combine(Misc.FolderPath, "secrets.xml"));
                    }
                    RetryLogin();
                }

                return; // This should only run after Client.ExecuteAndWait fails/ends
            });
            DiscordThread.Name = "Spaghetti";
            DiscordThread.Start();
        }
        #endregion

        #region Browser handling
        #region Process stuff
        /// <summary>
        /// Returns one single process of (first) keyword match found.
        /// </summary>
        /// <param name="Processes">System.Tuple of Browser and Process, browser processes currently running.</param>
        /// <returns>System.Tuple of Browser, Process, Website; Everything you can gather from scraping a thread.</returns>
        public Tuple<Browser, Process, Website> GetKeywordProcess(Tuple<Browser, Process>[] Processes)
        {
            foreach (var pair in Processes)
            {
                foreach (var w in WebCache2)
                {
                    if (w.Blacklist != null)
                        if (pair.Item2.MainWindowTitle.ToLower().Contains(w.Blacklist.ToLower()))
                            continue;

                    if (pair.Item2.MainWindowTitle.ToLower().Contains(w.Keyword))
                        return new Tuple<Browser, Process, Website>(pair.Item1, pair.Item2, w);
                }
            }
            return null;
        } 

        /// <summary>
        /// Gets browser processes running currently on the system
        /// </summary>
        /// <returns>An array of System.Tuple of Browser and Process, every browser process running on the system</returns>
        public Tuple<Browser, Process>[] GetBrowserProcesses()
        {
            Process[] processes = Process.GetProcesses();

            List<Tuple<Browser, Process>> ret = new List<Tuple<Browser, Process>>();

            foreach (var b in SupportedBrowsers)
            {
                foreach (var p in processes)
                {
                    if (p.ProcessName.Contains(b.ProcessName))
                        ret.Add(new Tuple<Browser, Process>(b, p));
                }
            }

            if (ret.Count != 0)
                return ret.ToArray();
            else
                return null;
        }
        #endregion

        /// <summary>
        /// Removes everything unnecessary from browser window title
        /// </summary>
        /// <param name="fullTitle">Full title of the browser window</param>
        /// <param name="usedBrowser">AnimeChanger.Browser, browser thread where the title was gathered</param>
        /// <param name="usedSite">AnimeChanger.Website, website where the anime is being watched</param>
        /// <returns>System.String, parsed string</returns>
        public string RemoveWebString(string fullTitle, Browser usedBrowser, Website usedSite)
        {
            var retString = fullTitle;
            foreach (string s in usedBrowser.RemoveBrowserTitles)
            {
                retString = retString.Replace(s, "");
            }

            if (GlobalFilters != null)
            {
                foreach (Filter filter in GlobalFilters)
                {
                    if (filter.Blacklist != null)
                        if (fullTitle.ToLower().Contains(filter.Blacklist.ToLower()))
                            continue;

                    if (filter.Keyword != null)
                    {
                        if (fullTitle.ToLower().Contains(filter.Keyword.ToLower()))
                        {
                            retString = filter.Parse(retString);
                        }
                    }
                    else
                        retString = filter.Parse(retString);
                }
            }

            foreach (var filter in usedSite.Filters)
            {
                if (filter.Blacklist != null)
                    if (fullTitle.ToLower().Contains(filter.Blacklist.ToLower()))
                        continue;

                if (filter.Keyword != null)
                {
                    if (fullTitle.ToLower().Contains(filter.Keyword.ToLower()))
                    {
                        retString = filter.Parse(retString);
                    }
                }
                else
                    retString = filter.Parse(retString);
            }

            retString = retString.Remove(retString.IndexOf("╚"), retString.LastIndexOf("╚") - retString.IndexOf("╚") + 1);

            return retString;
        }

        /// <summary>
        /// Passes login information from LoginForm to DiscordThread.
        /// </summary>
        /// <param name="sec">AnimeChanger.Secrets, login information.</param>
        public void PassSecrets(Secrets sec)
        {
            StartClient(sec);
        }
        #endregion

        #region Other
        /// <summary>
        /// Main timer loop
        /// </summary>
        /// <param name="client">Discord.DiscordClient, used to assign the status</param>
        internal void TimerCheck()
        {
            var BrowserProcesses = GetBrowserProcesses();

            if (BrowserProcesses == null)
                return;

            var rightProcess = GetKeywordProcess(BrowserProcesses);

            if (rightProcess == null)
            {
                if (RetryInt >= 3)
                {
                    if (lastTitle != null)
                    {
                        ChangeTextboxText("");
                        Client.SetGame(null);
                        lastTitle = null;
                    }

                    RetryInt = 3;
                }

                RetryInt++;
                return;
            }
            else
            {
                RetryInt = 0;
            }

            var title = RemoveWebString(rightProcess.Item2.MainWindowTitle, rightProcess.Item1, rightProcess.Item3);

            if (title == null)
                return;

            if (title != lastTitle)
            {
                Client.SetGame(new Game(title));
                ChangeTextboxText(title);
            }

            lastTitle = title;
        }
        #endregion

        #region Cross Thread Talking
        /// <summary>
        /// Changes the text on UI's TextBox.
        /// </summary>
        /// <param name="text">Text that is set.</param>
        internal void ChangeTextboxText(string text)
        {
            TitleBox.Invoke((MethodInvoker)delegate {
                TitleBox.Text = text;
            });
        }

        /// <summary>
        /// Changes the text on UI's Status label.
        /// </summary>
        /// <param name="text">Text that is set.</param>
        internal void ChangeStatusLabel(string text)
        {
            StatusLabel.Invoke((MethodInvoker)delegate {
                StatusLabel.Text = text;
            });
        }

        internal void RetryLogin()
        {
            Invoke((MethodInvoker)delegate
            {
                LoginBtn.Text = "Please wait";
                LoginBtn.Enabled = false;
                System.Timers.Timer stop_police = new System.Timers.Timer(5000);
                stop_police.AutoReset = false;
                stop_police.Elapsed += (delegate {
                    LoginBtn.Invoke((MethodInvoker)delegate {
                        LoginBtn.Text = "Log in";
                        LoginBtn.Enabled = true;
                        LoginBtn_Click(this, new EventArgs());

                        stop_police.Stop();
                        stop_police.Dispose();
                    });
                });
                stop_police.Start();
            });
        }
        #endregion

        #region Events
        private void RefreshBtn_Click(object sender, EventArgs e)
        {
            WebCache2 = null;
            WebCache2 = XML.GetWebsiteFilters();

            GlobalFilters = null;
            GlobalFilters = XML.GetGlobalFilters();
        }

        private void LoginBtn_Click(object sender, EventArgs e)
        {
            if (!File.Exists(Path.Combine(Misc.FolderPath, "secrets.xml")))
            {            
                LoginForm login = new LoginForm(this);
                login.Show();
            }
            else
            {
                Secrets secrets = Misc.ReadSecrets();
                if (secrets != null)
                    StartClient(secrets);
                else
                {
                    File.Delete(Path.Combine(Misc.FolderPath, "secrets.xml"));
                    RetryLogin();
                }
            }
        }

        private void Client_Closing(object sender, FormClosingEventArgs e)
        {
            if (Client != null)
            {
                Client.SetGame(null);
                Client.Disconnect();
            }
        }
        #endregion
    }
}
