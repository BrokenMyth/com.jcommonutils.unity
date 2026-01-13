using System;
using System.Collections.Generic;
using System.IO;
using Helper;
using UnityEngine;
using UnityEngine.UI;

namespace BepInExHelper
{
    [Serializable]
    public class PlayerDataList
    {
        public List<int> list;
        private string jsonpath;

        public PlayerDataList()
        {
        }

        public PlayerDataList(string path)
        {
            jsonpath = path;
        }
        public void Click(int id)
        {
            if (!list.Contains(id))
                list.Add(id);
            else
                Remove(id);
        }

        public void Add(int id)
        {
            if (!list.Contains(id))
                list.Add(id);
        }

        public void Remove(int id)
        {
            list.RemoveAll(x => x == id);
        }

        public void Save()
        {
            Save(jsonpath);
        }
        public void Save(string path)
        {
            JsonHelper.SaveToJson(path, this);
        }

        public void Load()
        {
            Load(jsonpath);
        }
        public void Load(string path)
        {
            var data = JsonHelper.LoadFromJsonOrNull<PlayerDataList>(path);
            if (data == null)
            {
                data = new PlayerDataList(path)
                {
                    list = new List<int>()
                };
                Save(path);
            }

            list = data.list;
        }
    }

    public class Like
    {
        public static void AddLoveImageUI(GameObject root, string loveImagePath, bool show)
        {
            var loveIconName = "LoveIcon";
            if (!root.transform.Find(loveIconName))
            {
                var data = File.ReadAllBytes(loveImagePath);

                var texture2D = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                texture2D.LoadImage(data);

                var sprite = Sprite.Create(
                    texture2D,
                    new Rect(0, 0, texture2D.width, texture2D.height),
                    new Vector2(0.5f, 0.5f),
                    100f
                );
                // 2️⃣ 创建 UI GameObject
                var imgGO = new GameObject(loveIconName, typeof(RectTransform), typeof(Image));
                imgGO.name = loveIconName;
                imgGO.transform.SetParent(root.transform, false);
                imgGO.transform.localScale = Vector3.one;
                // 3️⃣ 设置 RectTransform
                var rectTransform = imgGO.GetComponent<RectTransform>();
                rectTransform.anchorMin = new Vector2(1f, 1f);
                rectTransform.anchorMax = new Vector2(1f, 1f);
                rectTransform.pivot = new Vector2(1f, 1f);
                rectTransform.anchoredPosition = new Vector2(0f, 0f);
                rectTransform.sizeDelta = new Vector2(50f, 50f);
                // 4️⃣ 设置 Image
                var image = imgGO.GetComponent<Image>();
                image.sprite = sprite;
                image.raycastTarget = false;
                root.transform.Find(loveIconName).gameObject.SetActive(show);
            }
            else
            {
                root.transform.Find(loveIconName).gameObject.SetActive(show);
            }
        }
    }
}