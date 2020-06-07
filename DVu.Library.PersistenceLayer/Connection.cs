using System;
using System.Data;
using System.Data.Common;
using System.Configuration;
using DVu.Library.PersistenceInterface;

namespace DVu.Library.PersistenceLayer
{
    internal class Connection
    {
        private readonly string _rdbConnectionKey;
        private DbConnection _dbConnection;
        private DbTransaction _localDbTransaction;

        internal Connection(string rdbConnectionKey)
        {
            _rdbConnectionKey = rdbConnectionKey;
        }

        private void SetupLocalDbTransaction()
        {
            // committed / rolledback trans have Connection set to null
            if (_localDbTransaction != null && _localDbTransaction.Connection != null)
                throw new ApplicationException("A local pending transaction exists.");

            if (_dbConnection == null)
                _dbConnection = GetDbConnection();
            else if (_dbConnection.State == ConnectionState.Closed)
                _dbConnection.Open();
        }

        internal DbTransaction BeginLocalDbTransaction(IsolationLevel iso)
        {
            SetupLocalDbTransaction();
            _localDbTransaction = _dbConnection.BeginTransaction(iso);
            return _localDbTransaction;
        }

        internal DbTransaction BeginLocalDbTransaction()
        {
            SetupLocalDbTransaction();
            _localDbTransaction = _dbConnection.BeginTransaction();
            return _localDbTransaction;
        }

        internal void CommitLocalDbTransaction()
        {
            if (_localDbTransaction == null || _localDbTransaction.Connection == null)
                throw new ApplicationException("No local pending transaction exists.");
            try
            {
                _localDbTransaction.Commit();
            }
            finally
            {
                _dbConnection.Close();
                _localDbTransaction = null;
            }
        }

        internal void RollbackLocalDbTransaction()
        {
            if (_localDbTransaction == null || _localDbTransaction.Connection == null)
                throw new ApplicationException("No local pending transaction exists.");
            try
            {
                _localDbTransaction.Rollback();
            }
            finally
            {
                _dbConnection.Close();
                _localDbTransaction = null;
            }
        }
        
        internal DbCommand GetDbCommand()
        {
            if (_dbConnection == null)
                _dbConnection = GetDbConnection();
            else if (_dbConnection.State == ConnectionState.Closed)
                _dbConnection.Open();
            var command = _dbConnection.CreateCommand();
            command.Transaction = _localDbTransaction;
            return command;
        }

        internal DbConnection GetDbConnection()
        {
            //DVu.Library.Cryptography.Crypto crypto = new DVu.Library.Cryptography.Crypto();
            //string decrypted = crypto.Decrypt(ConfigurationManager.AppSettings["DBConnectString"]);
            //SqlConnection sqlConnection = new SqlConnection(decrypted);
            var setting = ConfigurationManager.ConnectionStrings[_rdbConnectionKey];
            var factory = DbProviderFactories.GetFactory(setting.ProviderName);
            var connection = factory.CreateConnection();
            connection.ConnectionString = setting.ConnectionString;
            connection.Open();
            return connection;
        }

        internal string GetDbConnectionServerName()
        {
            var setting = ConfigurationManager.ConnectionStrings[_rdbConnectionKey];
            var factory = DbProviderFactories.GetFactory(setting.ProviderName);
            var connection = factory.CreateConnection();
            connection.ConnectionString = setting.ConnectionString;
            return connection.DataSource;
        }

        internal DbDataAdapter GetDbDataAdapter()
        {
            var factory = DbProviderFactories.GetFactory(ConfigurationManager.ConnectionStrings[_rdbConnectionKey].ProviderName);
            return factory.CreateDataAdapter();
        }

        internal DbParameter AddCommandParameter(DbCommand command, string parameterName, object parameterValue)
        {
            var providerName = ConfigurationManager.ConnectionStrings[_rdbConnectionKey].ProviderName;
            var parameter = command.CreateParameter();
            parameter.ParameterName = providerName == "System.Data.SqlClient" ? string.Format("@{0}", parameterName) : parameterName;
            if (parameterValue == null || parameterValue.ToString() == "Null")
                parameter.Value = DBNull.Value;
            else
                parameter.Value = parameterValue is IObjectKey ? ((IObjectKey)parameterValue).ToXml().ToString() : parameterValue;
                //parameter.Value = parameterValue is IObjectKey ? ((IObjectKey)parameterValue).ToXml().ToString() : parameterValue.ToString();
            command.Parameters.Add(parameter);
            return parameter;
        }

        internal string[] GetParameterNames(string rdbConnectionKey, string commandText)
        {
            string parameterPrefix = ConfigurationManager.ConnectionStrings[rdbConnectionKey].ProviderName == "System.Data.SqlClient" ? "@" : ":";
            var parameterNames = new System.Text.StringBuilder();
            var startIndex = commandText.IndexOf(parameterPrefix, StringComparison.Ordinal);
            while (startIndex > -1 && startIndex < commandText.Length)
            {
                startIndex++;
                var parameterNameLength = commandText.IndexOf(" ", startIndex, StringComparison.Ordinal) > startIndex ? commandText.IndexOf(" ", startIndex, StringComparison.Ordinal) - startIndex : -1;
                if (parameterNameLength > -1)
                {
                    parameterNames.Append(commandText.Substring(startIndex, parameterNameLength)).Append("|");
                    startIndex += parameterNameLength;
                    startIndex = commandText.IndexOf(parameterPrefix, startIndex, StringComparison.Ordinal);
                }
                else
                {
                    parameterNames.Append(commandText.Substring(startIndex));
                    break;
                }
            }
            if (startIndex == -1) // parameter is not the last token on commandText, remove the last "|"
                parameterNames.Remove(parameterNames.Length - 1, 1);
            return parameterNames.ToString().Split('|');
        }
    }
}
