using System;
using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.Hooks;

namespace HouseRegions
{
    [ApiVersion(2, 1)]
    public class HouseRegionsPlugin : TerrariaPlugin
    {
        public const string DefinePermission = "houseregions.define";
        public const string DeletePermission = "houseregions.delete";
        public const string SharePermission = "houseregions.share";
        public const string ShareWithGroupsPermission = "houseregions.sharewithgroups";
        public const string NoLimitsPermission = "houseregions.nolimits";
        public const string HousingMasterPermission = "houseregions.housingmaster";
        public const string CfgPermission = "houseregions.cfg";

        public static HouseRegionsPlugin? LatestInstance { get; private set; }

        public static string DataDirectory => Path.Combine(TShock.SavePath, "House Regions");
        public static string ConfigFilePath => Path.Combine(DataDirectory, "Config.xml");

        private bool _hooksEnabled;
        private bool _isDisposed;
        internal PluginTrace Trace { get; }
        public Configuration Config { get; private set; } = null!;
        public HousingManager HousingManager { get; private set; } = null!;
        private UserInteractionHandler? _userInteractionHandler;

        public override string Name => "House Regions";
        public override Version Version => typeof(HouseRegionsPlugin).Assembly.GetName().Version!;
        public override string Author => "CoderCow (modernized)";
        public override string Description => "A simple TShock regions wrapper for player housing purposes.";

        public HouseRegionsPlugin(Main game) : base(game)
        {
            Trace = new PluginTrace("[Housing] ");
            LatestInstance = this;
            Order = 1;
        }

        public override void Initialize()
        {
            ServerApi.Hooks.GamePostInitialize.Register(this, OnGamePostInitialize);
        }

        private void OnGamePostInitialize(EventArgs e)
        {
            ServerApi.Hooks.GamePostInitialize.Deregister(this, OnGamePostInitialize);

            if (!Directory.Exists(DataDirectory))
                Directory.CreateDirectory(DataDirectory);

            if (!InitConfig())
                return;

            HousingManager = new HousingManager(Trace, Config);
            _userInteractionHandler = new UserInteractionHandler(Trace, Config, HousingManager, ReloadConfiguration);

            GetDataHandlers.TileEdit += OnTileEdit;
            _hooksEnabled = true;
        }

        private bool InitConfig()
        {
            if (File.Exists(ConfigFilePath))
            {
                try
                {
                    Config = Configuration.Read(ConfigFilePath);
                }
                catch (Exception ex)
                {
                    Trace.WriteLineError($"Reading the configuration file failed. This plugin will be disabled. Exception details:\n{ex}");
                    Dispose();
                    return false;
                }
            }
            else
            {
                Config = new Configuration();
            }

            return true;
        }

        private Configuration? ReloadConfiguration()
        {
            if (_isDisposed)
                return null;

            Config = Configuration.Read(ConfigFilePath);
            HousingManager.Config = Config;

            return Config;
        }

        private void OnTileEdit(object? sender, GetDataHandlers.TileEditEventArgs e)
        {
            if (_isDisposed || !_hooksEnabled || e.Handled || _userInteractionHandler == null)
                return;

            e.Handled = _userInteractionHandler.HandleTileEdit(e.Player, e.Action, e.EditData, new Point(e.X, e.Y), e.Style);
        }

        protected override void Dispose(bool disposing)
        {
            if (_isDisposed)
                return;

            if (disposing)
            {
                GetDataHandlers.TileEdit -= OnTileEdit;
                _userInteractionHandler?.Dispose();
                ServerApi.Hooks.GamePostInitialize.Deregister(this, OnGamePostInitialize);
            }

            base.Dispose(disposing);
            _isDisposed = true;
        }
    }
}
