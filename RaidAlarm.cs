using System;
using System.Collections.Generic;
using CompanionServer;
using Oxide.Core;
using ProtoBuf;
using UnityEngine;


namespace Oxide.Plugins
{
    [Info("Raid Alarm", "haggbart", "0.4.0")]
    [Description("Receive raid notifications through the official Rust companion mobile app")]
    internal class RaidAlarm : RustPlugin
    {
        #region init, data and cleanup

        private static HashSet<ulong> disabled = new HashSet<ulong>();

        private const string PERMISSION = "raidalarm.use";

        private void SaveData() => Interface.Oxide.DataFileSystem.WriteObject(Name, disabled);

        private void ReadData() => disabled = Interface.Oxide.DataFileSystem.ReadObject<HashSet<ulong>>(Name);

        private void Init()
        {
            config = Config.ReadObject<PluginConfig>();
            SaveConfig();
            permission.RegisterPermission(PERMISSION, this);
            ReadData();
        }

        private void OnServerSave() => SaveData();

        private void Unload() => SaveData();

        #endregion


        #region config

        private PluginConfig config;

        private class PluginConfig
        {
            public bool usePermissions;
        }

        protected override void LoadDefaultConfig() => Config.WriteObject(GetDefaultConfig(), true);
        private new void SaveConfig() => Config.WriteObject(config, true);

        private static PluginConfig GetDefaultConfig()
        {
            return new PluginConfig
            {
                usePermissions = false
            };
        }

        #endregion config


        #region localization

        private static class Loc
        {
            public const string TITLE = "AlarmTitle";
            public const string BODY = "AlarmBody";
            public const string HELP = "AlarmHelp";
            public const string HELP_COMMANDS = "AlarmHelpCommands";
            public const string STATUS_ENABLED = "AlarmStatusEnabled";
            public const string STATUS_DISABLED = "AlarmStatusDisabled";
            public const string TEST_SENT = "AlarmTestSent";
            public const string TEST_DESTROYED = "AlarmTestDestroyedItem";
            public const string NO_PERMISSION = "NoPermission";
        }

        private string GetStatusText(BasePlayer player)
        {
            return lang.GetMessage(disabled.Contains(player.userID) ? Loc.STATUS_DISABLED : Loc.STATUS_ENABLED, this, player.UserIDString);
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                [Loc.TITLE] = "You're getting raided!",
                [Loc.BODY] = "{0} destroyed at {1}",
                [Loc.HELP] = "To receive Raid Alarm notifications, " +
                             "you need the official Rust+ companion app on your mobile device and pair it this server. " +
                             "To do this, press Esc and click \"Rust+\" in main menu.\n\n" +
                             "Use /raidalarm test to test your alarm. To do disable, use /raidalarm disable",
                [Loc.HELP_COMMANDS] = "Available commands: \n/raidalarm status|enable|disable|test",
                [Loc.STATUS_ENABLED] = "Raid Alarm is enabled.",
                [Loc.STATUS_DISABLED] = "Raid Alarm is disabled.",
                [Loc.TEST_SENT] = "Test notification sent. If you don't receive it, make sure you're paired with the server.",
                [Loc.TEST_DESTROYED] = "chair",
                [Loc.NO_PERMISSION] = "You don't have the permission to use this command."
            }, this);
        }

        #endregion

        private readonly Dictionary<string, DateTime> raidblocked = new Dictionary<string, DateTime>();
        private DateTime lastAttack = DateTime.Now;

        private void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            if (info == null) return;

            if (!IsRaidEntity(entity)) return;
            if (info.InitiatorPlayer == null) return;

            // prevent spam
            TimeSpan timesince = DateTime.Now - lastAttack;
            if (timesince.TotalSeconds < 1) return;
            lastAttack = DateTime.Now;

            var buildingPrivilege = entity.GetBuildingPrivilege();
            if (buildingPrivilege == null || buildingPrivilege.authorizedPlayers.IsEmpty()) return;

            var victims = new List<ulong>(buildingPrivilege.authorizedPlayers.Count);
            foreach (PlayerNameID victim in buildingPrivilege.authorizedPlayers)
            {
                if (victim == null) continue;
                if (config.usePermissions && !permission.UserHasPermission(victim.userid.ToString(), PERMISSION)) continue;
                if (victim.userid == info.InitiatorPlayer.userID) return;
                if (disabled.Contains(victim.userid)) continue;
                victims.Add(victim.userid);
            }

            // raidblock
            String grid = GetGrid(entity.transform.position);
            raidblocked[grid] = lastAttack;

            NotificationList.SendNotificationTo(victims, NotificationChannel.SmartAlarm, lang.GetMessage(Loc.TITLE, this),
                string.Format(lang.GetMessage(Loc.BODY, this), entity.ShortPrefabName, grid), Util.GetServerPairingData());
        }

        private static bool IsRaidEntity(BaseCombatEntity entity)
        {
            if (entity is Door) return true;

            if (!(entity is BuildingBlock)) return false;

            return ((BuildingBlock) entity).grade != BuildingGrade.Enum.Twigs;
        }

        private static string GetGrid(Vector3 pos)
        {
            const float scale = 150f;
            float x = pos.x + World.Size / 2f;
            float z = pos.z + World.Size / 2f;
            int lat = (int) (x / scale);
            char latChar = (char) ('A' + lat);
            int lon = (int) (World.Size / scale - z / scale);
            return latChar + lon.ToString();
        }

        [ChatCommand("raidalarm")]
        private void ChatRaidAlarm(BasePlayer player, string command, string[] args)
        {
            if (config.usePermissions && !permission.UserHasPermission(player.UserIDString, PERMISSION))
            {
                SendReply(player, lang.GetMessage(Loc.NO_PERMISSION, this, player.UserIDString));
                return;
            }

            if (args.Length == 0)
            {
                SendReply(player, lang.GetMessage(Loc.HELP, this, player.UserIDString));
                return;
            }

            switch (args[0].ToLower())
            {
                case "status":
                    SendReply(player, GetStatusText(player));
                    break;
                case "enable":
                    disabled.Remove(player.userID);
                    SendReply(player, lang.GetMessage(Loc.STATUS_ENABLED, this, player.UserIDString));
                    break;
                case "disable":
                    disabled.Add(player.userID);
                    SendReply(player, lang.GetMessage(Loc.STATUS_DISABLED, this, player.UserIDString));
                    break;
                case "test":
                    if (disabled.Contains(player.userID))
                    {
                        SendReply(player, lang.GetMessage(Loc.STATUS_DISABLED, this, player.UserIDString));
                        return;
                    }

                    NotificationList.SendNotificationTo(player.userID, NotificationChannel.SmartAlarm,
                        lang.GetMessage(Loc.TITLE, this, player.UserIDString),
                        string.Format(lang.GetMessage(Loc.BODY, this, player.UserIDString),
                            string.Format(lang.GetMessage(Loc.TEST_DESTROYED, this, player.UserIDString)),
                            GetGrid(player.transform.position)), Util.GetServerPairingData());
                    SendReply(player, lang.GetMessage(Loc.TEST_SENT, this, player.UserIDString));
                    break;
                default:
                    SendReply(player, lang.GetMessage(Loc.HELP_COMMANDS, this, player.UserIDString));
                    break;
            }
        }

        #region Dev API

        private bool IsRaidBlocked(BasePlayer player)
        {
            DateTime now;
            return raidblocked.TryGetValue(GetGrid(player.transform.position), out now) && (DateTime.Now - now).TotalHours < 1;
        }

        #endregion Dev API
    }
}
