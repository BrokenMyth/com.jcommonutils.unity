using System;

namespace CommonUtils
{
    public class Singleton<T> where T : class, new()
    {
        private static readonly Lazy<T> Lazy =
            new Lazy<T>(() => new T(), isThreadSafe: true);

        public static T Instance => Lazy.Value;
    }
}