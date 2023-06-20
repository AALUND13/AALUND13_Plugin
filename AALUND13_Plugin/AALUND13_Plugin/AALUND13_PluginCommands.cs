using NLog.Fluent;
using Sandbox.Game.World;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Timers;
using Torch.Commands;
using Torch.Commands.Permissions;
using VRage.Game;
using VRage.Game.ModAPI;
using System.Net;
using System.Text.RegularExpressions;
using System.Reflection;
using Torch.Utils;
using LitJson;
using System.Runtime.Remoting.Contexts;
using Torch.Mod;
using Torch.Managers.ChatManager;
using Sandbox.Engine.Multiplayer;
using Torch.API.Managers;
using VRage.Plugins;
using VRageMath;
using System.Windows.Controls;
using VRage.Input;

namespace AALUND13_Plugin
{
    [Category("AALUND13_Plugin")]
    public class AALUND13_PluginCommands : CommandModule
    {
        //TODO: Make rename the project
        public AALUND13_Plugin Plugin => (AALUND13_Plugin)Context.Plugin;
        public bool CheckTags(PublishedFileDetail modDetail)
        {
            foreach (string tag in modDetail.Tags)
            {
                if (Plugin.Config.blacklistedTags.Contains(tag.ToLower()))
                {
                    Context.Respond($"'{modDetail.Title}' has be blacklisted. becuase have blacklisted tag '{tag}'");
                    return false;
                }
            }

            return true;
        }

        public bool CheckModSubscribers(PublishedFileDetail modDetail)
        {
            if (modDetail.Subscriptions < Plugin.Config.VoteModMinSubscribersNeeded)
            {
                Context.Respond($"'{modDetail.Title}' Dont have enough Subscribers. mod Subscribers {modDetail.Subscriptions}");
                return false;
            }

            return true;
        }

        public class PublishedFileDetail
        {
            public string PublishedFileId { get; set; }
            public int Result { get; set; }
            public string Creator { get; set; }
            public int CreatorAppId { get; set; }
            public int ConsumerAppId { get; set; }
            public string Filename { get; set; }
            public int FileSize { get; set; }
            public string FileUrl { get; set; }
            public string HContentFile { get; set; }
            public string PreviewUrl { get; set; }
            public string HContentPreview { get; set; }
            public string Title { get; set; }
            public string Description { get; set; }
            public int TimeCreated { get; set; }
            public int TimeUpdated { get; set; }
            public int Visibility { get; set; }
            public int Banned { get; set; }
            public string BanReason { get; set; }
            public int Subscriptions { get; set; }
            public int Favorited { get; set; }
            public int LifetimeSubscriptions { get; set; }
            public int LifetimeFavorited { get; set; }
            public int Views { get; set; }
            public List<string> Tags { get; set; }
        }

        public static List<PublishedFileDetail> GetModById(string modID)
        {
            string url = "https://api.steampowered.com/ISteamRemoteStorage/GetPublishedFileDetails/v1/";
            string data = $"itemcount=1&publishedfileids[0]={modID}";

            try
            {
                using (WebClient client = new WebClient())
                {
                    client.Headers[HttpRequestHeader.ContentType] = "application/x-www-form-urlencoded";
                    string response = client.UploadString(url, "POST", data);
                    List<PublishedFileDetail> modList = ParseResponse(response);
                    return modList;
                }
            }
            catch (WebException ex)
            {
                Log.Error(ex.Message);
                return null;
            }
        }

        private static List<PublishedFileDetail> ParseResponse(string response)
        {
            List<PublishedFileDetail> modList = new List<PublishedFileDetail>();

            JsonData jsonData = JsonMapper.ToObject(response);
            JsonData details = jsonData["response"]["publishedfiledetails"];

            foreach (JsonData detail in details)
            {
                PublishedFileDetail mod = new PublishedFileDetail();
                mod.PublishedFileId = detail["publishedfileid"].ToString();
                mod.Result = (int)detail["result"];
                mod.Creator = detail["creator"].ToString();
                mod.CreatorAppId = (int)detail["creator_app_id"];
                mod.ConsumerAppId = (int)detail["consumer_app_id"];
                mod.Filename = detail["filename"].ToString();
                mod.FileSize = (int)detail["file_size"];
                mod.FileUrl = detail["file_url"].ToString();
                mod.HContentFile = detail["hcontent_file"].ToString();
                mod.PreviewUrl = detail["preview_url"].ToString();
                mod.HContentPreview = detail["hcontent_preview"].ToString();
                mod.Title = detail["title"].ToString();
                mod.Description = detail["description"].ToString();
                mod.TimeCreated = (int)detail["time_created"];
                mod.TimeUpdated = (int)detail["time_updated"];
                mod.Visibility = (int)detail["visibility"];
                mod.Banned = (int)detail["banned"];
                mod.BanReason = detail["ban_reason"].ToString();
                mod.Subscriptions = (int)detail["subscriptions"];
                mod.Favorited = (int)detail["favorited"];
                mod.LifetimeSubscriptions = (int)detail["lifetime_subscriptions"];
                mod.LifetimeFavorited = (int)detail["lifetime_favorited"];
                mod.Views = (int)detail["views"];

                // Parse the tags
                JsonData tagsData = detail["tags"];
                mod.Tags = new List<string>();
                foreach (JsonData tagData in tagsData)
                {
                    string tag = tagData["tag"].ToString();
                    mod.Tags.Add(tag);
                }

                modList.Add(mod);
            }

            return modList;
        }


        [Command("votemod", "Starts a vote to add a mod to the server.")]
        [Permission(MyPromoteLevel.None)]
        public void VoteMod(ulong modID = 0)
        {
            if (!Plugin.Config.VoteMod)
            {
                Context.Respond($"Vote mod functionality is currently disabled.");
                return;
            }

            if (modID != 0)
            {
                var modList = GetModById(modID.ToString());

                if (modList == null || modList.Count == 0)
                {
                    return;
                }

                var mod = modList[0];

                if (ModExists(modID))
                {
                    Context.Respond($"'{mod.Title}' already exists in this world");
                    return;
                }

                if (modID != 0 && !checkModOnWorkshop(modID.ToString(), mod))
                {
                    return;
                }

                if (modID != 0 && !CheckTags(mod))
                {
                    return;
                }

                if (modID != 0 && !CheckModSubscribers(mod))
                {
                    return;
                }

                if (MyAPIGateway.Players.Count < Plugin.Config.VoteModMinPlayer)
                {
                    Context.Respond($"Need at least {Plugin.Config.VoteModMinPlayer} player in this server to vote for a mod");
                    return;
                }

                if (Plugin.modVoteParticipants.Contains(Context.Player.SteamUserId))
                {
                    Context.Respond("You have already voted to add a mod to the server.");
                    return;
                }

                if (Plugin.modId == 0)
                {
                    Context.Torch.CurrentSession?.Managers?.GetManager<IChatManagerServer>()?.SendMessageAsSelf($"{Context.Player.DisplayName} Voted '{mod.Title}' mod be been added to the world.");
                    Context.Torch.CurrentSession?.Managers?.GetManager<IChatManagerServer>()?.SendMessageAsSelf("Do !AALUND13_Plugin VoteMod if you want this mod to be added into the server.");
                    Plugin.modId = modID;
                }
            }

            if (Plugin.modId != 0)
            {
                if (MyAPIGateway.Players.Count < Plugin.Config.VoteModMinPlayer)
                {
                    Context.Respond($"Need at least {Plugin.Config.VoteModMinPlayer} player in this server to vote for a mod");
                    return;
                }

                if (Plugin.modVoteParticipants.Contains(Context.Player.SteamUserId))
                {
                    Context.Respond("You have already voted to add a mod to the server.");
                    return;
                }

                //Add player that use to voteParticipants
                Plugin.modVoteParticipants.Add(Context.Player.SteamUserId);



                //Check if voteParticipants >= VoteModPlayerNeeded it true restart the server otherwise dont
                if (Plugin.modVoteParticipants.Count >= Plugin.VoteModPlayerNeeded)
                {
                    AddModByID(Plugin.modId);

                    if (Plugin.modVoteTimer != null)
                    {
                        Plugin.modVoteTimer.Stop();
                        Plugin.modVoteTimer.Dispose();

                        Plugin.modVoteParticipants.Clear();
                        Plugin.modVoteTimer.Stop();
                        Plugin.modVoteTimer.Dispose();
                        Plugin.modVoteTimer = null;
                        Plugin.modId = 0;
                    }

                    List<PublishedFileDetail> modLists = GetModById(modID.ToString());

                    if (modLists != null && modLists.Count > 0)
                    {
                        // Access the mod details
                        PublishedFileDetail mod = modLists[0];
                        Context.Torch.CurrentSession?.Managers?.GetManager<IChatManagerServer>()?.SendMessageAsSelf($"'{mod.Title}' mod has been added to the world.");
                    }
                    Plugin.modId = modID;

                }
                else
                {
                    int PlayerNeeded = Plugin.VoteModPlayerNeeded - Plugin.modVoteParticipants.Count;
                    Context.Torch.CurrentSession?.Managers?.GetManager<IChatManagerServer>()?.SendMessageAsSelf($"{Context.Player} voted to add mod to the server. Votes Needed: {PlayerNeeded}");

                    // Start or reset the timer
                    if (Plugin.modVoteTimer != null)
                    {
                        Plugin.modVoteTimer.Stop();
                        Plugin.modVoteTimer.Dispose();
                    }
                    Plugin.modVoteTimer = new Timer(TimeSpan.FromMinutes(5).TotalMilliseconds);
                    Plugin.modVoteTimer.Elapsed += ModVoteTimerElapsed;
                    Plugin.modVoteTimer.Start();
                }
            }
        }

        private void ModVoteTimerElapsed(object sender, ElapsedEventArgs e)
        {
            // Reset the voting variables after the specified duration
            Plugin.modVoteParticipants.Clear();
            Plugin.modVoteTimer.Stop();
            Plugin.modVoteTimer.Dispose();
            Plugin.modVoteTimer = null;
            Plugin.modId = 0;
            Context.Torch.CurrentSession?.Managers?.GetManager<IChatManagerServer>()?.SendMessageAsSelf("Voting period ended.");
        }

        private bool checkModOnWorkshop(string modID, PublishedFileDetail modDetail)
        {
            // Access the mod details
            if (modDetail.CreatorAppId == 244850)
            {
                List<string> tags = modDetail.Tags;
                if (modDetail.Tags.Contains("mod"))
                {
                    return true;
                }
                else
                {
                    Context.Respond($"Mod Id {modID} is not a mod");
                }
            }
            else
            {
                Context.Respond($"Mod Id {modID} not exist on space engineers.");
            }
            return false;
        }

        //-----------------------------------------------------------//

        [Command("getmods", "This command will get all the mods in the world")]
        [Permission(MyPromoteLevel.None)]
        public void GetMods()
        {
            // Loop through all mods
            var modList = MyAPIGateway.Session.Mods.ToList();
            foreach (var mod in modList)
            {
                Context.Respond("-----------------------------------");
                Context.Respond($"Mod Name: {mod.FriendlyName}");
                Context.Respond($"Mod ID: {mod.PublishedFileId}");
            }
        }

        //-----------------------------------------------------------//

        [Command("removemod name", "This command will get all the mods in the world")]
        [Permission(MyPromoteLevel.Admin)]
        public void RemoveModsByName(string modName)
        {
            // Loop through all mods
            var modList = MyAPIGateway.Session.Mods.ToList();
            foreach (var mod in modList)
            {
                if (mod.FriendlyName.Contains(modName))
                {
                    Context.Torch.CurrentSession?.Managers?.GetManager<IChatManagerServer>()?.SendMessageAsSelf($"'{mod.FriendlyName}' mod has been remove from the world.");
                    Plugin.Torch.CurrentSession.KeenSession.Mods.Remove(mod);
                    return;
                }
            }
            Context.Respond($"'{modName}' not found in the world mod lists");
            return;
        }
        //-----------------------------------------------------------//

        [Command("removemod id", "This command will get all the mods in the world")]
        [Permission(MyPromoteLevel.Admin)]
        public void RemoveModsByIds(ulong ModID)
        {
            // Loop through all mods
            var modList = MyAPIGateway.Session.Mods.ToList();
            foreach (var mod in modList)
            {
                if (mod.PublishedFileId.Equals(ModID))
                {
                    Context.Torch.CurrentSession?.Managers?.GetManager<IChatManagerServer>()?.SendMessageAsSelf($"'{mod.FriendlyName}' mod has been remove from the world.");
                    Plugin.Torch.CurrentSession.KeenSession.Mods.Remove(mod);
                    return;
                }
            }
            Context.Respond($"'{ModID}' not found in the world mod lists");
            return;
        }

        //-----------------------------------------------------------//

        [Command("addmod", "This command adds mods to the world.")]
        [Permission(MyPromoteLevel.Admin)]
        public void AddMod(ulong modID)
        {
            var modList = GetModById(modID.ToString());

            if (modList == null || modList.Count == 0)
            {
                return;
            }

            var mod = modList[0];

            if (!ModExists(modID))
            {
                if (checkModOnWorkshop(modID.ToString(), mod))
                {
                    Context.Torch.CurrentSession?.Managers?.GetManager<IChatManagerServer>()?.SendMessageAsSelf($"'{mod.Title}' mod has been added to the world.");
                    AddModByID(modID);
                }
            }
            else
            {
                Context.Respond($"'{mod.Title}' already exists in this world.");
            }
            Context.Torch.Save();
        }

        //-----------------------------------------------------------//

        [Command("voterestart", "Starts a vote to restart the server.")]
        [Permission(MyPromoteLevel.None)]
        public void VoteRestart()
        {
            //Check if the command is enable
            if (!Plugin.Config.VoteRestart)
            {
                Context.Respond($"Vote restart functionality is currently disabled.");
                return;
            }

            //Check if voteParticipants already voted to restart the server
            if (Plugin.voteParticipants.Contains(Context.Player.SteamUserId))
            {
                Context.Respond("You have already voted to restart the server.");
                return;
            }

            //Add player that use to voteParticipants
            Plugin.voteParticipants.Add(Context.Player.SteamUserId);

            // Start or reset the timer
            if (Plugin.voteTimer != null)
            {
                Plugin.voteTimer.Stop();
                Plugin.voteTimer.Dispose();
            }
            else
            {
                Plugin.voteTimer = new Timer(TimeSpan.FromMinutes(5).TotalMilliseconds);
                Plugin.voteTimer.Elapsed += VoteTimerElapsed;
                Plugin.voteTimer.Start();
            }

            //Check if voteParticipants >= VoteRestartPlayerNeeded it true restart the server otherwise dont
            if (Plugin.voteParticipants.Count >= Plugin.VoteRestartPlayerNeeded)
            {
                if (Plugin.voteTimer != null)
                {
                    Plugin.voteTimer.Stop();
                    Plugin.voteTimer.Dispose();

                    Plugin.voteParticipants.Clear();
                    Plugin.voteTimer.Stop();
                    Plugin.voteTimer.Dispose();
                    Plugin.voteTimer = null;
                }
                RestartServer();
            }
            else
            {
                int PlayerNeeded = Plugin.VoteRestartPlayerNeeded - Plugin.voteParticipants.Count;
                Context.Torch.CurrentSession?.Managers?.GetManager<IChatManagerServer>()?.SendMessageAsSelf($"{Context.Player.DisplayName} voted to restart the server. Votes Needed: {PlayerNeeded}");
            }
        }

        //-----------------------------------------------------------//

        private void RestartServer()
        {
            // Implement your logic here to restart the server
            Context.Respond("Server restarting...");
            Plugin.Torch.Restart();
        }

        private void VoteTimerElapsed(object sender, ElapsedEventArgs e)
        {
            // Reset the voting variables after the specified duration
            Plugin.voteParticipants.Clear();
            Plugin.voteTimer.Stop();
            Plugin.voteTimer.Dispose();
            Plugin.voteTimer = null;
            Context.Torch.CurrentSession?.Managers?.GetManager<IChatManagerServer>()?.SendMessageAsSelf("Voting period ended.");
        }

        private void AddModByID(ulong id)
        {
            Plugin.Torch.CurrentSession.KeenSession.Mods.Add(new MyObjectBuilder_Checkpoint.ModItem()
            {
                PublishedFileId = id,
                FriendlyName = "Mod not yet initialized"
            });
        }

        private static bool ModExists(ulong modId)
        {
            var modItems = MyAPIGateway.Session.Mods;
            foreach (var modItem in modItems)
            {
                if (modItem.PublishedFileId == modId && modId != 0)
                {
                    return true;
                }
            }
            return false;
        }
    }

    [Category("AALUND13_Plugin_config")]
    public class AALUND13_PluginConfigCommands : CommandModule
    {
        public AALUND13_Plugin Plugin => (AALUND13_Plugin)Context.Plugin;

        private HashSet<string> validTags = new HashSet<string>
        {
            "block",
            "skybox",
            "character",
            "animation",
            "respawn Ship",
            "production",
            "script",
            "modpack",
            "asteroid",
            "planet",
            "hud",
            "other",
            "npc",
            "serverscripts",
            "font"
        };

        //-----------------------------------------------------------//

        [Command("vote_mod_min_player", "This command will set needed player percentage of vote restart command")]
        [Permission(MyPromoteLevel.Moderator)]
        public void voteModMinPlayer(int voteModMinPlayer)
        {
            Plugin.Config.VoteModMinPlayer = Math.Abs(voteModMinPlayer);
            Context.Respond($"'Vote Restart Player Percent Needed' now set too {Math.Abs(voteModMinPlayer)}");
            Plugin.Save();
        }

        //-----------------------------------------------------------//

        [Command("vote_mod_min_subscribers_needed", "This command will set needed player percentage of vote restart command")]
        [Permission(MyPromoteLevel.Moderator)]
        public void VoteModMinSubscribersNeeded(int voteModMinSubscribersNeeded)
        {
            Plugin.Config.VoteModMinSubscribersNeeded = Math.Abs(voteModMinSubscribersNeeded);
            Context.Respond($"'Vote Restart Player Percent Needed' now set too {Math.Abs(voteModMinSubscribersNeeded)}");
            Plugin.Save();
        }

        //-----------------------------------------------------------//

        [Command("vote_restart_player_percent_needed", "This command will set needed player percentage of vote restart command")]
        [Permission(MyPromoteLevel.Moderator)]
        public void VoteRestartPlayerPercentNeeded(int voteRestartPlayerPercentNeeded)
        {
            Plugin.Config.VoteRestartYesPercentageNeeded = (int)Math.Round((double)MathHelper.Clamp(voteRestartPlayerPercentNeeded, 0, 100));
            Context.Respond($"'Vote Restart Player Percent Needed' now set too {(int)Math.Round((double)MathHelper.Clamp(voteRestartPlayerPercentNeeded, 0, 100))}");
            Plugin.Save();
        }

        //-----------------------------------------------------------//

        [Command("vote_mod_player_percent_needed", "This command will set needed player percentage of vote mod command")]
        [Permission(MyPromoteLevel.Moderator)]
        public void VoteModPlayerPercentNeeded(int voteModPlayerPercentNeeded)
        {
            Plugin.Config.VoteModYesPercentageNeeded = (int)Math.Round((double)MathHelper.Clamp(voteModPlayerPercentNeeded, 0, 100));
            Context.Respond($"'Vote Mod Player Percent Needed' now set too {(int)Math.Round((double)MathHelper.Clamp(voteModPlayerPercentNeeded, 0, 100))}");
            Plugin.Save();
        }

        //-----------------------------------------------------------//

        [Command("vote_restart", "This command will enable or disable vote restart")]
        [Permission(MyPromoteLevel.Moderator)]
        public void ConfigVoteRestart(bool enable)
        {
            if (!enable)
            {
                Context.Respond("Vote restart now disable");
            }
            else
            {
                Context.Respond("Vote restart now enable");
            }
            Plugin.Config.VoteRestart = enable;
            Plugin.Save();
        }

        //-----------------------------------------------------------//

        [Command("vote_mod", "This command will enable or disable vote mod")]
        [Permission(MyPromoteLevel.Moderator)]
        public void ConfigVoteMod(bool enable)
        {
            if (!enable)
            {
                Context.Respond("Vote mod now disable");
            }
            else
            {
                Context.Respond("Vote mod now enable");
            }
            Plugin.Config.VoteMod = enable;
            Plugin.Save();
        }
        //-----------------------------------------------------------//

        [Command("blacklist_tag", "This command will add or remove blacklist tags")]
        [Permission(MyPromoteLevel.Moderator)]
        public void ConfigBlacklistTag(string tag = "none", bool add = true)
        {
            if (tag == "none")
            {
                // Loop through all tags
                Context.Respond($"----------Tags----------");
                foreach (var tags in Plugin.Config.blacklistedTags)
                {
                    Context.Respond($"Tag Name: {tags}");
                }
                Context.Respond($"-------Valid Tags-------");
                foreach (var validTags in validTags)
                {
                    Context.Respond($"Tag Name: {validTags}");
                }
                return;
            }

            if (validTags.Contains(tag))
            {
                if (add)
                {
                    if (!Plugin.Config.blacklistedTags.Contains(tag.ToLower()))
                    {
                        Context.Respond($"'{tag.ToLower()}' has be added to the blacklisted tag");
                        Plugin.Config.blacklistedTags.Add(tag.ToLower());
                        Plugin.updateList = true;
                        Plugin.Save();
                    }
                    else
                    {
                        Context.Respond($"'{tag.ToLower()}' is already in blacklist tag");
                    }
                }
                else
                {
                    if (Plugin.Config.blacklistedTags.Contains(tag.ToLower()))
                    {
                        Context.Respond($"'{tag.ToLower()}' has be remove to the blacklisted tag");
                        Plugin.Config.blacklistedTags.Remove(tag.ToLower());
                        Plugin.updateList = true;
                        Plugin.Save();
                    }
                    else
                    {
                        Context.Respond($"'{tag.ToLower()}' not in blacklisted tag");
                    }
                }
            }
            else
            {
                Context.Respond($"'{tag.ToLower()}' is not a valid tag");
            }
        }
    }
}
