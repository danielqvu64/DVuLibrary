using System;
using DVu.Library.PersistenceLayer;

namespace DVu.Library.BusinessObject
{
    [Serializable]
    internal sealed class OldDirtyState : APersistenceObjectState
	{
        private static volatile OldDirtyState _instance;
        private static readonly object SyncRoot = new object();
        
		private OldDirtyState() { }

		public static OldDirtyState GetInstance()
		{
            if (_instance == null)
                lock (SyncRoot)
                {
                    if (_instance == null)
                        _instance = new OldDirtyState();
                }
            return _instance;
        }

		public override void Commit(APersistenceObject persistenceObject)
		{
			PersistenceFacade.GetInstance().Update(persistenceObject);
            persistenceObject.PreviousState = this;
			persistenceObject.State = OldCleanState.GetInstance();
		}

		public override void Delete(APersistenceObject obj)
		{
            obj.PreviousState = this;
			obj.State = OldDeleteState.GetInstance();
		}

        public override void Refresh(APersistenceObject obj)
        {
            obj.Load();
        }
    }
}
