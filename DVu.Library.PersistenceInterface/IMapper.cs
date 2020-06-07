using System;
using System.Collections.Specialized;

namespace DVu.Library.PersistenceInterface
{
    public delegate void WsProxyFinanlizeCallBack(System.ServiceModel.ICommunicationObject proxy);

    public interface IRdbMapper
    {
        string RdbConnectionKey { get; set; }
    }

	public interface IRecordSetMapper
	{
        void Get(IPersistenceObjectCollection persistenceObjectCollection, IObjectKey objectKey);
        void Get(IPersistenceObjectCollection persistenceObjectCollection, HybridDictionary parameters);
        void Get(IPersistenceObjectCollection persistenceObjectCollection, IObjectKey objectKey, HybridDictionary parameters);
    }

	public interface IRecordMapper
	{
        void Get(IPersistenceObject persistenceObject);
        void Delete(IPersistenceObject persistenceObject);
        void Update(IPersistenceObject persistenceObject);
        void Insert(IPersistenceObject persistenceObject);
	}

    public interface IRecordObjectMapper
    {
        void Get(IPersistenceObject persistenceObject, object sourceObject);
    }

    public sealed class RdbObjectMapperInfo
    {
        public string RdbEntityName;
        public string RdbConnectionKey;
        public string MethodNameSelect;
        public string MethodNameUpdate;
        public string MethodNameInsert;
        public string MethodNameDelete;
    }

    public sealed class WsObjectMapperInfo
    {
        public string ProxyClassNameSelect;
        public string ProxyClassNameUpdate;
        public string ProxyClassNameInsert;
        public string ProxyClassNameDelete;
        public string MethodNameSelect;
        public string MethodNameUpdate;
        public string MethodNameInsert;
        public string MethodNameDelete;
        public string ProxyCallbackClassNameSelect;
        public string ProxyCallbackClassNameUpdate;
        public string ProxyCallbackClassNameInsert;
        public string ProxyCallbackClassNameDelete;
        public string ProxyCallbackPropertyNameSelect;
        public string ProxyCallbackPropertyNameUpdate;
        public string ProxyCallbackPropertyNameInsert;
        public string ProxyCallbackPropertyNameDelete;
    }

    [AttributeUsage(AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
    public sealed class StringValueAttribute : Attribute
    {
        private readonly string _stringValue;

        public StringValueAttribute(string stringValue)
        {
            _stringValue = stringValue;
        }

        public string StringValue { get { return _stringValue; } }
    }

    public enum ObjectMapperType
    {
        RdbRecord, RdbSet, WsRecord, WsSet, Obj
    }

    public static class Extension
    {
        public static string GetStringValue(this Enum value)
        {
            if (value == null)
                throw new ArgumentNullException("value");

            var stringValue = value.ToString();
            var fieldInfo = value.GetType().GetField(stringValue);
            var attributes = (StringValueAttribute[])fieldInfo.GetCustomAttributes(typeof(StringValueAttribute), false);

            return attributes.Length > 0 ? attributes[0].StringValue : stringValue;
        }

        public static T GetEnumValue<T>(this string stringValue, bool isCustomAttribute)
        {
            var type = typeof(T);
            var values = Enum.GetValues(type);
            foreach (T o in values)
            {
                if (isCustomAttribute)
                {
                    var fieldInfo = type.GetField(o.ToString());
                    var attributes =
                        (StringValueAttribute[]) fieldInfo.GetCustomAttributes(typeof (StringValueAttribute), false);
                    if (attributes.Length > 0 && String.Compare(attributes[0].StringValue, stringValue, StringComparison.OrdinalIgnoreCase) == 0)
                        return o;
                }
                else if (o.ToString() == stringValue)
                    return o;
            }
            try
            {
                return (T)Enum.Parse(typeof(T), stringValue, true);
            }
            catch
            {
                return default(T);
            }
        }
    }
}