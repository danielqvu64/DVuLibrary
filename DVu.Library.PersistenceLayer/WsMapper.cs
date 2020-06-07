using System;
using System.Linq;
using System.Collections;
using System.Collections.Specialized;
using System.Reflection;
using DVu.Library.PersistenceInterface;
using DVu.Library.Utility;

namespace DVu.Library.PersistenceLayer
{
    public class WsRecordMapper : IRecordMapper
    {
        private readonly Type _proxyTypeSelect;
        private readonly Type _proxyTypeInsert;
        private readonly Type _proxyTypeUpdate;
        private readonly Type _proxyTypeDelete;
        private readonly Type _callbackTypeSelect;
        private readonly Type _callbackTypeInsert;
        private readonly Type _callbackTypeUpdate;
        private readonly Type _callbackTypeDelete;
        private readonly WsObjectMapperInfo _mapperInfo;

        public WsRecordMapper(WsObjectMapperInfo mapperInfo)
        {
            _mapperInfo = mapperInfo;
            if (mapperInfo.ProxyClassNameSelect != null)
                _proxyTypeSelect = Type.GetType(mapperInfo.ProxyClassNameSelect);
            if (mapperInfo.ProxyClassNameInsert != null)
                _proxyTypeInsert = Type.GetType(mapperInfo.ProxyClassNameInsert);
            if (mapperInfo.ProxyClassNameUpdate != null)
                _proxyTypeUpdate = Type.GetType(mapperInfo.ProxyClassNameUpdate);
            if (mapperInfo.ProxyClassNameDelete != null)
                _proxyTypeDelete = Type.GetType(mapperInfo.ProxyClassNameDelete);
            if (mapperInfo.ProxyCallbackClassNameSelect != null)
                _callbackTypeSelect = Type.GetType(mapperInfo.ProxyCallbackClassNameSelect);
            if (mapperInfo.ProxyCallbackClassNameInsert != null)
                _callbackTypeInsert = Type.GetType(mapperInfo.ProxyCallbackClassNameInsert);
            if (mapperInfo.ProxyCallbackClassNameUpdate != null)
                _callbackTypeUpdate = Type.GetType(mapperInfo.ProxyCallbackClassNameUpdate);
            if (mapperInfo.ProxyCallbackClassNameDelete != null)
                _callbackTypeDelete = Type.GetType(mapperInfo.ProxyCallbackClassNameDelete);
        }

        public void Get(IPersistenceObject persistenceObject)
        {
            if (_proxyTypeSelect == null)
                throw new ApplicationException(string.Format("Proxy Get Method is not defined for object {0}.", persistenceObject.GetType().Name));
            using (var proxy = (IDisposable)Activator.CreateInstance(_proxyTypeSelect))
            {
                try
                {
                    var fieldMapperCollection = MapperFactory.GetInstance().GetFieldMappers(persistenceObject.GetType().FullName);
                    var methodInfo = _proxyTypeSelect.GetMethod(_mapperInfo.MethodNameSelect);
                    var parameterValueCollection = PopulateMethodParameters(methodInfo, fieldMapperCollection, persistenceObject.ObjectKey);
                    ProcessProxyCallBack(proxy, _callbackTypeSelect, _mapperInfo.ProxyCallbackPropertyNameSelect);
                    var result = methodInfo.Invoke(proxy, parameterValueCollection);
                    //Populate(persistenceObject, result);
                    Populate(persistenceObject, methodInfo, parameterValueCollection, result);
                }
                catch (TargetInvocationException ex)
                {
                    if (ex.InnerException != null)
                        throw ex.InnerException;
                    throw;
                }
            }
        }

        public void Insert(IPersistenceObject persistenceObject)
        {
            if (_proxyTypeInsert == null)
                throw new ApplicationException(string.Format("Proxy Insert Method is not defined for object {0}.", persistenceObject.GetType().Name));
            using (var proxy = (IDisposable)Activator.CreateInstance(_proxyTypeInsert))
            {
                try
                {
                    var fieldMapperCollection = MapperFactory.GetInstance().GetFieldMappers(persistenceObject.GetType().FullName);
                    var methodInfo = _proxyTypeInsert.GetMethod(_mapperInfo.MethodNameInsert);
                    var parameterValueCollection = PopulateMethodParameters(methodInfo, fieldMapperCollection, persistenceObject);
                    ProcessProxyCallBack(proxy, _callbackTypeInsert, _mapperInfo.ProxyCallbackPropertyNameInsert);
                    var result = methodInfo.Invoke(proxy, parameterValueCollection);
                    Populate(persistenceObject, methodInfo, parameterValueCollection, result);
                }
                catch (TargetInvocationException ex)
                {
                    if (ex.InnerException != null)
                        throw ex.InnerException;
                    throw;
                }
            }
        }

        public void Update(IPersistenceObject persistenceObject)
        {
            if (_proxyTypeUpdate == null)
                throw new ApplicationException(string.Format("Proxy Update Method is not defined for object {0}.", persistenceObject.GetType().Name));
            using (var proxy = (IDisposable)Activator.CreateInstance(_proxyTypeUpdate))
            {
                try
                {
                    var fieldMapperCollection = MapperFactory.GetInstance().GetFieldMappers(persistenceObject.GetType().FullName);
                    var methodInfo = _proxyTypeUpdate.GetMethod(_mapperInfo.MethodNameUpdate);
                    var parameterValueCollection = PopulateMethodParameters(methodInfo, fieldMapperCollection, persistenceObject);
                    ProcessProxyCallBack(proxy, _callbackTypeUpdate, _mapperInfo.ProxyCallbackPropertyNameUpdate);
                    var result = methodInfo.Invoke(proxy, parameterValueCollection);
                    Populate(persistenceObject, methodInfo, parameterValueCollection, result);
                    SyncUpObjectKeyAfterUpdate(persistenceObject);
                }
                catch (TargetInvocationException ex)
                {
                    if (ex.InnerException != null)
                        throw ex.InnerException;
                    throw;
                }
            }
        }

        public void Delete(IPersistenceObject persistenceObject)
        {
            if (_proxyTypeDelete == null)
                throw new ApplicationException(string.Format("Proxy Delete Method is not defined for object {0}.", persistenceObject.GetType().Name));
            using (var proxy = (IDisposable)Activator.CreateInstance(_proxyTypeDelete))
            {
                try
                {
                    var fieldMapperCollection = MapperFactory.GetInstance().GetFieldMappers(persistenceObject.GetType().FullName);
                    var methodInfo = _proxyTypeDelete.GetMethod(_mapperInfo.MethodNameDelete);
                    var parameterValueCollection = PopulateMethodParameters(methodInfo, fieldMapperCollection, persistenceObject.ObjectKey);
                    ProcessProxyCallBack(proxy, _callbackTypeDelete, _mapperInfo.ProxyCallbackPropertyNameDelete);
                    var result = methodInfo.Invoke(proxy, parameterValueCollection);
                    Populate(persistenceObject, methodInfo, parameterValueCollection, result);
                }
                catch (TargetInvocationException ex)
                {
                    if (ex.InnerException != null)
                        throw ex.InnerException;
                    throw;
                }
            }
        }

        internal static void ProcessProxyCallBack(object proxy, Type callbackObjectType, string callbackPropertyName)
        {
            if (callbackObjectType == null)
                return;
            var callbackObject = Activator.CreateInstance(callbackObjectType, true);
            var callBackDelegateInfo = callbackObjectType.GetProperty(callbackPropertyName, typeof(WsProxyFinanlizeCallBack), new Type[0]);
            if (callbackObject != null && callBackDelegateInfo != null)
                ((WsProxyFinanlizeCallBack)GeneralUtility.GetInstance().GetMemberValue(callbackObject, callBackDelegateInfo.Name)).Invoke((System.ServiceModel.ICommunicationObject)proxy);
        }

        internal static object[] PopulateMethodParameters(MethodInfo methodInfo, HybridDictionary fieldMapperCollection, object source)
        {
            var gu = GeneralUtility.GetInstance();
            var sourceType = source.GetType();
            var parameterInfoCollection = from paramInfo in methodInfo.GetParameters()
                                          orderby paramInfo.Position
                                          select new { paramInfo.Name, paramInfo.IsOut };
            var parameterValueCollection = new object[parameterInfoCollection.Count()];
            var index = 0;
            foreach (var parameterInfo in parameterInfoCollection)
            {
                if (!parameterInfo.IsOut)
                {
                    var mapperFound = false;
                    foreach (DictionaryEntry item in fieldMapperCollection)
                    {
                        var itemValue = item.Value.ToString();
                        var itemKey = item.Key.ToString();
                        if (itemValue != parameterInfo.Name) continue;
                        mapperFound = true;
                        if (source is IDictionary)
                        {
                            var dictionary = (IDictionary)source;
                            if (dictionary.Contains(itemKey))
                                parameterValueCollection[index] = dictionary[itemKey];
                            else
                                throw new ApplicationException(string.Format("Non-existence business object field specified in configuration for WebMethod Parameter: {0}", parameterInfo.Name));
                        }
                        else
                        {
                            if (sourceType.GetMember(itemKey, BindingFlags.Public | BindingFlags.Instance).Length > 0)
                                parameterValueCollection[index] = gu.GetMemberValue(source, itemKey);
                            else
                                throw new ApplicationException(string.Format("Non-existence business object field specified in configuration for WebMethod Parameter: {0}", parameterInfo.Name));
                        }
                        break;
                    }
                    if (!mapperFound) // implied name from parameterInfo.Name
                    {
                        if (source is IDictionary)
                        {
                            var dictionary = (IDictionary)source;
                            if (dictionary.Contains(parameterInfo.Name))
                                parameterValueCollection[index] = dictionary[parameterInfo.Name];
                            else
                                throw new ApplicationException(string.Format("Non-existence business object field specified in configuration for WebMethod Parameter: {0}", parameterInfo.Name));
                        }
                        else
                        {
                            if (sourceType.GetMember(parameterInfo.Name, BindingFlags.Public | BindingFlags.Instance).Length > 0)
                                parameterValueCollection[index] = gu.GetMemberValue(source, parameterInfo.Name);
                            else
                                throw new ApplicationException(string.Format("Non-existence business object field specified in configuration for WebMethod Parameter: {0}", parameterInfo.Name));
                        }
                    }
                }
                index++;
            }
            return parameterValueCollection;
        }

        //internal static void Populate(IPersistenceObject persistenceObject, object result)
        //{
        //    if (result == null)
        //        return;
        //    var gu = GeneralUtility.GetInstance();
        //    var persistenceObjectType = persistenceObject.GetType();
        //    var fieldMapperCollection = MapperFactory.GetInstance().GetFieldMappers(persistenceObjectType.FullName);

        //    var fieldPropertyNameCollection = (from propertyInfo in persistenceObjectType.GetProperties(BindingFlags.Instance | BindingFlags.Public)
        //                                       where propertyInfo.GetCustomAttributes(typeof(NotMappedAttribute), true).Length == 0
        //                                       select propertyInfo.Name)
        //                                      .Union
        //                                      (from fieldInfo in persistenceObjectType.GetFields(BindingFlags.Instance | BindingFlags.Public)
        //                                       where fieldInfo.GetCustomAttributes(typeof(NotMappedAttribute), true).Length == 0
        //                                       select fieldInfo.Name);

        //    var resultType = result.GetType();
        //    foreach (var fieldPropertyName in fieldPropertyNameCollection)
        //        if (fieldMapperCollection.Contains(fieldPropertyName))
        //        {
        //            if (resultType.GetMember(fieldMapperCollection[fieldPropertyName].ToString(), BindingFlags.Public | BindingFlags.Instance).Length > 0)
        //                gu.SetMemberValue(persistenceObject, fieldPropertyName, gu.GetMemberValue(result, fieldMapperCollection[fieldPropertyName].ToString()));
        //            else
        //                throw new ApplicationException(string.Format("WebMethodResult Property: {0} does not exist for business object field: {1}.", fieldMapperCollection[fieldPropertyName], fieldPropertyName));
        //        }
        //        else
        //        { // implied name from fieldPropertyName
        //            if (resultType.GetMember(fieldPropertyName, BindingFlags.Public | BindingFlags.Instance).Length > 0)
        //                gu.SetMemberValue(persistenceObject, fieldPropertyName, gu.GetMemberValue(result, fieldPropertyName));
        //            else
        //                throw new ApplicationException(string.Format("Implied WebMethodResult Property: {0} does not exist for business object field: {1}.", fieldPropertyName, fieldPropertyName));
        //        }
        //}

        internal static void Populate(IPersistenceObject persistenceObject, MethodInfo methodInfo, object[] returnParameterValueCollection, object returnResult)
        {
            if (returnParameterValueCollection == null)
                return;

            var gu = GeneralUtility.GetInstance();
            var persistenceObjectType = persistenceObject.GetType();
            var fieldMapperCollection = MapperFactory.GetInstance().GetFieldMappers(persistenceObjectType.FullName);

            var parameterNameCollection = from paramInfo in methodInfo.GetParameters()
                                          orderby paramInfo.Position
                                          select paramInfo.Name;

            foreach (DictionaryEntry item in fieldMapperCollection)
            {
                var itemValue = item.Value.ToString();
                var itemKey = item.Key.ToString();
                if (itemValue.IndexOf("{returnParameter}") == 0)
                {
                    if (itemValue.IndexOf("{returnParameter}.") == 0)
                        SetMemberValue(persistenceObjectType, persistenceObject, itemKey, gu.GetMemberValue(returnResult, itemValue.Substring("{returnParameter}.".Length)));
                    else
                        SetMemberValue(persistenceObjectType, persistenceObject, itemKey, returnResult);
                }
                else
                {
                    var index = 0;
                    foreach (var parameterName in parameterNameCollection)
                    {
                        if (itemValue.IndexOf(parameterName) == 0)
                        {
                            if (itemValue.IndexOf(parameterName + ".") == 0)
                                SetMemberValue(persistenceObjectType, persistenceObject, itemKey, gu.GetMemberValue(returnParameterValueCollection[index], itemValue.Substring(parameterName.Length + 1)));
                            else if (itemValue.IndexOf("[") > -1)
                                SetMemberValue(persistenceObjectType, persistenceObject, itemKey, gu.GetMemberValue(returnParameterValueCollection[index], itemValue));
                            else
                                SetMemberValue(persistenceObjectType, persistenceObject, itemKey, returnParameterValueCollection[index]);
                            break;
                        }
                        // implied names
                        if (persistenceObjectType.GetMember(parameterName, BindingFlags.Public | BindingFlags.Instance).Length > 0)
                            SetMemberValue(persistenceObjectType, persistenceObject, parameterName, returnParameterValueCollection[index]);
                        index++;
                    }
                }
            }
        }

        internal static void SetMemberValue(Type persistenceObjectType, IPersistenceObject persistenceObject, string targetMemberName, object value)
        {
            var gu = GeneralUtility.GetInstance();
            var memberType = gu.GetMemberType(persistenceObjectType, persistenceObject, targetMemberName);
            if (memberType != null && memberType.GetInterface("IPersistenceObject") != null &&
                PersistenceFacade.GetInstance().HasObjectToObjectMapper(memberType.FullName))
            {
                var memberObject = Activator.CreateInstance(memberType, true);
                ((IPersistenceObject)memberObject).LoadFromObject(value);
                gu.SetMemberValue(persistenceObject, targetMemberName, memberObject);
            }
            else
                gu.SetMemberValue(persistenceObject, targetMemberName, value);
        }

        private static void SyncUpObjectKeyAfterUpdate(IPersistenceObject persistenceObject)
        {
            var gu = GeneralUtility.GetInstance();
            var fielNameCollection =
                (from fieldInfo in persistenceObject.ObjectKeyType.GetFields(BindingFlags.Public | BindingFlags.Instance)
                 select fieldInfo.Name)
                .Union
                (from propertyInfo in persistenceObject.ObjectKeyType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                 select propertyInfo.Name);
            foreach (var fieldName in fielNameCollection)
                gu.SetMemberValue(persistenceObject.ObjectKey, fieldName, gu.GetMemberValue(persistenceObject, fieldName));
        }
    }

    public class WsSetMapper : IRecordSetMapper
    {
        private readonly Type _proxyTypeSelect;
        private readonly Type _callbackTypeSelect;
        private readonly WsObjectMapperInfo _mapperInfo;

        public WsSetMapper(WsObjectMapperInfo mapperInfo)
        {
            _mapperInfo = mapperInfo;
            if (mapperInfo.ProxyClassNameSelect != null)
                _proxyTypeSelect = Type.GetType(mapperInfo.ProxyClassNameSelect);
            if (mapperInfo.ProxyCallbackClassNameSelect != null)
                _callbackTypeSelect = Type.GetType(mapperInfo.ProxyCallbackClassNameSelect);
        }

        public void Get(IPersistenceObjectCollection persistenceObjectCollection, IObjectKey parentObjectKey)
        {
            if (_proxyTypeSelect == null)
                throw new ApplicationException(string.Format("Proxy Get Method is not defined for object {0}.", persistenceObjectCollection.GetType().Name));
            using (var proxy = (IDisposable)Activator.CreateInstance(_proxyTypeSelect))
            {
                try
                {
                    var fieldMapperCollection = MapperFactory.GetInstance().GetFieldMappers(persistenceObjectCollection.GetType().FullName);
                    var methodInfo = _proxyTypeSelect.GetMethod(_mapperInfo.MethodNameSelect);
                    var parameterValueCollection = WsRecordMapper.PopulateMethodParameters(methodInfo, fieldMapperCollection, persistenceObjectCollection.ObjectKey);
                    WsRecordMapper.ProcessProxyCallBack(proxy, _callbackTypeSelect, _mapperInfo.ProxyCallbackPropertyNameSelect);
                    var result = methodInfo.Invoke(proxy, parameterValueCollection);
                    Populate(persistenceObjectCollection, result);
                    WsRecordMapper.Populate(persistenceObjectCollection, methodInfo, parameterValueCollection, result);
                }
                catch (TargetInvocationException ex)
                {
                    if (ex.InnerException != null)
                        throw ex.InnerException;
                    throw;
                }
            }
        }

        public void Get(IPersistenceObjectCollection persistenceObjectCollection, HybridDictionary parameterCollection)
        {
            if (_proxyTypeSelect == null)
                throw new ApplicationException(string.Format("Proxy Get Method is not defined for object {0}.", persistenceObjectCollection.GetType().Name));
            using (var proxy = (IDisposable)Activator.CreateInstance(_proxyTypeSelect))
            {
                try
                {
                    var fieldMapperCollection = MapperFactory.GetInstance().GetFieldMappers(persistenceObjectCollection.GetType().FullName);
                    var methodInfo = _proxyTypeSelect.GetMethod(_mapperInfo.MethodNameSelect);
                    var parameterValueCollection = WsRecordMapper.PopulateMethodParameters(methodInfo, fieldMapperCollection, parameterCollection);
                    WsRecordMapper.ProcessProxyCallBack(proxy, _callbackTypeSelect, _mapperInfo.ProxyCallbackPropertyNameSelect);
                    var result = methodInfo.Invoke(proxy, parameterValueCollection);
                    Populate(persistenceObjectCollection, result);
                    WsRecordMapper.Populate(persistenceObjectCollection, methodInfo, parameterValueCollection, result);
                }
                catch (TargetInvocationException ex)
                {
                    if (ex.InnerException != null)
                        throw ex.InnerException;
                    throw;
                }
            }
        }

        public void Get(IPersistenceObjectCollection persistenceObjectCollection, IObjectKey parentObjectKey, HybridDictionary parameterCollection)
        {
            if (_proxyTypeSelect == null)
                throw new ApplicationException(string.Format("Proxy Get Method is not defined for object {0}.", persistenceObjectCollection.GetType().Name));
            using (var proxy = (IDisposable)Activator.CreateInstance(_proxyTypeSelect))
            {
                try
                {
                    var fieldMapperCollection = MapperFactory.GetInstance().GetFieldMappers(persistenceObjectCollection.GetType().FullName);
                    var methodInfo = _proxyTypeSelect.GetMethod(_mapperInfo.MethodNameSelect);

                    // copy objectKey collection and parameterCollection into a combined collection
                    var objectKeyFieldCollection = parentObjectKey.ToDictionary();
                    var combinedParameterCollection = new HybridDictionary(objectKeyFieldCollection.Count + parameterCollection.Count);
                    foreach (DictionaryEntry item in objectKeyFieldCollection)
                        combinedParameterCollection.Add(item.Key, item.Value);
                    foreach (DictionaryEntry item in parameterCollection)
                        combinedParameterCollection.Add(item.Key, item.Value);

                    var parameterValueCollection = WsRecordMapper.PopulateMethodParameters(methodInfo, fieldMapperCollection, combinedParameterCollection);
                    WsRecordMapper.ProcessProxyCallBack(proxy, _callbackTypeSelect, _mapperInfo.ProxyCallbackPropertyNameSelect);
                    var result = methodInfo.Invoke(proxy, parameterValueCollection);
                    Populate(persistenceObjectCollection, result);
                    WsRecordMapper.Populate(persistenceObjectCollection, methodInfo, parameterValueCollection, result);
                }
                catch (TargetInvocationException ex)
                {
                    if (ex.InnerException != null)
                        throw ex.InnerException;
                    throw;
                }
            }
        }

        private static void Populate(IPersistenceObjectCollection persistenceObjectCollection, object result)
        {
            if (result == null)
                return;
            if (!(result is IList))
                throw new ApplicationException("Unexpected non-collection returns from Web Service");
            var list = ((IList)result);
            persistenceObjectCollection.Initialize(list.Count);
            for (var index = 0; index < list.Count; index++)
            {
                IPersistenceObject persistenceObject = persistenceObjectCollection.CreateObjectForRetrieval();
                //WsRecordMapper.Populate(persistenceObject,  );
                WsRecordMapper.Populate(persistenceObject, list[index]);
                persistenceObjectCollection.AddRetrievedObject(persistenceObject);
            }
        }
    }
}