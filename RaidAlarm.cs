using System.Collections.Generic;
using CompanionServer;
using Oxide.Core;
using ProtoBuf;
using UnityEngine;


namespace Oxide.Plugins
{
    
    [Info("RaidAlarm", "haggbart", "0.1.1")]
    [Description("Receive raid notifications through the official Rust companion mobile app")]
    internal class RaidAlarm : RustPlugin
    {
        #region init, data and cleanup
        
        private static HashSet<ulong> disabled = new HashSet<ulong>();
        
        private void SaveData() =>
            Interface.Oxide.DataFileSystem.WriteObject(Title, disabled);

        private void ReadData() =>
            disabled = Interface.Oxide.DataFileSystem.ReadObject<HashSet<ulong>>(Title);

        
        private void Init() => ReadData();
        
        private void OnServerSave() => SaveData();
        
        private void Unload() => SaveData();

        #endregion
        

        #region localization

        private static class AlarmLoc
        {
            public const string TITLE = "AlarmTitle";
            public const string BODY = "AlarmBody";
            public const string HELP = "AlarmHelp";
            public const string HELP_COMMANDS = "AlarmHelpCommands";
            public const string STATUS_ENABLED = "AlarmStatusEnabled";
            public const string STATUS_DISABLED = "AlarmStatusDisabled";
            public const string TEST_SENT = "AlarmTestSent";
            public const string TEST_DESTROYED = "AlarmTestDestroyedItem";
        }

        private string GetStatusText(BasePlayer player)
        {
            return lang.GetMessage(disabled.Contains(player.userID) ? 
                AlarmLoc.STATUS_DISABLED : AlarmLoc.STATUS_ENABLED, this, player.UserIDString);
        }
        
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                [AlarmLoc.TITLE] = "You're getting raided!",
                [AlarmLoc.BODY] = "{0} destroyed at {1}",
                [AlarmLoc.HELP] = "To receive Raid Alarm notifications, " +
                                   "you need the official Rust+ companion app on your mobile device and pair it this server. " +
                                   "To do this, press Esc and click \"Rust+\" in main menu.\n\n " +
                                   "Use /raidalarm test to test your alarm. To do disable, use /raidalarm disable",
                [AlarmLoc.HELP_COMMANDS] = "Available commands: \n/raidalarm status|enable|disable|test",
                [AlarmLoc.STATUS_ENABLED] = "Raid Alarm is enabled.",
                [AlarmLoc.STATUS_DISABLED] = "Raid Alarm is disabled.",
                [AlarmLoc.TEST_SENT] = "Test notification sent. If you don't receive it, make sure you're paired with the server.",
                [AlarmLoc.TEST_DESTROYED] = "chair"
            }, this);
        }

        #endregion
        
        
        private void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            if (info == null) return;
            
            if (!IsRaidEntity(entity)) return;
            if (info.InitiatorPlayer == null) return;

            var buildingPrivilege = entity.GetBuildingPrivilege();
            if (buildingPrivilege == null || buildingPrivilege.authorizedPlayers.IsEmpty()) return;
            
            var victims = new List<ulong>(buildingPrivilege.authorizedPlayers.Count);
            foreach (PlayerNameID victim in buildingPrivilege.authorizedPlayers)
            {
                if (disabled.Contains(victim.userid)) continue;
                victims.Add(victim.userid);
            }
            
            NotificationList.SendNotificationTo(
                victims, NotificationChannel.SmartAlarm, lang.GetMessage(AlarmLoc.TITLE, this),
                string.Format(lang.GetMessage(AlarmLoc.BODY, this), 
                    entity.ShortPrefabName, GetGrid(entity.transform.position)), Util.GetServerPairingData());
        }

        private static bool IsRaidEntity(BaseCombatEntity entity)
        {
            if (entity is Door) return true;

            if (!(entity is BuildingBlock)) return false;
            
            return ((BuildingBlock)entity).grade != BuildingGrade.Enum.Twigs;
        }
        
        
        private static string GetGrid(Vector3 pos)
        {
            const float scale = 150f;
            float x = pos.x + World.Size/2f;
            float z = pos.z + World.Size/2f;
            int lat = (int)(x / scale);
            char latChar = (char)('A' + lat);
            int lon = (int)(World.Size/scale - z/scale);
            return latChar + lon.ToString();
        }
        
        
        [ChatCommand("raidalarm")]
        private void ChatRaidAlarm(BasePlayer player, string command, string[] args)
        {
            if (args.Length == 0)
            {
                SendReply(player, lang.GetMessage(AlarmLoc.HELP, this, player.UserIDString));
                return;
            }

            switch (args[0])
            {
                case "status":
                    SendReply(player, GetStatusText(player));
                    break;
                case "enable":
                    disabled.Remove(player.userID);
                    SendReply(player, lang.GetMessage(AlarmLoc.STATUS_ENABLED, this, player.UserIDString));
                    break;
                case "disable":
                    disabled.Add(player.userID);
                    SendReply(player, lang.GetMessage(AlarmLoc.STATUS_DISABLED, this, player.UserIDString));
                    break;     
                case "test":
                    if (disabled.Contains(player.userID))
                    {
                        SendReply(player, lang.GetMessage(AlarmLoc.STATUS_DISABLED, this, player.UserIDString));
                        return;
                    }
                    
                    NotificationList.SendNotificationTo(player.userID, NotificationChannel.SmartAlarm,
                        lang.GetMessage(AlarmLoc.TITLE, this, player.UserIDString),
                        string.Format(lang.GetMessage(AlarmLoc.BODY, this, player.UserIDString), 
                            string.Format(lang.GetMessage(AlarmLoc.TEST_DESTROYED, this, player.UserIDString)), GetGrid(player.transform.position)), 
                        Util.GetServerPairingData());
                    SendReply(player, lang.GetMessage(AlarmLoc.TEST_SENT, this, player.UserIDString));
                    break;
                default:
                    SendReply(player, lang.GetMessage(AlarmLoc.HELP_COMMANDS, this, player.UserIDString));
                    break;
            }
        }
    }
}    
