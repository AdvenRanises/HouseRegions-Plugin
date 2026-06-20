using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using TShockAPI;
using TShockAPI.DB;

namespace HouseRegions
{
    public class HousingManager
    {
        private const string HouseRegionNameAppendix = "*H_";
        private const char HouseRegionNameNumberSeparator = ':';

        private Configuration _config;

        public PluginTrace Trace { get; private set; }

        public Configuration Config
        {
            get => _config;
            set => _config = value ?? throw new ArgumentNullException(nameof(value));
        }

        public HousingManager(PluginTrace trace, Configuration config)
        {
            Trace = trace ?? throw new ArgumentNullException(nameof(trace));
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        public void CreateHouseRegion(TSPlayer player, Rectangle area, bool checkOverlaps = true, bool checkPermissions = false, bool checkDefinePermission = false)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));
            if (!player.IsLoggedIn) throw new PlayerNotLoggedInException();

            CreateHouseRegion(player.Account, player.Group, area, checkOverlaps, checkPermissions, checkDefinePermission);
        }

        public void CreateHouseRegion(UserAccount user, Group group, Rectangle area, bool checkOverlaps = true, bool checkPermissions = false, bool checkDefinePermission = false)
        {
            if (user == null) throw new ArgumentNullException(nameof(user));
            if (group == null) throw new ArgumentNullException(nameof(group));
            if (!(area.Width > 0 && area.Height > 0)) throw new ArgumentException("Area must have positive width and height.", nameof(area));

            int maxHouses = int.MaxValue;
            if (checkPermissions)
            {
                if (!group.HasPermission(HouseRegionsPlugin.DefinePermission))
                    throw new MissingPermissionException(HouseRegionsPlugin.DefinePermission);

                if (!group.HasPermission(HouseRegionsPlugin.NoLimitsPermission))
                {
                    if (_config.MaxHousesPerUser > 0)
                        maxHouses = _config.MaxHousesPerUser;

                    if (!CheckHouseRegionValidSize(area, out Configuration.HouseSizeConfig restrictingSizeConfig))
                        throw new InvalidHouseSizeException(restrictingSizeConfig);
                }
            }

            if (checkOverlaps && CheckHouseRegionOverlap(user.Name, area))
                throw new HouseOverlapException();

            int houseIndex;
            string? houseName = null;
            for (houseIndex = 1; houseIndex <= maxHouses; houseIndex++)
            {
                houseName = ToHouseRegionName(user.Name, houseIndex);
                if (TShock.Regions.GetRegionByName(houseName) == null)
                    break;
            }
            if (houseIndex > maxHouses)
                throw new LimitEnforcementException("Max amount of houses reached.");

            if (!TShock.Regions.AddRegion(
                area.X, area.Y, area.Width, area.Height, houseName!, user.Name, Main.worldID.ToString(),
                _config.DefaultZIndex))
                throw new InvalidOperationException("House region might already exist.");
        }

        public string ToHouseRegionName(string owner, int houseIndex)
        {
            if (string.IsNullOrWhiteSpace(owner)) throw new ArgumentException("Owner cannot be empty.", nameof(owner));
            if (houseIndex <= 0) throw new ArgumentOutOfRangeException(nameof(houseIndex));

            return string.Concat(
                HouseRegionNameAppendix, owner, HouseRegionNameNumberSeparator, houseIndex
            );
        }

        public bool TryGetHouseRegionAtPlayer(TSPlayer player, out string? owner, out int houseIndex, out Region? region)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));

            for (int i = 0; i < TShock.Regions.Regions.Count; i++)
            {
                region = TShock.Regions.Regions[i];
                if (region.InArea(player.TileX, player.TileY) && TryGetHouseRegionData(region.Name, out owner, out houseIndex))
                    return true;
            }

            owner = null;
            region = null;
            houseIndex = -1;
            return false;
        }

        public bool TryGetHouseRegionData(string regionName, out string? owner, out int houseIndex)
        {
            if (regionName == null) throw new ArgumentNullException(nameof(regionName));

            owner = null;
            houseIndex = -1;

            if (!regionName.StartsWith(HouseRegionNameAppendix))
                return false;

            int separatorIndex = regionName.LastIndexOf(HouseRegionNameNumberSeparator);
            if (
                separatorIndex == -1 || separatorIndex == regionName.Length - 1 ||
                separatorIndex <= HouseRegionNameAppendix.Length
            )
                return false;

            string houseIndexRaw = regionName.Substring(separatorIndex + 1);
            if (!int.TryParse(houseIndexRaw, out houseIndex))
                return false;

            owner = regionName.Substring(HouseRegionNameAppendix.Length, separatorIndex - HouseRegionNameAppendix.Length);
            return true;
        }

        public void SetHouseRegionOwner(Region region, string newOwnerName)
        {
            if (region == null) throw new ArgumentNullException(nameof(region));
            if (newOwnerName == null) throw new ArgumentNullException(nameof(newOwnerName));

            string? currentOwner;
            int index;
            if (!TryGetHouseRegionData(region.Name, out currentOwner, out index))
                throw new ArgumentException("The given region is not a house region.", nameof(region));

            if (currentOwner == newOwnerName)
                return;

            string newRegionName = ToHouseRegionName(newOwnerName, index);
            TShock.DB.Query("UPDATE Regions SET RegionName=@0,Owner=@1 WHERE RegionName=@2 AND WorldID=@3", newRegionName, newOwnerName, region.Name, Main.worldID.ToString());
            region.Name = newRegionName;
            region.Owner = newOwnerName;
        }

        public bool IsHouseRegion(string regionName)
        {
            string? dummy;
            int dummy2;
            return TryGetHouseRegionData(regionName, out dummy, out dummy2);
        }

        public bool CheckHouseRegionOverlap(string owner, Rectangle regionArea)
        {
            for (int i = 0; i < TShock.Regions.Regions.Count; i++)
            {
                Region tsRegion = TShock.Regions.Regions[i];
                if (
                    regionArea.Right < tsRegion.Area.Left || regionArea.X > tsRegion.Area.Right ||
                    regionArea.Bottom < tsRegion.Area.Top || regionArea.Y > tsRegion.Area.Bottom
                )
                    continue;

                string? houseOwner;
                int houseIndex;
                if (!TryGetHouseRegionData(tsRegion.Name, out houseOwner, out houseIndex))
                {
                    if (_config.AllowTShockRegionOverlapping || tsRegion.Name.StartsWith("*"))
                        continue;

                    return true;
                }
                if (houseOwner == owner)
                    continue;

                return true;
            }

            return false;
        }

        public bool CheckHouseRegionValidSize(Rectangle regionArea, out Configuration.HouseSizeConfig problematicConfig)
        {
            int areaTotalTiles = regionArea.Width * regionArea.Height;

            problematicConfig = _config.MinSize;
            if (
                regionArea.Width < _config.MinSize.Width || regionArea.Height < _config.MinSize.Height ||
                areaTotalTiles < _config.MinSize.TotalTiles
            )
                return false;

            problematicConfig = _config.MaxSize;
            if (
                regionArea.Width > _config.MaxSize.Width || regionArea.Height > _config.MaxSize.Height ||
                areaTotalTiles > _config.MaxSize.TotalTiles
            )
                return false;

            problematicConfig = default;
            return true;
        }

        public bool CheckHouseRegionValidSize(Rectangle regionArea)
        {
            Configuration.HouseSizeConfig dummy;
            return CheckHouseRegionValidSize(regionArea, out dummy);
        }
    }
}
