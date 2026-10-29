// Compile-check stubs of the BepInEx 5 API (signatures only).
using System;
namespace BepInEx
{
    [AttributeUsage(AttributeTargets.Class)] public class BepInPlugin : Attribute { public BepInPlugin(string guid, string name, string version) { } }
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)] public class BepInDependency : Attribute { public enum DependencyFlags { HardDependency = 1, SoftDependency = 2 } public BepInDependency(string guid, DependencyFlags f = DependencyFlags.HardDependency) { } }
    public abstract class BaseUnityPlugin : UnityEngine.MonoBehaviour { public Configuration.ConfigFile Config { get; } public Logging.ManualLogSource Logger { get; } public PluginInfo Info { get; } }
    public class PluginInfo { public string Location { get; } }
    public static class Paths { public static string BepInExRootPath, PluginPath, ConfigPath, GameRootPath, BepInExConfigPath; }
}
namespace BepInEx.Logging
{
    public enum LogLevel { None = 0, Fatal = 1, Error = 2, Warning = 4, Message = 8, Info = 16, Debug = 32, All = 63 }
    public class ManualLogSource { public ManualLogSource(string n) { } public void Log(LogLevel l, object d) { } public void LogInfo(object d) { } public void LogWarning(object d) { } public void LogError(object d) { } public void LogMessage(object d) { } public void LogDebug(object d) { } public void LogFatal(object d) { } }
    public static class Logger { public static ManualLogSource CreateLogSource(string n) { return null; } }
}
namespace BepInEx.Configuration
{
    public abstract class AcceptableValueBase { }
    public class AcceptableValueRange<T> : AcceptableValueBase where T : IComparable { public AcceptableValueRange(T min, T max) { } public T MinValue { get; } public T MaxValue { get; } }
    public class AcceptableValueList<T> : AcceptableValueBase where T : IEquatable<T> { public AcceptableValueList(params T[] v) { } }
    public class ConfigDescription { public ConfigDescription(string d, AcceptableValueBase a = null, params object[] tags) { } }
    public class ConfigDefinition { public ConfigDefinition(string s, string k) { } public string Section { get; } public string Key { get; } }
    public abstract partial class ConfigEntryBase { public ConfigDefinition Definition { get; } public object BoxedValue { get; set; } public object DefaultValue { get; } public ConfigDescription Description { get; } }
    public abstract partial class ConfigEntryBase { public ConfigFile ConfigFile { get; } }
    public sealed class ConfigEntry<T> : ConfigEntryBase { public T Value { get; set; } public event EventHandler SettingChanged; }
    public class ConfigFile
    {
        public ConfigFile(string path, bool saveOnInit) { }
        public ConfigEntry<T> Bind<T>(string section, string key, T def, string desc) { return null; }
        public ConfigEntry<T> Bind<T>(string section, string key, T def, ConfigDescription desc = null) { return null; }
        public ConfigEntry<T> Bind<T>(ConfigDefinition d, T def, ConfigDescription desc = null) { return null; }
        public void Save() { } public void Reload() { } public bool SaveOnConfigSet { get; set; } public string ConfigFilePath { get; }
        public bool TryGetEntry<T>(string s, string k, out ConfigEntry<T> e) { e = null; return false; }
    }
}
