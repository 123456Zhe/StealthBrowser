using CefSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StealthBrowser.Managers {
	/// <summary>
	/// DownloadManager stores download metadata in a list, since CefSharp does not.
	/// </summary>
	internal static class DownloadManager {

		private static Dictionary<int, DownloadItem> downloads;
		private static Dictionary<int, string> downloadNames;
		private static List<int> downloadCancelRequests;

		public static Dictionary<int, DownloadItem> Downloads => downloads;
		// 便携化：下载存到程序目录下的 downloads 文件夹
		public static string CalcDownloadPath(DownloadItem item) {
			string dir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "downloads");
			System.IO.Directory.CreateDirectory(dir);
			string name = item.SuggestedFileName;
			if (string.IsNullOrEmpty(name)) name = "download";
			return System.IO.Path.Combine(dir, System.IO.Path.GetFileName(name));
		}
		public static List<int> CancelRequests => downloadCancelRequests;

		public static void Init() {

			downloads = new Dictionary<int, DownloadItem>();
			downloadNames = new Dictionary<int, string>();
			downloadCancelRequests = new List<int>();

		}

		public static void UpdateDownloadItem(DownloadItem item) {
			lock (downloads) {

				// SuggestedFileName comes full only in the first attempt so keep it somewhere
				if (item.SuggestedFileName != "") {
					downloadNames[item.Id] = item.SuggestedFileName;
				}

				// Set it back if it is empty
				if (item.SuggestedFileName == "" && downloadNames.ContainsKey(item.Id)) {
					item.SuggestedFileName = downloadNames[item.Id];
				}

				downloads[item.Id] = item;

				//UpdateSnipProgress();
			}
		}

		public static bool DownloadsInProgress() {
			foreach (DownloadItem item in downloads.Values) {
				if (item.IsInProgress) {
					return true;
				}
			}
			return false;
		}

		public static void Cancel(int downloadId) {
			lock (downloadCancelRequests) {
				if (!downloadCancelRequests.Contains(downloadId)) {
					downloadCancelRequests.Add(downloadId);
				}
			}
		}

	}
}
