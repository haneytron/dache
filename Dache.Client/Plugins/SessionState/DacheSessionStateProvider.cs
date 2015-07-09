using Dache.Client.Configuration;
using Dache.Client.Serialization;
using Dache.Core.Logging;
using System;
using System.Collections.Specialized;
using System.IO;
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
        // The cache client needs to be static because n copies of this class get instantiated
        private static readonly ICacheClient _cacheClient = null;

        // The application name
        private string _applicationName = null;
        // The session state config
        private SessionStateSection _sessionStateSection = null;

        /// <summary>
        /// Static constructor.
        /// </summary>
        static DacheSessionStateProvider()
        {
            // Use the user provided settings
            var cacheClientConfig = CacheClientConfigurationSection.Settings;

            if (cacheClientConfig == null) throw new InvalidOperationException("You cannot use the Dache session state provider without supplying Dache configuration in your web or app config file");

            // TODO: the below sucks. Improve it.

            // Clone to protect from mutated state
            var cacheClientConfigClone = (CacheClientConfigurationSection)cacheClientConfig.Clone();
            // Use ProtoBuf serializer
            cacheClientConfigClone.CustomSerializer.Type = typeof(ProtoBufSerializer).AssemblyQualifiedName;
            // Use Debug logger
            cacheClientConfigClone.CustomLogger.Type = typeof(DebugLogger).AssemblyQualifiedName;
            _cacheClient = new CacheClient(cacheClientConfigClone);
        }

        /// <summary>
        /// Initializes the provider.
        /// </summary>
        /// <param name="name">The friendly name of the provider.</param>
        /// <param name="config">A collection of the name/value pairs representing the provider-specific attributes specified in the configuration for this provider.</param>
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

        /// <summary>
        /// Creates a new SessionStateStoreData object to be used for the current request.
        /// </summary>
        /// <param name="context">The HttpContext for the current request.</param>
        /// <param name="timeout">The session-state Timeout value for the new SessionStateStoreData.</param>
        /// <returns>A new SessionStateStoreData for the current request.</returns>
        public override SessionStateStoreData CreateNewStoreData(HttpContext context, int timeout)
        {
            return new SessionStateStoreData(new SessionStateItemCollection(), SessionStateUtility.GetSessionStaticObjects(context), timeout);
        }

        /// <summary>
        /// Adds a new session-state item to the data store.
        /// </summary>
        /// <param name="context">The HttpContext for the current request.</param>
        /// <param name="id">The SessionID for the current request.</param>
        /// <param name="timeout">The session Timeout for the current request.</param>
        public override void CreateUninitializedItem(HttpContext context, string id, int timeout)
        {
            var now = DateTime.UtcNow;

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

        /// <summary>
        /// Releases all resources used by the SessionStateStoreProviderBase implementation.
        /// </summary>
        public override void Dispose()
        {
            // Do nothing
        }

        /// <summary>
        /// Called by the SessionStateModule object at the end of a request.
        /// </summary>
        /// <param name="context">The HttpContext for the current request.</param>
        public override void EndRequest(HttpContext context)
        {
            // Do nothing
        }

        /// <summary>
        /// Returns read-only session-state data from the session data store.
        /// </summary>
        /// <param name="context">The HttpContext for the current request.</param>
        /// <param name="id">The SessionID for the current request.</param>
        /// <param name="locked">When this method returns, contains a Boolean value that is set to true if the requested session item is locked at the session data store; otherwise, false.</param>
        /// <param name="lockAge">When this method returns, contains a TimeSpan object that is set to the amount of time that an item in the session data store has been locked.</param>
        /// <param name="lockId">When this method returns, contains an object that is set to the lock identifier for the current request. For details on the lock identifier, see "Locking Session-Store Data" in the SessionStateStoreProviderBase class summary.</param>
        /// <param name="actions">When this method returns, contains one of the SessionStateActions values, indicating whether the current session is an uninitialized, cookieless session.</param>
        /// <returns>A SessionStateStoreData populated with session values and information from the session data store.</returns>
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
            var now = DateTime.UtcNow;

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
                    var sessionItems = new SessionStateItemCollection();

                    // Deserialize
                    using (var memoryStream = new MemoryStream(serializedItems))
                    {
                        if (memoryStream.Length > 0)
                        {
                            using (var reader = new BinaryReader(memoryStream))
                            {
                                sessionItems = SessionStateItemCollection.Deserialize(reader);
                            }
                            
                        }
                    }

                    item = new SessionStateStoreData(sessionItems, SessionStateUtility.GetSessionStaticObjects(context), timeout);
                }
            }

            return item;
        }

        /// <summary>
        /// Returns read-only session-state data from the session data store.
        /// </summary>
        /// <param name="context">The HttpContext for the current request.</param>
        /// <param name="id">The SessionID for the current request.</param>
        /// <param name="locked">When this method returns, contains a Boolean value that is set to true if a lock is successfully obtained; otherwise, false.</param>
        /// <param name="lockAge">When this method returns, contains a TimeSpan object that is set to the amount of time that an item in the session data store has been locked.</param>
        /// <param name="lockId">When this method returns, contains an object that is set to the lock identifier for the current request. For details on the lock identifier, see "Locking Session-Store Data" in the SessionStateStoreProviderBase class summary.</param>
        /// <param name="actions">When this method returns, contains one of the SessionStateActions values, indicating whether the current session is an uninitialized, cookieless session.</param>
        /// <returns>A SessionStateStoreData populated with session values and information from the session data store.</returns>
        public override SessionStateStoreData GetItemExclusive(HttpContext context, string id, out bool locked, out TimeSpan lockAge, out object lockId, out SessionStateActions actions)
        {
            var cacheKey = string.Format(_cacheKey, id, _applicationName);
            var now = DateTime.UtcNow;

            // Get the current session
            DacheSessionState currentSession = null;
            _cacheClient.TryGet<DacheSessionState>(cacheKey, out currentSession);

            // Obtain a lock if possible. Ignore the record if it is expired.
            // TODO: this isn't actually thread safe due to a lack of "global" lock
                
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

        /// <summary>
        /// Called by the SessionStateModule object for per-request initialization.
        /// </summary>
        /// <param name="context">The HttpContext for the current request.</param>
        public override void InitializeRequest(HttpContext context)
        {
            // Do nothing
        }

        /// <summary>
        /// Releases a lock on an item in the session data store.
        /// </summary>
        /// <param name="context">The HttpContext for the current request.</param>
        /// <param name="id">The session identifier for the current request.</param>
        /// <param name="lockId">The lock identifier for the current request.</param>
        public override void ReleaseItemExclusive(HttpContext context, string id, object lockId)
        {
            var cacheKey = string.Format(_cacheKey, id, _applicationName);
            var now = DateTime.UtcNow;

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

        /// <summary>
        /// Deletes item data from the session data store.
        /// </summary>
        /// <param name="context">The HttpContext for the current request.</param>
        /// <param name="id">The session identifier for the current request.</param>
        /// <param name="lockId">The lock identifier for the current request.</param>
        /// <param name="item">The SessionStateStoreData that represents the item to delete from the data store.</param>
        public override void RemoveItem(HttpContext context, string id, object lockId, SessionStateStoreData item)
        {
            var cacheKey = string.Format(_cacheKey, id, _applicationName);
            var now = DateTime.UtcNow;

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

        /// <summary>
        /// Updates the expiration date and time of an item in the session data store.
        /// </summary>
        /// <param name="context">The HttpContext for the current request.</param>
        /// <param name="id">The session identifier for the current request.</param>
        public override void ResetItemTimeout(HttpContext context, string id)
        {
            var cacheKey = string.Format(_cacheKey, id, _applicationName);
            DacheSessionState currentSession = null;
            if (_cacheClient.TryGet<DacheSessionState>(cacheKey, out currentSession))
            {
                currentSession.Expires = DateTime.UtcNow.AddMinutes(_sessionStateSection.Timeout.TotalMinutes);
                _cacheClient.AddOrUpdate(cacheKey, currentSession);
            }
        }

        /// <summary>
        /// Updates the session-item information in the session-state data store with values from the current request, and clears the lock on the data.
        /// </summary>
        /// <param name="context">The HttpContext for the current request.</param>
        /// <param name="id">The session identifier for the current request.</param>
        /// <param name="item">The SessionStateStoreData object that contains the current session values to be stored.</param>
        /// <param name="lockId">The lock identifier for the current request.</param>
        /// <param name="newItem">true to identify the session item as a new item; false to identify the session item as an existing item.</param>
        public override void SetAndReleaseItemExclusive(HttpContext context, string id, SessionStateStoreData item, object lockId, bool newItem)
        {
            // Serialize the SessionStateItemCollection as a string
            var sessionItems = (SessionStateItemCollection)item.Items;
            byte[] serializedItems = new byte[0];
            if (sessionItems != null)
            {
                using (var memoryStream = new MemoryStream())
                {
                    using (var writer = new BinaryWriter(memoryStream))
                    {
                        sessionItems.Serialize(writer);
                        writer.Close();
                        serializedItems = memoryStream.ToArray();
                    }
                }
            }

            var now = DateTime.UtcNow;
            var cacheKey = string.Format(_cacheKey, id, _applicationName);

            // Try and get the existing session item
            DacheSessionState currentSession = null;
            bool success = _cacheClient.TryGet<DacheSessionState>(cacheKey, out currentSession);

            // Create or update the session item
            _cacheClient.AddOrUpdate(cacheKey, new DacheSessionState
            {
                SessionId = id,
                ApplicationName = _applicationName,
                Created = success ? currentSession.Created : now,
                Expires = now.AddMinutes(item.Timeout),
                LockDate = success ? currentSession.LockDate : now,
                LockId = success ? currentSession.LockId : 0,
                Timeout = success ? currentSession.Timeout : item.Timeout,
                Locked = false,
                SessionItems = serializedItems,
                Flags = success ? currentSession.Flags : SessionStateActions.None
            });
        }

        /// <summary>
        /// Sets a reference to the SessionStateItemExpireCallback delegate for the Session_OnEnd event defined in the Global.asax file.
        /// </summary>
        /// <param name="expireCallback">The SessionStateItemExpireCallback delegate for the Session_OnEnd event defined in the Global.asax file.</param>
        /// <returns>true if the session-state store provider supports calling the Session_OnEnd event; otherwise, false.</returns>
        public override bool SetItemExpireCallback(SessionStateItemExpireCallback expireCallback)
        {
            return false;
        }
    }
}
