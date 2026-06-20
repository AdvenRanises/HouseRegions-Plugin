using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.Xna.Framework;
using Terraria;
using TShockAPI;
using TShockAPI.DB;

namespace HouseRegions
{
    public class UserInteractionHandler : IDisposable
    {
        private class CommandExecDummyPlayer : TSPlayer
        {
            private readonly Action<string, Color> _sendMessageHandler;

            public CommandExecDummyPlayer(
                TSPlayer originalPlayer, Action<string, Color> sendMessageHandler
            ) : base(originalPlayer.Name)
            {
                Account = originalPlayer.Account;
                IsLoggedIn = originalPlayer.IsLoggedIn;
                Group = originalPlayer.Group;

                _sendMessageHandler = sendMessageHandler;
            }

            public override void SendMessage(string msg, Color color)
            {
                _sendMessageHandler?.Invoke(msg, color);
            }
        }

        private readonly PluginTrace _trace;
        private Configuration _config;
        private readonly HousingManager _housingManager;
        private readonly Func<Configuration?> _reloadConfigurationCallback;

        private readonly Dictionary<int, CommandInteraction> _activeInteractions = new();

        public UserInteractionHandler(
            PluginTrace trace, Configuration config, HousingManager housingManager,
            Func<Configuration?> reloadConfigurationCallback
        )
        {
            _trace = trace ?? throw new ArgumentNullException(nameof(trace));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _housingManager = housingManager ?? throw new ArgumentNullException(nameof(housingManager));
            _reloadConfigurationCallback = reloadConfigurationCallback ?? throw new ArgumentNullException(nameof(reloadConfigurationCallback));

            Commands.ChatCommands.Add(new Command((string)null!, HandleHouseCommand, "house", "housing"));
        }

        private void HandleHouseCommand(CommandArgs args)
        {
            if (args == null || IsDisposed)
                return;

            StopInteraction(args.Player);

            if (args.Parameters.Count >= 1)
            {
                string subCommand = args.Parameters[0].ToLowerInvariant();

                if (TryExecuteSubCommand(subCommand, args))
                    return;
            }

            args.Player.SendMessage("House Regions", Color.White);
            args.Player.SendMessage("A simple TShock regions wrapper for player housing purposes.", Color.White);
            args.Player.SendMessage(string.Empty, Color.Yellow);

            int playerHouseCount = 0;
            for (int i = 0; i < TShock.Regions.Regions.Count; i++)
            {
                string? houseOwner;
                int dummy;
                if (
                    _housingManager.TryGetHouseRegionData(TShock.Regions.Regions[i].Name, out houseOwner, out dummy) &&
                    houseOwner == args.Player.Account?.Name
                )
                    playerHouseCount++;
            }

            string statsMessage = string.Format(
                "You've defined {0} of {1} possible houses so far.", playerHouseCount, _config.MaxHousesPerUser
            );
            args.Player.SendMessage(statsMessage, Color.Yellow);
            args.Player.SendMessage("Type \"/house commands\" to get a list of available commands.", Color.Yellow);
            args.Player.SendMessage("To get more general information about this plugin type \"/house help\".", Color.Yellow);
        }

        private bool TryExecuteSubCommand(string commandNameLC, CommandArgs args)
        {
            switch (commandNameLC)
            {
                case "commands":
                case "cmds":
                {
                    int pageNumber;
                    if (!PaginationTools.TryParsePageNumber(args.Parameters, 1, args.Player, out pageNumber))
                        return true;

                    List<string> terms = new List<string>();
                    terms.Add("/house info");
                    terms.Add("/house scan");
                    if (args.Player.Group.HasPermission(HouseRegionsPlugin.HousingMasterPermission))
                        terms.Add("/house summary");
                    if (args.Player.Group.HasPermission(HouseRegionsPlugin.DefinePermission))
                    {
                        terms.Add("/house define");
                        terms.Add("/house resize");
                    }
                    if (args.Player.Group.HasPermission(HouseRegionsPlugin.DeletePermission))
                        terms.Add("/house delete");
                    if (args.Player.Group.HasPermission(HouseRegionsPlugin.SharePermission))
                    {
                        terms.Add("/house setowner");
                        terms.Add("/house share");
                        terms.Add("/house unshare");
                    }
                    if (args.Player.Group.HasPermission(HouseRegionsPlugin.ShareWithGroupsPermission))
                    {
                        terms.Add("/house sharegroup");
                        terms.Add("/house unsharegroup");
                    }
                    if (args.Player.Group.HasPermission(HouseRegionsPlugin.CfgPermission))
                        terms.Add("/house reloadconfig");

                    List<string> lines = PaginationTools.BuildLinesFromTerms(terms);
                    PaginationTools.SendPage(args.Player, pageNumber, lines, new PaginationTools.Settings
                    {
                        HeaderFormat = "House Commands (Page {0} of {1})",
                        LineTextColor = Color.LightGray,
                    });

                    return true;
                }
                case "summary":
                    HouseSummaryCommand_Exec(args);
                    return true;
                case "info":
                    HouseInfoCommand_Exec(args);
                    return true;
                case "scan":
                    HouseScanCommand_Exec(args);
                    return true;
                case "define":
                case "def":
                    if (!args.Player.Group.HasPermission(HouseRegionsPlugin.DefinePermission))
                    {
                        args.Player.SendErrorMessage("You do not have the necessary permission to do that.");
                        return true;
                    }
                    HouseDefineCommand_Exec(args);
                    return true;
                case "resize":
                    if (!args.Player.Group.HasPermission(HouseRegionsPlugin.DefinePermission))
                    {
                        args.Player.SendErrorMessage("You do not have the necessary permission to do that.");
                        return true;
                    }
                    HouseResizeCommand_Exec(args);
                    return true;
                case "delete":
                case "del":
                    if (!args.Player.Group.HasPermission(HouseRegionsPlugin.DeletePermission))
                    {
                        args.Player.SendErrorMessage("You do not have the necessary permission to do that.");
                        return true;
                    }
                    HouseDeleteCommand_Exec(args);
                    return true;
                case "setowner":
                    if (!args.Player.Group.HasPermission(HouseRegionsPlugin.SharePermission))
                    {
                        args.Player.SendErrorMessage("You do not have the necessary permission to do that.");
                        return true;
                    }
                    HouseSetOwnerCommand_Exec(args);
                    return true;
                case "shareuser":
                case "share":
                    if (!args.Player.Group.HasPermission(HouseRegionsPlugin.SharePermission))
                    {
                        args.Player.SendErrorMessage("You do not have the necessary permission to do that.");
                        return true;
                    }
                    HouseShareCommand_Exec(args);
                    return true;
                case "unshareuser":
                case "unshare":
                    if (!args.Player.Group.HasPermission(HouseRegionsPlugin.SharePermission))
                    {
                        args.Player.SendErrorMessage("You do not have the necessary permission to do that.");
                        return true;
                    }
                    HouseUnshareCommand_Exec(args);
                    return true;
                case "sharegroup":
                case "shareg":
                    if (!args.Player.Group.HasPermission(HouseRegionsPlugin.ShareWithGroupsPermission))
                    {
                        args.Player.SendErrorMessage("You do not have the necessary permission to do that.");
                        return true;
                    }
                    HouseShareGroupCommand_Exec(args);
                    return true;
                case "unsharegroup":
                case "unshareg":
                    if (!args.Player.Group.HasPermission(HouseRegionsPlugin.ShareWithGroupsPermission))
                    {
                        args.Player.SendErrorMessage("You do not have the necessary permission to do that.");
                        return true;
                    }
                    HouseUnshareGroupCommand_Exec(args);
                    return true;
                case "reloadconfiguration":
                case "reloadconfig":
                case "reloadcfg":
                {
                    if (!args.Player.Group.HasPermission(HouseRegionsPlugin.CfgPermission))
                    {
                        args.Player.SendErrorMessage("You do not have the necessary permission to do that.");
                        return true;
                    }

                    if (args.Parameters.Count == 2 && args.Parameters[1].Equals("help", StringComparison.InvariantCultureIgnoreCase))
                    {
                        args.Player.SendMessage("Command reference for /house reloadconfiguration (Page 1 of 1)", Color.Lime);
                        args.Player.SendMessage("/house reloadconfiguration|reloadconfig|reloadcfg", Color.White);
                        args.Player.SendMessage("Reloads House Region's configuration file and applies all new settings.", Color.LightGray);
                        return true;
                    }

                    _trace.WriteLineInfo("Reloading configuration file.");
                    try
                    {
                        _config = _reloadConfigurationCallback()!;
                        _trace.WriteLineInfo("Configuration file successfully reloaded.");

                        if (args.Player != TSPlayer.Server)
                            args.Player.SendSuccessMessage("Configuration file successfully reloaded.");
                    }
                    catch (Exception ex)
                    {
                        _trace.WriteLineError(
                            "Reloading the configuration file failed. Keeping old configuration. Exception details:\n{0}", ex
                        );
                    }

                    return true;
                }
            }

            return false;
        }

        private void HouseSummaryCommand_Exec(CommandArgs args)
        {
            if (args == null || IsDisposed)
                return;

            int pageNumber = 1;
            if (args.Parameters.Count > 2)
            {
                if (args.Parameters[1].Equals("help", StringComparison.InvariantCultureIgnoreCase))
                {
                    HouseSummaryCommand_HelpCallback(args);
                    return;
                }

                args.Player.SendErrorMessage("Proper syntax: /house summary [page]");
                args.Player.SendInfoMessage("Type /house summary help to get more information about this command.");
                return;
            }

            var ownerHouses = new Dictionary<string, int>(TShock.Regions.Regions.Count);
            for (int i = 0; i < TShock.Regions.Regions.Count; i++)
            {
                Region tsRegion = TShock.Regions.Regions[i];
                string? owner;
                int dummy;
                if (!_housingManager.TryGetHouseRegionData(tsRegion.Name, out owner, out dummy))
                    continue;

                int houseCount;
                if (!ownerHouses.TryGetValue(owner!, out houseCount))
                    ownerHouses.Add(owner!, 1);
                else
                    ownerHouses[owner!] = houseCount + 1;
            }

            IEnumerable<string> ownerHousesTermSelector = ownerHouses.Select(
                pair => string.Concat(pair.Key, " (", pair.Value, ")")
            );

            PaginationTools.SendPage(
                args.Player, pageNumber, PaginationTools.BuildLinesFromTerms(ownerHousesTermSelector), new PaginationTools.Settings
                {
                    HeaderFormat = string.Format("House Owners ({0}/{1}):"),
                    FooterFormat = string.Format("Type /house summary {0} for more."),
                    NothingToDisplayString = "There are no house regions in this world."
                }
            );
        }

        private void HouseSummaryCommand_HelpCallback(CommandArgs args)
        {
            if (args == null || IsDisposed)
                return;

            int pageNumber;
            if (!PaginationTools.TryParsePageNumber(args.Parameters, 2, args.Player, out pageNumber))
                return;

            args.Player.SendMessage("Command reference for /house summary (Page 1 of 1)", Color.Lime);
            args.Player.SendMessage("/house summary [page]", Color.White);
            args.Player.SendMessage("Displays all house owners and the amount of house regions they own.", Color.LightGray);
        }

        private void HouseInfoCommand_Exec(CommandArgs args)
        {
            if (args == null || IsDisposed)
                return;

            int pageNumber = 1;
            if (args.Parameters.Count > 2)
            {
                if (args.Parameters[1].Equals("help", StringComparison.InvariantCultureIgnoreCase))
                {
                    HouseInfoCommand_HelpCallback(args);
                    return;
                }

                args.Player.SendErrorMessage("Proper syntax: /house info [page]");
                args.Player.SendInfoMessage("Type /house info help to get more information about this command.");
                return;
            }

            string? owner;
            Region? region;
            if (!TryGetHouseRegionAtPlayer(args.Player, out owner, out region))
                return;

            List<string> lines = new List<string>
            {
                "Owned by: " + owner
            };

            if (region!.AllowedIDs.Count > 0)
            {
                IEnumerable<string> sharedUsersSelector = region.AllowedIDs.Select(userId =>
                {
                    UserAccount? user = TShock.UserAccounts.GetUserAccountByID(userId);
                    if (user != null)
                        return user.Name;
                    else
                        return string.Concat("{ID: ", userId, "}");
                });

                List<string> extraLines = PaginationTools.BuildLinesFromTerms(sharedUsersSelector.Distinct());
                extraLines[0] = "Shared with: " + extraLines[0];
                lines.AddRange(extraLines);
            }
            else
            {
                lines.Add("House is not shared with any users.");
            }

            if (region.AllowedGroups.Count > 0)
            {
                List<string> extraLines = PaginationTools.BuildLinesFromTerms(region.AllowedGroups.Distinct());
                extraLines[0] = "Shared with groups: " + extraLines[0];
                lines.AddRange(extraLines);
            }
            else
            {
                lines.Add("House is not shared with any groups.");
            }

            PaginationTools.SendPage(
                args.Player, pageNumber, lines, new PaginationTools.Settings
                {
                    HeaderFormat = string.Format("Information About This House ({0}/{1}):"),
                    FooterFormat = string.Format("Type /house info {0} for more information.")
                }
            );

            SendAreaDottedFakeWiresTimed(args.Player, region.Area, 5000);
        }

        private void HouseInfoCommand_HelpCallback(CommandArgs args)
        {
            if (args == null || IsDisposed)
                return;

            int pageNumber;
            if (!PaginationTools.TryParsePageNumber(args.Parameters, 2, args.Player, out pageNumber))
                return;

            args.Player.SendMessage("Command reference for /house info (Page 1 of 1)", Color.Lime);
            args.Player.SendMessage("/house info [page]", Color.White);
            args.Player.SendMessage("Displays several information about the house at your current positon.", Color.LightGray);
            args.Player.SendMessage("Will also display the boundaries of the house by wires.", Color.LightGray);
        }

        private void HouseScanCommand_Exec(CommandArgs args)
        {
            if (args == null || IsDisposed)
                return;

            if (args.Parameters.Count > 1)
            {
                if (args.Parameters[1].Equals("help", StringComparison.InvariantCultureIgnoreCase))
                {
                    HouseScanCommand_HelpCallback(args);
                    return;
                }

                args.Player.SendErrorMessage("Proper syntax: /house scan");
                args.Player.SendInfoMessage("Type /house scan help to get more information about this command.");
                return;
            }

            Point playerLocation = new Point(args.Player.TileX, args.Player.TileY);
            List<Rectangle> houseAreasToDisplay = new List<Rectangle>(
                from r in TShock.Regions.Regions
                where Math.Sqrt(Math.Pow(playerLocation.X - r.Area.Center.X, 2) + Math.Pow(playerLocation.Y - r.Area.Center.Y, 2)) <= 200
                select r.Area
            );
            if (houseAreasToDisplay.Count == 0)
            {
                args.Player.SendSuccessMessage("There are no nearby house regions.");
                return;
            }

            foreach (Rectangle regionArea in houseAreasToDisplay)
                SendAreaDottedFakeWires(args.Player, regionArea);
            args.Player.SendInfoMessage("Hold a wire or wire tool to see all nearby house regions.");

            System.Threading.Timer? hideTimer = null;
            hideTimer = new System.Threading.Timer(state =>
            {
                foreach (Rectangle regionArea in houseAreasToDisplay)
                    SendAreaDottedFakeWires(args.Player, regionArea, false);

                hideTimer?.Dispose();
            },
            null, 10000, Timeout.Infinite
            );
        }

        private void HouseScanCommand_HelpCallback(CommandArgs args)
        {
            if (args == null || IsDisposed)
                return;

            int pageNumber;
            if (!PaginationTools.TryParsePageNumber(args.Parameters, 2, args.Player, out pageNumber))
                return;

            args.Player.SendMessage("Command reference for /house scan (Page 1 of 1)", Color.Lime);
            args.Player.SendMessage("/house scan", Color.White);
            args.Player.SendMessage("Displays all house region boundaries close to your character's position", Color.LightGray);
            args.Player.SendMessage("as wires.", Color.LightGray);
        }

        private void HouseDefineCommand_Exec(CommandArgs args)
        {
            if (args == null || IsDisposed)
                return;

            if (args.Parameters.Count > 1)
            {
                if (args.Parameters[1].Equals("help", StringComparison.InvariantCultureIgnoreCase))
                {
                    HouseDefineCommand_HelpCallback(args);
                    return;
                }

                args.Player.SendErrorMessage("Proper syntax: /house define");
                args.Player.SendInfoMessage("Type /house define help to get more help to this command.");
                return;
            }

            if (!args.Player.IsLoggedIn)
            {
                args.Player.SendErrorMessage("You have to be logged in in order to define houses.");
                return;
            }

            Point point1 = Point.Zero;
            Point point2 = Point.Zero;
            Rectangle houseArea = Rectangle.Empty;
            args.Player.SendMessage("First Mark", Color.IndianRed);
            args.Player.SendMessage("Mark the top left tile of your house by placing a wire using wrench", Color.MediumSpringGreen);
            args.Player.SendMessage("or by altering the tile otherwise.", Color.MediumSpringGreen);

            CommandInteraction interaction = StartOrResetCommandInteraction(args.Player, 60000);
            interaction.TileEditCallback = (playerLocal, editAction, editData, tileLocation, style) =>
            {
                if (editAction == GetDataHandlers.EditAction.KillWire2 || editAction == GetDataHandlers.EditAction.KillWire || editAction == GetDataHandlers.EditAction.KillWire3 || editAction == GetDataHandlers.EditAction.KillWire4)
                {
                    if (tileLocation == point1)
                    {
                        point1 = Point.Zero;

                        if (houseArea != Rectangle.Empty)
                            SendAreaDottedFakeWires(playerLocal, houseArea, false);

                        playerLocal.SendTileSquareCentered(tileLocation.X, tileLocation.Y, 1);

                        if (point2 != Point.Zero)
                            SendFakeWireCross(playerLocal, point2);

                        args.Player.SendMessage("First Mark", Color.IndianRed);
                        args.Player.SendMessage("Mark the top left tile of your house by placing a wire using wrench", Color.MediumSpringGreen);
                        args.Player.SendMessage("or by altering the tile otherwise.", Color.MediumSpringGreen);
                        args.Player.SendMessage(string.Empty, Color.MediumSpringGreen);
                        interaction.ResetTimer();
                    }
                    else if (tileLocation == point2)
                    {
                        point2 = Point.Zero;

                        if (houseArea != Rectangle.Empty)
                            SendAreaDottedFakeWires(playerLocal, houseArea, false);

                        playerLocal.SendTileSquareCentered(tileLocation.X, tileLocation.Y, 1);

                        if (point1 != Point.Zero)
                            SendFakeWireCross(playerLocal, point1);

                        args.Player.SendMessage("Second Mark", Color.IndianRed);
                        args.Player.SendMessage("Mark the bottom right tile of your house by placing a wire using wrench", Color.MediumSpringGreen);
                        args.Player.SendMessage("or by altering the tile otherwise.", Color.MediumSpringGreen);
                        args.Player.SendMessage(string.Empty, Color.MediumSpringGreen);
                        interaction.ResetTimer();
                    }
                    return new CommandInteractionResult { IsHandled = true, IsInteractionCompleted = false };
                }

                if (point1 == Point.Zero || point2 == Point.Zero)
                {
                    if (point1 == Point.Zero)
                        point1 = tileLocation;
                    else
                        point2 = tileLocation;

                    playerLocal.SendTileSquareCentered(tileLocation.X, tileLocation.Y, 1);
                    SendFakeWireCross(playerLocal, tileLocation);

                    if (point1 != Point.Zero && point2 != Point.Zero)
                    {
                        houseArea = new Rectangle(
                            Math.Min(point1.X, point2.X), Math.Min(point1.Y, point2.Y),
                            Math.Abs(point1.X - point2.X), Math.Abs(point1.Y - point2.Y)
                        );
                        SendAreaDottedFakeWires(playerLocal, houseArea);

                        args.Player.SendMessage("Final Mark", Color.IndianRed);
                        args.Player.SendMessage("Mark any point inside your house to accept, or any point outside the house to cancel.", Color.MediumSpringGreen);
                        args.Player.SendMessage(string.Empty, Color.MediumSpringGreen);
                    }
                    else
                    {
                        if (point2 == Point.Zero)
                        {
                            args.Player.SendMessage("Second Mark", Color.IndianRed);
                            args.Player.SendMessage("Mark the bottom right tile of your house by placing a wire using wrench", Color.MediumSpringGreen);
                            args.Player.SendMessage("or by altering the tile otherwise.", Color.MediumSpringGreen);
                            args.Player.SendMessage(string.Empty, Color.MediumSpringGreen);
                        }
                        else
                        {
                            args.Player.SendMessage("First Mark", Color.IndianRed);
                            args.Player.SendMessage("Mark the bottom right tile of your house by placing a wire using wrench", Color.MediumSpringGreen);
                            args.Player.SendMessage("or by altering the tile otherwise.", Color.MediumSpringGreen);
                            args.Player.SendMessage(string.Empty, Color.MediumSpringGreen);
                        }
                    }

                    interaction.ResetTimer();

                    return new CommandInteractionResult { IsHandled = true, IsInteractionCompleted = false };
                }
                else
                {
                    playerLocal.SendTileSquareCentered(point1.X, point1.Y, 1);
                    playerLocal.SendTileSquareCentered(point2.X, point2.Y, 1);
                    SendAreaDottedFakeWires(playerLocal, houseArea, false);
                    playerLocal.SendTileSquareCentered(tileLocation.X, tileLocation.Y, 1);

                    if (
                        tileLocation.X >= houseArea.Left && tileLocation.X <= houseArea.Right &&
                        tileLocation.Y >= houseArea.Top && tileLocation.Y <= houseArea.Bottom
                    )
                    {
                        try
                        {
                            if (houseArea.Width <= 0 || houseArea.Height <= 0)
                            {
                                playerLocal.SendErrorMessage("The house has to be at least one block high and wide.");
                            }
                            else
                            {
                                _housingManager.CreateHouseRegion(playerLocal, houseArea, true, true, true);
                                playerLocal.SendMessage("House was successfully created. Other players can no longer change blocks", Color.MediumSpringGreen);
                                playerLocal.SendMessage("inside the defined house region.", Color.MediumSpringGreen);
                            }
                        }
                        catch (InvalidHouseSizeException ex)
                        {
                            ExplainInvalidRegionSize(playerLocal, houseArea, ex.RestrictingConfig);
                        }
                        catch (HouseOverlapException)
                        {
                            if (_config.AllowTShockRegionOverlapping)
                            {
                                playerLocal.SendErrorMessage("The house would overlap with another house where you're not the owner of.");
                            }
                            else
                            {
                                playerLocal.SendErrorMessage("The house would overlap with another house where you're not the owner of or");
                                playerLocal.SendErrorMessage("it overlaps with a TShock region.");
                            }
                        }
                        catch (LimitEnforcementException)
                        {
                            playerLocal.SendErrorMessage(
                                "You have reached the maximum of {0} houses. Delete at least one of your other houses first.",
                                _config.MaxHousesPerUser
                            );
                        }
                    }
                    else
                    {
                        playerLocal.SendWarningMessage("Defining of house was aborted.");
                    }

                    return new CommandInteractionResult { IsHandled = true, IsInteractionCompleted = true };
                }
            };
            interaction.TimeExpiredCallback = (playerLocal) =>
            {
                playerLocal.SendErrorMessage("Waited too long. No house will be defined.");
            };
            interaction.AbortedCallback = (playerLocal) =>
            {
                if (point1 != Point.Zero)
                    playerLocal.SendTileSquareCentered(point1.X, point1.Y, 1);
                if (point2 != Point.Zero)
                    playerLocal.SendTileSquareCentered(point2.X, point2.Y, 1);
                if (houseArea != Rectangle.Empty)
                    SendAreaDottedFakeWires(playerLocal, houseArea, false);
            };
        }

        private void HouseDefineCommand_HelpCallback(CommandArgs args)
        {
            if (args == null || IsDisposed)
                return;

            int pageNumber;
            if (!PaginationTools.TryParsePageNumber(args.Parameters, 2, args.Player, out pageNumber))
                return;

            switch (pageNumber)
            {
                default:
                    args.Player.SendMessage("Command reference for /house define (Page 1 of 2)", Color.Lime);
                    args.Player.SendMessage("/house define|def", Color.White);
                    args.Player.SendMessage("Switches to house definition mode. You have to set two points in order to", Color.LightGray);
                    args.Player.SendMessage("create a new house. Write /house define, then hit the top left corner of your", Color.LightGray);
                    args.Player.SendMessage("house, after this hit the bottom right corner, then new house region will be defined.", Color.LightGray);
                    args.Player.SendMessage("NOTE: Using wrench to mark the house region corners is recommended, you can also", Color.IndianRed);
                    break;
                case 2:
                    args.Player.SendMessage("revoke already defined points by using wire cutter while in definition mode, try it!", Color.IndianRed);
                    args.Player.SendMessage("Already existing houses can always be resized by using /house resize later.", Color.LightGray);
                    break;
            }
        }

        private void HouseResizeCommand_Exec(CommandArgs args)
        {
            if (args == null || IsDisposed)
                return;

            Action invalidSyntax = () =>
            {
                args.Player.SendErrorMessage("Proper syntax: /house resize <up|down|left|right> <amount>");
                args.Player.SendInfoMessage("Type /house resize help to get more information about this command.");
            };

            if (args.Parameters.Count >= 2 && args.Parameters[1].Equals("help", StringComparison.InvariantCultureIgnoreCase))
            {
                HouseResizeCommand_HelpCallback(args);
                return;
            }

            Region? region;
            string? owner;
            if (!TryGetAccessibleHouseRegionAtPlayer(args.Player, out owner, out region))
                return;

            int amount;
            if (args.Parameters.Count < 3 || !int.TryParse(args.Parameters[args.Parameters.Count - 1], out amount))
            {
                invalidSyntax();
                return;
            }

            Rectangle newArea = region!.Area;
            List<int> directions = new List<int>();
            for (int i = 1; i < args.Parameters.Count - 1; i++)
            {
                switch (args.Parameters[i].ToLowerInvariant())
                {
                    case "up":
                    case "u":
                        newArea.Y -= amount;
                        newArea.Height += amount;
                        directions.Add(0);
                        break;
                    case "down":
                    case "d":
                        newArea.Height += amount;
                        directions.Add(2);
                        break;
                    case "left":
                    case "l":
                        newArea.X -= amount;
                        newArea.Width += amount;
                        directions.Add(3);
                        break;
                    case "right":
                    case "r":
                        newArea.Width += amount;
                        directions.Add(1);
                        break;
                }
            }

            if (newArea.Width < 0)
                newArea.Width = 1;
            if (newArea.Height < 0)
                newArea.Height = 1;

            Configuration.HouseSizeConfig restrictingSizeConfig;
            if (!_housingManager.CheckHouseRegionValidSize(newArea, out restrictingSizeConfig))
            {
                ExplainInvalidRegionSize(args.Player, newArea, restrictingSizeConfig);
                return;
            }

            if (_housingManager.CheckHouseRegionOverlap(owner!, newArea))
            {
                if (_config.AllowTShockRegionOverlapping)
                {
                    args.Player.SendErrorMessage("The house region would overlap either with another house not owned by you or");
                    args.Player.SendErrorMessage("with a TShock region.");
                }
                else
                {
                    args.Player.SendErrorMessage("The house region would overlap with another house not owned by you.");
                }

                return;
            }

            Rectangle oldArea = region.Area;
            region.Area = newArea;
            foreach (int direction in directions)
            {
                if (!ResizeRegion(region.Name, amount, direction))
                {
                    args.Player.SendErrorMessage("Internal error has occured.");
                    region.Area = oldArea;
                    return;
                }
            }

            args.Player.SendSuccessMessage("House was successfully resized.");
            SendAreaDottedFakeWires(args.Player, oldArea, false);
            SendAreaDottedFakeWiresTimed(args.Player, newArea, 2000);
        }

        private bool ResizeRegion(string regionName, int amount, int direction)
        {
            try
            {
                var method = typeof(RegionManager).GetMethod("ResizeRegion");
                if (method != null)
                {
                    return (bool)method.Invoke(TShock.Regions, new object[] { regionName, amount, direction })!;
                }
            }
            catch { }

            var region = TShock.Regions.GetRegionByName(regionName);
            if (region == null) return false;

            Rectangle newArea = region.Area;
            switch (direction)
            {
                case 0: newArea.Y -= amount; newArea.Height += amount; break;
                case 1: newArea.Width += amount; break;
                case 2: newArea.Height += amount; break;
                case 3: newArea.X -= amount; newArea.Width += amount; break;
            }

            TShock.DB.Query("UPDATE Regions SET X=@0, Y=@1, Width=@2, Height=@3 WHERE RegionName=@4 AND WorldID=@5",
                newArea.X, newArea.Y, newArea.Width, newArea.Height, regionName, Main.worldID.ToString());
            region.Area = newArea;
            return true;
        }

        private void HouseResizeCommand_HelpCallback(CommandArgs args)
        {
            if (args == null || IsDisposed)
                return;

            int pageNumber;
            if (!PaginationTools.TryParsePageNumber(args.Parameters, 2, args.Player, out pageNumber))
                return;

            switch (pageNumber)
            {
                default:
                    args.Player.SendMessage("Command reference for /house resize (Page 1 of 3)", Color.Lime);
                    args.Player.SendMessage("/house resize <u|d|l|r> <amount>", Color.White);
                    args.Player.SendMessage("Resizes the current house to one direction by the given amount.", Color.LightGray);
                    args.Player.SendMessage(string.Empty, Color.IndianRed);
                    args.Player.SendMessage("u|d|l|r = The directions to resize to (up, left, down, right).", Color.LightGray);
                    break;
                case 2:
                    args.Player.SendMessage("amount = The amount of tiles to expand, can also be negative to shrink", Color.LightGray);
                    args.Player.SendMessage(" the house region.", Color.LightGray);
                    args.Player.SendMessage(string.Empty, Color.IndianRed);
                    args.Player.SendMessage("NOTE: If you hold a wire or wire tool, then you can see the new boundaries", Color.IndianRed);
                    args.Player.SendMessage("of the house region after the resize.", Color.IndianRed);
                    break;
                case 3:
                    args.Player.SendMessage("NOTE: You have to own a house in order to resize it, just having", Color.IndianRed);
                    args.Player.SendMessage("build access is not sufficient.", Color.IndianRed);
                    break;
            }
        }

        private void HouseDeleteCommand_Exec(CommandArgs args)
        {
            if (args == null || IsDisposed)
                return;

            if (args.Parameters.Count > 1)
            {
                if (args.Parameters[1].Equals("help", StringComparison.InvariantCultureIgnoreCase))
                {
                    HouseDeleteCommand_HelpCallback(args);
                    return;
                }

                args.Player.SendErrorMessage("Proper syntax: /house delete");
                args.Player.SendInfoMessage("Type /house delete help to get more information about this command.");
                return;
            }

            Region? region;
            if (!TryGetAccessibleHouseRegionAtPlayer(args.Player, out region))
                return;

            if (!TShock.Regions.DeleteRegion(region!.Name))
            {
                args.Player.SendErrorMessage("Internal error has occured.");
                return;
            }

            args.Player.SendSuccessMessage("The house was successfully deleted.");
        }

        private void HouseDeleteCommand_HelpCallback(CommandArgs args)
        {
            if (args == null || IsDisposed)
                return;

            int pageNumber;
            if (!PaginationTools.TryParsePageNumber(args.Parameters, 2, args.Player, out pageNumber))
                return;

            args.Player.SendMessage("Command reference for /house delete (Page 1 of 1)", Color.Lime);
            args.Player.SendMessage("/house delete|del", Color.White);
            args.Player.SendMessage("Deletes the house region where your character currently stands in.", Color.LightGray);
            args.Player.SendMessage(string.Empty, Color.IndianRed);
            args.Player.SendMessage("NOTE: You have to own a house in order to remove it, just having", Color.IndianRed);
            args.Player.SendMessage("build access is not sufficient.", Color.IndianRed);
        }

        private void HouseSetOwnerCommand_Exec(CommandArgs args)
        {
            if (args == null || IsDisposed)
                return;

            if (args.Parameters.Count < 2)
            {
                args.Player.SendErrorMessage("Proper syntax: /house setowner <player>");
                args.Player.SendInfoMessage("Type /house setowner help to get more information about this command.");
                return;
            }

            string newOwnerRaw = args.ParamsToSingleString(1);
            if (newOwnerRaw.Equals("help", StringComparison.InvariantCultureIgnoreCase))
            {
                HouseSetOwnerCommand_HelpCallback(args);
                return;
            }

            UserAccount? tsUser;
            if (!TShockEx.MatchUserByPlayerName(newOwnerRaw, out tsUser, args.Player))
                return;

            Region? region;
            if (!TryGetAccessibleHouseRegionAtPlayer(args.Player, out region))
                return;

            if (tsUser!.Name == region!.Owner)
            {
                args.Player.SendErrorMessage($"{tsUser.Name} is already the owner of this region.");
                return;
            }

            Group? tsGroup = TShock.Groups.GetGroupByName(tsUser.Group);
            if (tsGroup == null)
            {
                args.Player.SendErrorMessage("The new owner's TShock group could not be determined.");
                return;
            }

            try
            {
                _housingManager.CreateHouseRegion(tsUser, tsGroup, region.Area, false, true, false);
            }
            catch (LimitEnforcementException)
            {
                args.Player.SendErrorMessage("The new owner of the house would exceed their house limit.");
                return;
            }
            catch (Exception ex)
            {
                args.Player.SendErrorMessage("Internal error has occured: " + ex.Message);
                return;
            }

            if (!TShock.Regions.DeleteRegion(region.Name))
            {
                args.Player.SendErrorMessage("Internal error has occured when deleting the old house region.");
                return;
            }

            args.Player.SendSuccessMessage($"The owner of this house has been set to \"{tsUser.Name}\" and all shared users and groups were deleted from it.");
        }

        private void HouseSetOwnerCommand_HelpCallback(CommandArgs args)
        {
            if (args == null || IsDisposed)
                return;

            int pageNumber;
            if (!PaginationTools.TryParsePageNumber(args.Parameters, 2, args.Player, out pageNumber))
                return;

            args.Player.SendMessage("Command reference for /house setowner (Page 1 of 1)", Color.Lime);
            args.Player.SendMessage("/house setowner <player>", Color.White);
            args.Player.SendMessage("Changes the owning user of the house at you character.", Color.LightGray);
            args.Player.SendMessage(string.Empty, Color.IndianRed);
            args.Player.SendMessage("NOTE: You have to own a house in order to change its owner, just having", Color.IndianRed);
            args.Player.SendMessage("build access is not sufficient.", Color.IndianRed);
        }

        private void HouseShareCommand_Exec(CommandArgs args)
        {
            if (args == null || IsDisposed)
                return;

            if (args.Parameters.Count < 2)
            {
                args.Player.SendErrorMessage("Proper syntax: /house share <user>");
                args.Player.SendInfoMessage("Type /house share help to get more information about this command.");
                return;
            }

            string shareTargetRaw = args.ParamsToSingleString(1);
            if (shareTargetRaw.Equals("help", StringComparison.InvariantCultureIgnoreCase))
            {
                HouseShareCommand_HelpCallback(args);
                return;
            }

            UserAccount? tsUser;
            if (!TShockEx.MatchUserByPlayerName(shareTargetRaw, out tsUser, args.Player))
                return;

            Region? region;
            if (!TryGetAccessibleHouseRegionAtPlayer(args.Player, out region))
                return;

            if (!TShock.Regions.AddNewUser(region!.Name, tsUser!.Name))
            {
                args.Player.SendErrorMessage("Internal error has occured.");
                return;
            }

            args.Player.SendSuccessMessage("User \"{0}\" has build access to this house now.", tsUser.Name);
        }

        private void HouseShareCommand_HelpCallback(CommandArgs args)
        {
            if (args == null || IsDisposed)
                return;

            int pageNumber;
            if (!PaginationTools.TryParsePageNumber(args.Parameters, 2, args.Player, out pageNumber))
                return;

            args.Player.SendMessage("Command reference for /house share (Page 1 of 1)", Color.Lime);
            args.Player.SendMessage("/house shareuser|share <user>", Color.White);
            args.Player.SendMessage("Grants build access to another user for the house at you character.", Color.LightGray);
            args.Player.SendMessage(string.Empty, Color.IndianRed);
            args.Player.SendMessage("NOTE: You have to own a house in order to share it, just having", Color.IndianRed);
            args.Player.SendMessage("build access is not sufficient.", Color.IndianRed);
        }

        private void HouseUnshareCommand_Exec(CommandArgs args)
        {
            if (args == null || IsDisposed)
                return;

            if (args.Parameters.Count < 2)
            {
                args.Player.SendErrorMessage("Proper syntax: /house unshare <user>");
                args.Player.SendInfoMessage("Type /house unshare help to get more information about this command.");
                return;
            }

            string shareTargetRaw = args.ParamsToSingleString(1);
            if (shareTargetRaw.Equals("help", StringComparison.InvariantCultureIgnoreCase))
            {
                HouseUnshareCommand_HelpCallback(args);
                return;
            }

            UserAccount? tsUser;
            if (!TShockEx.MatchUserByPlayerName(shareTargetRaw, out tsUser, args.Player))
                return;

            Region? region;
            if (!TryGetAccessibleHouseRegionAtPlayer(args.Player, out region))
                return;

            if (!TShock.Regions.RemoveUser(region!.Name, tsUser!.Name))
            {
                args.Player.SendErrorMessage("Internal error has occured.");
                return;
            }

            args.Player.SendSuccessMessage("User \"{0}\" has no more build access to this house anymore.", tsUser.Name);
        }

        private void HouseUnshareCommand_HelpCallback(CommandArgs args)
        {
            if (args == null || IsDisposed)
                return;

            int pageNumber;
            if (!PaginationTools.TryParsePageNumber(args.Parameters, 2, args.Player, out pageNumber))
                return;

            args.Player.SendMessage("Command reference for /house share (Page 1 of 1)", Color.Lime);
            args.Player.SendMessage("/house unshareuser|unshare <user>", Color.White);
            args.Player.SendMessage("Removes build access of another user for the house at you character.", Color.LightGray);
            args.Player.SendMessage(string.Empty, Color.IndianRed);
            args.Player.SendMessage("NOTE: You have to own a house in order to alter shares of it,", Color.IndianRed);
            args.Player.SendMessage("just having build access is not sufficient.", Color.IndianRed);
        }

        private void HouseShareGroupCommand_Exec(CommandArgs args)
        {
            if (args == null || IsDisposed)
                return;

            if (args.Parameters.Count < 2)
            {
                args.Player.SendErrorMessage("Proper syntax: /house sharegroup <group>");
                args.Player.SendInfoMessage("Type /house sharegroup help to get more information about this command.");
                return;
            }

            string shareTargetRaw = args.ParamsToSingleString(1);
            if (shareTargetRaw.Equals("help", StringComparison.InvariantCultureIgnoreCase))
            {
                HouseShareGroupCommand_HelpCallback(args);
                return;
            }

            Group? tsGroup = TShock.Groups.GetGroupByName(shareTargetRaw);
            if (tsGroup == null)
            {
                args.Player.SendErrorMessage("A group with the name \"{0}\" does not exist.", shareTargetRaw);
                return;
            }

            Region? region;
            if (!TryGetAccessibleHouseRegionAtPlayer(args.Player, out region))
                return;

            if (!TShock.Regions.AllowGroup(region!.Name, tsGroup.Name))
            {
                args.Player.SendErrorMessage("Internal error has occured.");
                return;
            }

            args.Player.SendSuccessMessage("All users of group \"{0}\" have build access to this house now.", tsGroup.Name);
        }

        private void HouseShareGroupCommand_HelpCallback(CommandArgs args)
        {
            if (args == null || IsDisposed)
                return;

            int pageNumber;
            if (!PaginationTools.TryParsePageNumber(args.Parameters, 2, args.Player, out pageNumber))
                return;

            args.Player.SendMessage("Command reference for /house sharegroup (Page 1 of 1)", Color.Lime);
            args.Player.SendMessage("/house sharegroup|shareg <group>", Color.White);
            args.Player.SendMessage("Grants build access to all users in a TShock group for the house at you character.", Color.LightGray);
            args.Player.SendMessage(string.Empty, Color.IndianRed);
            args.Player.SendMessage("NOTE: You have to own a house in order to share it, just having", Color.IndianRed);
            args.Player.SendMessage("build access is not sufficient.", Color.IndianRed);
        }

        private void HouseUnshareGroupCommand_Exec(CommandArgs args)
        {
            if (args == null || IsDisposed)
                return;

            if (args.Parameters.Count < 2)
            {
                args.Player.SendErrorMessage("Proper syntax: /house unsharegroup <group>");
                args.Player.SendInfoMessage("Type /house unsharegroup help to get more information about this command.");
                return;
            }

            string shareTargetRaw = args.ParamsToSingleString(1);
            if (shareTargetRaw.Equals("help", StringComparison.InvariantCultureIgnoreCase))
            {
                HouseUnshareGroupCommand_HelpCallback(args);
                return;
            }

            Group? tsGroup = TShock.Groups.GetGroupByName(shareTargetRaw);
            if (tsGroup == null)
            {
                args.Player.SendErrorMessage("A group with the name \"{0}\" does not exist.", shareTargetRaw);
                return;
            }

            Region? region;
            if (!TryGetAccessibleHouseRegionAtPlayer(args.Player, out region))
                return;

            if (!TShock.Regions.RemoveGroup(region!.Name, tsGroup.Name))
            {
                args.Player.SendErrorMessage("Internal error has occured.");
                return;
            }

            args.Player.SendSuccessMessage("Users of group \"{0}\" have no more build access to this house anymore.", tsGroup.Name);
        }

        private void HouseUnshareGroupCommand_HelpCallback(CommandArgs args)
        {
            if (args == null || IsDisposed)
                return;

            int pageNumber;
            if (!PaginationTools.TryParsePageNumber(args.Parameters, 2, args.Player, out pageNumber))
                return;

            args.Player.SendMessage("Command reference for /house unsharegroup (Page 1 of 1)", Color.Lime);
            args.Player.SendMessage("/house unsharegroup|unshareg <group>", Color.White);
            args.Player.SendMessage("Removes build access of all users in a TShock group for the house at you character.", Color.LightGray);
            args.Player.SendMessage(string.Empty, Color.IndianRed);
            args.Player.SendMessage("NOTE: You have to own a house in order to alter shares of it,", Color.IndianRed);
            args.Player.SendMessage("just having build access is not sufficient.", Color.IndianRed);
        }

        public bool TryGetHouseRegionAtPlayer(TSPlayer player, out string? owner, out Region? region)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));

            int dummy;
            if (!_housingManager.TryGetHouseRegionAtPlayer(player, out owner, out dummy, out region))
            {
                player.SendErrorMessage("There's no house on your current position.");
                return false;
            }

            return true;
        }

        public bool TryGetAccessibleHouseRegionAtPlayer(TSPlayer player, out string? owner, out Region? region)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));

            if (!TryGetHouseRegionAtPlayer(player, out owner, out region))
                return false;

            if (player.Account?.Name != owner && !player.Group.HasPermission(HouseRegionsPlugin.HousingMasterPermission))
            {
                player.SendErrorMessage("You're not the owner of this house.");
                return false;
            }

            return true;
        }

        private bool TryGetAccessibleHouseRegionAtPlayer(TSPlayer player, out Region? region)
        {
            string? dummy;
            return TryGetAccessibleHouseRegionAtPlayer(player, out dummy, out region);
        }

        private void SendFakeTileWire(TSPlayer player, Point tileLocation)
        {
            var tile = Main.tile[tileLocation.X, tileLocation.Y];
            if (tile.Wire2())
                return;

            tile.Wire2(true);
            player.SendTileSquareCentered(tileLocation.X, tileLocation.Y, 1);
            tile.Wire2(false);
        }

        private void SendAreaDottedFakeWiresTimed(TSPlayer player, Rectangle area, int timeMs)
        {
            SendAreaDottedFakeWires(player, area);

            System.Threading.Timer? hideTimer = null;
            hideTimer = new System.Threading.Timer(state =>
            {
                SendAreaDottedFakeWires(player, area, false);
                hideTimer?.Dispose();
            },
            null, timeMs, Timeout.Infinite
            );
        }

        private void SendAreaDottedFakeWires(TSPlayer player, Rectangle area, bool setOrUnset = true)
        {
            foreach (Point boundaryPoint in EnumerateRegionBoundaries(area))
                if (((boundaryPoint.X + boundaryPoint.Y) & 1) == 0)
                    if (setOrUnset)
                        SendFakeTileWire(player, boundaryPoint);
                    else
                        player.SendTileSquareCentered(boundaryPoint.X, boundaryPoint.Y, 1);
        }

        private static IEnumerable<Point> EnumerateRegionBoundaries(Rectangle area)
        {
            for (int x = area.Left; x <= area.Right; x++)
            {
                yield return new Point(x, area.Top);
                yield return new Point(x, area.Bottom);
            }
            for (int y = area.Top + 1; y < area.Bottom; y++)
            {
                yield return new Point(area.Left, y);
                yield return new Point(area.Right, y);
            }
        }

        private void SendFakeWireCross(TSPlayer player, Point crossLocation)
        {
            SendFakeTileWire(player, crossLocation);
            SendFakeTileWire(player, crossLocation.OffsetEx(-1, 0));
            SendFakeTileWire(player, crossLocation.OffsetEx(1, 0));
            SendFakeTileWire(player, crossLocation.OffsetEx(0, -1));
            SendFakeTileWire(player, crossLocation.OffsetEx(0, 1));
        }

        private void ExplainInvalidRegionSize(TSPlayer toPlayer, Rectangle area, Configuration.HouseSizeConfig restrictingConfig)
        {
            if (restrictingConfig.Equals(_config.MinSize))
            {
                toPlayer.SendErrorMessage("This region has no valid house size, it's too small:");
                toPlayer.SendErrorMessage("Min width: {0} (you've tried to set {1}).", restrictingConfig.Width, area.Width);
                toPlayer.SendErrorMessage("Min height: {0} (you've tried to set {1}).", restrictingConfig.Height, area.Height);
                toPlayer.SendErrorMessage("Min total blocks: {0} (you've tried to set {1}).", restrictingConfig.TotalTiles, area.Width * area.Height);
            }
            else
            {
                toPlayer.SendErrorMessage("This region has no valid house size, it's too large:");
                toPlayer.SendErrorMessage("Max width: {0} (you've tried to set {1}).", restrictingConfig.Width, area.Width);
                toPlayer.SendErrorMessage("Max height: {0} (you've tried to set {1}).", restrictingConfig.Height, area.Height);
                toPlayer.SendErrorMessage("Max total blocks: {0} (you've tried to set {1}).", restrictingConfig.TotalTiles, area.Width * area.Height);
            }
        }

        public bool HandleTileEdit(TSPlayer player, GetDataHandlers.EditAction editAction, short editData, Point location, short style)
        {
            if (!_activeInteractions.TryGetValue(player.Index, out var interaction))
                return false;

            if (DateTime.UtcNow - interaction.StartTime > interaction.Timeout)
            {
                interaction.TimeExpiredCallback?.Invoke(player);
                _activeInteractions.Remove(player.Index);
                return false;
            }

            var result = interaction.TileEditCallback?.Invoke(player, editAction, editData, location, style);
            if (result?.IsHandled == true)
            {
                if (result.IsInteractionCompleted)
                    _activeInteractions.Remove(player.Index);
                return true;
            }

            return false;
        }

        private CommandInteraction StartOrResetCommandInteraction(TSPlayer player, int timeoutMs)
        {
            StopInteraction(player);
            var interaction = new CommandInteraction(player, TimeSpan.FromMilliseconds(timeoutMs));
            _activeInteractions[player.Index] = interaction;
            return interaction;
        }

        public void StopInteraction(TSPlayer player)
        {
            if (_activeInteractions.TryGetValue(player.Index, out var interaction))
            {
                interaction.AbortedCallback?.Invoke(player);
                _activeInteractions.Remove(player.Index);
            }
        }

        public class CommandInteraction
        {
            public TSPlayer Player { get; }
            public DateTime StartTime { get; private set; }
            public TimeSpan Timeout { get; }

            public Func<TSPlayer, GetDataHandlers.EditAction, short, Point, short, CommandInteractionResult>? TileEditCallback { get; set; }
            public Action<TSPlayer>? TimeExpiredCallback { get; set; }
            public Action<TSPlayer>? AbortedCallback { get; set; }

            public CommandInteraction(TSPlayer player, TimeSpan timeout)
            {
                Player = player;
                Timeout = timeout;
                StartTime = DateTime.UtcNow;
            }

            public void ResetTimer() => StartTime = DateTime.UtcNow;
        }

        public class CommandInteractionResult
        {
            public bool IsHandled { get; set; }
            public bool IsInteractionCompleted { get; set; }
        }

        private bool _isDisposed;
        public bool IsDisposed => _isDisposed;

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            foreach (var interaction in _activeInteractions.Values)
                interaction.AbortedCallback?.Invoke(interaction.Player);
            _activeInteractions.Clear();
        }
    }

    public static class PointExtensions
    {
        public static Point OffsetEx(this Point point, int offsetX, int offsetY) =>
            new Point(point.X + offsetX, point.Y + offsetY);
    }

    public static class CommandArgsExtensions
    {
        public static string ParamsToSingleString(this CommandArgs args, int index)
        {
            return string.Join(" ", args.Parameters.Skip(index));
        }
    }

    public static class TShockEx
    {
        public static bool MatchUserByPlayerName(string search, out UserAccount? account, TSPlayer player)
        {
            account = TShock.UserAccounts.GetUserAccountByName(search);
            if (account == null)
            {
                player.SendErrorMessage($"No user found matching \"{search}\".");
                return false;
            }
            return true;
        }
    }
}
