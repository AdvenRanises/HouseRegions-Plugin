using System;
using System.IO;
using System.Xml;
using System.Xml.Schema;

namespace HouseRegions
{
    public class Configuration
    {
        #region Nested: HouseSizeConfig
        public struct HouseSizeConfig
        {
            public int TotalTiles { get; private set; }
            public int Width { get; private set; }
            public int Height { get; private set; }

            public static HouseSizeConfig FromXmlElement(XmlElement rootElement)
            {
                if (rootElement == null) throw new ArgumentNullException(nameof(rootElement));

                int totalTiles = int.Parse(rootElement["TotalTiles"]!.InnerText);
                int width = int.Parse(rootElement["Width"]!.InnerText);
                int height = int.Parse(rootElement["Height"]!.InnerText);

                return new HouseSizeConfig(totalTiles, width, height);
            }

            public HouseSizeConfig(int totalTiles, int width, int height) : this()
            {
                TotalTiles = totalTiles;
                Width = width;
                Height = height;
            }
        }
        #endregion

        public const string CurrentVersion = "1.0";

        public int MaxHousesPerUser { get; set; }
        public HouseSizeConfig MinSize { get; set; }
        public HouseSizeConfig MaxSize { get; set; }
        public bool AllowTShockRegionOverlapping { get; set; }
        public int DefaultZIndex { get; set; }

        public static Configuration Read(string filePath)
        {
            XmlReaderSettings configReaderSettings = new XmlReaderSettings
            {
                ValidationType = ValidationType.Schema,
                ValidationFlags = XmlSchemaValidationFlags.ProcessIdentityConstraints | XmlSchemaValidationFlags.ReportValidationWarnings
            };

            string configSchemaPath = Path.Combine(Path.GetDirectoryName(filePath)!, Path.GetFileNameWithoutExtension(filePath) + ".xsd");
            configReaderSettings.Schemas.Add(null, configSchemaPath);

            XmlDocument document = new XmlDocument();
            using (XmlReader configReader = XmlReader.Create(filePath, configReaderSettings))
                document.Load(configReader);

            XmlElement rootElement = document.DocumentElement!;
            string fileVersionRaw = rootElement.HasAttribute("Version") ? rootElement.GetAttribute("Version") : "1.0";

            if (fileVersionRaw != CurrentVersion)
            {
                throw new FormatException($"The configuration file is either outdated or too new. Expected version was: {CurrentVersion}. File version is: {fileVersionRaw}");
            }

            Configuration resultingConfig = new Configuration();
            resultingConfig.MaxHousesPerUser = int.Parse(rootElement["MaxHousesPerUser"]!.InnerText);
            resultingConfig.MinSize = HouseSizeConfig.FromXmlElement(rootElement["MinHouseSize"]!);
            resultingConfig.MaxSize = HouseSizeConfig.FromXmlElement(rootElement["MaxHouseSize"]!);
            resultingConfig.AllowTShockRegionOverlapping = bool.Parse(rootElement["AllowTShockRegionOverlapping"]!.InnerText);
            resultingConfig.DefaultZIndex = int.Parse(rootElement["DefaultZIndex"]!.InnerText);

            return resultingConfig;
        }
    }
}
