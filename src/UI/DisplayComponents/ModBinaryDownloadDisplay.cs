using System;

using UnityEngine;
using UnityEngine.UI;

namespace ModIO.UI
{
    // TODO(@jackson): TEXT -> TEXTMESH/FLEX
    /// <summary>Represents the status of a mod binary download visually.</summary>
    public class ModBinaryDownloadDisplay : MonoBehaviour, IModViewElement
    {
        // ---------[ NESTED DATA-TYPES ]---------
        [Serializable]
        private struct DownloadSpeed
        {
            public int lastIndex;
            public int stepsRecorded;
            public float[] timeStepMarker;
            public Int64[] bytesReceived;

            public void Reset()
            {
                this.lastIndex = -1;
                this.stepsRecorded = 0;
            }

            public void AddMarker(float timeStamp, Int64 byteCount)
            {
                // ignore if not newer timeStamp
                if(lastIndex >= 0
                   && (timeStamp - this.timeStepMarker[lastIndex] <= 0f))
                {
                    return;
                }

                ++lastIndex;
                lastIndex %= timeStepMarker.Length;

                this.timeStepMarker[lastIndex] = timeStamp;
                this.bytesReceived[lastIndex] = byteCount;

                ++stepsRecorded;

            }

            public Int64 GetAverageDownloadSpeed()
            {
                if(stepsRecorded <= 1)
                {
                    return 0;
                }

                Debug.Assert(lastIndex >= 0);
                Debug.Assert(this.timeStepMarker.Length == this.bytesReceived.Length);

                int finalIndex = lastIndex - 1;
                if(this.stepsRecorded <= this.timeStepMarker.Length)
                {
                    finalIndex = this.stepsRecorded-1;
                }
                else if(finalIndex < 0)
                {
                    finalIndex += this.timeStepMarker.Length;
                }


                // for(int i = 1;
                //     initialByteCount == 0
                //     && i < timeStepMarker.Length
                //     && i < stepsRecorded;
                //     ++i)
                // {
                //     int thisIndex = (firstIndex + i) % timeStepMarker.Length;
                //     initialTimeStamp = this.timeStepMarker[thisIndex];
                //     initialByteCount = this.bytesReceived[thisIndex];
                // }


                float initialTimeStamp = this.timeStepMarker[lastIndex];
                Int64 initialByteCount = this.bytesReceived[lastIndex];

                float finalTimeStamp = this.timeStepMarker[finalIndex];
                Int64 finalByteCount = this.bytesReceived[finalIndex];

                return (Int64)((finalByteCount - initialByteCount)/(finalTimeStamp - initialTimeStamp));
            }
        }

        // ---------[ FIELDS ]---------
        // --- Components ---
        /// <summary>Component to display the total number of bytes for the download.</summary>
        public Text bytesTotalText      = null;
        /// <summary>Component to display the number of bytes received.</summary>
        public Text bytesReceivedText   = null;
        /// <summary>Component to display the percentage completed.</summary>
        public Text percentageText      = null;
        /// <summary>Component to display the number of bytes being downloaded per second.</summary>
        public Text bytesPerSecondText  = null;
        /// <summary>Component to display the estimated time remaining for the download.</summary>
        public Text timeRemainingText   = null;
        /// <summary>Component to display the progress of the download.</summary>
        public HorizontalProgressBar progressBar = null;
        /// <summary>Determines whether the component should hide if not downloading.</summary>
        public bool hideIfInactive = true;

        // --- Display Data---
        /// <summary>Parent ModView.</summary>
        private ModView m_view = null;

        /// <summary>ModId of the mod currently being monitored.</summary>
        private int m_modId = ModProfile.NULL_ID;

        /// <summary>The currently running update coroutine.</summary>
        private Coroutine m_updateCoroutine = null;

        /// <summary>Download Info.</summary>
        private FileDownloadInfo m_downloadInfo = null;

        /// <summary>Current download speed.</summary>
        private DownloadSpeed m_downloadSpeed = new DownloadSpeed()
        {
            lastIndex = -1,
            stepsRecorded = 0,
            timeStepMarker = new float[10],
            bytesReceived = new Int64[10],
        };

        // ---------[ INITIALIZATION ]---------
        protected virtual void Awake()
        {
            DownloadClient.modfileDownloadStarted += OnDownloadStarted;
        }

        protected virtual void OnDestroy()
        {
            DownloadClient.modfileDownloadStarted -= OnDownloadStarted;
        }

        protected virtual void OnEnable()
        {
            if(this.m_downloadInfo != null)
            {
                if(this.m_updateCoroutine == null)
                {
                    this.m_updateCoroutine = this.StartCoroutine(this.UpdateCoroutine());
                }
            }
            else if(this.hideIfInactive)
            {
                this.gameObject.SetActive(false);
            }
        }

        protected virtual void OnDisable()
        {
            if(this.m_updateCoroutine != null)
            {
                this.StopCoroutine(this.m_updateCoroutine);
            }
        }

        // --- IMODVIEWELEMENT INTERFACE ---
        /// <summary>IModViewElement interface.</summary>
        public void SetModView(ModView view)
        {
            // early out
            if(this.m_view == view) { return; }

            // unhook
            if(this.m_view != null)
            {
                this.m_view.onProfileChanged -= DisplayProfile;
            }

            // assign
            this.m_view = view;

            // hook
            if(this.m_view != null)
            {
                this.m_view.onProfileChanged += DisplayProfile;
                this.DisplayProfile(this.m_view.profile);
            }
            else
            {
                this.DisplayProfile(null);
            }
        }

        // ---------[ UI FUNCTIONALITY ]---------
        /// <summary>Displays tags of a profile.</summary>
        public void DisplayProfile(ModProfile profile)
        {
            this.m_downloadSpeed.Reset();

            if(profile != null)
            {
                this.m_modId = profile.id;

                foreach(var kvp in DownloadClient.modfileDownloadMap)
                {
                    if(kvp.Key.modId == this.m_modId)
                    {
                        OnDownloadStarted(kvp.Key, kvp.Value);
                        return;
                    }
                }
            }
            else
            {
                this.m_modId = ModProfile.NULL_ID;
            }

            if(this.m_updateCoroutine != null)
            {
                this.StopCoroutine(this.m_updateCoroutine);
                this.m_updateCoroutine = null;
            }
        }

        /// <summary>Updates the UI representation every frame.</summary>
        protected virtual System.Collections.IEnumerator UpdateCoroutine()
        {
            Debug.Assert(this.m_downloadInfo != null);

            while(this != null
                  && this.m_downloadInfo != null)
            {
                if(m_downloadInfo.request != null
                   && m_downloadInfo.request.downloadedBytes > 0
                   && !m_downloadInfo.isDone)
                {
                    this.m_downloadSpeed.AddMarker(Time.unscaledTime,
                                                   (Int64)m_downloadInfo.request.downloadedBytes);
                }

                this.UpdateComponents();

                yield return new WaitForSecondsRealtime(1f);
            }
        }

        /// <summary>Updates the display components.</summary>
        protected virtual void UpdateComponents()
        {
            // collect vars
            Int64 fileSize = 0;
            Int64 bytesReceived = 0;
            Int64 downloadSpeed = 0;
            float percentComplete = 0f;

            if(this.m_downloadInfo != null)
            {
                fileSize = this.m_downloadInfo.fileSize;
                bytesReceived = (Int64)this.m_downloadInfo.request.downloadedBytes;
                percentComplete = (float)bytesReceived / (float)fileSize;
                downloadSpeed = this.m_downloadSpeed.GetAverageDownloadSpeed();
            }

            if(this.bytesTotalText != null)
            {
                this.bytesTotalText.text = UIUtilities.ByteCountToDisplayString(fileSize);
            }
            if(this.bytesReceivedText != null)
            {
                this.bytesReceivedText.text = UIUtilities.ByteCountToDisplayString(bytesReceived);
            }
            if(this.percentageText != null)
            {
                this.percentageText.text = (percentComplete * 100f).ToString("0.0") + "%";
            }
            if(this.bytesPerSecondText != null)
            {
                this.bytesPerSecondText.text = (UIUtilities.ByteCountToDisplayString(downloadSpeed)
                                                + "/s");
            }
            if(this.timeRemainingText != null)
            {
                string timeRemainingDisplayString = string.Empty;
                if(fileSize > 0 && downloadSpeed > 0)
                {
                    Int64 secondsRemaining = (int)((fileSize - bytesReceived) / downloadSpeed);

                    TimeSpan remaining = TimeSpan.FromSeconds(secondsRemaining);
                    timeRemainingDisplayString = (remaining.TotalHours + ":"
                                                  + remaining.Minutes + ":"
                                                  + remaining.Seconds);
                }

                this.timeRemainingText.text = timeRemainingDisplayString;
            }

            if(this.progressBar != null)
            {
                this.progressBar.percentComplete = percentComplete;
            }
        }

        // ---------[ EVENTS ]---------
        /// <summary>Initializes the component display.</summary>
        protected virtual void OnDownloadStarted(ModfileIdPair idPair, FileDownloadInfo downloadInfo)
        {
            if(this.m_modId == idPair.modId
               && downloadInfo != null)
            {
                this.m_downloadInfo = downloadInfo;

                if(!this.isActiveAndEnabled && this.hideIfInactive)
                {
                    this.gameObject.SetActive(true);
                }

                if(this.isActiveAndEnabled
                   && this.m_updateCoroutine == null)
                {
                    this.m_updateCoroutine = this.StartCoroutine(this.UpdateCoroutine());
                }
            }
        }
    }
}