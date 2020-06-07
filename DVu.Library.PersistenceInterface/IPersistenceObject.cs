using System;

namespace DVu.Library.PersistenceInterface
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public class NonXmlSerializedAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public class NotMappedAttribute : Attribute { }

    public interface IPersistenceObject
    {
        void Load();
        void Load(ObjectMapperType objectMapperType);
        void LoadFromObject(object sourceObject);
        bool Validate();
        void Save();
        void Delete();
        IObjectKey ObjectKey { get; set; }
        Type ObjectKeyType { get; }
    }

    public interface IPersistenceObjectCollection : IPersistenceObject
    {
        IPersistenceObject CreateObjectForRetrieval();
        void Initialize(int capacity);
        void AddRetrievedObject(IPersistenceObject obj);
    }

    public interface IObjectKey
    {
        string ToString();
        System.Xml.Linq.XElement ToXml();
        System.Collections.Specialized.HybridDictionary ToDictionary();
    }

    public interface IRowVersion
    {
        System.Data.SqlTypes.SqlBytes RowVersion { get; set; }
    }

    public interface ISetVersion
    {
        IRowVersion SetHeader { get; set; }
    }

    public interface IRowIdentity
    {
        System.Data.SqlTypes.SqlInt32 RowIdentity { get; set; }
    }
}
