using System;
using System.Collections.Generic;
using UnityEngine;

namespace ModIO.UI
{
    /// <summary>Manages requests made for ModProfiles.</summary>
    public class ModProfileRequestManager : MonoBehaviour
    {
        // ---------[ FIELDS ]---------
        /// <summary>Cached requests.</summary>
        public Dictionary<string, RequestPage<ModProfile>> requestCache = new Dictionary<string, RequestPage<ModProfile>>();

        /// <summary>Cached profiles.</summary>
        public Dictionary<int, ModProfile> profileCache = new Dictionary<int, ModProfile>();

        // ---------[ FUNCTIONALITY ]---------
        /// <summary>Fetchs page of ModProfiles grabbing from the cache where possible.</summary>
        public virtual void FetchModProfilePage(RequestFilter filter, int offsetIndex, int profileCount,
                                                Action<RequestPage<ModProfile>> onSuccess,
                                                Action<WebRequestError> onError)
        {
            // ensure indicies are positive
            if(offsetIndex < 0) { offsetIndex = 0; }
            if(profileCount < 0) { profileCount = 0; }

            // check if results already cached
            string filterString = filter.GenerateFilterString();
            RequestPage<ModProfile> cachedPage = null;
            if(this.requestCache.TryGetValue(filterString, out cachedPage))
            {
                // early out if no results
                if(cachedPage.resultTotal == 0)
                {
                    if(onSuccess != null)
                    {
                        RequestPage<ModProfile> requestPage = new RequestPage<ModProfile>();
                        requestPage.size = profileCount;
                        requestPage.resultOffset = offsetIndex;
                        requestPage.resultOffset = 0;
                        requestPage.items = new ModProfile[0];

                        onSuccess(requestPage);
                    }
                    return;
                }

                // early out if index is beyond results
                if(offsetIndex >= cachedPage.resultTotal)
                {
                    if(onSuccess != null)
                    {
                        RequestPage<ModProfile> requestPage = new RequestPage<ModProfile>();
                        requestPage.size = profileCount;
                        requestPage.resultOffset = offsetIndex;
                        requestPage.resultTotal = cachedPage.resultTotal;
                        requestPage.items = new ModProfile[0];

                        onSuccess(requestPage);
                    }
                    return;
                }

                // clamp last index
                int clampedLastIndex = offsetIndex + profileCount-1;
                if(clampedLastIndex >= cachedPage.resultTotal)
                {
                    // NOTE(@jackson): cachedPage.resultTotal > 0
                    clampedLastIndex = cachedPage.resultTotal - 1;
                }

                // check if entire result set encompassed by cache
                int cachedLastIndex = cachedPage.resultOffset + cachedPage.items.Length;
                if(cachedPage.resultOffset <= offsetIndex
                   && clampedLastIndex <= cachedLastIndex)
                {
                    // check for nulls
                    bool nullFound = false;
                    for(int arrayIndex = offsetIndex - cachedPage.resultOffset;
                        arrayIndex < cachedPage.items.Length
                        && arrayIndex <= clampedLastIndex - cachedPage.resultOffset
                        && !nullFound;
                        ++arrayIndex)
                    {
                        nullFound = (cachedPage.items[arrayIndex] == null);
                    }

                    // return if no nulls found
                    if(!nullFound)
                    {
                        if(onSuccess != null)
                        {
                            RequestPage<ModProfile> requestPage = new RequestPage<ModProfile>();
                            requestPage.size = profileCount;
                            requestPage.resultOffset = offsetIndex;
                            requestPage.resultTotal = cachedPage.resultTotal;

                            // fill array
                            requestPage.items = new ModProfile[clampedLastIndex - offsetIndex + 1];
                            Array.Copy(cachedPage.items, offsetIndex - cachedPage.resultOffset,
                                       requestPage.items, 0, requestPage.items.Length);

                            onSuccess(requestPage);
                        }
                        return;
                    }
                }
            }

            // PaginationParameters
            APIPaginationParameters pagination = new APIPaginationParameters();
            pagination.offset = offsetIndex;
            pagination.limit = profileCount;

            // Send Request
            APIClient.GetAllMods(filter, pagination,
            (r) =>
            {
                if(this != null)
                {
                    this.CacheRequestPage(filter, r);
                }

                if(onSuccess != null)
                {
                    onSuccess(r);
                }
            }, onError);
        }

        /// <summary>Append the response page to the cached data.</summary>
        public virtual void CacheRequestPage(RequestFilter filter, RequestPage<ModProfile> page)
        {
            // asserts
            Debug.Assert(filter != null);
            Debug.Assert(page != null);

            // store request
            string filterString = filter.GenerateFilterString();
            RequestPage<ModProfile> cachedPage = null;
            if(this.requestCache.TryGetValue(filterString, out cachedPage))
            {
                this.requestCache[filterString] = this.CombinePages(cachedPage, page);
            }
            else
            {
                this.requestCache.Add(filterString, page);
            }

            foreach(ModProfile profile in page.items)
            {
                this.profileCache[profile.id] = profile;
            }
        }

        /// <summary>Combines two response pages.</summary>
        public virtual RequestPage<ModProfile> CombinePages(RequestPage<ModProfile> pageA,
                                                            RequestPage<ModProfile> pageB)
        {
            // asserts
            Debug.Assert(pageA != null);
            Debug.Assert(pageB != null);
            Debug.Assert(pageA.resultTotal == pageB.resultTotal);

            RequestPage<ModProfile> combinedPage = new RequestPage<ModProfile>();

            // calc last indicies
            int pageALastIndex = pageA.resultOffset + pageA.items.Length;
            int pageBLastIndex = pageB.resultOffset + pageB.items.Length;

            // calc offset
            combinedPage.resultOffset = (pageA.resultOffset <= pageB.resultOffset
                                         ? pageA.resultOffset
                                         : pageB.resultOffset);
            // create array
            int newArrayLength = (pageALastIndex >= pageBLastIndex
                                  ? pageALastIndex
                                  : pageBLastIndex) - combinedPage.resultOffset;
            combinedPage.items = new ModProfile[newArrayLength];

            // fill array
            Array.Copy(pageA.items, 0,
                       combinedPage.items,
                       pageA.resultOffset - combinedPage.resultOffset,
                       pageA.items.Length);
            Array.Copy(pageB.items, 0,
                       combinedPage.items,
                       pageB.resultOffset - combinedPage.resultOffset,
                       pageB.items.Length);

            // fill out details
            combinedPage.size = newArrayLength;
            combinedPage.resultTotal = pageA.resultTotal;

            return combinedPage;
        }

        /// <summary>Gets an individual ModProfile by id.</summary>
        public virtual void GetModProfile(int id,
                                          Action<ModProfile> onSuccess, Action<WebRequestError> onError)
        {
            ModProfile profile = null;
            if(profileCache.TryGetValue(id, out profile))
            {
                if(onSuccess != null) { onSuccess(profile); }
                return;
            }

            profile = CacheClient.LoadModProfile(id);
            if(profile != null)
            {
                profileCache.Add(id, profile);
                if(onSuccess != null) { onSuccess(profile); }
                return;
            }

            APIClient.GetMod(id, (p) =>
            {
                if(this != null)
                {
                    profileCache.Add(id, p);
                }

                if(onSuccess != null)
                {
                    onSuccess(p);
                }
            },
            onError);
        }

        /// <summary>Gets a collection of ModProfiles by id.</summary>
        public virtual void GetModProfiles(IList<int> orderedIdList,
                                           Action<IList<ModProfile>> onSuccess,
                                           Action<WebRequestError> onError)
        {
            ModProfile[] results = new ModProfile[orderedIdList.Count];
            List<int> missingIds = new List<int>(orderedIdList.Count);

            // grab from cache
            for(int i = 0; i < orderedIdList.Count; ++i)
            {
                int modId = orderedIdList[i];
                ModProfile profile = null;
                profileCache.TryGetValue(modId, out profile);
                results[i] = profile;

                if(profile == null)
                {
                    missingIds.Add(modId);
                }
            }

            // check disk for any missing profiles
            foreach(ModProfile profile in CacheClient.IterateFilteredModProfiles(missingIds))
            {
                int index = orderedIdList.IndexOf(profile.id);
                if(index >= 0)
                {
                    results[index] = profile;
                }

                missingIds.Remove(profile.id);
            }

            // if no missing profiles, early out
            if(missingIds.Count == 0)
            {
                onSuccess(results);
                return;
            }

            // fetch missing profiles
            RequestFilter filter = new RequestFilter();
            filter.fieldFilters.Add(API.GetAllModsFilterFields.id,
                new InArrayFilter<int>() { filterArray = missingIds.ToArray(), });

            APIClient.GetAllMods(filter, null, (r) =>
            {
                if(this != null)
                {
                    foreach(ModProfile profile in r.items)
                    {
                        profileCache.Add(profile.id, profile);
                    }
                }

                foreach(ModProfile profile in r.items)
                {
                    int i = orderedIdList.IndexOf(profile.id);
                    if(i >= 0)
                    {
                        results[i] = profile;
                    }
                    onSuccess(results);
                }
            }, onError);
        }
    }
}