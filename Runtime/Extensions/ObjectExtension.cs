namespace Extensions
{
    public static class ObjectExtension
    {
        /// <summary>
        /// 检查对象是否为null
        /// </summary>
        /// <param name="self"></param>
        /// <returns></returns>
        public static bool IsNull(this object self)
        {
            return self == null;
        }

        /// <summary>
        /// 检查对象是否不为null
        /// </summary>
        /// <param name="self"></param>
        /// <returns></returns>
        public static bool IsNotNull(this object self)
        {
            return !self.IsNull();
        }
    }
}