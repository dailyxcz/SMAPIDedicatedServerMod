﻿using DedicatedServer.Chat;
using DedicatedServer.Config;
using DedicatedServer.Crops;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;
using System;
using System.Collections.Generic;
using System.Reflection;

// TODO move config value checking to the ModEntry, or another dedicated class, to be performed
// prior to any updates / Execute() calls. Also make sure to check validity of newly added fields, like
// the cave type selection and the PetName

namespace DedicatedServer.HostAutomatorStages
{
    internal class StartFarmStage : HostAutomatorStage
    {
        private IMonitor monitor;
        private ModConfig config;
        private CropSaver cropSaver = null;
        private AutomatedHost automatedHost = null;

        public StartFarmStage(IModHelper helper, IMonitor monitor, ModConfig config) : base(helper)
        {
            this.monitor = monitor;
            this.config = config;
            helper.Events.GameLoop.SaveLoaded += onSaveLoaded;
            cropSaver = new CropSaver(helper, monitor, config);
            cropSaver.Enable();
            helper.Events.GameLoop.DayStarted += ReadyCheckHelper.OnDayStarted;
            helper.Events.GameLoop.ReturnedToTitle += onReturnToTitle;
        }

        private void logConfigError(string error)
        {
            monitor.Log($"Error in DedicatedServer mod config file. {error}", LogLevel.Error);
        }

        private void exit(int statusCode)
        {
            monitor.Log("Exiting...", LogLevel.Error);
            Environment.Exit(statusCode);
        }

        public override void Execute(object sender, UpdateTickedEventArgs e)
        {
            if (Game1.activeClickableMenu is not TitleMenu menu)
            {
                return;
            }
            
            MethodInfo info = typeof(LoadGameMenu).GetMethod("FindSaveGames", BindingFlags.Static | BindingFlags.NonPublic);
            object result = info.Invoke(obj: null, parameters: Array.Empty<object>());
            List<Farmer> farmers = result as List<Farmer>;
            if (farmers == null)
            {
                return;
            }

            Farmer hostedFarmer = null;
            foreach (Farmer farmer in farmers)
            {
                if (!farmer.slotCanHost)
                {
                    continue;
                }
                if (farmer.farmName.Value == config.FarmName)
                {
                    hostedFarmer = farmer;
                    break;
                }
            }

            if (hostedFarmer != null)
            {
                monitor.Log($"Hosting {hostedFarmer.slotName} on co-op", LogLevel.Debug);
                
                // Mechanisms pulled from CoopMenu.HostFileSlot
                Game1.multiplayerMode = 2;
                SaveGame.Load(hostedFarmer.slotName);
                Game1.exitActiveMenu();
            }
            else
            {
                monitor.Log($"Failed to find farm slot. Creating new farm \"{config.FarmName}\" and hosting on co-op", LogLevel.Debug);
                // Mechanism pulled from CoopMenu.HostNewFarmSlot; CharacterCustomization class; and AdvancedGameOptions class
                Game1.resetPlayer();

                // Starting cabins
                if (config.StartingCabins < 0 || config.StartingCabins > 3)
                {
                    logConfigError("Starting cabins must be an integer in [0, 3]");
                    exit(-1);
                }
                Game1.startingCabins = config.StartingCabins;

                // Cabin layout
                if (config.CabinLayout != "nearby" && config.CabinLayout != "separate")
                {
                    logConfigError("Cabin layout must be either \"nearby\" or \"separate\"");
                    exit(-1);
                }
                if (config.CabinLayout == "separate")
                {
                    Game1.cabinsSeparate = true;
                }
                else
                {
                    Game1.cabinsSeparate = false;
                }

                // Profit margin
                if (config.ProfitMargin != "normal" && config.ProfitMargin != "75%" && config.ProfitMargin != "50%" && config.ProfitMargin != "25%")
                {
                    logConfigError("Profit margin must be one of \"normal\", \"75%\", \"50%\", or \"25%\"");
                    exit(-1);
                }
                if (config.ProfitMargin == "normal")
                {
                    Game1.player.difficultyModifier = 1f;
                }
                else if (config.ProfitMargin == "75%")
                {
                    Game1.player.difficultyModifier = 0.75f;
                }
                else if (config.ProfitMargin == "50%")
                {
                    Game1.player.difficultyModifier = 0.5f;
                }
                else
                {
                    Game1.player.difficultyModifier = 0.25f;
                }

                // Money style
                if (config.MoneyStyle != "shared" && config.MoneyStyle != "separate")
                {
                    logConfigError("Money style must be either \"shared\" or \"separate\"");
                    exit(-1);
                }
                if (config.MoneyStyle == "separate")
                {
                    Game1.player.team.useSeparateWallets.Value = true;
                }
                else
                {
                    Game1.player.team.useSeparateWallets.Value = false;
                }

                // Farm name
                Game1.player.farmName.Value = config.FarmName;

                // Pet species
                if (config.PetSpecies != null && config.PetSpecies != "dog" && config.PetSpecies != "cat")
                {
                    logConfigError("PetSpecies must be either \"dog\" or \"cat\"");
                    exit(-1);
                }
                if (config.AcceptPet && config.PetSpecies == null)
                {
                    logConfigError("PetSpecies must be specified if AcceptPet is true");
                }
                if (config.PetSpecies == "cat")
                {
                    Game1.player.catPerson = true;
                }
                else
                {
                    Game1.player.catPerson = false;
                }

                // Pet breed
                if (config.PetBreed.HasValue && (config.PetBreed < 0 || config.PetBreed > 2))
                {
                    logConfigError("PetBreed must be an integer in [0, 2]");
                    exit(-1);
                }
                if (config.AcceptPet && !config.PetBreed.HasValue)
                {
                    logConfigError("PetBreed must be specified if AcceptPet is true");
                }
                if (config.PetBreed.HasValue)
                {
                    Game1.player.whichPetBreed = config.PetBreed.Value;
                } else
                {
                    Game1.player.whichPetBreed = 0;
                }

                // Farm type
                if (config.FarmType != "standard" && config.FarmType != "riverland" && config.FarmType != "forest" && config.FarmType != "hilltop" && config.FarmType != "wilderness" && config.FarmType != "fourcorners" && config.FarmType != "beach")
                {
                    logConfigError("Farm type must be one of \"standard\", \"riverland\", \"forest\", \"hilltop\", \"wilderness\", \"fourcorners\", or \"beach\"");
                    exit(-1);
                }
                if (config.FarmType == "standard")
                {
                    Game1.whichFarm = 0;
                }
                else if (config.FarmType == "riverland")
                {
                    Game1.whichFarm = 1;
                }
                else if (config.FarmType == "forest")
                {
                    Game1.whichFarm = 2;
                }
                else if (config.FarmType == "hilltop")
                {
                    Game1.whichFarm = 3;
                }
                else if (config.FarmType == "wilderness")
                {
                    Game1.whichFarm = 4;
                }
                else if (config.FarmType == "fourcorners")
                {
                    Game1.whichFarm = 5;
                }
                else if (config.FarmType == "beach")
                {
                    Game1.whichFarm = 6;
                }

                // Community center bundles type
                if (config.CommunityCenterBundles != "normal" && config.CommunityCenterBundles != "remixed")
                {
                    logConfigError("Community center bundles must be either \"normal\" or \"remixed\"");
                    exit(-1);
                }
                if (config.CommunityCenterBundles == "normal")
                {
                    Game1.bundleType = Game1.BundleType.Default;
                }
                else
                {
                    Game1.bundleType = Game1.BundleType.Remixed;
                }

                // Guarantee year 1 completable flag
                Game1.game1.SetNewGameOption("YearOneCompletable", config.GuaranteeYear1Completable);

                // Mine rewards type
                if (config.MineRewards != "normal" && config.MineRewards != "remixed")
                {
                    logConfigError("Mine rewards must be either \"normal\" or \"remixed\"");
                    exit(-1);
                }
                if (config.MineRewards == "normal")
                {
                    Game1.game1.SetNewGameOption("MineChests", Game1.MineChestType.Default);
                }
                else
                {
                    Game1.game1.SetNewGameOption("MineChests", Game1.MineChestType.Remixed);
                }

                // Monsters spawning at night on farm
                Game1.spawnMonstersAtNight = config.SpawnMonstersOnFarmAtNight;
                Game1.game1.SetNewGameOption("SpawnMonstersAtNight", config.SpawnMonstersOnFarmAtNight);

                // Random seed
                Game1.startingGameSeed = config.RandomSeed;

                // Configuration is done; Set server bot constants
                Game1.player.Name = "ServerBot";
                Game1.player.displayName = Game1.player.Name;
                Game1.player.favoriteThing.Value = "Farms";
                Game1.player.isCustomized.Value = true;
                Game1.multiplayerMode = 2;

                // Start game
                menu.createdNewCharacter(true);
            }

            Disable();
        }

        private void onSaveLoaded(object sender, SaveLoadedEventArgs e)
        {
            Game1.onScreenMenus.Remove(Game1.chatBox);
            var chatBox = new EventDrivenChatBox();
            Game1.chatBox = chatBox;
            Game1.onScreenMenus.Add(chatBox);
            automatedHost = new AutomatedHost(helper, monitor, config, chatBox);
            automatedHost.Enable();
        }

        private void onReturnToTitle(object sender, ReturnedToTitleEventArgs e)
        {
            if (automatedHost != null)
            {
                automatedHost.Disable();
                automatedHost = null;
            }
        }
    }
}
