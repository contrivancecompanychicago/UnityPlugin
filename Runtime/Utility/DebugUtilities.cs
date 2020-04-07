using System.Collections.Generic;

using UnityEngine;

using UnityWebRequest = UnityEngine.Networking.UnityWebRequest;
using UnityWebRequestAsyncOperation = UnityEngine.Networking.UnityWebRequestAsyncOperation;

namespace ModIO
{
    /// <summary>Tools to assist in debugging.</summary>
    public static class DebugUtilities
    {
        // ---------[ Nested Data-Types ]---------
        /// <summary>Pairing of the WWWForm field types.</summary>
        public struct RequestInfo
        {
            public string userIdString;
            public IEnumerable<API.StringValueParameter> stringFields;
            public IEnumerable<API.BinaryDataParameter> binaryFields;
            public string downloadLocation;
        }

        // ---------[ Web Requests ]---------
        /// <summary>Mapping of tracked WebRequests with their sent data.</summary>
        public static Dictionary<UnityWebRequest, RequestInfo> webRequestInfo = new Dictionary<UnityWebRequest, RequestInfo>();

        /// <summary>Tracks and logs a request upon it completing.</summary>
        public static void DebugRequestOperation(UnityWebRequestAsyncOperation operation,
                                                 LocalUser userData)
        {
            #if DEBUG
                Debug.Assert(operation != null);

                RequestInfo info = new RequestInfo()
                {
                    userIdString = DebugUtilities.GenerateUserIdString(userData.profile),
                    stringFields = null,
                    binaryFields = null,
                };

                // get upload data
                if(operation.webRequest.uploadHandler != null)
                {
                    List<API.StringValueParameter> sf;
                    List<API.BinaryDataParameter> bf;

                    DebugUtilities.ParseUploadData(operation.webRequest.uploadHandler.data,
                                                   out sf,
                                                   out bf);

                    info.stringFields = sf;
                    info.binaryFields = bf;
                }

                DebugUtilities.webRequestInfo.Add(operation.webRequest, info);

                // handle completion
                if(operation.isDone)
                {
                    DebugUtilities.OnOperationCompleted(operation);
                }
                else
                {
                    operation.completed += DebugUtilities.OnOperationCompleted;
                }

            #endif // DEBUG
        }

        /// <summary>Tracks and logs a download upon it completing.</summary>
        public static void DebugDownloadOperation(UnityWebRequestAsyncOperation operation,
                                                  LocalUser userData,
                                                  string downloadLocation)
        {
            #if DEBUG
                Debug.Assert(operation != null);

                RequestInfo info = new RequestInfo()
                {
                    userIdString = DebugUtilities.GenerateUserIdString(userData.profile),
                    stringFields = null,
                    binaryFields = null,
                    downloadLocation = downloadLocation,
                };

                // get upload data
                if(operation.webRequest.uploadHandler != null)
                {
                    List<API.StringValueParameter> sf;
                    List<API.BinaryDataParameter> bf;

                    DebugUtilities.ParseUploadData(operation.webRequest.uploadHandler.data,
                                                   out sf,
                                                   out bf);

                    info.stringFields = sf;
                    info.binaryFields = bf;
                }

                DebugUtilities.webRequestInfo.Add(operation.webRequest, info);

                // handle completion
                if(operation.isDone)
                {
                    DebugUtilities.OnOperationCompleted(operation);
                }
                else
                {
                    operation.completed += DebugUtilities.OnOperationCompleted;
                }

            #endif // DEBUG
        }

        /// <summary>Callback upon request operation completion.</summary>
        private static void OnOperationCompleted(AsyncOperation operation)
        {
            #if DEBUG
                // get vars
                UnityWebRequestAsyncOperation o = operation as UnityWebRequestAsyncOperation;
                UnityWebRequest webRequest = o.webRequest;
                RequestInfo info;
                if(!DebugUtilities.webRequestInfo.TryGetValue(webRequest, out info))
                {
                    info = new RequestInfo()
                    {
                        userIdString = "NONE_RECORDED",
                        stringFields = null,
                        binaryFields = null,
                        downloadLocation = null,
                    };
                }

                // generate strings
                var timeStampString = (ServerTimeStamp.Now.ToString()
                                       + " ["
                                       + ServerTimeStamp.ToLocalDateTime(ServerTimeStamp.Now)
                                       + "]");

                string requestString = DebugUtilities.GenerateRequestDebugString(webRequest,
                                                                                 info.userIdString,
                                                                                 info.stringFields,
                                                                                 info.binaryFields);

                string responseString = DebugUtilities.GenerateResponseDebugString(webRequest);

                // generate log string
                var logString = new System.Text.StringBuilder();
                logString.AppendLine("[mod.io] Web Request Completed");
                logString.Append("URL: ");
                logString.Append(webRequest.url);
                logString.Append(" (");
                logString.Append(webRequest.method.ToUpper());
                logString.AppendLine(")");

                if(!string.IsNullOrEmpty(info.downloadLocation))
                {
                    logString.Append("Download Location: ");
                    logString.AppendLine(info.downloadLocation);
                }

                logString.AppendLine();

                logString.AppendLine("------[ Request ]------");
                logString.AppendLine(requestString);

                logString.AppendLine("------[ Response ]------");
                logString.Append("Time Stamp: ");
                logString.AppendLine(timeStampString);
                logString.AppendLine(responseString);

                // log
                Debug.Log(logString.ToString());

            #endif // DEBUG
        }

        /// <summary>Generates a debug-friendly string of a web request.</summary>
        public static string GenerateRequestDebugString(UnityWebRequest webRequest,
                                                        string userIdString,
                                                        IEnumerable<API.StringValueParameter> stringFields,
                                                        IEnumerable<API.BinaryDataParameter> binaryFields)
        {
            // check user string
            if(userIdString == null)
            {
                userIdString = "[NOT RECORDED]";
            }

            // build string
            var requestString = new System.Text.StringBuilder();

            requestString.Append("URL: ");
            requestString.Append(webRequest.url);
            requestString.Append(" (");
            requestString.Append(webRequest.method.ToUpper());
            requestString.AppendLine(")");

            requestString.Append("User: ");
            requestString.AppendLine(userIdString);

            // add request headers
            requestString.AppendLine("Headers:");
            foreach(string headerKey in APIClient.MODIO_REQUEST_HEADER_KEYS)
            {
                string headerValue = webRequest.GetRequestHeader(headerKey);
                if(headerValue != null)
                {
                    requestString.Append("  ");
                    requestString.Append(headerKey);
                    requestString.Append(':');

                    if(headerKey.ToUpper() == "AUTHORIZATION")
                    {
                        if(headerValue != null
                           && headerValue.StartsWith("Bearer ")
                           && headerValue.Length > 8)
                        {
                            requestString.Append("Bearer [OAUTH_TOKEN]");
                        }
                        else // NULL
                        {
                            requestString.Append(headerValue);
                        }
                    }
                    else
                    {
                        requestString.Append(headerValue);
                    }

                    requestString.AppendLine();
                }
            }

            // add string fields
            requestString.AppendLine("String Fields:");
            if(stringFields == null)
            {
                requestString.AppendLine("  NONE");
            }
            else
            {
                int countInsertIndex = requestString.Length-1;
                int count = 0;

                foreach(var svp in stringFields)
                {
                    requestString.Append("  ");
                    requestString.Append(svp.key);
                    requestString.Append(':');
                    requestString.Append(svp.value);
                    requestString.AppendLine();
                    ++count;
                }

                requestString.Insert(countInsertIndex, "[" + count.ToString() + "]");
            }

            // add binary fields
            requestString.AppendLine("Binary Fields:");
            if(binaryFields == null)
            {
                requestString.AppendLine("  NONE");
            }
            else
            {
                int countInsertIndex = requestString.Length;
                int count = 0;

                foreach(var bdp in binaryFields)
                {
                    requestString.Append("  ");
                    requestString.Append(bdp.key);
                    requestString.Append(':');
                    requestString.Append(bdp.fileName);
                    requestString.Append(" (");
                    requestString.Append(bdp.contents == null
                                         ? "NULL_DATA"
                                         : ValueFormatting.ByteCount(bdp.contents.Length, null));
                    requestString.Append(")");
                    requestString.AppendLine();
                    ++count;
                }

                requestString.Insert(countInsertIndex, "[" + count.ToString() + "]");
            }

            return requestString.ToString();
        }

        /// <summary>Generates a debug-friendly string of a web request response.</summary>
        public static string GenerateResponseDebugString(UnityWebRequest webRequest)
        {
            // get info
            var responseString = new System.Text.StringBuilder();

            responseString.Append("URL: ");
            responseString.Append(webRequest.url);
            responseString.Append(" (");
            responseString.Append(webRequest.method.ToUpper());
            responseString.AppendLine(")");

            responseString.Append("Response Code: ");
            responseString.AppendLine(webRequest.responseCode.ToString());

            responseString.Append("Response Error: ");
            if(string.IsNullOrEmpty(webRequest.error))
            {
                responseString.AppendLine("NO_ERROR");
            }
            else
            {
                responseString.AppendLine(webRequest.error);
            }

            // add request headers
            responseString.AppendLine("Headers:");

            var responseHeaders = webRequest.GetResponseHeaders();
            if(responseHeaders == null
               || responseHeaders.Count == 0)
            {
                responseString.AppendLine("  NONE");
            }
            else
            {
                foreach(var kvp in responseHeaders)
                {
                    responseString.Append("  ");
                    responseString.Append(kvp.Key);
                    responseString.Append(':');
                    responseString.Append(kvp.Value);
                    responseString.AppendLine();
                }
            }

            // body
            responseString.AppendLine("Body:");

            string bodyText = null;
            try
            {
                if(webRequest.downloadHandler == null)
                {
                    bodyText = "  NULL_DOWNLOAD_HANDLER";
                }
                else
                {
                    bodyText = webRequest.downloadHandler.text;
                }
            }
            catch
            {
                bodyText = "  TEXT_ACCESS_NOT_SUPPORTED";
            }
            responseString.AppendLine(bodyText);

            return responseString.ToString();
        }

        /// <summary>Parses an UploadHandler's payload and generates the request info elements.</summary>
        public static void ParseUploadData(byte[] data,
                                           out List<API.StringValueParameter> stringFields,
                                           out List<API.BinaryDataParameter> binaryFields)
        {
            stringFields = null;
            binaryFields = null;

            // early out
            if(data == null || data.Length == 0) { return; }

            // get dataString and delimiter
            string dataString = System.Text.Encoding.UTF8.GetString(data);
            string lineEnd = "\r\n";
            int lineEndIndex = -1;

            lineEndIndex = dataString.IndexOf(lineEnd, 1);
            if(lineEndIndex < 0) { return; }

            string delimiter = dataString.Substring(0, lineEndIndex).Trim();
            string[] sections = dataString.Split(new string[] { delimiter }, System.StringSplitOptions.RemoveEmptyEntries);
            stringFields = new List<API.StringValueParameter>();
            binaryFields = new List<API.BinaryDataParameter>();

            foreach(string s in sections)
            {
                string searchString = null;
                int searchIndex = 0;
                int elementStartIndex = 0;
                int elementEndIndex = 0;

                // Content-Type
                searchString = "Content-Type: ";
                searchIndex = s.IndexOf(searchString);
                if(searchIndex < 0)
                {
                    continue;
                }

                elementStartIndex = searchIndex + searchString.Length;
                elementEndIndex = s.IndexOf(lineEnd, elementStartIndex);

                string contentType = s.Substring(elementStartIndex, elementEndIndex - elementStartIndex);

                // process text
                if(contentType.Contains(@"text/plain"))
                {
                    var newStringParam = new API.StringValueParameter();

                    // get key
                    searchString = "name=\"";
                    searchIndex = s.IndexOf(searchString);

                    if(searchIndex < 0)
                    {
                        newStringParam.key = "KEY_NOT_FOUND";
                    }
                    else
                    {
                        elementStartIndex = searchIndex + searchString.Length;
                        elementEndIndex = s.IndexOf("\"", elementStartIndex);

                        newStringParam.key = s.Substring(elementStartIndex, elementEndIndex - elementStartIndex);
                    }

                    // get value
                    searchString = lineEnd + lineEnd;
                    searchIndex = s.IndexOf(searchString);

                    if(searchIndex < 0)
                    {
                        newStringParam.value = "VALUE_NOT_FOUND";
                    }
                    else
                    {
                        elementStartIndex = searchIndex + searchString.Length;
                        newStringParam.value = s.Substring(elementStartIndex).Trim();
                    }
                }
                // process literally anything else
                else
                {
                    var newBinaryParam = new API.BinaryDataParameter()
                    {
                        mimeType = contentType,
                    };

                    // get key
                    searchString = "name=\"";
                    searchIndex = s.IndexOf(searchString);

                    if(searchIndex < 0)
                    {
                        newBinaryParam.key = "KEY_NOT_FOUND";
                    }
                    else
                    {
                        elementStartIndex = searchIndex + searchString.Length;
                        elementEndIndex = s.IndexOf("\"", elementStartIndex);

                        newBinaryParam.key = s.Substring(elementStartIndex, elementEndIndex - elementStartIndex);
                    }

                    // get fileName
                    searchString = "filename=\"";
                    searchIndex = s.IndexOf(searchString);

                    if(searchIndex < 0)
                    {
                        newBinaryParam.fileName = "FILENAME_NOT_FOUND";
                    }
                    else
                    {
                        elementStartIndex = searchIndex + searchString.Length;
                        elementEndIndex = s.IndexOf("\"", elementStartIndex);

                        newBinaryParam.fileName = s.Substring(elementStartIndex, elementEndIndex - elementStartIndex);
                    }

                    // get contents
                    searchString = lineEnd + lineEnd;
                    searchIndex = s.IndexOf(searchString);

                    if(searchIndex < 0)
                    {
                        newBinaryParam.contents = null;
                    }
                    else
                    {
                        elementStartIndex = searchIndex + searchString.Length;
                        newBinaryParam.contents = new byte[s.Length - elementStartIndex - lineEnd.Length];
                    }
                }
            }
        }

        // ---------[ General ]---------
        /// <summary>Generates a user identifying string to debug with.</summary>
        public static string GenerateUserIdString(UserProfile profile)
        {
            if(profile == null)
            {
                return "NULL_USER_PROFILE";
            }

            string username = profile.username;
            if(string.IsNullOrEmpty(username))
            {
                username = "NO_USERNAME";
            }

            return ("[" + profile.id.ToString() + "] " + username);
        }
    }
}
