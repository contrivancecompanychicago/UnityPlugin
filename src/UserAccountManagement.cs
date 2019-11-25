// #define ENABLE_STEAMWORKS_FACEPUNCH
// #define ENABLE_STEAMWORKS_NET
// #define ENABLE_STEAM_OTHER
// #define ENABLE_GOG_AUTHENTICATION

/*** NOTE:
 * If building to a platform other than Mac, Windows (exe), or Linux,
 * the Unity #define directives as specified at [https://docs.unity3d.com/Manual/PlatformDependentCompilation.html]
 * act to enable those specific authentication methods and thus do not require manual activation.
 ***/

using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;

using Newtonsoft.Json;

using Debug = UnityEngine.Debug;

namespace ModIO
{
    /// <summary>A collection of user management functions provided for convenience.</summary>
    public static class UserAccountManagement
    {
        // ---------[ NESTED DATA-TYPES ]---------
        /// <summary>Data structure for the user data file.</summary>
        [System.Serializable]
        private class StoredUserData
        {
            public int activeUserIndex = -1;
            public LocalUserData[] userData = new LocalUserData[0];
        }

        // ---------[ CONSTANTS ]---------
        #if ENABLE_STEAMWORKS_FACEPUNCH || ENABLE_STEAMWORKS_NET || ENABLE_STEAM_OTHER
            public static readonly string PROFILE_URL_POSTFIX = "?ref=steam";
        #elif ENABLE_GOG_AUTHENTICATION
            public static readonly string PROFILE_URL_POSTFIX = "?ref=gog";
        #else
            public static readonly string PROFILE_URL_POSTFIX = string.Empty;
        #endif

        // ---------[ DATA ]---------
        /// <summary>Currently loaded user data.</summary>
        private static StoredUserData m_storedUserData;

        /// <summary>User data for the currently active user.</summary>
        private static LocalUserData m_activeUserData;

        // ---------[ DATA LOADING ]---------
        static UserAccountManagement()
        {
            byte[] userFileData = UserAccountManagement.ReadUserDataFile();
            UserAccountManagement.m_storedUserData = UserAccountManagement.ParseStoredUserData(userFileData);

            // set initial values if no data
            if(UserAccountManagement.m_storedUserData == null)
            {
                UserAccountManagement.m_storedUserData = new StoredUserData();
                UserAccountManagement.m_storedUserData.activeUserIndex = -1;

                UserAccountManagement.m_activeUserData = new LocalUserData()
                {
                    modioUserId = ModProfile.NULL_ID,
                    localUserId = null,
                    enabledModIds = new int[0],
                };
            }

        }

        /// <summary>Load user data file.</summary>
        private static byte[] ReadUserDataFile()
        {
            byte[] data;

            #if UNITY_EDITOR
                string filePath = IOUtilities.CombinePath(UnityEngine.Application.dataPath,
                                                          "Editor Default Resources",
                                                          "modio",
                                                          "user.data");

                data = IOUtilities.LoadBinaryFile(filePath);

            #elif ENABLE_STEAMWORKS_FACEPUNCH
                string filePath = "modio_user.data";

                if(Steamworks.SteamRemoteStorage.FileExists(filePath))
                {
                    data = Steamworks.SteamRemoteStorage.FileRead(filePath);
                }

            #elif ENABLE_STEAMWORKS_NET
                string filePath = "modio_user.data";

                if(Steamworks.SteamRemoteStorage.FileExists(filePath))
                {
                    int fileSize = Steamworks.SteamRemoteStorage.GetFileSize(filePath);

                    if(fileSize > 0)
                    {
                        if(fileSize > 1024) { fileSize = 1024; }

                        data = new byte[fileSize];
                        Steamworks.SteamRemoteStorage.FileRead(filePath, data, fileSize);
                    }
                }

            #elif UNITY_STANDALONE_WIN
                string filePath = IOUtilities.Combine("%APPDATA%",
                                                      "modio",
                                                      "game-" + PluginSettings.data.gameId,
                                                      "user.data");

                data = IOUtilities.LoadBinaryFile(filePath);

            #elif UNITY_STANDALONE_OSX
                string filePath = ("~/Library/Application Support/mod.io/"
                                   + "game-" + PluginSettings.data.gameId
                                   + "/user.data");

                data = IOUtilities.LoadBinaryFile(filePath);

            #elif UNITY_STANDALONE_LINUX
                string filePath = ("~/.config/mod.io/"
                                   + "game-" + PluginSettings.data.gameId
                                   + "/user.data");

                data = IOUtilities.LoadBinaryFile(filePath);

            #else
                throw new System.NotImplementedException("[mod.io] The loading of user data for this"
                                                         + " build definition has not been implemented.");

            #endif

            return data;
        }

        /// <summary>Writes data to the user data file.</summary>
        private static bool TryWriteUserDataFile(byte[] data)
        {
            bool success = false;

            if(data == null)
            {
                Debug.LogWarning("[mod.io] Attempted to write null-data file.");
                data = new byte[0];
            }

            #if UNITY_EDITOR
                string filePath = IOUtilities.CombinePath(UnityEngine.Application.dataPath,
                                                          "Editor Default Resources",
                                                          "modio",
                                                          "user.data");

                success = IOUtilities.WriteBinaryFile(filePath, data);

            #elif ENABLE_STEAMWORKS_FACEPUNCH
                string filePath = "modio_user.data";

                success = Steamworks.SteamRemoteStorage.FileWrite(filePath, data);

            #elif ENABLE_STEAMWORKS_NET
                string filePath = "modio_user.data";
                int fileSize = data.Length;

                // TODO(@jackson): Verify a good max size!
                if(fileSize > 1024) { fileSize = 1024; }

                success = Steamworks.SteamRemoteStorage.FileRead(filePath, data, fileSize);

            #elif UNITY_STANDALONE_WIN
                string filePath = IOUtilities.Combine("%APPDATA%",
                                                      "modio",
                                                      "game-" + PluginSettings.data.gameId,
                                                      "user.data");

                success = IOUtilities.WriteBinaryFile(filePath, data);

            #elif UNITY_STANDALONE_OSX
                string filePath = ("~/Library/Application Support/mod.io/"
                                   + "game-" + PluginSettings.data.gameId
                                   + "/user.data");

                success = IOUtilities.WriteBinaryFile(filePath, data);

            #elif UNITY_STANDALONE_LINUX
                string filePath = ("~/.config/mod.io/"
                                   + "game-" + PluginSettings.data.gameId
                                   + "/user.data");

                success = IOUtilities.WriteBinaryFile(filePath, data);

            #else
                throw new System.NotImplementedException("[mod.io] The loading of user data for this"
                                                         + " build definition has not been implemented.");

            #endif

            return success;
        }

        /// <summary>Parses user data file.</summary>
        private static StoredUserData ParseStoredUserData(byte[] data)
        {
            // early out
            if(data == null || data.Length == 0)
            {
                return null;
            }

            // attempt to parse data
            StoredUserData storedUserData = null;
            try
            {
                string dataString = Encoding.UTF8.GetString(data);
                storedUserData = JsonConvert.DeserializeObject<StoredUserData>(dataString);
            }
            catch(Exception e)
            {
                string warningInfo = ("[mod.io] Failed to parse user data from file.");

                Debug.LogWarning(warningInfo
                                 + Utility.GenerateExceptionDebugString(e));

                storedUserData = null;
            }

            return storedUserData;
        }

        /// <summary>Generates user data file.</summary>
        private static byte[] GenerateUserFileData(StoredUserData storedUserData)
        {
            if(storedUserData == null)
            {
                storedUserData = new StoredUserData();
            }

            // create json data bytes
            byte[] data = null;

            try
            {
                string dataString = JsonConvert.SerializeObject(storedUserData);
                data = Encoding.UTF8.GetBytes(dataString);
            }
            catch(Exception e)
            {
                string warningInfo = ("[mod.io] Failed to generate user file data.");

                Debug.LogWarning(warningInfo
                                 + Utility.GenerateExceptionDebugString(e));

                data = new byte[0];
            }

            return data;
        }

        /// <summary>Takes any changes in m_activeUserData and writes them to disk.</summary>
        private static void WriteActiveUserData()
        {
            // check if exists
            if(UserAccountManagement.m_storedUserData.activeUserIndex < 0)
            {
                UserAccountManagement.m_storedUserData.activeUserIndex
                = UserAccountManagement.m_storedUserData.userData.Length;
            }

            // update array size
            if(UserAccountManagement.m_storedUserData.activeUserIndex >
               UserAccountManagement.m_storedUserData.userData.Length-1)
            {
                LocalUserData[] newUserData = new LocalUserData[UserAccountManagement.m_storedUserData.activeUserIndex+1];
                Array.Copy(UserAccountManagement.m_storedUserData.userData, newUserData,
                           UserAccountManagement.m_storedUserData.userData.Length);

                newUserData[UserAccountManagement.m_storedUserData.activeUserIndex]
                = UserAccountManagement.m_activeUserData;

                UserAccountManagement.m_storedUserData.userData = newUserData;
            }

            // write file
            byte[] fileData = UserAccountManagement.GenerateUserFileData(UserAccountManagement.m_storedUserData);
            UserAccountManagement.TryWriteUserDataFile(fileData);
        }

        // ---------[ USER ACTIONS ]---------
        /// <summary>Returns the enabled mods for the active user.</summary>
        public static List<int> GetEnabledModIds()
        {
            return new List<int>(UserAccountManagement.m_activeUserData.enabledModIds);
        }

        /// <summary>Sets the enabled mods for the active user.</summary>
        public static void SetEnabledModIds(IEnumerable<int> modIds)
        {
            int[] modIdArray;

            if(modIds == null)
            {
                modIdArray = new int[0];
            }
            else
            {
                modIdArray = modIds.ToArray();
            }

            UserAccountManagement.m_activeUserData.enabledModIds = modIdArray;
        }

        // ---------[ UTILITY ]---------
        /// <summary>A wrapper function for setting the UserAuthenticationData.wasTokenRejected to false.</summary>
        public static void MarkAuthTokenRejected()
        {
            UserAuthenticationData data = UserAuthenticationData.instance;
            data.wasTokenRejected = true;
            UserAuthenticationData.instance = data;
        }

        // ---------[ AUTHENTICATION ]---------
        /// <summary>Requests a login code be sent to an email address.</summary>
        public static void RequestSecurityCode(string emailAddress,
                                               Action onSuccess,
                                               Action<WebRequestError> onError)
        {
            if(onSuccess == null)
            {
                APIClient.SendSecurityCode(emailAddress, null, onError);
            }
            else
            {
                APIClient.SendSecurityCode(emailAddress, (m) => onSuccess(), onError);
            }
        }

        /// <summary>Attempts to authenticate a user using an emailed security code.</summary>
        public static void AuthenticateWithSecurityCode(string securityCode,
                                                        Action<UserProfile> onSuccess,
                                                        Action<WebRequestError> onError)
        {
            APIClient.GetOAuthToken(securityCode, (t) =>
            {
                UserAuthenticationData authData = new UserAuthenticationData()
                {
                    token = t,
                    wasTokenRejected = false,
                    externalAuthToken = null,
                    externalAuthId = null,
                };

                UserAuthenticationData.instance = authData;
                UserAccountManagement.FetchUserProfile(onSuccess, onError);
            },
            onError);
        }

        /// <summary>Attempts to reauthenticate using the enabled service.</summary>
        public static void ReauthenticateWithExternalAuthToken(Action<UserProfile> onSuccess,
                                                               Action<WebRequestError> onError)
        {
            Debug.Assert(!string.IsNullOrEmpty(UserAuthenticationData.instance.externalAuthToken));

            Action<string, Action<string>, Action<WebRequestError>> authAction = null;

            #if ENABLE_STEAMWORKS_FACEPUNCH || ENABLE_STEAMWORKS_NET || ENABLE_STEAM_OTHER
                authAction = APIClient.RequestSteamAuthentication;
            #elif ENABLE_GOG_AUTHENTICATION
                authAction = APIClient.RequestGOGAuthentication;
            #endif

            #if DEBUG
                if(authAction == null)
                {
                    Debug.LogError("[mod.io] Cannot reauthenticate without enabling an external"
                                   + " authentication service. Please refer to this file"
                                   + " (UserAccountManagement.cs) and uncomment the #define for the"
                                   + " desired service at the beginning of the file.");
                }
            #endif

            authAction.Invoke(UserAuthenticationData.instance.externalAuthToken, (t) =>
            {
                UserAuthenticationData authData = new UserAuthenticationData()
                {
                    token = t,
                    wasTokenRejected = false,
                };

                UserAuthenticationData.instance = authData;
                UserAccountManagement.FetchUserProfile(onSuccess, onError);
            },
            onError);
        }

        /// <summary>Stores the oAuthToken and steamTicket and fetches the UserProfile.</summary>
        private static void FetchUserProfile(Action<UserProfile> onSuccess,
                                             Action<WebRequestError> onError)
        {
            APIClient.GetAuthenticatedUser((p) =>
            {
                UserAuthenticationData data = UserAuthenticationData.instance;
                data.userId = p.id;
                UserAuthenticationData.instance = data;

                if(onSuccess != null)
                {
                    onSuccess(p);
                }
            },
            onError);
        }

        // ---------[ STEAM AUTHENTICATION ]---------
        #if ENABLE_STEAMWORKS_FACEPUNCH || ENABLE_STEAMWORKS_NET || ENABLE_STEAM_OTHER
        /// <summary>Attempts to authenticate a user using a Steam Encrypted App Ticket.</summary>
        /// <remarks>This version is designed to match the Steamworks.NET implementation by
        /// @rlabrecque at https://github.com/rlabrecque/Steamworks.NET</remarks>
        public static void AuthenticateWithSteamEncryptedAppTicket(byte[] pTicket, uint pcbTicket,
                                                                   Action<UserProfile> onSuccess,
                                                                   Action<WebRequestError> onError)
        {
            string encodedTicket = Utility.EncodeEncryptedAppTicket(pTicket, pcbTicket);
            UserAccountManagement.AuthenticateWithSteamEncryptedAppTicket(encodedTicket,
                                                                          onSuccess, onError);
        }

        /// <summary>Attempts to authenticate a user using a Steam Encrypted App Ticket.</summary>
        /// <remarks>This version is designed to match the FacePunch.SteamWorks implementation by
        /// @garrynewman at https://github.com/Facepunch/Facepunch.Steamworks</remarks>
        public static void AuthenticateWithSteamEncryptedAppTicket(byte[] authTicketData,
                                                                   Action<UserProfile> onSuccess,
                                                                   Action<WebRequestError> onError)
        {
            string encodedTicket = Utility.EncodeEncryptedAppTicket(authTicketData, (uint)authTicketData.Length);
            UserAccountManagement.AuthenticateWithSteamEncryptedAppTicket(encodedTicket,
                                                                          onSuccess, onError);
        }


        /// <summary>Attempts to authenticate a user using a Steam Encrypted App Ticket.</summary>
        public static void AuthenticateWithSteamEncryptedAppTicket(string encodedTicket,
                                                                   Action<UserProfile> onSuccess,
                                                                   Action<WebRequestError> onError)
        {
            APIClient.RequestSteamAuthentication(encodedTicket, (t) =>
            {
                UserAuthenticationData authData = new UserAuthenticationData()
                {
                    token = t,
                    wasTokenRejected = false,
                    externalAuthToken = encodedTicket,
                    externalAuthId = null,
                };

                UserAuthenticationData.instance = authData;
                UserAccountManagement.FetchUserProfile(onSuccess, onError);
            },
            onError);
        }
        #endif

        // ---------[ GOG AUTHENTICATION ]---------
        #if ENABLE_GOG_AUTHENTICATION
        /// <summary>Attempts to authenticate a user using a GOG Encrypted App Ticket.</summary>
        public static void AuthenticateWithGOGEncryptedAppTicket(byte[] data, uint dataSize,
                                                                 Action<UserProfile> onSuccess,
                                                                 Action<WebRequestError> onError)
        {
            string encodedTicket = Utility.EncodeEncryptedAppTicket(data, dataSize);
            UserAccountManagement.AuthenticateWithGOGEncryptedAppTicket(encodedTicket,
                                                                        onSuccess, onError);
        }

        /// <summary>Attempts to authenticate a user using a GOG Encrypted App Ticket.</summary>
        public static void AuthenticateWithGOGEncryptedAppTicket(string encodedTicket,
                                                                 Action<UserProfile> onSuccess,
                                                                 Action<WebRequestError> onError)
        {
            APIClient.RequestGOGAuthentication(encodedTicket, (t) =>
            {
                UserAuthenticationData authData = new UserAuthenticationData()
                {
                    token = t,
                    wasTokenRejected = false,
                    externalAuthToken = encodedTicket,
                    externalAuthId = null,
                };

                UserAuthenticationData.instance = authData;
                UserAccountManagement.FetchUserProfile(onSuccess, onError);
            },
            onError);
        }
        #endif
    }
}
