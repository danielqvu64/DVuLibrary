using System;
using System.Configuration;
using System.Xml.XPath;
using System.Xml.Linq;
using System.Linq;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using DVu.Library.PersistenceInterface;

namespace DVu.Library.PersistenceLayer
{
    internal sealed class MapperFactory
    {
        private static volatile MapperFactory _instance;
        private static readonly object SyncRoot = new object();

        private MapperFactory() { }

        public static MapperFactory GetInstance()
        {
            if (_instance == null)
                lock (SyncRoot)
                {
                    if (_instance == null)
                        _instance = new MapperFactory();
                }
            return _instance;
        }

        private XElement _mapperXml;
        private readonly HybridDictionary _objectMapperCollection = new HybridDictionary(); // cache
        private readonly HybridDictionary _fieldMapperCollection = new HybridDictionary(); // cache
        private Dictionary<string, Connection> _connectionCollection;
        
        private XElement MapperXml
        {
            get {
                return _mapperXml ??
                       (_mapperXml = XElement.Load(ConfigurationManager.AppSettings["BusinessObjectMappersFile"]));
            }
        }

        internal object GetObjectMapper(string businessObjectClassName, ObjectMapperType objectMapperType)
        {
            var objectMapperKey = string.Format("{0}|{1}", businessObjectClassName, objectMapperType);
            object objectMapper = null;
            if (!_objectMapperCollection.Contains(objectMapperKey))
            {
                var mapperObject =
                    (from businessObjectElement in MapperXml.Elements("BusinessObject")
                    where (string)businessObjectElement.Attribute("BusinessObjectClassName") == businessObjectClassName
                    from objectMapperElement in businessObjectElement.XPathSelectElements(string.Format("ObjectMapper[@Type='{0}']", objectMapperType))
                    select objectMapperElement).FirstOrDefault();

                object[] args = null;
                var objectMapperClassName = (string)((IEnumerable)MapperXml.XPathEvaluate("MapperClasses/Mapper[@Type='" + objectMapperType.GetStringValue() + "']/@MapperClassName")).Cast<XAttribute>().FirstOrDefault();
                switch (objectMapperType)
                {
                    case ObjectMapperType.RdbRecord:
                    case ObjectMapperType.RdbSet:
                        if (mapperObject != null)
                            args = new object[] {
                                                    new RdbObjectMapperInfo
                                                        {
                                                            MethodNameDelete = (string)((IEnumerable)mapperObject.XPathEvaluate("MethodMapper[@Type='Delete']/@MethodName")).Cast<XAttribute>().FirstOrDefault(),
                                                            MethodNameInsert = (string)((IEnumerable)mapperObject.XPathEvaluate("MethodMapper[@Type='Insert']/@MethodName")).Cast<XAttribute>().FirstOrDefault(),
                                                            MethodNameSelect = (string)((IEnumerable)mapperObject.XPathEvaluate("MethodMapper[@Type='Select']/@MethodName")).Cast<XAttribute>().FirstOrDefault(),
                                                            MethodNameUpdate = (string)((IEnumerable)mapperObject.XPathEvaluate("MethodMapper[@Type='Update']/@MethodName")).Cast<XAttribute>().FirstOrDefault(),
                                                            RdbConnectionKey = (string)mapperObject.Attribute("RdbConnectionKey"),
                                                            RdbEntityName = (string)mapperObject.Attribute("RdbEntityName")
                                                        }
                                                };
                        break;
                    case ObjectMapperType.WsRecord:
                    case ObjectMapperType.WsSet:
                        if (mapperObject != null)
                            args = new object[] {
                                                    new WsObjectMapperInfo
                                                        {
                                                            MethodNameDelete = (string)((IEnumerable)mapperObject.XPathEvaluate("MethodMapper[@Type='Delete']/@MethodName")).Cast<XAttribute>().FirstOrDefault(),
                                                            MethodNameInsert = (string)((IEnumerable)mapperObject.XPathEvaluate("MethodMapper[@Type='Insert']/@MethodName")).Cast<XAttribute>().FirstOrDefault(),
                                                            MethodNameSelect = (string)((IEnumerable)mapperObject.XPathEvaluate("MethodMapper[@Type='Select']/@MethodName")).Cast<XAttribute>().FirstOrDefault(),
                                                            MethodNameUpdate = (string)((IEnumerable)mapperObject.XPathEvaluate("MethodMapper[@Type='Update']/@MethodName")).Cast<XAttribute>().FirstOrDefault(),
                                                            ProxyClassNameDelete = (string)((IEnumerable)mapperObject.XPathEvaluate("MethodMapper[@Type='Delete']/@ProxyClassName")).Cast<XAttribute>().FirstOrDefault(),
                                                            ProxyClassNameInsert = (string)((IEnumerable)mapperObject.XPathEvaluate("MethodMapper[@Type='Insert']/@ProxyClassName")).Cast<XAttribute>().FirstOrDefault(),
                                                            ProxyClassNameSelect = (string)((IEnumerable)mapperObject.XPathEvaluate("MethodMapper[@Type='Select']/@ProxyClassName")).Cast<XAttribute>().FirstOrDefault(),
                                                            ProxyClassNameUpdate = (string)((IEnumerable)mapperObject.XPathEvaluate("MethodMapper[@Type='Update']/@ProxyClassName")).Cast<XAttribute>().FirstOrDefault(),
                                                            ProxyCallbackClassNameDelete = (string)((IEnumerable)mapperObject.XPathEvaluate("MethodMapper[@Type='Delete']/@ProxyCallbackClassName")).Cast<XAttribute>().FirstOrDefault(),
                                                            ProxyCallbackClassNameInsert = (string)((IEnumerable)mapperObject.XPathEvaluate("MethodMapper[@Type='Insert']/@ProxyCallbackClassName")).Cast<XAttribute>().FirstOrDefault(),
                                                            ProxyCallbackClassNameSelect = (string)((IEnumerable)mapperObject.XPathEvaluate("MethodMapper[@Type='Select']/@ProxyCallbackClassName")).Cast<XAttribute>().FirstOrDefault(),
                                                            ProxyCallbackClassNameUpdate = (string)((IEnumerable)mapperObject.XPathEvaluate("MethodMapper[@Type='Update']/@ProxyCallbackClassName")).Cast<XAttribute>().FirstOrDefault(),
                                                            ProxyCallbackPropertyNameDelete = (string)((IEnumerable)mapperObject.XPathEvaluate("MethodMapper[@Type='Delete']/@ProxyCallbackPropertyName")).Cast<XAttribute>().FirstOrDefault(),
                                                            ProxyCallbackPropertyNameInsert = (string)((IEnumerable)mapperObject.XPathEvaluate("MethodMapper[@Type='Insert']/@ProxyCallbackPropertyName")).Cast<XAttribute>().FirstOrDefault(),
                                                            ProxyCallbackPropertyNameSelect = (string)((IEnumerable)mapperObject.XPathEvaluate("MethodMapper[@Type='Select']/@ProxyCallbackPropertyName")).Cast<XAttribute>().FirstOrDefault(),
                                                            ProxyCallbackPropertyNameUpdate = (string)((IEnumerable)mapperObject.XPathEvaluate("MethodMapper[@Type='Update']/@ProxyCallbackPropertyName")).Cast<XAttribute>().FirstOrDefault()
                                                        }
                                                };
                        break;
                    case ObjectMapperType.Obj:
                        break;
                }
                if (objectMapperClassName != null)
                {
                    var type = Type.GetType(objectMapperClassName);
                    if (type != null)
                        objectMapper = type.InvokeMember(null, BindingFlags.CreateInstance | BindingFlags.Instance | BindingFlags.Public, null, null, args);
                    _objectMapperCollection.Add(objectMapperKey, objectMapper);
                }
                return objectMapper;
            }
            return _objectMapperCollection[objectMapperKey];
        }

        internal object GetObjectMapper(string businessObjectClassName)
        {
            var defaultMapperType =
                (from businessObject in MapperXml.Elements("BusinessObject")
                 where (string)businessObject.Attribute("BusinessObjectClassName") == businessObjectClassName
                 from mapper in businessObject.Elements("ObjectMapper")
                 select (string)mapper.Attribute("Type")).FirstOrDefault();

            return GetObjectMapper(businessObjectClassName, defaultMapperType.GetEnumValue<ObjectMapperType>(false));
        }

        internal HybridDictionary GetFieldMappers(string businessObjectClassName, ObjectMapperType objectMapperType)
        {
            var fieldMapperKey = string.Format("{0}|{1}", businessObjectClassName, objectMapperType);
            if (!_fieldMapperCollection.Contains(fieldMapperKey))
            {
                var fieldMappers =
                    from businessObjectElement in MapperXml.Elements("BusinessObject")
                    where (string)businessObjectElement.Attribute("BusinessObjectClassName") == businessObjectClassName
                    from objectMapperElement in businessObjectElement.XPathSelectElements(string.Format("ObjectMapper[@Type='{0}']", objectMapperType))
                    from fieldMapperElement in objectMapperElement.Elements("FieldMapper")
                    select new
                    {
                        PersistenceFieldName = (string)fieldMapperElement.Attribute("PersistenceFieldName"),
                        ObjectFieldName = (string)fieldMapperElement.Attribute("ObjectFieldName")
                    };
                var mappers = new HybridDictionary();
                foreach (var fieldMapper in fieldMappers)
                    mappers.Add(fieldMapper.ObjectFieldName, fieldMapper.PersistenceFieldName);
                _fieldMapperCollection.Add(fieldMapperKey, mappers);
                return mappers;
            }
            return (HybridDictionary)_fieldMapperCollection[fieldMapperKey];
        }

        internal HybridDictionary GetFieldMappers(string businessObjectClassName)
        {
            var defaultMapperType =
                (from businessObject in MapperXml.Elements("BusinessObject")
                 where (string)businessObject.Attribute("BusinessObjectClassName") == businessObjectClassName
                 from mapper in businessObject.Elements("ObjectMapper")
                 select (string)mapper.Attribute("Type")).FirstOrDefault();

            return GetFieldMappers(businessObjectClassName, defaultMapperType.GetEnumValue<ObjectMapperType>(false));
        }

        internal Dictionary<string, Connection> GetConnections()
        {
            if (_connectionCollection == null)
            {
                _connectionCollection = new Dictionary<string, Connection>();
                var rdbConnectionKeys =
                    (from mapper in MapperXml.XPathSelectElements("//ObjectMapper[@Type='RdbRecord' or @Type='RdbSet']")
                     let rdbConnectionKey = (String)mapper.Attribute("RdbConnectionKey")
                     select rdbConnectionKey).Distinct();
                foreach (string connectionKey in rdbConnectionKeys)
                    _connectionCollection.Add(connectionKey, new Connection(connectionKey));
            }
            return _connectionCollection;
        }
    }
}