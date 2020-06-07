using System;

namespace DVu.Library.BusinessObject
{
    [Serializable]
    internal sealed class OldCleanState : APersistenceObjectState
	{
        private static volatile OldCleanState _instance;
        private static readonly object SyncRoot = new object();
        
		private OldCleanState() { }

		public static OldCleanState GetInstance()
		{
            if (_instance == null)
                lock (SyncRoot)
                {
                    if (_instance == null)
                        _instance = new OldCleanState();
                }
            return _instance;
        }

		public override void Delete(APersistenceObject obj)
		{
            obj.PreviousState = this;
			obj.State = OldDeleteState.GetInstance();
		}

		public override void FieldDataChange(APersistenceObject obj)
		{
            obj.PreviousState = this;
			obj.State = OldDirtyState.GetInstance();
		}
	}
}
