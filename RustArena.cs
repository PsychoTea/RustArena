using System;
using System.Collections.Generic;
using System.Linq;
using Oxide.Game.Rust.Cui;
using UnityEngine;
using Oxide.Core.Plugins;
using System.Reflection;
using Oxide.Core;
using Rust;
using Steamworks;
using System.Text;

namespace Oxide.Plugins
{
    [Info("RustArena", "PsychoTea", "1.0.0")]
    internal class RustArena : RustPlugin
    {
        class StoredData
        {
            public Dictionary<ulong, int> kills = new Dictionary<ulong, int>();
            public Dictionary<ulong, int> deaths = new Dictionary<ulong, int>();

            public Dictionary<ulong, string> playerClass = new Dictionary<ulong, string>();
        }
        StoredData storedData;

        #region Global Variables
        string RAPrefix = "<size=16><color=aqua>Rust Arena:</color><color=orange> ";
        string RASuffix = "</color></size>";
        private Timer _timer;
        List<string> scoreboardList = new List<string>();
        List<string> messages = new List<string>();
        Dictionary<int, string> infoMessages = new Dictionary<int, string>();
        Dictionary<BasePlayer, string> kicked = new Dictionary<BasePlayer, string>();
        int scoreboardRefresh = 20;
        int messageNumbers = 0;

        #endregion

        #region json
        string json = @"[	
						{
							""name"": ""Scoreboard"",
							""parent"": ""HUD/Overlay"",
							""components"":
							[
								{
                                     ""type"":""UnityEngine.UI.Image"",
                                     ""color"":""0.1 0.1 0.1 0.2"",
                                }, 
								{    
									""type"":""RectTransform"",
									""anchormin"": ""0.85 0.4"",
									""anchormax"": ""1 0.8""
								},
							]
						},
						{
							""parent"": ""Scoreboard"",
							""components"":
							[
								{
									""type"":""UnityEngine.UI.Text"",
									""text"":""Kills Leaderboard"",
									""fontSize"":16,
									""align"": ""MiddleCenter"",
								},
								{ 
									""type"":""RectTransform"",
									""anchormin"": ""0 0.89"",
									""anchormax"": ""1 1""
								},
                            ]
                        },
                        {
							""parent"": ""Scoreboard"",
							""components"":
							[
								{
									""type"":""UnityEngine.UI.Text"",
									""text"":""Name | Kills | Deaths"",
									""fontSize"":13,
									""align"": ""MiddleCenter"",
								},
								{ 
									""type"":""RectTransform"",
									""anchormin"": ""0 0.7875"",
									""anchormax"": ""1 1""
								},
                            ]
                        },
                        {
							""parent"": ""Scoreboard"",
							""components"":
							[
								{
									""type"":""UnityEngine.UI.Text"",
									""text"":""{text}"",
									""fontSize"":12,
									""align"": ""MiddleCenter"",
								},
								{ 
									""type"":""RectTransform"",
									""anchormin"": ""0 0"",
									""anchormax"": ""1 0.87""
								},
                            ]
                        },									
					]
					";
        #endregion

        #region Oxide Hooks

        void Loaded()
        {
            _timer = timer.Every(20, () => Messages());
            _timer = timer.Every(30, () => TOD_Sky.Instance.Cycle.Hour = 12);
            /*_timer = timer.Every(3600, () =>
            {
                BaseEntity entity = GameManager.server.CreateEntity("Assets/Prefabs/NPC/Patrol Helicopter/PatrolHelicopter.prefab", new Vector3(), new Quaternion(), true);
                if (entity != null)
                {
                    entity.GetComponent<PatrolHelicopterAI>().SetInitialDestination(new Vector3(0, 100, 0));
                    entity.Spawn(true);
                }
            });*/

            _timer = timer.Every(1, () =>
            {
                if (scoreboardRefresh <= 0)
                {
                    UpdateScoreboard();
                    scoreboardRefresh = 20;
                }
            });
            _timer = timer.Every(1, () => scoreboardRefresh = scoreboardRefresh - 1);

            storedData = Interface.GetMod().DataFileSystem.ReadObject<StoredData>("KillStats");
        }

        void Unloaded()
        {
            _timer.Destroy();
        }

        void OnPlayerInit(BasePlayer player)
        {
            if (player.displayName.Contains("\""))
            {
                Network.Net.sv.Kick(player.net.connection, "Please remove the \" in your name.");
            }

            if (player == BasePlayer.Find("PsychoTea") || player == BasePlayer.Find("[God] PsychoTea"))
                PrintToChat("<size=16><color=aqua>Rust Arena:</color> <color=red>[Owner]</color> <color=lime>PsychoTea</color> <color=orange>has joined the game!</color></size>");
            else
                PrintToChat(RAPrefix + "<color=lime>{0}</color> has joined the game" + RASuffix, player.displayName);

            UpdateScoreboard();
            scoreboardRefresh = 20;

            if (!(storedData.playerClass.ContainsKey(player.userID)))
            {
                storedData.playerClass.Add(player.userID, "assault");
                SendReply(player, RAPrefix + "You have been set to assault class. Do /class to change." + RASuffix);
                timer.Once(10, () => SendReply(player, RAPrefix + "Want a different gun? Do /class." + RASuffix));
            }
            else SetClass(player, storedData.playerClass[player.userID]);
        }

        void OnPlayerDisconnected(BasePlayer player)
        {
            if (kicked.ContainsKey(player))
            {
                if (String.IsNullOrEmpty(kicked[player])) PrintToChat(RAPrefix + "<color=lime>{0}</color> was kicked!" + RASuffix, player.displayName);
                else PrintToChat(RAPrefix + "<color=lime>{0}</color> was kicked for: {1}" + RASuffix, player.displayName, kicked[player]);
            }
            else PrintToChat(RAPrefix + "<color=lime>{0}</color> has left the game!" + RASuffix, player.displayName);
            foreach (KeyValuePair<BasePlayer, string> var in kicked)
            {
                Puts(var.Key + " " + var.Value);
            }
        }

        void OnEntityBuilt(Planner plan, GameObject obj)
        {
            BasePlayer player = plan.ownerPlayer;
            if (player != BasePlayer.Find("76561198096850385"))
            {
                var StartBlock = obj.GetComponent<BaseNetworkable>();
                SendReply(player, RAPrefix + "<color=red>Building is not allowed!</color>" + RASuffix);
                StartBlock.Kill(BaseNetworkable.DestroyMode.Gib);
            }
        }

        object OnItemCraft(ItemCraftTask task, BasePlayer player)
        {
            SendReply(player, RAPrefix + "<color=red>Crafting is not allowed!</color>" + RASuffix);
            task.cancelled = true;
            foreach (var amount in task.blueprint.ingredients)
                player.inventory.GiveItem(amount.itemid, (int)amount.amount * task.amount, false);
            return false;
        }

        void OnPlayerSleepEnded(BasePlayer player)
        {
            GiveClass(player);
        }

        object OnPlayerChat(ConsoleSystem.Arg arg)
        {
            string message = arg.GetString(0, "text");
            BasePlayer player = (BasePlayer)arg.connection.player;

            if (player == BasePlayer.Find("PsychoTea") || player == BasePlayer.Find("[God] PsychoTea"))
            {
                ConsoleSystem.Broadcast("chat.add", player.userID.ToString(), "<size=16><color=red>[Owner] </color><color=lime>PsychoTea: </color>" + message + "</size>", 1.0);
                return true;
            }

            return null;
        }

        void OnEntityDeath(BaseCombatEntity entity, HitInfo hitinfo)
        {
            if (entity != null && entity is BasePlayer)
            {
                BasePlayer victim = entity.ToPlayer();
                ulong vicSteamID = victim.userID;

                if (!storedData.deaths.ContainsKey(victim.userID))
                    storedData.deaths.Add(vicSteamID, 1);
                else
                    storedData.deaths[vicSteamID] = storedData.deaths[vicSteamID] + 1;

                if (hitinfo != null && hitinfo.Initiator != null && hitinfo.Initiator is BasePlayer)
                {
                    BasePlayer attacker = hitinfo.Initiator.ToPlayer();
                    ulong attSteamId = attacker.userID;

                    if (!storedData.kills.ContainsKey(attacker.userID))
                        storedData.kills.Add(attSteamId, 1);
                    else
                        storedData.kills[attSteamId] = storedData.kills[attSteamId] + 1;
                }
                Interface.GetMod().DataFileSystem.WriteObject("KillStats", storedData);
            }
        }

        void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            /*
            if (!(info.HitEntity is BasePlayer))
            {
                info.damageTypes.ScaleAll(0);
            }
             */
        }

        private void OnWeaponFired(BaseProjectile projectile, BasePlayer player)
        {
            projectile.GetItem().condition = projectile.GetItem().info.condition.max;
            projectile.SendNetworkUpdateImmediate();
        }

        private void OnRocketLaunched(BasePlayer player)
        {
            var weapon = player.GetActiveItem().GetHeldEntity() as BaseProjectile;
            if (weapon == null) return;
            player.GetActiveItem().condition = player.GetActiveItem().info.condition.max;
            weapon.SendNetworkUpdateImmediate();

            if (storedData.playerClass[player.userID] == "rocketman")
            {
                weapon.primaryMagazine.contents = weapon.primaryMagazine.capacity;
                weapon.SendNetworkUpdateImmediate();
            }
        }

        bool CanBeWounded(BasePlayer player, HitInfo info)
        {
            return false;
        }

        void OnPlayerAttack(BasePlayer attacker, HitInfo hitinfo)
        {
            if (hitinfo.HitEntity as BasePlayer)
            {
                SendReply(attacker, "<color=yellow>You hit {0}</color>", hitinfo.HitEntity.ToPlayer().displayName);
                SendReply(hitinfo.HitEntity.ToPlayer(), "<color=yellow>You were hit by {0}</color>", attacker.displayName);
            }
        }

        void OnPlayerRespawned(BasePlayer player) => player.EndSleeping();
        
        #endregion

        #region Commands
        [ChatCommand("donate")]
        private void DonateCommand(BasePlayer player, string command, string[] args)
        {
            SendReply(player, RAPrefix + "If you wish to donate to the server, feel free to send money to psychocastkids@gmail.com on PayPal." + RASuffix);
        }

        [ChatCommand("players")]
        private void PlayersCommand(BasePlayer player, string command, string[] args)
        {
            SendReply(player, "<size=16><color=aqua>======Rust Arena: Players======</color></size>");
            SendReply(player, "<color=lime>{0} players online</color>", BasePlayer.activePlayerList.Count.ToString());
            var Online = BasePlayer.activePlayerList as IList<BasePlayer>;
            List<string> OnlineList = new List<string>();
            foreach (BasePlayer target in Online)
            {
                string targetString = target.displayName;
                OnlineList.Add(targetString);
            }
            string joined = string.Join(", ", OnlineList.ToArray());
            SendReply(player, joined);
        }

        [ChatCommand("die")]
        private void RespawnChatCommand(BasePlayer player, string command, string[] args) => player.Die();

        [ConsoleCommand("chat")]
        private void ChatConsoleCommand(ConsoleSystem.Arg arg)
        {
            if (arg.Player() == null)
            {
                StringBuilder builder = new StringBuilder();
                foreach (string value in arg.Args)
                {
                    builder.Append(value);
                    builder.Append(" ");
                }
                string msg = string.Format("<size=16><color=lime>Server:</color><color=aqua> " + builder.ToString() + "</color></size>");
                PrintToChat(msg);
                msg = "";
                return;
            }
            else
            {
                arg.ReplyWith("<color=red>No permission!</color>");
                return;
            }
        }

        [ChatCommand("help")]
        void helpCmd(BasePlayer player, string command, string[] args)
        {
            SendReply(player, "<color=orange>===</color><color=aqua>Rust Arena: Help</color><color=orange>===</color>");
            SendReply(player, "<color=lime>/donate - </color><color=orange>How to donate to the server</color>");
            SendReply(player, "<color=lime>/players - </color><color=orange>Shows a list of online players</color>");
            SendReply(player, "<color=lime>/stats {name} - </color><color=orange>Shows you your kills/deaths. Specify another player to see theirs</color>");
            SendReply(player, "<color=lime>(ConsoleCommand) die - </color><color=orange>Instantly kills you.</color>");
        }

        [ChatCommand("stats")]
        void statsCmd(BasePlayer player, string cmd, string[] args)
        {
            BasePlayer target = null;

            if (args.Length == 0)
                target = player;
            else if (args.Length > 0)
            {
                target = GetPlayer(args[0], player);
                if (target == null) SendReply(player, "That player could not be found!");
            }

            if (target != null)
            {
                if (!storedData.kills.ContainsKey(target.userID))
                {
                    storedData.kills.Add(target.userID, 0);
                    Interface.GetMod().DataFileSystem.WriteObject("KillStats", storedData);
                }

                if (!storedData.deaths.ContainsKey(target.userID))
                {
                    storedData.deaths.Add(target.userID, 0);
                    Interface.GetMod().DataFileSystem.WriteObject("KillStats", storedData);
                }

                string playerNamePrefix;

                if (player != target)
                    messages.Add(String.Format("{0} has", target.displayName));

                messages.Add(String.Format("{0} kills. {1} deaths.", storedData.kills[target.userID].ToString(), storedData.deaths[target.userID].ToString()));

                foreach (string item in messages)
                    player.SendConsoleCommand("chat.add", 76561198206240711, item);

                messages.Clear();
            }
        }

        [ChatCommand("id")]
        void idCmd(BasePlayer player, string cmd, string[] args)
        {
            SendReply(player, player.userID.ToString());
        }

        [ChatCommand("kick")]
        void kickCmd(BasePlayer player, string cmd, string[] args)
        {
            if (player.net.connection.authLevel == 2)
            {
                if (args.Length == 0) SendReply(player, "Error! /kick {player} {reason}");
                else if (args.Length == 1)
                {
                    kicked.Add(GetPlayer(args[0], player), "");
                    Network.Net.sv.Kick(GetPlayer(args[0], player).net.connection, "Kicked.");
                    timer.Once(5, () => kicked.Clear());
                }
                else if (args.Length > 1)
                {
                    string msg = "";
                    for (int i = 1; i < args.Length; i++)
                        msg = msg + " " + args[i];
                    kicked.Add(GetPlayer(args[0], player), msg);
                    Network.Net.sv.Kick(GetPlayer(args[0], player).net.connection, msg);
                    timer.Once(5, () => kicked.Clear());
                }
            }
            else SendReply(player, "You do not have permission to use this command!");
        }

        [ChatCommand("class")]
        void classCmd(BasePlayer player, string cmd, string[] args)
        {
            if (args.Length == 0)
            {
                SendReply(player, RAPrefix + @"Availible classes:
                      - Assault
                      - Sniper
                      - Support
                      - RocketMan
                      Do /class info {class} for more info.
                      Do /class {class} to change class." + RASuffix);
            }
            else if (args.Length >= 1)
            {
                switch (args[0].ToLower())
                {
                    case "assault":
                        player.Die();
                        SetClass(player, "assault");
                        GiveClass(player);
                        break;
                    case "sniper":
                        player.Die();
                        SetClass(player, "sniper");
                        GiveClass(player);
                        break;
                    case "support":
                        player.Die();
                        SetClass(player, "support");
                        GiveClass(player);
                        break;
                    case "rocketman":
                        player.Die();
                        SetClass(player, "rocketman");
                        GiveClass(player);
                        break;
                    case "info":
                        if (args.Length >= 2)
                        {
                            switch (args[1].ToLower())
                            {
                                case "assault":
                                    SendReply(player, RAPrefix + @"With assault class, you get:
        - Assault Rifle (Holosight)
        - Bandages (x5)
        - Medical Synringes (x5)
        - 5.56 Rifle Ammo (x50)
        - Metal Facemask and Chestplate
        - Green Shirt, Pants, Boots
        - Roadsign Kilt" + RASuffix);
                                    break;

                                case "sniper":
                                    SendReply(player, RAPrefix + @"With sniper class, you get:
        - Bolt Action Rifle (Holosight)
        - Semi-Automatic Pistol (Holosight&Silencer)
        - Bandages (x5)
        - Medical Synringes (x5)
        - HV 5.56 Rifle Ammo (x50)
        - HV Pistol Ammo (x50)
        - Metal Facemask
        - Black Shirt, White Pants" + RASuffix);
                                    break;

                                case "support":
                                    SendReply(player, RAPrefix + @"With support class, you get:
        - SMG (Holosight)
        - Semi-Automatic Pistol (Holosight)
        - Bandages (x20)
        - Medical Synringes (x20)
        - HV Pistol Ammo (x50)
        - Metal Facemask and Chestplate
        - Green Shirt, Pants, Boots
        - Roadsign Kilt" + RASuffix);
                                    break;

                                case "rocketman":
                                    SendReply(player, RAPrefix + @"With rocket man class, you get:
        - Rocket Launcher (Infinite Rockets)
        - Bandages (x5)
        - Medical Synringes (x5)
        - Metal Facemask and Chestplate
        - Green Shirt, Pants, Boots
        - Roadsign Kilt" + RASuffix);
                                    break;
                            }
                        }
                        else
                        {
                            SendReply(player, RAPrefix + "Incorrect syntax. Use /class info {class}.\nDo /class to see classes." + RASuffix);
                        }
                        break;
                    default:
                        SendReply(player, RAPrefix + "Unrecognized command! Use /class {class/info}" + RASuffix);
                        break;
                }
            }
        }
        #endregion

        #region Functions

        #region Messages

        void Messages()
        {
            infoMessages.Clear();
            infoMessages.Add(1, String.Format(RAPrefix + "If you love the server, please consider donating. Do <color=lime>/donate</color> for more info." + RASuffix));
            infoMessages.Add(2, String.Format(RAPrefix + "Why not invite your friends? They may love the server as much as you!" + RASuffix));
            infoMessages.Add(3, String.Format(RAPrefix + "We have a map! Go to: </color><color=orange>rustarena.no-ip.org:28015" + RASuffix));
            infoMessages.Add(4, String.Format(RAPrefix + "Want a different gun? Do /class." + RASuffix));

            messageNumbers = messageNumbers + 1;
            if (messageNumbers <= 0) messageNumbers = 1;
            if (messageNumbers > infoMessages.Count) messageNumbers = 1;
            PrintToChat(infoMessages[messageNumbers]);
        }

        #endregion

        void UpdateScoreboard()
        {
            if (BasePlayer.activePlayerList.Count > 0)
            {
                scoreboardList.Clear();
                if (storedData.kills != null)
                {
                    var kills = from pair in storedData.kills
                                orderby pair.Value descending
                                select pair;


                    if (kills != null)
                    {
                        foreach (KeyValuePair<ulong, int> pair in kills)
                        {
                            if (pair.Key.ToString() != null && pair.Value != null)
                            {
                                string playerName = null;
                                BasePlayer playerNameTemp = BasePlayer.Find(pair.Key.ToString());
                                if (playerNameTemp != null)
                                    playerName = playerNameTemp.displayName.ToString();
                                if (playerName != null)
                                {
                                    if (storedData.deaths.ContainsKey(pair.Key))
                                    {
                                        string text = string.Format(playerName + ": " + pair.Value + " | " + storedData.deaths[pair.Key]);
                                        scoreboardList.Add(text);
                                    }
                                }
                            }
                        }
                    }


                    if (scoreboardList.ToArray() != null)
                    {
                        string scoreboardListCsv = string.Join("\n", scoreboardList.ToArray());

                        if (scoreboardListCsv != null)
                        {
                            var Online = BasePlayer.activePlayerList as List<BasePlayer>;
                            foreach (BasePlayer playingPlayer in Online)
                            {
                                if (playingPlayer != null && scoreboardListCsv != null && json != null)
                                {
                                    var obj1 = new Facepunch.ObjectList?(new Facepunch.ObjectList("Scoreboard"));
                                    CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo() { connection = playingPlayer.net.connection }, null, "DestroyUI", obj1);
                                    var obj2 = new Facepunch.ObjectList?(new Facepunch.ObjectList(json.Replace("{text}", scoreboardListCsv)));
                                    if (obj2 != null && playingPlayer.net.connection != null)
                                        CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo() { connection = playingPlayer.net.connection }, null, "AddUI", obj2);
                                }
                            }
                        }
                    }
                }
            }
        }

        BasePlayer GetPlayer(string searchedPlayer, BasePlayer executer, string prefix = "")
        {
            BasePlayer targetPlayer = null;
            List<string> foundPlayers = new List<string>();
            string searchedLower = searchedPlayer.ToLower();

            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                if (player.displayName.ToLower().Contains(searchedLower)) foundPlayers.Add(player.displayName);
            }

            switch (foundPlayers.Count)
            {
                case 0:
                    SendReply(executer, prefix, "The Player can not be found.");
                    break;

                case 1:
                    targetPlayer = BasePlayer.Find(foundPlayers[0]);
                    break;

                default:
                    string players = ListToString(foundPlayers, 0, ", ");
                    SendReply(executer, prefix, "Multiple matching players found: \n" + players);
                    break;
            }

            return targetPlayer;
        }

        string ListToString(List<string> list, int first, string seperator)
        {
            return String.Join(seperator, list.Skip(first).ToArray());
        }

        void SetClass(BasePlayer player, string playerClass)
        {
            if (player != null && (playerClass.Contains("assault") || playerClass.Contains("sniper") || playerClass.Contains("support") || playerClass.Contains("rocketman")))
            {
                SendReply(player, RAPrefix + "Class changed to {0}" + RASuffix, playerClass);
                if (!(storedData.playerClass.ContainsKey(player.userID))) storedData.playerClass.Add(player.userID, playerClass);
                else storedData.playerClass[player.userID] = playerClass;
                Interface.GetMod().DataFileSystem.WriteObject("KillStats", storedData);
            }
        }

        void GiveClass(BasePlayer player)
        {
            if (storedData.playerClass.ContainsKey(player.userID))
            {
                var thompson = ItemManager.CreateByItemID((int)ItemManager.FindItemDefinition("pistol.semiauto").itemid, 1, false);
                var smg = ItemManager.CreateByItemID((int)ItemManager.FindItemDefinition("smg.2").itemid, 1, false);
                var bolt = ItemManager.CreateByItemID((int)ItemManager.FindItemDefinition("rifle.bolt").itemid, 1, false);
                var ak = ItemManager.CreateByItemID((int)ItemManager.FindItemDefinition("rifle.ak").itemid, 1, false);
                var hvPistolAmmo = ItemManager.CreateByItemID((int)ItemManager.FindItemDefinition("ammo.pistol.hv").itemid, 50, false);
                var hvRifleAmmo = ItemManager.CreateByItemID((int)ItemManager.FindItemDefinition("ammo.rifle.hv").itemid, 50, false);
                var medicalSyringe = ItemManager.CreateByItemID((int)ItemManager.FindItemDefinition("syringe.medical").itemid, 5, false);
                var bandage = ItemManager.CreateByItemID((int)ItemManager.FindItemDefinition("bandage").itemid, 5, false);
                var rocketLauncher = ItemManager.CreateByItemID((int)ItemManager.FindItemDefinition("rocket.launcher").itemid, 1, false);

                var head = ItemManager.CreateByItemID((int)ItemManager.FindItemDefinition("metal.facemask").itemid, 1, false);
                var chest = ItemManager.CreateByItemID((int)ItemManager.FindItemDefinition("metal.plate.torso").itemid, 1, false);
                var shirt = ItemManager.CreateByItemID((int)ItemManager.FindItemDefinition("tshirt").itemid, 1, false);
                var pants = ItemManager.CreateByItemID((int)ItemManager.FindItemDefinition("pants").itemid, 1, false);
                var boots = ItemManager.CreateByItemID((int)ItemManager.FindItemDefinition("shoes.boots").itemid, 1, false);
                var kilt = ItemManager.CreateByItemID((int)ItemManager.FindItemDefinition("roadsign.kilt").itemid, 1, false);

                switch (storedData.playerClass[player.userID])
                {
                    case "assault":
                        #region assualt
                        player.inventory.Strip();
                        player.health = 100;
                        player.metabolism.calories.value = 1000;
                        player.metabolism.hydration.value = 1000;

                        ak.contents.AddItem(ItemManager.FindItemDefinition("weapon.mod.holosight"), 1);
                        var akProj = ak.GetHeldEntity() as BaseProjectile;
                        akProj.primaryMagazine.contents = akProj.primaryMagazine.capacity;
                        player.inventory.GiveItem(ak, player.inventory.containerBelt);

                        player.inventory.GiveItem(bandage, player.inventory.containerBelt);

                        player.inventory.GiveItem(medicalSyringe, player.inventory.containerBelt);

                        var rifleAmmo = ItemManager.CreateByItemID((int)ItemManager.FindItemDefinition("ammo.rifle").itemid, 50, false);
                        player.inventory.GiveItem(rifleAmmo, player.inventory.containerMain);

                        player.inventory.GiveItem(head, player.inventory.containerWear);
                        player.inventory.GiveItem(chest, player.inventory.containerWear);
                        player.inventory.GiveItem(shirt, player.inventory.containerWear);
                        player.inventory.GiveItem(pants, player.inventory.containerWear);
                        player.inventory.GiveItem(boots, player.inventory.containerWear);
                        player.inventory.GiveItem(kilt, player.inventory.containerWear);
                        shirt.skin = 101;
                        boots.skin = 10080;
                        #endregion
                        break;

                    case "sniper":
                        #region sniper
                        player.inventory.Strip();
                        player.health = 100;
                        player.metabolism.calories.value = 1000;
                        player.metabolism.hydration.value = 1000;

                        bolt.contents.AddItem(ItemManager.FindItemDefinition("weapon.mod.holosight"), 1);
                        var boltProj = bolt.GetHeldEntity() as BaseProjectile;
                        boltProj.primaryMagazine.contents = boltProj.primaryMagazine.capacity;
                        player.inventory.GiveItem(bolt, player.inventory.containerBelt);

                        var pistol = ItemManager.CreateByItemID((int)ItemManager.FindItemDefinition("pistol.semiauto").itemid, 1, false);
                        pistol.contents.AddItem(ItemManager.FindItemDefinition("weapon.mod.holosight"), 1);
                        pistol.contents.AddItem(ItemManager.FindItemDefinition("weapon.mod.silencer"), 1);
                        var pistolProj = pistol.GetHeldEntity() as BaseProjectile;
                        pistolProj.primaryMagazine.contents = pistolProj.primaryMagazine.capacity;
                        player.inventory.GiveItem(pistol, player.inventory.containerBelt);

                        player.inventory.GiveItem(bandage, player.inventory.containerBelt);

                        player.inventory.GiveItem(medicalSyringe, player.inventory.containerBelt);

                        player.inventory.GiveItem(hvRifleAmmo, player.inventory.containerMain);

                        player.inventory.GiveItem(hvPistolAmmo, player.inventory.containerMain);

                        player.inventory.GiveItem(head, player.inventory.containerWear);
                        player.inventory.GiveItem(shirt, player.inventory.containerWear);
                        player.inventory.GiveItem(pants, player.inventory.containerWear);
                        shirt.skin = 10003;
                        pants.skin = 10021;
                        #endregion
                        break;

                    case "support":
                        #region support
                        player.inventory.Strip();
                        player.health = 100;
                        player.metabolism.calories.value = 1000;

                        player.metabolism.hydration.value = 1000;
                        smg.contents.AddItem(ItemManager.FindItemDefinition("weapon.mod.holosight"), 1);
                        var smgProj = smg.GetHeldEntity() as BaseProjectile;
                        smgProj.primaryMagazine.contents = smgProj.primaryMagazine.capacity;
                        player.inventory.GiveItem(smg, player.inventory.containerBelt);

                        thompson.contents.AddItem(ItemManager.FindItemDefinition("weapon.mod.holosight"), 1);
                        var thompsonProj = thompson.GetHeldEntity() as BaseProjectile;
                        thompsonProj.primaryMagazine.contents = thompsonProj.primaryMagazine.capacity;
                        player.inventory.GiveItem(thompson, player.inventory.containerBelt);

                        var bandageSupport = ItemManager.CreateByItemID((int)ItemManager.FindItemDefinition("bandage").itemid, 20, false);
                        player.inventory.GiveItem(bandageSupport, player.inventory.containerBelt);

                        var medicalSyringeSupport = ItemManager.CreateByItemID((int)ItemManager.FindItemDefinition("syringe.medical").itemid, 20, false);
                        player.inventory.GiveItem(medicalSyringeSupport, player.inventory.containerBelt);

                        var pistolAmmo = ItemManager.CreateByItemID((int)ItemManager.FindItemDefinition("ammo.pistol").itemid, 50, false);
                        player.inventory.GiveItem(hvPistolAmmo, player.inventory.containerMain);

                        player.inventory.GiveItem(head, player.inventory.containerWear);
                        player.inventory.GiveItem(chest, player.inventory.containerWear);
                        player.inventory.GiveItem(shirt, player.inventory.containerWear);
                        player.inventory.GiveItem(pants, player.inventory.containerWear);
                        player.inventory.GiveItem(boots, player.inventory.containerWear);
                        player.inventory.GiveItem(kilt, player.inventory.containerWear);
                        #endregion
                        break;

                    case "rocketman":
                        #region rocketman
                        player.inventory.Strip();
                        player.health = 100;
                        player.metabolism.calories.value = 1000;
                        player.metabolism.hydration.value = 1000;

                        var rocketLauncherProj = rocketLauncher.GetHeldEntity() as BaseProjectile;
                        rocketLauncherProj.primaryMagazine.contents = rocketLauncherProj.primaryMagazine.capacity;
                        player.inventory.GiveItem(rocketLauncher, player.inventory.containerBelt);
                        player.inventory.GiveItem(bandage, player.inventory.containerBelt);
                        player.inventory.GiveItem(medicalSyringe, player.inventory.containerBelt);

                        player.inventory.GiveItem(head, player.inventory.containerWear);
                        player.inventory.GiveItem(chest, player.inventory.containerWear);
                        player.inventory.GiveItem(shirt, player.inventory.containerWear);
                        player.inventory.GiveItem(pants, player.inventory.containerWear);
                        player.inventory.GiveItem(boots, player.inventory.containerWear);
                        player.inventory.GiveItem(kilt, player.inventory.containerWear);
                        #endregion
                        break;
                }
            }
        }

        #endregion
    }
}
