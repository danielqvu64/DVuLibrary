using System;

namespace DVu.Library.BusinessObject
{
    [Serializable]
	internal sealed class DeletedState : APersistenceObjectState
	{
        private static volatile DeletedState _instance;
        private static readonly object SyncRoot = new object();
        
		private DeletedState() { }

		public static DeletedState GetInstance()
		{
            if (_instance == null)
                lock (SyncRoot)
                {
                    if (_instance == null)
                        _instance = new DeletedState();
                }
            return _instance;
        }
	}
}
