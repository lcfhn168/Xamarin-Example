using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using System.IO.IsolatedStorage;
using System.Threading.Tasks;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using AO = Android.OS;
using System.Net.Http;

namespace FlipView {
    public abstract class DiskLRUCache {

        private readonly static IsolatedStorageFile ISF = null;

        /// <summary>
        /// 是否缓存，默认开启
        /// </summary>
        public bool EnableCache { get; set; }

        /// <summary>
        /// 缓存时长, 默认1天
        /// </summary>
        public TimeSpan CacheValidity { get; set; }

        public abstract string SubDir { get; }

        static DiskLRUCache() {
            ISF = IsolatedStorageFile.GetUserStoreForApplication();
        }

        private void CheckPath() {
            if (!ISF.DirectoryExists(this.SubDir)) {
                ISF.CreateDirectory(this.SubDir);
            }
        }

        public DiskLRUCache() {
            this.CacheValidity = TimeSpan.FromDays(1);
            this.EnableCache = true;

            this.CheckPath();
        }

        public async Task<Stream> GetStream(string url) {
            var uri = new Uri(url);
            return await this.GetStream(uri);
        }

        /// <summary>
        /// 获取文件流
        /// </summary>
        /// <param name="uri"></param>
        /// <returns></returns>
        public async Task<Stream> GetStream(Uri uri) {
            Stream stm = null;
            if (!this.EnableCache) {
                stm = await this.GetStreamAsync(uri);
            } else {
                var key = MD5(uri.AbsoluteUri);
                stm = await this.GetStreamFromCache(key);
                if (stm == null || stm.Length == 0) {
                    stm = await this.GetStreamAsync(uri);
                    if (stm != null) {
                        await this.SaveCache(key, stm);
                    }
                }
            }

            return stm;
        }

        /// <summary>
        /// 从网络获取文件流
        /// </summary>
        /// <param name="uri"></param>
        /// <returns></returns>
        private async Task<Stream> GetStreamAsync(System.Uri uri) {
            Stream stream = null;
            using (var client = new HttpClient())
            using (var rep = await client.GetAsync(uri)) {
                stream = await rep.Content.ReadAsStreamAsync();
            }
            return stream;
        }

        /// <summary>
        /// 从缓存获取文件流
        /// </summary>
        /// <param name="uri"></param>
        /// <returns></returns>
        private async Task<Stream> GetStreamFromCache(string key) {
            Stream stm = null;
            var path = Path.Combine(this.SubDir, key);
            if (!await this.IsExpire(path)) {
                stm = ISF.OpenFile(path, FileMode.Open, FileAccess.Read);
            }
            return stm;
        }

        /// <summary>
        /// 是否过期, 如果不存在，按过期处理
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        private Task<bool> IsExpire(string path) {
            //如果文件不存在， 直接是过期处理
            if (!ISF.FileExists(path))
                return Task.FromResult(true);

            var offset = ISF.GetLastWriteTime(path);
            return Task.FromResult(DateTime.Now - offset > this.CacheValidity);
        }

        /// <summary>
        /// 保存缓存
        /// </summary>
        /// <param name="key"></param>
        /// <param name="stm"></param>
        /// <returns></returns>
        private Task SaveCache(string key, Stream stm) {
            var path = Path.Combine(this.SubDir, key);
            //IOException Sharing violation on path
            using (var f = ISF.OpenFile(path, FileMode.Create, FileAccess.Write)) {
                return stm.CopyToAsync(f);
            }
        }

        private static string MD5(string input) {
            using (var md5Hasher = System.Security.Cryptography.MD5.Create()) {
                byte[] data = md5Hasher.ComputeHash(Encoding.UTF8.GetBytes(input));
                StringBuilder sBuilder = new StringBuilder();

                for (int i = 0; i < data.Length; i++) {
                    sBuilder.Append(data[i].ToString("x2"));
                }
                return sBuilder.ToString();
            }
        }

    }
}