using System;
using System.Linq;
using System.Transactions;
using System.Data.Common;
using System.Collections.Generic;
using DVu.Library.PersistenceLayer;
using DVu.Library.PersistenceInterface;

namespace DVu.Library.BusinessObject
{
    internal class ComparerExecutionSequence : IComparer<ATransactionPersistenceObject>
    {
        public int Compare(ATransactionPersistenceObject x, ATransactionPersistenceObject y)
        {
            return x.ExecutionSequence.CompareTo(y.ExecutionSequence);
        }
    }

	public class Transaction : IDisposable
	{
        private readonly List<ATransactionPersistenceObject> _transactionObjectCollection = new List<ATransactionPersistenceObject>();
        private DbTransaction _localDbTransaction;
        private string _localRdbConnectionKey = string.Empty;
        private bool _isCommitted = false;

        //internal DbTransaction LocalDbTransaction
        //{
        //    get { return _localRdbConnectionKey; }
        //    set { _localRdbConnectionKey = value; }
        //}

        public void Enlist(ATransactionPersistenceObject obj)
        {
            obj.IsDelayedCommit = true;
            obj.ExecutionSequence = _transactionObjectCollection.Count;
            _transactionObjectCollection.Add(obj);
        }

        public void Enlist(ATransactionPersistenceObject obj, int executionSequence)
        {
            obj.IsDelayedCommit = true;
            obj.ExecutionSequence = executionSequence;
            _transactionObjectCollection.Add(obj);
        }

        private void ClearEnlistedObjects()
        {
            foreach (var obj in _transactionObjectCollection)
                obj.IsDelayedCommit = false;
            _transactionObjectCollection.Clear();
        }

        public void Commit()
        {
            if (_transactionObjectCollection.Count == 0)
                return;

            if (IsDistributedTransaction)
            {
                using (var ts = new TransactionScope(TransactionScopeOption.Required))
                {
                    try
                    {
                        _transactionObjectCollection.Sort(new ComparerExecutionSequence());
                        foreach (var obj in _transactionObjectCollection)
                        {
                            obj.IsDelayedCommit = false;
                            obj.Commit();
                        }
                        ts.Complete();
                        _isCommitted = true;
                    }
                    catch (Exception)
                    {
                        foreach (var obj in _transactionObjectCollection)
                        {
                            obj.IsDelayedCommit = false;
                            obj.Refresh();
                        }
                        throw;
                    }
                    finally
                    {
                        ClearEnlistedObjects();
                    }
                }
            }
            else
            {
                try
                {
                    var pf = PersistenceFacade.GetInstance();
                    _localDbTransaction = pf.BeginLocalDbTransaction(_localRdbConnectionKey);
                    _transactionObjectCollection.Sort(new ComparerExecutionSequence());
                    foreach (var obj in _transactionObjectCollection)
                    {
                        obj.IsDelayedCommit = false;
                        obj.Commit();
                    }
                    pf.CommitLocalDbTransacstion(_localRdbConnectionKey);
                    _isCommitted = true;
                }
                catch (Exception)
                {
                    if (_localDbTransaction != null)
                        _localDbTransaction.Rollback();
                    foreach (var obj in _transactionObjectCollection)
                        obj.Refresh();
                    throw;
                }
                finally
                {
                    ClearEnlistedObjects();
                }
            }
        }

        public bool IsDistributedTransaction
        {
            get
            {
                var pf = PersistenceFacade.GetInstance();
                _localRdbConnectionKey = string.Empty;
                var previousRdbConnectionKey = string.Empty;
                var isDistributedTransaction = false;
                foreach (var objRdbConnectionKey in _transactionObjectCollection.Select(obj => pf.GetRdbConnectionKey((IPersistenceObject)obj)))
                {
                    if (objRdbConnectionKey == string.Empty)
                    {
                        isDistributedTransaction = true;
                        break;
                    }
                    if (previousRdbConnectionKey != string.Empty && 
                        pf.GetDbConnectionServerName(previousRdbConnectionKey) != pf.GetDbConnectionServerName(objRdbConnectionKey))
                    {
                        isDistributedTransaction = true;
                        break;
                    }
                    previousRdbConnectionKey = objRdbConnectionKey;
                }
                _localRdbConnectionKey = previousRdbConnectionKey;
                return isDistributedTransaction;
            }
        }

        #region IDisposable Members
        public void Dispose()
        {
            if (!_isCommitted)
            {
                // active transaction has connection != null
                if (_localDbTransaction != null && _localDbTransaction.Connection != null)
                    _localDbTransaction.Rollback();
                foreach (var obj in _transactionObjectCollection)
                {
                    obj.IsDelayedCommit = false;
                    obj.Refresh();
                }
            }
            if (_localDbTransaction != null)
                _localDbTransaction.Dispose();
        }
        #endregion
    }
}
