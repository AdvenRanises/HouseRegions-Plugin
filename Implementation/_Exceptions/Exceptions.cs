using System;

namespace HouseRegions
{
    public class HouseOverlapException : Exception
    {
        public HouseOverlapException() : base("The house would overlap with another house or region.") { }
        public HouseOverlapException(string message) : base(message) { }
    }

    public class InvalidHouseSizeException : Exception
    {
        public Configuration.HouseSizeConfig RestrictingConfig { get; }

        public InvalidHouseSizeException(Configuration.HouseSizeConfig restrictingConfig)
            : base("The size of the house does not match with the configured min / max settings.")
        {
            RestrictingConfig = restrictingConfig;
        }

        public InvalidHouseSizeException() : base("The size of the house does not match with the configured min / max settings.") { }
    }

    public class LimitEnforcementException : Exception
    {
        public LimitEnforcementException(string message) : base(message) { }
    }

    public class MissingPermissionException : Exception
    {
        public string Permission { get; }

        public MissingPermissionException(string permission)
            : base($"Missing permission: {permission}")
        {
            Permission = permission;
        }
    }

    public class PlayerNotLoggedInException : Exception
    {
        public PlayerNotLoggedInException() : base("Player is not logged in.") { }
    }
}
