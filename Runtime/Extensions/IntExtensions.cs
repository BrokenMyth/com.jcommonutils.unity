using Manager;

namespace Hotfix.Config.Extension
{
    public static partial class IntExtensions
    {
        /// <summary>
        /// 
        /// </summary>
        public static string HandleNumber(this int value, bool keepDecimal = true)
        {
            return TextHandleManager.HandleNumber(value, keepDecimal);
        }
    }
}