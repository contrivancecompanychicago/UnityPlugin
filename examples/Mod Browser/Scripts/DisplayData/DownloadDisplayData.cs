using System;
using UnityWebRequest = UnityEngine.Networking.UnityWebRequest;

namespace ModIO.UI
{
    [Serializable]
    public struct DownloadDisplayData
    {
        public Int64 bytesReceived;
        public Int64 bytesPerSecond;
        public Int64 bytesTotal;
    }

    public abstract class DownloadDisplayComponent : UnityEngine.MonoBehaviour
    {
        public abstract event Action<DownloadDisplayComponent> onClick;

        public abstract DownloadDisplayData data { get; set; }

        public abstract void Initialize();
        public abstract void DisplayDownload(FileDownloadInfo downloadInfo);

        [Obsolete]
        public void DisplayDownload(UnityWebRequest request, Int64 downloadSize)
        {
            UnityEngine.Debug.Assert(request != null);

            FileDownloadInfo download = new FileDownloadInfo()
            {
                request = request,
                target = string.Empty,
                fileSize = downloadSize,
            };

            DisplayDownload(download);
        }
    }
}
