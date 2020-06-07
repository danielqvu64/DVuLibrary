using System;
using DVu.Library.PersistenceInterface;
using DVu.Library.PersistenceLayer;

namespace DVu.Library.BusinessObject
{
    [Serializable]
    internal sealed class OldDeleteState : APersistenceObjectState
	{
        private static volatile OldDeleteState _instance;
        private static readonly object SyncRoot = new object();
        
		private OldDeleteState() { }

		public static OldDeleteState GetInstance()
		{
            if (_instance == null)
                lock (SyncRoot)
                {
                    if (_instance == null)
                        _instance = new OldDeleteState();
                }
            return _instance;
        }

		public override void Commit(APersistenceObject persistenceObject)
		{
			PersistenceFacade.GetInstance().Delete(persistenceObject);
            persistenceObject.PreviousState = this;
			persistenceObject.State = DeletedState.GetInstance();
		}

        public override void Refresh(APersistenceObject obj)
        {
            obj.Load();
        }
    }
}
