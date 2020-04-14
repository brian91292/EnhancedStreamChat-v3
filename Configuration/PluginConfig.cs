
using System.Runtime.CompilerServices;
using IPA.Config.Stores;

[assembly: InternalsVisibleTo(GeneratedStore.AssemblyVisibilityTarget)]
namespace EnhancedStreamChat.Configuration
{
    internal class PluginConfig
    {
        public static PluginConfig Instance { get; set; }
        public virtual int IntValue { get; set; } = 42; // Must be 'virtual' if you want BSIPA to detect a value change and save the config automatically.

        public virtual void OnReload()
        {
            // Do stuff after config is read from disk.
        }

        public virtual void Changed()
        {
            // Do stuff when the config is changed.
        }

        public virtual void CopyFrom(PluginConfig other)
        {
            // This instance's members populated from other
        }
    }
}
