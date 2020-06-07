using System;
using System.Data;
using System.Data.Common;
using System.Data.SqlTypes;
using System.Collections.Generic;
using System.Collections.Specialized;
using DVu.Library.PersistenceInterface;

namespace DVu.Library.PersistenceLayer
{
    public sealed class PersistenceFacade
	{
        private static volatile PersistenceFacade _instance;
        private static readonly object SyncRoot = new object();
        
		private PersistenceFacade() { }

		public static PersistenceFacade GetInstance()
		{
            if (_instance == null)
                lock (SyncRoot)
                {
                    if (_instance == null)
                        _instance = new PersistenceFacade();
                }
            return _instance;
        }

        private readonly Dictionary<string, Connection> _connectionCollection = MapperFactory.GetInstance().GetConnections();
        
        public bool HasObjectToObjectMapper(IPersistenceObject persistenceObject)
        {
            var mapper = MapperFactory.GetInstance().GetObjectMapper(persistenceObject.GetType().FullName, ObjectMapperType.Obj);
            return mapper != null;
        }

        public bool HasObjectToObjectMapper(string businessObjectClassName)
        {
            var mapper = MapperFactory.GetInstance().GetObjectMapper(businessObjectClassName, ObjectMapperType.Obj);
            return mapper != null;
        }

        public void Get(IPersistenceObject persistenceObject)
        {
            var persistenceObjectType = persistenceObject.GetType();
            var mapper = MapperFactory.GetInstance().GetObjectMapper(persistenceObjectType.FullName) as IRecordMapper;
            if (mapper == null)
                throw new ApplicationException("Mapper is not defined for " + persistenceObjectType.FullName);
            mapper.Get(persistenceObject);
        }

        public void Get(IPersistenceObject persistenceObject, ObjectMapperType objectMapperType)
        {
            var persistenceObjectType = persistenceObject.GetType();
            var mapper = MapperFactory.GetInstance().GetObjectMapper(persistenceObjectType.FullName, objectMapperType) as IRecordMapper;
            if (mapper == null)
                throw new ApplicationException(string.Format("Mapper Type {0} is not defined for {1}", objectMapperType, persistenceObjectType.FullName));
            mapper.Get(persistenceObject);
        }

        public void GetFromObject(IPersistenceObject persistenceObject, object sourceObject)
        {
            var persistenceObjectType = persistenceObject.GetType();
            var mapper = MapperFactory.GetInstance().GetObjectMapper(persistenceObjectType.FullName, ObjectMapperType.Obj) as IRecordObjectMapper;
            if (mapper == null)
                throw new ApplicationException(string.Format("Mapper Type Obj is not defined for {0}", persistenceObjectType.FullName));
            mapper.Get(persistenceObject, sourceObject);
        }

        public void Get(IPersistenceObjectCollection persistenceObjectCollection, IObjectKey parentObjectKey)
		{
            var persistenceObjectType = persistenceObjectCollection.GetType();
            var mapper = MapperFactory.GetInstance().GetObjectMapper(persistenceObjectType.FullName) as IRecordSetMapper;
			if (mapper == null)
                throw new ApplicationException("Mapper is not defined for " + persistenceObjectType.FullName);
            mapper.Get(persistenceObjectCollection, parentObjectKey);
        }

        public void Get(IPersistenceObjectCollection persistenceObjectCollection, HybridDictionary parameterCollection)
        {
            var persistenceObjectType = persistenceObjectCollection.GetType();
            var mapper = MapperFactory.GetInstance().GetObjectMapper(persistenceObjectType.FullName) as IRecordSetMapper;
            if (mapper == null)
                throw new ApplicationException("Mapper is not defined for " + persistenceObjectType.FullName);
            mapper.Get(persistenceObjectCollection, parameterCollection);
        }

        public void Get(IPersistenceObjectCollection persistenceObjectCollection, IObjectKey parentObjectKey, HybridDictionary parameterCollection)
        {
            var persistenceObjectType = persistenceObjectCollection.GetType();
            var mapper = MapperFactory.GetInstance().GetObjectMapper(persistenceObjectType.FullName) as IRecordSetMapper;
            if (mapper == null)
                throw new ApplicationException("Mapper is not defined for " + persistenceObjectType.FullName);
            mapper.Get(persistenceObjectCollection, parentObjectKey, parameterCollection);
        }

        public string GetRdbConnectionKey(IPersistenceObject obj)
        {
            var type = obj.GetType();
            var mapper = MapperFactory.GetInstance().GetObjectMapper(type.FullName);
            if (mapper == null)
                throw new ApplicationException("Mapper is not defined for " + type.FullName);
            if (mapper is IRdbMapper)
                return ((IRdbMapper)mapper).RdbConnectionKey;
            return string.Empty;
        }

        public DbCommand GetDbCommand(string rdbConnectionKey)
        {
            return _connectionCollection[rdbConnectionKey].GetDbCommand();
        }

        public DbConnection GetDbConnection(string rdbConnectionKey)
        {
            return _connectionCollection[rdbConnectionKey].GetDbConnection(); 
        }

        public string GetDbConnectionServerName(string rdbConnectionKey)
        {
            return _connectionCollection[rdbConnectionKey].GetDbConnectionServerName();
        }

        public DbDataAdapter GetDbDataAdapter(string rdbConnectionKey)
        {
            return _connectionCollection[rdbConnectionKey].GetDbDataAdapter();
        }

        public DbParameter AddCommandParameter(string rdbConnectionKey, DbCommand command, string parameterName, object parameterValue)
        {
            return _connectionCollection[rdbConnectionKey].AddCommandParameter(command, parameterName, parameterValue);
        }

        public string[] GetParameterNames(string rdbConnectionKey, string commandText)
        {
            return _connectionCollection[rdbConnectionKey].GetParameterNames(rdbConnectionKey, commandText);
        }

        public DbTransaction BeginLocalDbTransaction(string rdbConnectionKey, IsolationLevel iso)
        {
            return _connectionCollection[rdbConnectionKey].BeginLocalDbTransaction(iso);
        }

        public DbTransaction BeginLocalDbTransaction(string rdbConnectionKey)
        {
            return _connectionCollection[rdbConnectionKey].BeginLocalDbTransaction();
        }

        public void CommitLocalDbTransacstion(string rdbConnectionKey)
        {
            _connectionCollection[rdbConnectionKey].CommitLocalDbTransaction();
        }

        public void RollbackLocalDbTransacstion(string rdbConnectionKey)
        {
            _connectionCollection[rdbConnectionKey].RollbackLocalDbTransaction();
        }

        public void Insert(IPersistenceObject persistenceObject)
        {
            var persistenceObjectType = persistenceObject.GetType();
            var mapper = MapperFactory.GetInstance().GetObjectMapper(persistenceObjectType.FullName) as IRecordMapper;
            if (mapper == null)
                throw new ApplicationException("Mapper is not defined for " + persistenceObjectType.FullName);
            mapper.Insert(persistenceObject);
        }

        public void Update(IPersistenceObject persistenceObject)
		{
            var persistenceObjectType = persistenceObject.GetType();
            var mapper = MapperFactory.GetInstance().GetObjectMapper(persistenceObjectType.FullName) as IRecordMapper;
			if (mapper == null)
				throw new ApplicationException("Mapper is not defined for " + persistenceObjectType.FullName);
			mapper.Update(persistenceObject);
		}

        public void Delete(IPersistenceObject persistenceObject)
		{
            var persistenceObjectType = persistenceObject.GetType();
            var mapper = MapperFactory.GetInstance().GetObjectMapper(persistenceObjectType.FullName) as IRecordMapper;
			if (mapper == null)
				throw new ApplicationException("Mapper is not defined for " + persistenceObjectType.FullName);
            mapper.Delete(persistenceObject);
        }

        public void LogException(Exception ex, string machineName, string additionalData)
        {
            string rdbConnectionKey = System.Configuration.ConfigurationManager.AppSettings["ExceptionLogConnectionKey"];
            DbConnection connection = GetDbConnection(rdbConnectionKey);
            try
            {
                DbCommand command = connection.CreateCommand();
                command.CommandText = "applicationException_ins";
                command.CommandType = CommandType.StoredProcedure;

                AddCommandParameter(rdbConnectionKey, command, "type", ex.GetType().FullName);
                AddCommandParameter(rdbConnectionKey, command, "message", ex.Message);
                AddCommandParameter(rdbConnectionKey, command, "stack_trace", ex.StackTrace);
                AddCommandParameter(rdbConnectionKey, command, "source", ex.Source);
                AddCommandParameter(rdbConnectionKey, command, "target_site", ex.TargetSite == null ? SqlString.Null : ex.TargetSite.ToString());
                AddCommandParameter(rdbConnectionKey, command, "base_exception_type", ex.GetBaseException().GetType().FullName);
                AddCommandParameter(rdbConnectionKey, command, "machine_name", machineName);
                AddCommandParameter(rdbConnectionKey, command, "date_time", DateTime.Now);
                if (additionalData != null)
                    AddCommandParameter(rdbConnectionKey, command, "additional_data", additionalData);

                command.ExecuteNonQuery();
            }
            finally
            {
                if (connection.State == ConnectionState.Open)
                    connection.Close();
            }
            if (ex.InnerException != null)
                LogException(ex.InnerException, machineName, additionalData);
        }
	}
}
