using IPA.Logging;
using IPALogger = IPA.Logging.Logger;

namespace EnhancedStreamChat
{
    internal static class Logger
    {
        internal static IPALogger log { get; set; }
        internal static IPALogger cclog => log.GetChildLogger("ChatCore");
    }
}
