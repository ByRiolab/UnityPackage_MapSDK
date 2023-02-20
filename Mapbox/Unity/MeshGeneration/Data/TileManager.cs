using Mapbox.Unity.Map.Interfaces;
using System;
using System.Collections;
using System.Collections.Generic;
using Mapbox.Map;
using UnityEngine;
using System.Linq;
using Mapbox.Unity.MeshGeneration.Data;
using System.Threading.Tasks;
namespace Mapbox.Unity.MeshGeneration.Data
{

    public class TileManager : MonoBehaviour
    {
        internal class TileData
        {
            public CanonicalTileId key;
            public byte[] data;
            public bool useMipMap;
            public bool useCompression;
            public Action<Texture2D> callback;
            public TileData(CanonicalTileId key, byte[] data, bool useMipMap, bool useCompression, Action<Texture2D> callback = null)
            {
                this.key = key;
                this.data = data;
                this.useMipMap = useMipMap;
                this.useCompression = useCompression;
                this.callback = callback;
            }
        }

        internal class TextureLoader
        {
            public TileData tileData;
            // public CanonicalTileId key;
            // public byte[] data;
            // public bool useMipMap;
            // public bool useCompression;

            public Action<Texture2D> OnComplete;
        }

        public static Dictionary<CanonicalTileId, Texture2D> _textureCache = new Dictionary<CanonicalTileId, Texture2D>();
        private static Dictionary<string, TextureLoader> onList = new Dictionary<string, TextureLoader>();
        private static int maxItemsPerFrame = 4;
        private static int maxItemsPerFrameOnSecondPass = 1;

        private static List<TileData> _loadingQueue = new List<TileData>();

        public static void AddToQueue(CanonicalTileId key, byte[] data, bool useMipMap, bool useCompression, Action<Texture2D> callback = null)
        {
            _loadingQueue.Add(new TileData(key, data, useMipMap, useCompression, callback));
        }

        public static void Subscribe(string ID, CanonicalTileId key, byte[] data, bool useMipMap, bool useCompression, Action<Texture2D> onComplete)
        {

            if (onList.ContainsKey(ID))
            {
                var textLoader = onList[ID];
                _loadingQueue.Add(textLoader.tileData);
                textLoader.tileData = new TileData(key, data, useMipMap, useCompression, textLoader.OnComplete);
                textLoader.OnComplete = onComplete;
                onList[ID] = textLoader;
            }
            else
            {
                var textLoader = new TextureLoader();
                textLoader.OnComplete = onComplete;
                textLoader.tileData = new TileData(key, data, useMipMap, useCompression);
                onList.Add(ID, textLoader);
            }
        }

        private static Texture2D DownloadTexture(TileData tileData)
        {
            return DownloadTexture(tileData.key, tileData.data, tileData.useMipMap, tileData.useCompression);
        }
        private static Texture2D DownloadTexture(CanonicalTileId key, byte[] data, bool useMipMap = true, bool useCompression = false)
        {
            if (data == null)
            {
                return null;
            }
            var tex = new Texture2D(0, 0, TextureFormat.RGB24, useMipMap);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.LoadImage(data);
            // if (useCompression)
            // {
            //     // High quality = true seems to decrease image quality?
            //     tex.Compress(useCompression);
            // }
            tex.Compress(useCompression);
            tex.Apply(useMipMap);

            AddTileToCache(key, tex);
            // if(!_textureCache.ContainsKey(key))
            // {
            //     _textureCache.Add(key, tex);
            // }
            // AddTileToCache(key,tex);
            return tex;
            // }
        }

        public static bool IsTileCached(CanonicalTileId tileId)
        {
            return _textureCache.ContainsKey(tileId);
        }
        public static Texture2D GetTileTexture(CanonicalTileId tileId)
        {
            return _textureCache[tileId];
        }


        void Update()
        {
            //Get the first numPerIteration items from the list
            //randomize list 
            //shuffle list 
            // var shuffledList = onList.OrderBy(x => UnityEngine.Random.value).ToList();
            // for(int i = 0; i < maxItemsPerFrame; i++)
            // {
            //     if(onList.Count > 0)
            //     {
            //         var index = UnityEngine.Random.Range(0, onList.Count);
            //         var item = onList.ElementAt(index);
            //         var texture = DownloadTexture(item.Value.key, item.Value.data, item.Value.useMipMap, item.Value.useCompression);
            //         item.Value.OnComplete(texture);
            //         onList.Remove(item.Key);
            //     }
            // }
            var items = onList.Keys.Take(maxItemsPerFrame).ToList();
            //Remove them from the list
            foreach (var item in items)
            {
                var data = onList[item];
                // var texture = DownloadTexture(data.key, data.data, data.useMipMap, data.useCompression);
                var texture = DownloadTexture(data.tileData);
                data.OnComplete(texture);
                onList.Remove(item);
            }

            var queueItems = _loadingQueue.Take(maxItemsPerFrameOnSecondPass).ToList();
            foreach (var item in queueItems)
            {
                if (_textureCache.ContainsKey(item.key))
                {
                    item.callback?.Invoke(_textureCache[item.key]);
                    _loadingQueue.Remove(item);
                }
                else
                {
                    var texture = DownloadTexture(item);
                    if (item.callback != null)
                    {
                        item.callback?.Invoke(texture);
                    }
                    _loadingQueue.Remove(item);
                }
                // _loadingQueue.Remove(item);
            }

            // Debug.Log("onList.Count: " + onList.Count + " _loadingQueue.Count: " + _loadingQueue.Count);
        }

        public static void AddTileToCache(CanonicalTileId tileId, Texture2D texture)
        {
            if (!_textureCache.ContainsKey(tileId))
            {
                // var temp = Instantiate(texture);
                _textureCache.Add(tileId, texture);
                // Debug.Log("Tile added to cache: " + tileId);
            }
        }

        private static void RemoveTileFromCache(CanonicalTileId tileId)
        {
            _textureCache.Remove(tileId);
        }

        //free memory when is Destroyed
        void OnDestroy()
        {
            Debug.Log("Destroying " + _textureCache.Count + " textures alloctaed in Random Access Memory (RAM)");
            foreach (var item in _textureCache)
            {
                Destroy(item.Value);
            }
            _textureCache.Clear();
        }

        void OnApplicationQuit()
        {
            Debug.Log("Destroying " + _textureCache.Count + " textures alloctaed in Random Access Memory (RAM)");
            foreach (var item in _textureCache)
            {
                Destroy(item.Value);
            }
            _textureCache.Clear();
        }

    }
}
