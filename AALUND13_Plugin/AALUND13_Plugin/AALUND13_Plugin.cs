using NLog;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Controls;
using Torch;
using Torch.API;
using Torch.API.Managers;
using Torch.API.Plugins;
using Torch.API.Session;
using Torch.Session;
using System.Timers;
using VRage.Plugins;
using NLog.Fluent;
using System.Windows.Threading;
using System.Net;
using System.Xml;

namespace AALUND13_Plugin
{
    public class AALUND13_Plugin : TorchPluginBase, IWpfPlugin
    {

        public static readonly Logger Log = LogManager.GetCurrentClassLogger();

        private static readonly string CONFIG_FILE_NAME = "AALUND13_PluginConfig.cfg";

        private AALUND13_PluginControl _control;
        public UserControl GetControl() => _control ?? (_control = new AALUND13_PluginControl(this));

        private Persistent<AALUND13_PluginConfig> _config;
        public AALUND13_PluginConfig Config => _config?.Data;

        public int VoteRestartPlayerNeeded = 10000000;
        public int VoteModPlayerNeeded = 10000000;

        public Timer timer;
        public Timer restartTimer;

        public HashSet<ulong> voteParticipants = new HashSet<ulong>();
        public Timer voteTimer;

        public HashSet<ulong> modVoteParticipants = new HashSet<ulong>();
        public ulong modId = 0;
        public Timer modVoteTimer;

        public int timeToRestart = 5;
        public bool pluginIsUpToDate = true;

        public string GetVersionFromManifest(string manifestUrl)
        {
            string version = string.Empty;

            using (var webClient = new WebClient())
            {
                try
                {
                    string xmlString = webClient.DownloadString(manifestUrl);

                    var xmlDocument = new XmlDocument();
                    xmlDocument.LoadXml(xmlString);

                    XmlNode versionNode = xmlDocument.SelectSingleNode("/PluginManifest/Version");
                    if (versionNode != null)
                    {
                        version = versionNode.InnerText;
                    }
                }
                catch (Exception ex)
                {
                    // Handle any exceptions that occur during the download or parsing of the XML
                    Log.Error(ex.Message);
                }
            }

            return version;
        }

        private void StartRestartTimer()
        {
            restartTimer = new Timer(60000); // 60 second interval
            restartTimer.Elapsed += RestartTimerElapsed;
            restartTimer.Start();
        }

        private void RestartTimerElapsed(object sender, ElapsedEventArgs e)
        {
            timeToRestart--;
            if (timeToRestart < 1)
            {
                Torch.Restart();
            }
            Torch.CurrentSession?.Managers?.GetManager<IChatManagerServer>()?.SendMessageAsSelf($"Restarting in {timeToRestart} minutes");
            Log.Info($"Restarting in {timeToRestart} minutes");
        }

        private void StartTimer()
        {
            timer = new Timer(60000); // 10 second interval
            timer.Elapsed += TimerElapsed;
            timer.Start();
        }

        public static bool downloadLatestVersionPlugin(string path, string fileName)
        {
            string url = "https://raw.githubusercontent.com/AALUND13/AALUND13_Plugin/master/Build/AALUND13_Plugin.zip";
            string savePath = Path.Combine(path, fileName);

            try
            {
                using (WebClient webClient = new WebClient())
                {
                    webClient.DownloadFile(url, savePath);
                    Log.Info("ZIP file downloaded successfully.");
                }
                return true;
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to download the ZIP file: {ex.Message}");
                return false;
            }
        }

        private void TimerElapsed(object sender, ElapsedEventArgs e)
        {
            if (pluginIsUpToDate)
            {
                Log.Info("Making github request...");

                string originalPath = StoragePath;
                string trimmedPath = originalPath.TrimEnd("Instance".ToCharArray());
                string path = Path.Combine(trimmedPath, "Plugins");

                string manifestUrl = "https://raw.githubusercontent.com/AALUND13/AALUND13_Plugin/master/AALUND13_Plugin/manifest.xml";
                string versionFromGithub = GetVersionFromManifest(manifestUrl);

                if (versionFromGithub != string.Empty)
                {
                    Log.Info("Successfully got the version from Github. Comparing version");
                    if (Version != versionFromGithub)
                    {
                        Log.Info($"Plugin is not up to date | version from Github: {versionFromGithub}, plugin Version: {Version}");
                        Log.Info("Updateing plugin");

                        Torch.CurrentSession?.Managers?.GetManager<IChatManagerServer>()?.SendMessageAsSelf("'AALUND13 Plugin' Is Out Of Date! Updating plugin...");
                        if (downloadLatestVersionPlugin(path, "AALUND13_Plugin.zip"))
                        {
                            Log.Info("Successfully downloaded latest version of 'AALUND13 Plugin'");
                            Log.Info("Restarting in 5 minutes");
                            Torch.CurrentSession?.Managers?.GetManager<IChatManagerServer>()?.SendMessageAsSelf("Successfully downloaded latest version of 'AALUND13 Plugin'");
                            Torch.CurrentSession?.Managers?.GetManager<IChatManagerServer>()?.SendMessageAsSelf("Restarting in 5 minutes");
                            StartRestartTimer();
                            pluginIsUpToDate = false;
                        }
                        else
                        {
                            Log.Info("Falled to download latest version of 'AALUND13 Plugin'");
                            Torch.CurrentSession?.Managers?.GetManager<IChatManagerServer>()?.SendMessageAsSelf("Falled to download latest version of 'AALUND13 Plugin'");
                        }
                    }
                    else
                    {
                        Log.Info($"Plugin is up to date | version from Github: {versionFromGithub}, plugin Version: {Version}");
                    }
                }
                else
                {
                    Log.Error("Github request falled");
                }
            }
        }

        public override void Init(ITorchBase torch)
        {
            base.Init(torch);
            SetupConfig();
            StartTimer();
            Log.Error(Version.ToString());

            var sessionManager = Torch.Managers.GetManager<TorchSessionManager>();
            if (sessionManager != null)
                sessionManager.SessionStateChanged += SessionChanged;
            else
                Log.Warn("No session manager loaded!");
            Save();
        }

        public override void Update()
        {
            VoteRestartPlayerNeeded = (int)Math.Ceiling((decimal)MyAPIGateway.Multiplayer.Players.Count / (100 / Config.VoteRestartYesPercentageNeeded));
            VoteModPlayerNeeded = (int)Math.Ceiling((decimal)MyAPIGateway.Multiplayer.Players.Count / (100 / Config.VoteModYesPercentageNeeded));
        }

        private void SessionChanged(ITorchSession session, TorchSessionState state)
        {

            switch (state)
            {
                
                case TorchSessionState.Loaded:
                    Log.Info("Session Loaded!");
                    break;

                case TorchSessionState.Unloading:
                    Log.Info("Session Unloading!");

                    break;
            }
        }

        private void SetupConfig()
        {

            var configFile = Path.Combine(StoragePath, CONFIG_FILE_NAME);

            try
            {

                _config = Persistent<AALUND13_PluginConfig>.Load(configFile);

            }
            catch (Exception e)
            {
                Log.Warn(e);
            }

            if (_config?.Data == null)
            {

                Log.Info("Create Default Config, because none was found!");

                _config = new Persistent<AALUND13_PluginConfig>(configFile, new AALUND13_PluginConfig());
                _config.Save();
            }
        }

        public void Save()
        {
            try
            {
                _config.Save();
                Log.Info("Configuration Saved.");
            }
            catch (IOException e)
            {
                Log.Warn(e, "Configuration failed to save");
            }
        }
    }
}
