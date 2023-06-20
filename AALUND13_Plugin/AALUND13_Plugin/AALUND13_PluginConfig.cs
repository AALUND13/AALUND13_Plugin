using System;
using System.Collections.Generic;
using Torch;
using static VRage.Game.ObjectBuilders.Definitions.MyObjectBuilder_EmoteDefinition;

namespace AALUND13_Plugin
{
    public class AALUND13_PluginConfig : ViewModel
    {

        private int _VoteRestartYesPercentageNeeded = 75;
        private int _VoteModYesPercentageNeeded = 75;
        private int _VoteModMinSubscribersNeeded = 1000;

        private int _VoteModMinPlayer = 2;
        private bool _VoteRestart = true;
        private bool _VoteMod = true;

        private HashSet<string> _blacklistedTags = new HashSet<string>
        {
            "skybox",
            "planet",
            "respawn ship",
            "hud",
            "animation",
            "character"
        };

        public int VoteRestartYesPercentageNeeded { get => _VoteRestartYesPercentageNeeded; set => SetValue(ref _VoteRestartYesPercentageNeeded, value); }
        public int VoteModYesPercentageNeeded { get => _VoteModYesPercentageNeeded; set => SetValue(ref _VoteModYesPercentageNeeded, value); }
        public int VoteModMinSubscribersNeeded { get => _VoteModMinSubscribersNeeded; set => SetValue(ref _VoteModMinSubscribersNeeded, value); }
        public int VoteModMinPlayer { get => _VoteModMinPlayer; set => SetValue(ref _VoteModMinPlayer, value); }
        public bool VoteRestart { get => _VoteRestart; set => SetValue(ref _VoteRestart, value); }
        public bool VoteMod { get => _VoteMod; set => SetValue(ref _VoteMod, value); }
        public HashSet<string> blacklistedTags { get => _blacklistedTags; set => SetValue(ref _blacklistedTags, value); }
    }
}
