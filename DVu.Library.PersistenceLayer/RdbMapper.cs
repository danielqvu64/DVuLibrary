using System;
using System.Configuration;
using System.Data.SqlTypes;
using System.Linq;
using System.Data;
using System.Data.Common;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Reflection;
using DVu.Library.PersistenceInterface;
using DVu.Library.Utility;

namespace DVu.Library.PersistenceLayer
{
	public class RdbRecordMapper : IRecordMapper, IRdbMapper
	{
        private readonly RdbObjectMapperInfo _rdbObjectMapperInfo;

        public RdbRecordMapper(RdbObjectMapperInfo mapperInfo)
        {
            _rdbObjectMapperInfo = mapperInfo;
        }

        public string RdbConnectionKey
        {
            get { return _rdbObjectMapperInfo.RdbConnectionKey; }
            set { _rdbObjectMapperInfo.RdbConnectionKey = value; }
        }
            
        public void Get(IPersistenceObject persistenceObject)
        {
            var dr = GetDataReader(persistenceObject.ObjectKey);
            using (dr)
            {
                if (dr.Read())
                    Populate(persistenceObject, dr);
            }
        }

        private DbDataReader GetDataReader(IObjectKey objectKey)
		{
            var pf = PersistenceFacade.GetInstance();
			var connection = pf.GetDbConnection(_rdbObjectMapperInfo.RdbConnectionKey);
            var command = connection.CreateCommand();
            command.CommandText = _rdbObjectMapperInfo.MethodNameSelect ?? string.Format("{0}_sel", _rdbObjectMapperInfo.RdbEntityName);
			command.CommandType = CommandType.StoredProcedure;
            pf.AddCommandParameter(_rdbObjectMapperInfo.RdbConnectionKey, command, "objectKey", objectKey);
			return command.ExecuteReader(CommandBehavior.CloseConnection);
		}

        public void Delete(IPersistenceObject persistenceObject)
		{
			DbCommand command = null;
			try
			{
                var pf = PersistenceFacade.GetInstance();
                command = pf.GetDbCommand(_rdbObjectMapperInfo.RdbConnectionKey);
                command.CommandText = _rdbObjectMapperInfo.MethodNameDelete ?? string.Format("{0}_del", _rdbObjectMapperInfo.RdbEntityName);
				command.CommandType = CommandType.StoredProcedure;
                pf.AddCommandParameter(_rdbObjectMapperInfo.RdbConnectionKey, command, "objectKey", persistenceObject.ObjectKey);
				command.ExecuteNonQuery();
			}
			finally
			{
				if (command != null && command.Transaction == null)
					command.Connection.Close();
			}
		}

        public void Update(IPersistenceObject persistenceObject)
        {
            DbCommand command = null;
            var pf = PersistenceFacade.GetInstance();
            try
            {
                command = pf.GetDbCommand(_rdbObjectMapperInfo.RdbConnectionKey);
                command.CommandText = _rdbObjectMapperInfo.MethodNameUpdate ?? string.Format("{0}_upd", _rdbObjectMapperInfo.RdbEntityName);
                command.CommandType = CommandType.StoredProcedure;
                pf.AddCommandParameter(_rdbObjectMapperInfo.RdbConnectionKey, command, "objectKey", persistenceObject.ObjectKey);
                AddCommandParameter(command, persistenceObject);
                command.ExecuteNonQuery();
                SyncUpObjectKeyAfterUpdate(persistenceObject);
                RetrieveRowVersion(persistenceObject, command);
            }
            finally
            {
                if (command != null && command.Transaction == null)
                    command.Connection.Close();
            }
        }

        private void RetrieveRowVersion(IPersistenceObject persistenceObject, DbCommand command)
        {
            var rowVersion = persistenceObject as IRowVersion;
            if (rowVersion == null)
                return;
            var providerName = ConfigurationManager.ConnectionStrings[RdbConnectionKey].ProviderName;
            var fieldMapperCollection = MapperFactory.GetInstance().GetFieldMappers(persistenceObject.GetType().FullName);
            var rowVersionPropertyName = typeof(IRowVersion).GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(p => p.Name).First();
            var parameterName = providerName == "System.Data.SqlClient"
                                    ? "@" + fieldMapperCollection[rowVersionPropertyName]
                                    : fieldMapperCollection[rowVersionPropertyName];
            rowVersion.RowVersion = new SqlBytes((SqlBinary)command.Parameters[parameterName.ToString()].Value);
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

        public void Insert(IPersistenceObject persistenceObject)
        {
            DbCommand command = null;
            var pf = PersistenceFacade.GetInstance();
            try
            {
                command = pf.GetDbCommand(_rdbObjectMapperInfo.RdbConnectionKey);
                command.CommandText = _rdbObjectMapperInfo.MethodNameInsert ?? string.Format("{0}_ins", _rdbObjectMapperInfo.RdbEntityName);
                command.CommandType = CommandType.StoredProcedure;
                AddCommandParameter(command, persistenceObject);
                command.ExecuteNonQuery();
                RetrieveRowVersion(persistenceObject, command);
            }
            finally
            {
                if (command != null && command.Transaction == null)
                    command.Connection.Close();
            }
        }

        private static void Populate(IPersistenceObject persistenceObject, IDataRecord dr)
        {
            var gu = GeneralUtility.GetInstance();
            var fieldMapperCollection = MapperFactory.GetInstance().GetFieldMappers(persistenceObject.GetType().FullName);
            var fieldPropertyNameCollection = GetFieldPropertyNameCollection(persistenceObject);

            foreach (var fieldPropertyName in fieldPropertyNameCollection)
                if (fieldMapperCollection.Contains(fieldPropertyName.Name))
                    gu.SetMemberValue(persistenceObject, fieldPropertyName.Name, dr[fieldMapperCollection[fieldPropertyName.Name].ToString()]);
                else // implied name from fieldPropertyName
                    gu.SetMemberValue(persistenceObject, fieldPropertyName.Name, dr[fieldPropertyName.Name]);
        }

        internal static void Populate(IPersistenceObject persistenceObject, DataRow dr)
        {
            var gu = GeneralUtility.GetInstance();
            var fieldMapperCollection = MapperFactory.GetInstance().GetFieldMappers(persistenceObject.GetType().FullName);
            var fieldPropertyNameCollection = GetFieldPropertyNameCollection(persistenceObject);

            foreach (var fieldPropertyName in fieldPropertyNameCollection)
                if (fieldMapperCollection.Contains(fieldPropertyName.Name))
                    gu.SetMemberValue(persistenceObject, fieldPropertyName.Name, dr[fieldMapperCollection[fieldPropertyName.Name].ToString()]);
                else // implied name from fieldPropertyName
                    gu.SetMemberValue(persistenceObject, fieldPropertyName.Name, dr[fieldPropertyName.Name]);
        }

        private void AddCommandParameter(DbCommand command, IPersistenceObject persistenceObject)
        {
            var gu = GeneralUtility.GetInstance();
            var pf = PersistenceFacade.GetInstance();
            var fieldMapperCollection = MapperFactory.GetInstance().GetFieldMappers(persistenceObject.GetType().FullName);
            var fieldPropertyNameCollection = GetFieldPropertyNameCollection(persistenceObject);

            foreach (var fieldPropertyName in fieldPropertyNameCollection) {
                DbParameter parameter;
                if (fieldMapperCollection.Contains(fieldPropertyName.Name))
                    parameter = pf.AddCommandParameter(_rdbObjectMapperInfo.RdbConnectionKey, command, fieldMapperCollection[fieldPropertyName.Name].ToString(), gu.GetMemberValue(persistenceObject, fieldPropertyName.Name));
                else // implied name from fieldPropertyName
                    parameter = pf.AddCommandParameter(_rdbObjectMapperInfo.RdbConnectionKey, command, fieldPropertyName.Name, gu.GetMemberValue(persistenceObject, fieldPropertyName.Name));
                if (!fieldPropertyName.IsRowVersion)
                    continue;
                parameter.Direction = ParameterDirection.InputOutput;
                if (parameter.Value == DBNull.Value)
                    parameter.Value = new SqlBytes(new byte[8]);
            }
        }

        private static IEnumerable<FieldPropertyName> GetFieldPropertyNameCollection(IPersistenceObject persistenceObject)
        {
            var rowVersionPropertyName = typeof(IRowVersion).GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(p => p.Name).First();
            var persistenceObjectType = persistenceObject.GetType();
            var fieldPropertyNameCollection = (from propertyInfo in persistenceObjectType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                                               where propertyInfo.GetCustomAttributes(typeof(NotMappedAttribute), true).Length == 0
                                               select new FieldPropertyName { Name = propertyInfo.Name, IsRowVersion = rowVersionPropertyName == propertyInfo.Name })
                                              .Union
                                              (from fieldInfo in persistenceObjectType.GetFields(BindingFlags.Public | BindingFlags.Instance)
                                               where fieldInfo.GetCustomAttributes(typeof(NotMappedAttribute), true).Length == 0
                                               select new FieldPropertyName { Name = fieldInfo.Name, IsRowVersion = false });
            return fieldPropertyNameCollection;
        }
    }

    public class FieldPropertyName
    {
        public string Name;
        public bool IsRowVersion;
    }

    public class RdbSetMapper : IRecordSetMapper, IRdbMapper
    {
        private readonly RdbObjectMapperInfo _rdbObjectMapperInfo;

        public RdbSetMapper(RdbObjectMapperInfo mapperInfo)
        {
            _rdbObjectMapperInfo = mapperInfo;
        }

        public string RdbConnectionKey
        {
            get { return _rdbObjectMapperInfo.RdbConnectionKey; }
            set { _rdbObjectMapperInfo.RdbConnectionKey = value; }
        }

        public void Get(IPersistenceObjectCollection persistenceObjectCollection, IObjectKey parentObjectKey)
        {
            var dt = GetDataTable(parentObjectKey);
            GetObjectFromRecordSet(persistenceObjectCollection, dt);
        }

        public void Get(IPersistenceObjectCollection persistenceObjectCollection, HybridDictionary parameterCollection)
        {
            var dt = GetDataTable(persistenceObjectCollection, parameterCollection);
            GetObjectFromRecordSet(persistenceObjectCollection, dt);
        }

        public void Get(IPersistenceObjectCollection persistenceObjectCollection, IObjectKey parentObjectKey, HybridDictionary parameterCollection)
        {
            var dt = GetDataTable(persistenceObjectCollection, parentObjectKey, parameterCollection);
            GetObjectFromRecordSet(persistenceObjectCollection, dt);
        }

        private static void GetObjectFromRecordSet(IPersistenceObjectCollection persistenceObjectCollection, DataTable dt)
        {
            persistenceObjectCollection.Initialize(dt.Rows.Count);
            for (var rowIndex = 0; rowIndex < dt.Rows.Count; rowIndex++)
            {
                var dr = dt.Rows[rowIndex];
                var persistenceObject = persistenceObjectCollection.CreateObjectForRetrieval();
                RdbRecordMapper.Populate(persistenceObject, dr);
                persistenceObjectCollection.AddRetrievedObject(persistenceObject);
            }
        }

        private DataTable GetDataTable(IObjectKey parentObjectKey)
        {
            var dt = new DataTable();
            var pf = PersistenceFacade.GetInstance();
            var connection = pf.GetDbConnection(_rdbObjectMapperInfo.RdbConnectionKey);

            using (connection)
            {
                var command = connection.CreateCommand();
                command.CommandText = _rdbObjectMapperInfo.MethodNameSelect ?? string.Format("{0}_sel", _rdbObjectMapperInfo.RdbEntityName);
                command.CommandType = CommandType.StoredProcedure;
                pf.AddCommandParameter(_rdbObjectMapperInfo.RdbConnectionKey, command, "objectKey", parentObjectKey);

                var adapter = pf.GetDbDataAdapter(_rdbObjectMapperInfo.RdbConnectionKey);
                adapter.SelectCommand = command;
                adapter.Fill(dt);

                return dt;
            }
        }

        private DataTable GetDataTable(IPersistenceObjectCollection persistenceObjectCollection, HybridDictionary parameterCollection)
        {
            var pf = PersistenceFacade.GetInstance();
            var fieldMapperCollection = MapperFactory.GetInstance().GetFieldMappers(persistenceObjectCollection.GetType().FullName);

            var dt = new DataTable();
            var connection = pf.GetDbConnection(_rdbObjectMapperInfo.RdbConnectionKey);

            using (connection)
            {
                var command = connection.CreateCommand();
                command.CommandText = _rdbObjectMapperInfo.MethodNameSelect ?? string.Format("{0}_sel", _rdbObjectMapperInfo.RdbEntityName);
                command.CommandType = CommandType.StoredProcedure;

                foreach (DictionaryEntry param in parameterCollection)
                    if (fieldMapperCollection.Contains(param.Key.ToString()))
                        pf.AddCommandParameter(_rdbObjectMapperInfo.RdbConnectionKey, command, fieldMapperCollection[param.Key.ToString()].ToString(), param.Value);
                    else // implied name from param.Key.ToString()
                        pf.AddCommandParameter(_rdbObjectMapperInfo.RdbConnectionKey, command, param.Key.ToString(), param.Value);

                var adapter = pf.GetDbDataAdapter(_rdbObjectMapperInfo.RdbConnectionKey);
                adapter.SelectCommand = command;
                adapter.Fill(dt);

                return dt;
            }
        }

        private DataTable GetDataTable(IPersistenceObjectCollection persistenceObjectCollection, IObjectKey parentObjectKey, HybridDictionary parameterCollection)
        {
            var pf = PersistenceFacade.GetInstance();
            var fieldMapperCollection = MapperFactory.GetInstance().GetFieldMappers(persistenceObjectCollection.GetType().FullName);
            
            var dt = new DataTable();
            var connection = pf.GetDbConnection(_rdbObjectMapperInfo.RdbConnectionKey);

            using (connection)
            {
                var command = connection.CreateCommand();
                command.CommandText = _rdbObjectMapperInfo.MethodNameSelect ?? string.Format("{0}_sel", _rdbObjectMapperInfo.RdbEntityName);
                command.CommandType = CommandType.StoredProcedure;

                pf.AddCommandParameter(_rdbObjectMapperInfo.RdbConnectionKey, command, "objectKey", parentObjectKey);
                foreach (DictionaryEntry param in parameterCollection)
                    if (fieldMapperCollection.Contains(param.Key.ToString()))
                        pf.AddCommandParameter(_rdbObjectMapperInfo.RdbConnectionKey, command, fieldMapperCollection[param.Key.ToString()].ToString(), param.Value);
                    else // implied name from param.Key.ToString()
                        pf.AddCommandParameter(_rdbObjectMapperInfo.RdbConnectionKey, command, param.Key.ToString(), param.Value);

                var adapter = pf.GetDbDataAdapter(_rdbObjectMapperInfo.RdbConnectionKey);
                adapter.SelectCommand = command;
                adapter.Fill(dt);

                return dt;
            }
        }
    }
}