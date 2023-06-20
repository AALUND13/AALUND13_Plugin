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

        public bool updateList = false;

        public HashSet<ulong> voteParticipants = new HashSet<ulong>();
        public Timer voteTimer;

        public HashSet<ulong> modVoteParticipants = new HashSet<ulong>();
        public ulong modId = 0;
        public Timer modVoteTimer;


        public override void Init(ITorchBase torch)
        {
            base.Init(torch);
            SetupConfig();

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
