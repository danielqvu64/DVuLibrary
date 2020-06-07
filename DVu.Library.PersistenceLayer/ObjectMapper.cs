using System.Reflection;
using System.Linq;
using DVu.Library.PersistenceInterface;
using DVu.Library.Utility;

namespace DVu.Library.PersistenceLayer
{
    public class ObjectRecordMapper : IRecordObjectMapper
    {
        #region IRecordObjectMapper Members
        public void Get(IPersistenceObject persistenceObject, object sourceObject)
        {
            if (sourceObject == null)
                return;
            var gu = GeneralUtility.GetInstance();
            var persistenceObjectType = persistenceObject.GetType();
            var fieldMapperCollection = MapperFactory.GetInstance().GetFieldMappers(persistenceObjectType.FullName, ObjectMapperType.Obj);

            var fieldPropertyNameCollection = (from propertyInfo in persistenceObjectType.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                                               where propertyInfo.GetCustomAttributes(typeof(NotMappedAttribute), true).Length == 0
                                               select propertyInfo.Name)
                                              .Union
                                              (from fieldInfo in persistenceObjectType.GetFields(BindingFlags.Instance | BindingFlags.Public)
                                               where fieldInfo.GetCustomAttributes(typeof(NotMappedAttribute), true).Length == 0
                                               select fieldInfo.Name);

            foreach (var fieldPropertyName in fieldPropertyNameCollection)
                if (fieldMapperCollection.Contains(fieldPropertyName))
                    gu.SetMemberValue(persistenceObject, fieldPropertyName, gu.GetMemberValue(sourceObject, fieldMapperCollection[fieldPropertyName].ToString()));
                else // implied name from fieldPropertyName
                    gu.SetMemberValue(persistenceObject, fieldPropertyName, gu.GetMemberValue(sourceObject, fieldPropertyName));
        }
        #endregion
    }
}
