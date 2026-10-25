namespace CameraMode
{
    /// <summary>
    /// Read by name through reflection by the F12 configuration manager; only the fields used
    /// here are declared. See https://github.com/BepInEx/BepInEx.ConfigurationManager
    /// </summary>
#pragma warning disable 0169, 0414, 0649
    internal sealed class ConfigurationManagerAttributes
    {
        public int? Order;
        public bool? IsAdvanced;
    }
#pragma warning restore 0169, 0414, 0649
}
