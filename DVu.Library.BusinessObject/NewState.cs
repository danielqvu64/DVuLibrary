using System;
using DVu.Library.PersistenceLayer;

namespace DVu.Library.BusinessObject
{
    [Serializable]
    internal sealed class NewState : APersistenceObjectState
	{
        private static volatile NewState _instance;
        private static readonly object SyncRoot = new object();
        
		private NewState() { }

		public static NewState GetInstance()
		{
            if (_instance == null)
                lock (SyncRoot)
                {
                    if (_instance == null)
                        _instance = new NewState();
                }
            return _instance;
        }

        public override void Commit(APersistenceObject persistenceObject)
        {
            PersistenceFacade.GetInstance().Insert(persistenceObject);
            persistenceObject.PreviousState = this;
            persistenceObject.State = OldCleanState.GetInstance();
        }
        
        public override void Delete(APersistenceObject obj)
        {
            obj.PreviousState = this;
            obj.State = OldDeleteState.GetInstance();
        }
    }
}
