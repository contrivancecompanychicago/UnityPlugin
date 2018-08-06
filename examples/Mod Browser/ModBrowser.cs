﻿using UnityEngine;

using System;
using System.Collections.Generic;

using ModIO;

public class ModBrowser : MonoBehaviour
{
    // ---------[ NESTED CLASSES ]---------
    [System.Serializable]
    private class ManifestData
    {
        public int lastCacheUpdate = -1;
    }

    // ---------[ CONST & STATIC ]---------
    public static string manifestFilePath { get { return CacheClient.GetCacheDirectory() + "browser_manifest.data"; } }

    // ---------[ FIELDS ]---------
    // --- Key Data ---
    public int gameId = GlobalSettings.GAME_ID;
    public string gameAPIKey = GlobalSettings.GAME_APIKEY;
    public bool isAutomaticUpdateEnabled = false;

    // --- Caching ---
    public GameProfile gameProfile;
    public List<ModProfile> modProfileCache;

    // --- Non Serialized ---
    [HideInInspector]
    public UserProfile userProfile = null;
    [HideInInspector]
    public int lastCacheUpdate = -1;

    // --- UI Components ---
    public Texture2D loadingPlaceholder;

    public ModThumbnailContainer thumbnailContainer;
    public ModInspector inspector;
    public UnityEngine.UI.Button backButton;

    public LogoSize logoThumbnailVersion;
    public LogoSize logoInspectorVersion;

    public bool isInspecting;
    public ModProfile inspectedProfile;
    public ModBinaryRequest activeDownload;


    // ---------[ INITIALIZATION ]---------
    protected bool _isInitialized = false;

    protected virtual void OnEnable()
    {
        // --- ui init ---
        thumbnailContainer.thumbnailClicked += OnThumbClicked;
        inspector.installClicked += OnInstallClicked;
        inspector.subscribeClicked += OnSubscribeClicked;

        isInspecting = false;
        thumbnailContainer.gameObject.SetActive(true);
        inspector.gameObject.SetActive(false);

        foreach(var thumb in this.thumbnailContainer.modThumbnails)
        {
            thumb.gameObject.SetActive(false);
        }
    }

    protected virtual void OnDisable()
    {
        // --- ui de-init ---
        inspector.subscribeClicked -= OnSubscribeClicked;
        inspector.installClicked -= OnInstallClicked;
        thumbnailContainer.thumbnailClicked -= OnThumbClicked;
    }

    protected virtual void Start()
    {
        // --- mod.io init ---
        if(this.gameId <= 0)
        {
            Debug.LogError("[mod.io] Game ID is missing. Save it to GlobalSettings or this MonoBehaviour before starting the app",
                           this);
            return;
        }
        if(System.String.IsNullOrEmpty(this.gameAPIKey))
        {
            Debug.LogError("[mod.io] Game API Key is missing. Save it to GlobalSettings or this MonoBehaviour before starting the app",
                           this);
            return;
        }

        APIClient.gameId = this.gameId;
        APIClient.gameAPIKey = this.gameAPIKey;

        string userToken = CacheClient.LoadAuthenticatedUserToken();

        if(!String.IsNullOrEmpty(userToken))
        {
            APIClient.userAuthorizationToken = userToken;

            ModManager.GetAuthenticatedUserProfile(this.Initialize,
                                                   (e) => { this.Initialize(null); WebRequestError.LogAsWarning(e); });
        }
        else
        {
            this.Initialize(null);
        }
    }

    protected virtual void Initialize(UserProfile userProfile)
    {
        this.userProfile = userProfile;

        // --- Load Manifest ---
        ManifestData manifest = CacheClient.ReadJsonObjectFile<ManifestData>(ModBrowser.manifestFilePath);
        if(manifest != null)
        {
            this.lastCacheUpdate = manifest.lastCacheUpdate;
        }

        // --- Load Mod Profiles ---
        this.modProfileCache = new List<ModProfile>(CacheClient.IterateAllModProfiles());

        // --- Load Game Profile ---
        ModManager.GetGameProfile(this.OnGetGameProfile,
                                  (e) => { WebRequestError.LogAsWarning(e); this.OnGetGameProfile(null); });
    }

    protected virtual void OnGetGameProfile(GameProfile game)
    {
        this.gameProfile = game;
        this._isInitialized = true;

        this.OnInitialized();
    }

    protected virtual void OnInitialized()
    {
        LoadModPage();
    }

    // ---------[ UPDATES ]---------
    private const float AUTOMATIC_UPDATE_INTERVAL = 15f;
    private bool isUpdateRunning = false;

    protected virtual void Update()
    {
        if(this._isInitialized
           && this.isAutomaticUpdateEnabled)
        {
            if(!isUpdateRunning
               && (ServerTimeStamp.Now - this.lastCacheUpdate) >= AUTOMATIC_UPDATE_INTERVAL)
            {
                PollForServerUpdates();
            }
        }

        if(activeDownload != null)
        {
            float downloaded = 0f;
            if(activeDownload.webRequest != null)
            {
                downloaded = activeDownload.webRequest.downloadProgress * 100f;
            }

            inspector.installButtonText.text = "Downloading [" + downloaded.ToString("00.00") + "%]";
        }
    }

    protected void PollForServerUpdates()
    {
        Debug.Assert(!isUpdateRunning);

        this.isUpdateRunning = true;

        int updateStartTimeStamp = ServerTimeStamp.Now;

        ModManager.FetchAllModEvents(this.lastCacheUpdate, updateStartTimeStamp,
                                     (me) => { this.ProcessUpdates(me, updateStartTimeStamp); },
                                     (e) => { WebRequestError.LogAsWarning(e); this.isUpdateRunning = false; });

        // TODO(@jackson): Add User Events
    }

    protected void ProcessUpdates(List<ModEvent> modEvents, int updateStartTimeStamp)
    {
        if(modEvents != null)
        {
            // - Event Handler Notification -
            Action<List<ModProfile>> onAvailable = (profiles) =>
            {
                this.OnModsAvailable(profiles);
            };
            Action<List<ModProfile>> onEdited = (profiles) =>
            {
                this.OnModsEdited(profiles);
            };
            Action<List<ModfileStub>> onReleasesUpdated = (modfiles) =>
            {
                this.OnModReleasesUpdated(modfiles);
            };
            Action<List<int>> onUnavailable = (ids) =>
            {
                this.OnModsUnavailable(ids);
            };
            Action<List<int>> onDeleted = (ids) =>
            {
                this.OnModsDeleted(ids);
            };
            Action onSuccess = () =>
            {
                this.lastCacheUpdate = updateStartTimeStamp;
                this.isUpdateRunning = false;

                ManifestData manifest = new ManifestData()
                {
                    lastCacheUpdate = this.lastCacheUpdate,
                };

                CacheClient.WriteJsonObjectFile(ModBrowser.manifestFilePath, manifest);

            };
            Action<WebRequestError> onError = (error) =>
            {
                WebRequestError.LogAsWarning(error);
                this.isUpdateRunning = false;
            };

            ModManager.ApplyModEventsToCache(modEvents,
                                             onAvailable, onEdited,
                                             onUnavailable, onDeleted,
                                             onReleasesUpdated,
                                             onSuccess,
                                             onError);
        }
        else
        {
            this.lastCacheUpdate = updateStartTimeStamp;
            this.isUpdateRunning = false;

            ManifestData manifest = new ManifestData()
            {
                lastCacheUpdate = this.lastCacheUpdate,
            };

            CacheClient.WriteJsonObjectFile(ModBrowser.manifestFilePath, manifest);
        }
    }

    // ---------[ EVENT HANDLING ]---------
    private void OnModsAvailable(IEnumerable<ModProfile> addedProfiles)
    {
        List<ModProfile> undisplayedProfiles = new List<ModProfile>(addedProfiles);
        List<int> cachedIds = modProfileCache.ConvertAll<int>(p => p.id);

        undisplayedProfiles.RemoveAll(p => cachedIds.Contains(p.id));

        this.modProfileCache.AddRange(undisplayedProfiles);

        LoadModPage();
    }
    private void OnModsEdited(IEnumerable<ModProfile> editedProfiles)
    {
        List<ModProfile> editedProfileList = new List<ModProfile>(editedProfiles);
        List<int> editedIds = editedProfileList.ConvertAll<int>(p => p.id);

        this.modProfileCache.RemoveAll(p => editedIds.Contains(p.id));
        this.modProfileCache.AddRange(editedProfileList);

        LoadModPage();
    }
    private void OnModReleasesUpdated(IEnumerable<ModfileStub> modfiles)
    {
        foreach(ModfileStub modfile in modfiles)
        {
            Debug.Log("Modfile Updated: " + modfile.version);
        }
    }
    private void OnModsUnavailable(IEnumerable<int> modIds)
    {
        List<int> removedModIds = new List<int>(modIds);
        this.modProfileCache.RemoveAll(p => removedModIds.Contains(p.id));

        LoadModPage();
    }
    private void OnModsDeleted(IEnumerable<int> modIds)
    {
        List<int> removedModIds = new List<int>(modIds);
        this.modProfileCache.RemoveAll(p => removedModIds.Contains(p.id));

        LoadModPage();
    }

    // ---------[ UI FUNCTIONALITY ]---------
    protected virtual void LoadModPage()
    {
        int pageSize = Mathf.Min(thumbnailContainer.modThumbnails.Length, this.modProfileCache.Count);
        this.thumbnailContainer.modIds = new int[pageSize];

        int i;

        for(i = 0; i < pageSize; ++i)
        {
            int thumbnailIndex = i;
            this.thumbnailContainer.modIds[i] = this.modProfileCache[i].id;
            this.thumbnailContainer.modThumbnails[i].sprite = CreateSpriteFromTexture(loadingPlaceholder);
            this.thumbnailContainer.modThumbnails[i].gameObject.SetActive(true);

            ModManager.GetModLogo(this.modProfileCache[i], logoThumbnailVersion,
                                  (t) => this.thumbnailContainer.modThumbnails[thumbnailIndex].sprite = CreateSpriteFromTexture(t),
                                  null);
        }

        while(i < this.thumbnailContainer.modThumbnails.Length)
        {
            this.thumbnailContainer.modThumbnails[i].gameObject.SetActive(false);
            ++i;
        }
    }

    protected virtual void OnThumbClicked(int index)
    {
        ModManager.GetModProfile(thumbnailContainer.modIds[index], OnGetInspectedProfile, null);
    }

    protected virtual void OnGetInspectedProfile(ModProfile profile)
    {
        // - set up inspector ui -
        inspectedProfile = profile;

        inspector.title.text = profile.name;
        inspector.author.text = profile.submittedBy.username;
        inspector.logo.sprite = CreateSpriteFromTexture(loadingPlaceholder);

        List<int> userSubscriptions = CacheClient.LoadAuthenticatedUserSubscriptions();

        if(userSubscriptions != null
           && userSubscriptions.Contains(profile.id))
        {
            inspector.subscribeButtonText.text = "Unsubscribe";
        }
        else
        {
            inspector.subscribeButtonText.text = "Subscribe";
        }

        ModManager.GetModLogo(profile, logoInspectorVersion,
                              (t) => inspector.logo.sprite = CreateSpriteFromTexture(t),
                              null);

        inspector.installButton.gameObject.SetActive(false);
        inspector.downloadButtonText.text = "Verifying local data";
        inspector.downloadButton.gameObject.SetActive(true);
        inspector.downloadButton.interactable = false;

        // - check binary status -
        ModManager.GetDownloadedBinaryStatus(profile.activeBuild,
                                             (status) =>
                                             {
                                                if(status == ModBinaryStatus.CompleteAndVerified)
                                                {
                                                    inspector.downloadButton.gameObject.SetActive(false);
                                                    inspector.installButton.gameObject.SetActive(true);
                                                }
                                                else
                                                {
                                                    inspector.downloadButtonText.text = "Download";
                                                    inspector.downloadButton.interactable = true;
                                                }
                                             });

        // - finalize -
        isInspecting = true;
        thumbnailContainer.gameObject.SetActive(false);
        inspector.gameObject.SetActive(true);
    }

    protected virtual void OnBackClicked()
    {
        if(isInspecting)
        {
            isInspecting = false;
            thumbnailContainer.gameObject.SetActive(true);
            inspector.gameObject.SetActive(false);
        }
    }

    protected virtual void OnSubscribeClicked()
    {
        List<int> subscriptions = CacheClient.LoadAuthenticatedUserSubscriptions();

        if(subscriptions == null)
        {
            subscriptions = new List<int>(1);
        }

        int modId = inspectedProfile.id;
        int subscriptionIndex = subscriptions.IndexOf(modId);

        if(subscriptionIndex == -1)
        {
            subscriptions.Add(modId);
            inspector.subscribeButtonText.text = "Unsubscribe";

            if(userProfile != null)
            {
                APIClient.SubscribeToMod(inspectedProfile.id,
                                         null, null);
            }
        }
        else
        {
            subscriptions.RemoveAt(subscriptionIndex);
            inspector.subscribeButtonText.text = "Subscribe";

            if(userProfile != null)
            {
                APIClient.UnsubscribeFromMod(inspectedProfile.id,
                                             null, null);
            }
        }

        CacheClient.SaveAuthenticatedUserSubscriptions(subscriptions);

        OnDownloadClicked();
    }

    protected virtual void OnDownloadClicked()
    {
        this.activeDownload = ModManager.GetActiveModBinary(inspectedProfile);

        if(this.activeDownload.isDone)
        {
            inspector.installButton.gameObject.SetActive(true);
            inspector.downloadButton.gameObject.SetActive(true);

            this.activeDownload = null;
        }
        else
        {
            inspector.downloadButtonText.text = "Initializing Download...";

            this.activeDownload.succeeded += (d) =>
            {
                inspector.installButton.gameObject.SetActive(true);
                inspector.downloadButton.gameObject.SetActive(true);

                this.activeDownload = null;
            };
            this.activeDownload.failed += (d) =>
            {
                inspector.installButton.gameObject.SetActive(true);
                inspector.downloadButton.gameObject.SetActive(true);

                this.activeDownload = null;
            };
        }
    }

    protected virtual void OnInstallClicked()
    {
        ModProfile modProfile = this.inspectedProfile;
        string unzipLocation = null;

        if(String.IsNullOrEmpty(unzipLocation))
        {
            Debug.LogWarning("[mod.io] This is a placeholder for game specific code that handles the"
                             + " installing the mod");
        }
        else
        {
            ModManager.UnzipModBinaryToLocation(modProfile.activeBuild, unzipLocation);
            // Do install code
        }
    }

    // ---------[ UTILITY ]---------
    protected Sprite CreateSpriteFromTexture(Texture2D texture)
    {
        return Sprite.Create(texture,
                             new Rect(0.0f, 0.0f, texture.width, texture.height),
                             Vector2.zero);
    }
}
