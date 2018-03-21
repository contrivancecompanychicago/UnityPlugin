#if UNITY_EDITOR

using System;
using System.IO;
using UnityEngine;
using UnityEditor;

namespace ModIO
{
    [ExecuteInEditMode]
    public class EditorSceneData : MonoBehaviour
    {
        public int modId = 0;
        public ModInfo modInfo = null;
        public EditableModFields modData = null;

        public string buildLocation = string.Empty;
        public bool setBuildAsPrimary = true;
        public ModfileEditableFields modfileValues = null;

        private Texture2D modLogoTexture = null;
        private string modLogoSource = string.Empty;
        private DateTime modLogoLastWrite = new DateTime();

        private void OnEnable()
        {
            ModManager.OnModLogoUpdated += OnModLogoUpdated;

            ReacquireModLogo();
        }

        private void OnDisable()
        {
            ModManager.OnModLogoUpdated -= OnModLogoUpdated;
        }

        private void OnModLogoUpdated(int modId, Texture2D modLogo, ImageVersion logoVersion)
        {
            // TODO(@jackson): Serialize this
            // TODO(@jackson): Other thumb sizes?
            if(this.modId == modId
               && !modData.logoIdentifier.isDirty
               && logoVersion == ImageVersion.Thumb_320x180)
            {
                modLogoTexture = modLogo;
                modData.logoIdentifier.value = modInfo.logo.thumb320x180;
                modLogoLastWrite = new DateTime();
            }
        }

        private void ReacquireModLogo()
        {
            if(modId > 0
               && !modData.logoIdentifier.isDirty)
            {
                modLogoSource = modInfo.logo.thumb320x180;

                modLogoTexture = ModManager.LoadCachedModLogo(modId, ImageVersion.Thumb_320x180);
                modLogoLastWrite = new DateTime();

                if(modLogoTexture == null)
                {
                    modLogoTexture = UISettings.Instance.LoadingPlaceholder320x180;

                    ModManager.DownloadModLogo(modId, ImageVersion.Thumb_320x180);
                }
            }
            else
            {
                modLogoSource = modData.logoIdentifier.value;

                if(File.Exists(modLogoSource))
                {
                    modLogoTexture = new Texture2D(0, 0);
                    modLogoTexture.LoadImage(File.ReadAllBytes(modLogoSource));

                    modLogoLastWrite = File.GetLastWriteTime(modLogoSource);
                }
            }
        }

        private void Update()
        {
            string newLogoLocal = modData.logoIdentifier.value;
            string newLogoServer = modInfo.logo.thumb320x180;
            string newLogoSource = (modId > 0 && newLogoLocal == "" ? newLogoServer : newLogoLocal);

            // TODO(@jackson): Handle file missing
            // - If file has changed or unsubmitted file is updated -
            if((modLogoSource != newLogoSource)
               || (File.Exists(modData.logoIdentifier.value) && File.GetLastWriteTime(modData.logoIdentifier.value) > modLogoLastWrite))
            {
                ReacquireModLogo();
            }
        }

        public Texture2D GetModLogoTexture()
        {
            return modLogoTexture;
        }
        public string GetModLogoSource()
        {
            return modLogoSource;
        }
    }
}
#endif
