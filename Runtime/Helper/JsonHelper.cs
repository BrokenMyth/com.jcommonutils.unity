using System.IO;
using Unity.Plastic.Newtonsoft.Json;
using UnityEngine;

namespace CommonUtils.Helper
{
    public static class JsonHelper
    {
        /// <summary>
        /// 类对象保存为 Json 文件
        /// BepinEx 插件需要使用下面的 JsonConvert , 否则无法写入 json
        /// </summary>
        public static void SaveToJson<T>(string path, T data)
        {
            string json = JsonConvert.SerializeObject(data, Formatting.Indented);
            File.WriteAllText(path, json);
        }
        /// <summary>
        /// 从 Json 文件读取并转回类
        /// </summary>
        public static T LoadFromJsonOrNull<T>(string path) where T : class
        {
            if (!File.Exists(path))
                return null;

            string json = File.ReadAllText(path);

            if (string.IsNullOrEmpty(json))
                return null;

            return JsonConvert.DeserializeObject<T>(json);
        }
        
        /// <summary>
        /// 类对象保存为 Json 文件
        /// </summary>
        public static void SaveToJsonByUtility<T>(string path, T data)
        {
            string json = JsonUtility.ToJson(data, true); // true = 格式化
            File.WriteAllText(path, json);
        }
    
    
        public static T LoadFromJsonOrNullByUtility<T>(string path) where T : class
        {
            if (!File.Exists(path))
                return null; 
            string json = File.ReadAllText(path); 
            if (string.IsNullOrEmpty(json)) return null; 
            return JsonUtility.FromJson<T>(json);
        }
    }
}