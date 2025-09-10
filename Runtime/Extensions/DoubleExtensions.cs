using Manager;

namespace Hotfix.Config.Extension
{
    public static partial class DoubleExtensions
    {
        /// <summary>
        /// 
        /// </summary>
        public static string HandleNumber(this double value, bool keepDecimal = true)
        {
            return TextHandleManager.HandleNumber(value, keepDecimal);
        }
    }
}