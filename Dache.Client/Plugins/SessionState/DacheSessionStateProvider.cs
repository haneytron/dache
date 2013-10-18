using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Text;
using System.Web;
using System.Web.Configuration;
using System.Web.Hosting;
using System.Web.SessionState;

namespace Dache.Client.Plugins.SessionState
{
    /// <summary>
    /// The Dache session state provider.
    /// </summary>
    public class DacheSessionStateProvider : SessionStateStoreProviderBase
    {
        // The cache key
        private const string _cacheKey = "__DacheCustomSessionState_SessionID:{0}_ApplicationName:{1}";
        // The cache client
        private readonly ICacheClient _cacheClient = new CacheClient();

        // The application name
        private string _applicationName = null;
        // The session state config
        private SessionStateSection _sessionStateSection = null;

        public override void Initialize(string name, NameValueCollection config)
        {
            // Initialize values from web.config
            if (config == null)
            {
                throw new ArgumentNullException("config");
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                name = "DacheSessionStateProvider";
            }

            if (string.IsNullOrWhiteSpace(config["description"]))
            {
                config.Remove("description");
                config.Add("description", "Dache Session State Store Provider");
            }

            // Initialize the abstract base class
            base.Initialize(name, config);

            // Initialize the application name
            _applicationName = HostingEnvironment.ApplicationVirtualPath;

            // Get <sessionState> configuration element
            var webConfig = WebConfigurationManager.OpenWebConfiguration(_applicationName);
            _sessionStateSection = (SessionStateSection)webConfig.GetSection("system.web/sessionState");

            // Initialize WriteExceptionsToEventLog
            if (string.Equals(config["writeExceptionsToEventLog"], bool.TrueString, StringComparison.OrdinalIgnoreCase))
            {
                // TODO: Inject custom logging to the cache client?
            }
        }

        public override SessionStateStoreData CreateNewStoreData(HttpContext context, int timeout)
        {
            return new SessionStateStoreData(new SessionStateItemCollection(), SessionStateUtility.GetSessionStaticObjects(context), timeout);
        }

        public override void CreateUninitializedItem(HttpContext context, string id, int timeout)
        {
            var now = DateTime.Now;

            _cacheClient.AddOrUpdate(string.Format(_cacheKey, id, _applicationName), new DacheSessionState
            {
                SessionId = id,
                ApplicationName = _applicationName,
                Created = now,
                Expires = now,
                LockDate = now,
                LockId = 0,
                Timeout = timeout,
                Locked = false,
                SessionItems = new byte[0],
                Flags = SessionStateActions.InitializeItem
            });
        }

        public override void Dispose()
        {
            // Do nothing
        }

        public override void EndRequest(HttpContext context)
        {
            // Do nothing
        }

        public override SessionStateStoreData GetItem(HttpContext context, string id, out bool locked, out TimeSpan lockAge, out object lockId, out SessionStateActions actions)
        {
            // Initial values for return value and out parameters
            SessionStateStoreData item = null;
            lockAge = TimeSpan.Zero;
            lockId = null;
            locked = false;
            actions = 0;

            // String to hold serialized SessionStateItemCollection
            byte[] serializedItems = null;
            // True if a record is found in the database
            bool foundRecord = false;
            // Timeout value from the data store
            int timeout = 0;

            var cacheKey = string.Format(_cacheKey, id, _applicationName);
            var now = DateTime.Now;

            // Get the current session
            DacheSessionState currentSession = null;
            _cacheClient.TryGet<DacheSessionState>(cacheKey, out currentSession);

            if (currentSession != null)
            {
                if (currentSession.Expires < now)
                {
                    // The record was expired - mark it as not locked
                    locked = false;
                    // Delete the current session
                    _cacheClient.Remove(cacheKey);
                }
                else
                {
                    foundRecord = true;
                }

                serializedItems = currentSession.SessionItems;
                lockId = currentSession.LockId;
                lockAge = now.Subtract(currentSession.LockDate);
                actions = currentSession.Flags;
                timeout = currentSession.Timeout;
            }

            // If the record was found and you obtained a lock, then set the lockId, clear the actions, and create the SessionStateStoreItem to return.
            if (foundRecord && !locked)
            {
                var incrementedLockId = (int)lockId + 1;
                lockId = incrementedLockId;

                currentSession.LockId = incrementedLockId;
                currentSession.Flags = SessionStateActions.None;

                // If the actions parameter is not InitializeItem, 
                // deserialize the stored SessionStateItemCollection.
                if (actions == SessionStateActions.InitializeItem)
                {
                    item = CreateNewStoreData(context, (int)_sessionStateSection.Timeout.TotalMinutes);
                }
                else
                {
                    // Deserialize
                    MemoryStream memoryStream = new MemoryStream(serializedItems);
                    SessionStateItemCollection sessionItems = new SessionStateItemCollection();

                    if (memoryStream.Length > 0)
                    {
                        BinaryReader reader = new BinaryReader(memoryStream);
                        sessionItems = SessionStateItemCollection.Deserialize(reader);
                    }

                    item = new SessionStateStoreData(sessionItems, SessionStateUtility.GetSessionStaticObjects(context), timeout);
                }
            }

            return item;
        }

        public override SessionStateStoreData GetItemExclusive(HttpContext context, string id, out bool locked, out TimeSpan lockAge, out object lockId, out SessionStateActions actions)
        {
            var cacheKey = string.Format(_cacheKey, id, _applicationName);
            var now = DateTime.Now;

            // Get the current session
            DacheSessionState currentSession = null;
            _cacheClient.TryGet<DacheSessionState>(cacheKey, out currentSession);

            // Obtain a lock if possible. Ignore the record if it is expired.
                
            // Set locked to true if the record was not updated and false if it was
            locked = !(currentSession != null && !currentSession.Locked && currentSession.Expires > now);

            if (!locked)
            {
                currentSession.Locked = true;
                currentSession.LockDate = now;

                _cacheClient.AddOrUpdate(cacheKey, currentSession);
            }

            return GetItem(context, id, out locked, out lockAge, out lockId, out actions);
        }

        public override void InitializeRequest(HttpContext context)
        {
            // Do nothing
        }

        public override void ReleaseItemExclusive(HttpContext context, string id, object lockId)
        {
            var cacheKey = string.Format(_cacheKey, id, _applicationName);
            var now = DateTime.Now;

            // Get the current session
            DacheSessionState currentSession = null;
            if (_cacheClient.TryGet<DacheSessionState>(cacheKey, out currentSession))
            {
                if (currentSession.LockId == (int)lockId)
                {
                    currentSession.Locked = false;
                    currentSession.Expires = now.AddMinutes(_sessionStateSection.Timeout.TotalMinutes);

                    _cacheClient.AddOrUpdate(cacheKey, currentSession);
                }
            }
        }

        public override void RemoveItem(HttpContext context, string id, object lockId, SessionStateStoreData item)
        {
            var cacheKey = string.Format(_cacheKey, id, _applicationName);
            var now = DateTime.Now;

            // Get the current session
            DacheSessionState currentSession = null;
            if (_cacheClient.TryGet<DacheSessionState>(cacheKey, out currentSession))
            {
                if (currentSession.LockId == (int)lockId)
                {
                    _cacheClient.Remove(cacheKey);
                }
            }
        }

        public override void ResetItemTimeout(HttpContext context, string id)
        {
            var cacheKey = string.Format(_cacheKey, id, _applicationName);
            DacheSessionState currentSession = null;
            if (_cacheClient.TryGet<DacheSessionState>(cacheKey, out currentSession))
            {
                currentSession.Expires = DateTime.Now.AddMinutes(_sessionStateSection.Timeout.TotalMinutes);
                _cacheClient.AddOrUpdate(cacheKey, currentSession);
            }
        }

        public override void SetAndReleaseItemExclusive(HttpContext context, string id, SessionStateStoreData item, object lockId, bool newItem)
        {
            // Serialize the SessionStateItemCollection as a string
            var sessionItems = (SessionStateItemCollection)item.Items;
            byte[] serializedItems = new byte[0];
            if (sessionItems != null)
            {
                var memoryStream = new MemoryStream();
                var writer = new BinaryWriter(memoryStream);
                sessionItems.Serialize(writer);
                writer.Close();
                serializedItems = memoryStream.ToArray();
            }

            var now = DateTime.Now;
            var cacheKey = string.Format(_cacheKey, id, _applicationName);

            // Try and get the existing session item
            DacheSessionState currentSession = null;
            _cacheClient.TryGet<DacheSessionState>(cacheKey, out currentSession);

            // Create or update the session item
            _cacheClient.AddOrUpdate(cacheKey, new DacheSessionState
            {
                SessionId = id,
                ApplicationName = _applicationName,
                Created = currentSession != null ? currentSession.Created : now,
                Expires = now.AddMinutes(item.Timeout),
                LockDate = currentSession != null ? currentSession.LockDate : now,
                LockId = currentSession != null ? currentSession.LockId : 0,
                Timeout = currentSession != null ? currentSession.Timeout : item.Timeout,
                Locked = false,
                SessionItems = serializedItems,
                Flags = currentSession != null ? currentSession.Flags : SessionStateActions.None
            });
        }

        public override bool SetItemExpireCallback(SessionStateItemExpireCallback expireCallback)
        {
            return false;
        }
    }
}
