
using Manager;

namespace Hotfix.Config.Extension
{
    public static partial class LongExtensions
    {
        /// <summary>
        /// 
        /// </summary>
        public static string HandleNumber(this long value, bool keepDecimal = true)
        {
            return TextHandleManager.HandleNumber(value, keepDecimal);
        }
    }
}