using System;

public class Singleton<T> where T : class, new()
{
    private static readonly Lazy<T> Lazy = new(() => new T(), true);

    public static T Instance => Lazy.Value;
}